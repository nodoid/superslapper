using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;
using SuperSlapper.Game.Screens;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace SuperSlapper.Game;

/// <summary>
/// The shared game. Every platform head boots this class; the only difference
/// between them is the graphics backend packaged by the platform project.
/// </summary>
public class SuperSlapperGame : XnaGame
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _batch = null!;
    private Assets _assets = null!;
    private GameContext _context = null!;
    private TextInputState _textInput = null!;
    private Matrix _virtualToScreen = Matrix.Identity;
    private Matrix _screenToVirtual = Matrix.Identity;
    private string _reportedView = string.Empty;
    private InputState _input = null!;
    private SelfTestRunner? _selfTest;
    private RenderTarget2D? _selfTestTarget;
    private DemoDirector? _demo;

    /// <summary>Optional self-test: renders every screen to disk and quits.</summary>
    public string? SelfTestOutputDirectory { get; set; }

    public int SelfTestFrames { get; set; } = 3;

    /// <summary>
    /// Raised once the game has loaded and its platform window and view exist. The
    /// iOS scene re-homes the view into itself on this, because the scene can
    /// connect before MonoGame has made one.
    /// </summary>
    public event Action? Ready;

    /// <summary>Runs the automated career playthrough and quits with an exit code.</summary>
    public bool PlaythroughMode { get; set; }

    /// <summary>Runs the automated season and quits with an exit code.</summary>
    public bool SeasonMode { get; set; }

    /// <summary>
    /// Plays a scripted career in real time so the running app can be filmed. The
    /// demo never quits on its own: iOS forbids that, and the capture is stopped
    /// from outside.
    /// </summary>
    public bool DemoMode { get; set; }

    /// <summary>Where the demo writes one PNG per beat, when the run is being checked.</summary>
    public string? DemoCaptureDirectory { get; set; }

    /// <summary>Where the demo writes its beat log, for the recorder following the run.</summary>
    public string? DemoLogFile { get; set; }

    /// <summary>
    /// Where the head wants a running trace of the session: screen changes, presses
    /// and failures. The iOS head points it at a file, because a phone leaves no
    /// console behind to read after the fact.
    /// </summary>
    public Action<string>? Trace { get; set; }

    private bool _playthroughDone;
    private long _frames;
    private string? _debugShotPath;
    private bool _debugShotTaken;

    public SuperSlapperGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = GameSettings.VirtualWidth,
            PreferredBackBufferHeight = GameSettings.VirtualHeight,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.HiDef
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        Window.Title = GameSettings.DisplayName;
    }

    protected override void Initialize()
    {
        Window.ClientSizeChanged += (_, _) => UpdateVirtualTransform();
        UpdateVirtualTransform();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        Gfx.Initialise(GraphicsDevice);

        _assets = new Assets(GraphicsDevice);
        _assets.LoadAll();

        // An automated run gets its own save folder, cleared first: a scripted
        // career can never touch the player's save, and the title screen looks the
        // same on every machine instead of depending on what is already on disk.
        var automated = SelfTestOutputDirectory is not null || PlaythroughMode || SeasonMode || DemoMode;
        var saveDirectory = automated
            ? Path.Combine(Path.GetTempPath(), "SuperSlapper-automated")
            : null;

        if (saveDirectory is not null && Directory.Exists(saveDirectory))
        {
            try
            {
                Directory.Delete(saveDirectory, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[save] could not clear {saveDirectory}: {ex.Message}");
            }
        }

        _context = new GameContext(_assets, GraphicsDevice, saveDirectory)
        {
            PersistEnabled = !automated
        };
        _textInput = new TextInputState(Window);
        _input = new InputState();
        _context.Screens = new ScreenManager(_context);

        _context.Trace = Trace;
        Button.Trace = Trace;
        InputState.Trace = Trace;
        Trace?.Invoke($"start: assets {_assets.LoadedTextureCount} textures, save {_context.SavePath}");

        foreach (var error in _assets.Errors)
        {
            Console.WriteLine($"[asset] {error}");
        }

        Console.WriteLine($"[assets] textures={_assets.LoadedTextureCount} " +
                          $"fonts={_assets.LoadedFontCount} sounds={_assets.LoadedSoundCount}");

        _context.Screens.SetRoot(new TitleScreen(_context));

        // A diagnostic hand: a launch can be pointed straight at one page and asked
        // to photograph it, so a page that will not draw can be looked at on the
        // phone without walking to it by thumb.
        var debugScreen = Environment.GetEnvironmentVariable("SUPERSLAPPER_SCREEN");
        if (!string.IsNullOrWhiteSpace(debugScreen))
        {
            _context.State = _context.Repository.Exists() ? _context.Repository.Load() : null;
            if (_context.State is not null)
            {
                _debugShotPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "shot_" + debugScreen + ".png");

                Screen root = debugScreen switch
                {
                    "belts" => new RecordsScreen(_context, RecordsScreen.Tab.Belts),
                    "records" => new RecordsScreen(_context, RecordsScreen.Tab.Rankings),
                    "settings" => new SettingsScreen(_context),
                    _ => new RecordsScreen(_context, RecordsScreen.Tab.Rules)
                };
                _context.Screens.SetRoot(root);
                Trace?.Invoke($"debug screen {debugScreen}");
            }
        }

        if (SelfTestOutputDirectory is not null)
        {
            _selfTest = new SelfTestRunner(SelfTestOutputDirectory, _assets);
        }

        if (DemoMode)
        {
            _demo = new DemoDirector(_context, DemoCaptureDirectory, DemoLogFile);
        }

        // The game is up: its window and view exist now. The iOS scene waits for
        // this when it connected before MonoGame had built them.
        Ready?.Invoke();
    }

    private void UpdateVirtualTransform()
    {
        var width = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferWidth);
        var height = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferHeight);

        var scale = Math.Min(width / (float)GameSettings.VirtualWidth,
            height / (float)GameSettings.VirtualHeight);
        var offsetX = (width - GameSettings.VirtualWidth * scale) / 2f;
        var offsetY = (height - GameSettings.VirtualHeight * scale) / 2f;

        _virtualToScreen = Matrix.CreateScale(scale, scale, 1f) *
                           Matrix.CreateTranslation(offsetX, offsetY, 0f);
        _screenToVirtual = Matrix.Invert(_virtualToScreen);

        // The frame is only worth reporting when the view actually changes size;
        // it is what says whether touches and drawing agree about the screen. The
        // touch panel's own size belongs in the same line: a tap is reported in that
        // space, and the two only agree once the panel has been scaled to the frame.
        var view = $"{width}x{height} scale {scale:0.####} offset {offsetX:0.#},{offsetY:0.#} " +
                   $"touch panel {TouchPanel.DisplayWidth}x{TouchPanel.DisplayHeight}";
        if (view != _reportedView)
        {
            _reportedView = view;
            Trace?.Invoke($"view back buffer {view}");
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if ((PlaythroughMode || SeasonMode) && !_playthroughDone)
        {
            _playthroughDone = true;
            var code = SeasonMode ? Playthrough.RunSeason(_context) : Playthrough.Run(_context, 3);
            Environment.ExitCode = code;
            Quit();
            return;
        }

        UpdateVirtualTransform();

        _textInput.Poll();
        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();
        var touches = TouchPanel.GetState();

        _input.Update(mouse, keyboard, touches, _screenToVirtual,
            new Point(GraphicsDevice.PresentationParameters.BackBufferWidth,
                GraphicsDevice.PresentationParameters.BackBufferHeight));
        _textInput.SetCursor(_input.Pointer);

        // The demo takes the frame's input: what it delivers is what the screens
        // see, whether or not anyone is touching the device being filmed.
        _demo?.Update(gameTime, _input);

        if (_input.BackPressed && _context.Screens.Current is not TitleScreen)
        {
            _context.Screens.Pop();
        }

        _context.Screens.Update(gameTime, _input);
        _selfTest?.Prepare(_context);

        // One line a second while the game is running. A phone that stops
        // answering taps is either a loop that has stopped or a screen that has
        // stopped listening, and this says which.
        if (++_frames % 60 == 0)
        {
            Trace?.Invoke($"beat {_frames} {_context.Screens.Current?.GetType().Name} " +
                          $"depth {_context.Screens.Depth}");
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        DrawScenes(gameTime);

        if (_debugShotPath is { } shotPath && !_debugShotTaken && _frames > 120)
        {
            _debugShotTaken = true;
            try
            {
                CaptureFrame(gameTime, shotPath);
                Console.WriteLine($"[shot] {shotPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[shot] failed: {ex}");
            }
        }

        if (_selfTest is { NeedsRender: true })
        {
            CaptureFrame(gameTime, _selfTest.PathFor(_selfTest.CurrentName));
            _selfTest.Captured();

            if (_selfTest.Finished)
            {
                FinishSelfTest();
            }
        }
        else if (_demo is { WantsCapture: true, CaptureName: { } name } demo)
        {
            CaptureFrame(gameTime, Path.Combine(demo.CaptureDirectory!, name + ".png"));
            demo.Captured();
        }

        base.Draw(gameTime);
    }

    /// <summary>
    /// Draws the screen stack in virtual space. The self-test passes an identity
    /// transform so its 1920x1080 target gets the whole frame on any device -
    /// phones are wider than 16:9 and would otherwise clip one edge.
    /// </summary>
    private void DrawScenes(GameTime gameTime, Matrix? transform = null)
    {
        var matrix = transform ?? _virtualToScreen;
        GraphicsDevice.Clear(Theme.Ink);
        GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

        _context.VirtualTransform = matrix;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, matrix);
        _context.Screens.Draw(_batch, gameTime);
        _batch.End();
    }

    /// <summary>Renders the current screen into a PNG, for the smoke test or the demo.</summary>
    private void CaptureFrame(GameTime gameTime, string path)
    {
        _selfTestTarget ??= new RenderTarget2D(GraphicsDevice, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight, false, SurfaceFormat.Color, DepthFormat.None);

        GraphicsDevice.SetRenderTarget(_selfTestTarget);
        DrawScenes(gameTime, Matrix.Identity);
        GraphicsDevice.SetRenderTarget(null);

        using (var stream = File.Create(path))
        {
            _selfTestTarget.SaveAsPng(stream, GameSettings.VirtualWidth, GameSettings.VirtualHeight);
        }

        Console.WriteLine($"[frame] captured {path}");
    }

    /// <summary>Ends the run once the last scenario is written.</summary>
    private void FinishSelfTest()
    {
        _selfTestTarget?.Dispose();
        _selfTestTarget = null;

        // The smoke test is only worth running if it can fail the build.
        // Every string the run asked for had to come out of the catalogue: a key
        // that resolved to itself is a missing translation, and a missing one is a
        // failure like any other.
        Console.WriteLine($"[strings] {Loc.Count} in {Loc.Language}" +
                          (Loc.Missing.Count == 0
                              ? ", none missing"
                              : $", {Loc.Missing.Count} missing: {string.Join(", ", Loc.Missing.Take(6))}"));

        if (Loc.Missing.Count > 0)
        {
            _selfTest.FailMissingStrings(Loc.Missing);
        }

        if (_selfTest!.FailureCount == 0)
        {
            Console.WriteLine("[selftest] complete");
        }
        else
        {
            Console.WriteLine("[selftest] FAILED - " + _selfTest.FailureCount +
                              " scenario(s) did not land where they were aimed");
        }

        Environment.ExitCode = _selfTest.FailureCount == 0 ? 0 : 1;
        Quit();
    }

    /// <summary>Leaves the game where the platform allows it. iOS does not.</summary>
    private void Quit()
    {
#if IOS
        // App Store rules forbid an app terminating itself; parking the loop is
        // the closest legal equivalent for the automated runs.
#else
        Exit();
#endif
    }
}

/// <summary>Desktop keyboard text entry plus the on-screen cursor position.</summary>
public sealed class TextInputState
{
    private readonly List<char> _pending = new();

    public TextInputState(GameWindow window)
    {
#if DESKTOPGL || WINDOWS
        // Only the desktop MonoGame backends expose Window.TextInput. Phones use
        // the on-screen keyboard supplied by the platform head instead.
        window.TextInput += (_, e) => _pending.Add(e.Character);
#else
        _ = window;
#endif
    }

    public Vector2 Cursor { get; private set; }

    internal void SetCursor(Vector2 position) => Cursor = position;

    internal void Poll()
    {
    }

    public string Consume()
    {
        if (_pending.Count == 0)
        {
            return string.Empty;
        }

        var text = new string(_pending.ToArray());
        _pending.Clear();
        return text;
    }
}
