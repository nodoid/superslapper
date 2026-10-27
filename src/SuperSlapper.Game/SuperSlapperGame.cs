using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;
using SuperSlapper.Game.Screens;

namespace SuperSlapper.Game;

/// <summary>
/// The shared game. Every platform head boots this class; the only difference
/// between them is the graphics backend packaged by the platform project.
/// </summary>
public class SuperSlapperGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _batch = null!;
    private Assets _assets = null!;
    private GameContext _context = null!;
    private TextInputState _textInput = null!;
    private Matrix _virtualToScreen = Matrix.Identity;
    private Matrix _screenToVirtual = Matrix.Identity;
    private InputState _input = null!;
    private SelfTestRunner? _selfTest;
    private RenderTarget2D? _selfTestTarget;

    /// <summary>Optional self-test: renders every screen to disk and quits.</summary>
    public string? SelfTestOutputDirectory { get; set; }

    public int SelfTestFrames { get; set; } = 3;

    /// <summary>Runs the automated career playthrough and quits with an exit code.</summary>
    public bool PlaythroughMode { get; set; }

    private bool _playthroughDone;

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

        _context = new GameContext(_assets, GraphicsDevice)
        {
            PersistEnabled = SelfTestOutputDirectory is null && !PlaythroughMode
        };
        _textInput = new TextInputState(Window);
        _input = new InputState();
        _context.Screens = new ScreenManager(_context);

        foreach (var error in _assets.Errors)
        {
            Console.WriteLine($"[asset] {error}");
        }

        Console.WriteLine($"[assets] textures={_assets.LoadedTextureCount} " +
                          $"fonts={_assets.LoadedFontCount} sounds={_assets.LoadedSoundCount}");

        _context.Screens.SetRoot(new TitleScreen(_context));

        if (SelfTestOutputDirectory is not null)
        {
            _selfTest = new SelfTestRunner(SelfTestOutputDirectory, _assets);
        }
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
    }

    protected override void Update(GameTime gameTime)
    {
        if (PlaythroughMode && !_playthroughDone)
        {
            _playthroughDone = true;
            var code = Playthrough.Run(_context, 3);
            Environment.ExitCode = code;
            Quit();
            return;
        }

        UpdateVirtualTransform();

        _textInput.Poll();
        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();
        var touches = TouchPanel.GetState();

        _input.Update(mouse, keyboard, touches, _screenToVirtual);
        _textInput.SetCursor(_input.Pointer);

        if (_input.BackPressed && _context.Screens.Current is not TitleScreen)
        {
            _context.Screens.Pop();
        }

        _context.Screens.Update(gameTime, _input);
        _selfTest?.Prepare(_context);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        DrawScenes(gameTime);

        if (_selfTest is { NeedsRender: true })
        {
            CaptureFrame(gameTime);
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

    /// <summary>Renders the current screen into a PNG for the automated smoke test.</summary>
    private void CaptureFrame(GameTime gameTime)
    {
        _selfTestTarget ??= new RenderTarget2D(GraphicsDevice, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight, false, SurfaceFormat.Color, DepthFormat.None);

        GraphicsDevice.SetRenderTarget(_selfTestTarget);
        DrawScenes(gameTime, Matrix.Identity);
        GraphicsDevice.SetRenderTarget(null);

        var path = _selfTest!.PathFor(_selfTest.CurrentName);
        using (var stream = File.Create(path))
        {
            _selfTestTarget.SaveAsPng(stream, GameSettings.VirtualWidth, GameSettings.VirtualHeight);
        }

        Console.WriteLine($"[selftest] captured {path}");
        _selfTest.Captured();

        if (_selfTest.Finished)
        {
            _selfTestTarget.Dispose();
            _selfTestTarget = null;
            Console.WriteLine("[selftest] complete");
            Quit();
        }
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
