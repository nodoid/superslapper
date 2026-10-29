using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Units;

namespace SuperSlapper.Game.Core;

public enum FighterKit
{
    Player = 0,
    Rival = 1,
    Green = 2,
    Champion = 3
}

/// <summary>Everything a screen needs that outlives it.</summary>
public sealed class GameContext
{
    /// <param name="saveDirectory">
    /// Overrides where the save and the sound preference live. The automated runs
    /// pass a scratch folder so a scripted career can never touch the player's.
    /// </param>
    public GameContext(Assets assets, GraphicsDevice device, string? saveDirectory = null)
    {
        Assets = assets;
        Device = device;
        SavePath = saveDirectory ?? SaveDirectory();
        Repository = new CareerRepository(SavePath);
        Settings = PlayerSettings.Load(SavePath);
        Catalogue.Load(Settings.Language);
        Music = new Music(assets);
        ApplySettings();
    }

    public Assets Assets { get; }
    public GraphicsDevice Device { get; }
    public Music Music { get; }
    public PlayerSettings Settings { get; }
    public CareerService Career { get; } = new();
    public CareerState? State { get; set; }
    public CareerRepository Repository { get; }

    /// <summary>Where the save and the player's preferences are kept.</summary>
    public string SavePath { get; }
    /// <summary>The units the player chose; the settings sheet is what changes it.</summary>
    public WeightUnit Units => Settings.Units;
    public ScreenManager Screens { get; internal set; } = null!;

    /// <summary>Virtual-pixels-to-screen matrix, refreshed every frame by the game.</summary>
    public Matrix VirtualTransform { get; set; } = Matrix.Identity;

    /// <summary>
    /// Where the game writes a running trace, when a head wants one. A phone has no
    /// console to read, so the iOS head points this at a file in the app's own
    /// container: screen changes, presses and any exception land there.
    /// </summary>
    public Action<string>? Trace { get; set; }

    /// <summary>Toggle between metric and imperial everywhere at once.</summary>
    public string UnitLabel => Loc.T(Units == WeightUnit.Metric ? "units.metric" : "units.imperial");

    public void ToggleUnits() =>
        SetUnits(Units == WeightUnit.Metric ? WeightUnit.Imperial : WeightUnit.Metric);

    /// <summary>
    /// Steps one of the two volume controls and keeps the choice. Shared by the
    /// sound chips on the title screen and the hub.
    /// </summary>
    public void CycleLevel(AudioChannel channel)
    {
        SetLevel(channel, (Settings.For(channel).Index + 1) % PlayerSettings.Levels.Length);
    }

    /// <summary>Sets one volume control to an exact level, for the settings sheet.</summary>
    public void SetLevel(AudioChannel channel, int level)
    {
        Settings.For(channel).Level = level;
        ApplySettings();
        KeepSettings();
    }

    /// <summary>Switches the units everywhere and keeps the choice.</summary>
    public void SetUnits(WeightUnit units)
    {
        Settings.Units = units;
        ApplySettings();
        KeepSettings();
    }

    /// <summary>Hands the saved preferences to the things that read and play them.</summary>
    private void ApplySettings()
    {
        Music.Volume = Settings.Music.Volume;
        Assets.EffectsVolume = Settings.Effects.Volume;
    }

    /// <summary>
    /// Writes the preferences out. The automated runs turn persistence off, and
    /// they keep their save in a scratch folder, so this can never touch a player's
    /// own settings.
    /// </summary>
    private void KeepSettings()
    {
        if (!PersistEnabled)
        {
            return;
        }

        Settings.Save(SavePath);

        // One line on every change: a phone has no other way to tell whether a
        // preference reached the disk, and this is where that is decided.
        Console.WriteLine($"[settings] {Settings.Music.Label}/{Settings.Effects.Label}/{Settings.Units} " +
                          $"-> {Path.Combine(SavePath, PlayerSettings.FileName)}");
    }

    public string FormatWeight(decimal pounds) => Weight.FromPounds(pounds).ToDisplayString(Units);

    /// <summary>Small allowances are rulebook constants, so show both systems: "1.4 kg / 3 lb".</summary>
    public string FormatWeightPair(decimal pounds)
    {
        var weight = Weight.FromPounds(pounds);
        var metric = Loc.T("units.kilograms", weight.In(WeightUnit.Metric).ToString("0.#"));
        var imperial = Loc.T("units.pounds", weight.In(WeightUnit.Imperial).ToString("0.#"));

        return Units == WeightUnit.Metric
            ? Loc.T("units.pair", metric, imperial)
            : Loc.T("units.pair", imperial, metric);
    }

    public static string SaveDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Path.GetTempPath(), "SuperSlapper");
        }

        return Path.Combine(root, "SuperSlapper");
    }

    /// <summary>
    /// Turned off for the automated self-test and playthrough runs so a scripted
    /// career can never overwrite the player's real save.
    /// </summary>
    public bool PersistEnabled { get; set; } = true;

    public void Persist()
    {
        if (!PersistEnabled || State is null)
        {
            return;
        }

        try
        {
            Repository.Save(State);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Save failed: {ex.Message}");
        }
    }
}

/// <summary>
/// A region a screen handles itself, without a button widget behind it: a calendar
/// day cell, a field on the new-career sheet, the whole screen during a bout. Named
/// so the automated button sweep can press it and report it by name.
/// </summary>
public sealed record TapTarget(string Name, Rectangle Bounds);

public abstract class Screen
{
    private readonly List<Button> _buttons = new();
    private readonly List<TapTarget> _targets = new();

    protected Screen(GameContext context) => Context = context;

    protected GameContext Context { get; }

    /// <summary>
    /// Every button this screen puts on show, in the order it built them. Screens
    /// fill this in as they lay themselves out, and the automated button sweep walks
    /// the list pressing each one, which is how a button that does nothing gets
    /// caught rather than shipped.
    /// </summary>
    public IReadOnlyList<Button> Buttons => _buttons;

    /// <summary>The regions this screen handles itself, for the same sweep.</summary>
    public IReadOnlyList<TapTarget> Targets => _targets;

    /// <summary>
    /// What this screen is showing right now, as a string: the calendar month, the
    /// tab the record book is on, the beat the bout is in. A control that changes
    /// nothing here, in the career or in the settings is a control that did nothing.
    /// </summary>
    public virtual string ViewState => string.Empty;

    /// <summary>Empties both control lists so a screen can rebuild its layout.</summary>
    protected void ClearControls()
    {
        _buttons.Clear();
        _targets.Clear();
    }

    /// <summary>Puts a button on this screen.</summary>
    protected void Add(Button button) => _buttons.Add(button);

    /// <summary>Declares a region this screen hit-tests itself.</summary>
    protected void AddTarget(string name, Rectangle bounds) =>
        _targets.Add(new TapTarget(name, bounds));

    /// <summary>
    /// The track this screen wants playing, named after its asset just like every
    /// other sound. Screens that want a different mood override it; the fight and
    /// the award ceremony are the two that do.
    /// </summary>
    public virtual string? MusicTrack => "music_menu";

    /// <summary>How long this screen has been visible, in seconds.</summary>
    protected float Elapsed { get; private set; }

    /// <summary>False only when a screen that needs a save is open without one.</summary>
    protected bool HasCareer => Context.State is not null;

    /// <summary>
    /// Standard stand-in for a screen that is open with no career loaded - after a
    /// save is cleared, for instance. The app stays on screen instead of throwing.
    /// </summary>
    protected void DrawNoCareer(SpriteBatch batch)
    {
        batch.Draw(Context.Assets.Tex("menu_bg"),
            new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight), Color.White);

        var panel = new Rectangle(460, 380, 1000, 320);
        batch.NineSlice(Context.Assets.Tex("panel"), panel, 40, Color.White);
        batch.Text(Context.Assets.Font("large"), Loc.T("common.noCareer"),
            new Vector2(panel.Center.X, panel.Top + 70), Theme.Gold, 1f, TextAlign.Centre);
        batch.WrappedText(Context.Assets.Font("body"), Loc.T("common.noCareerHint"),
            new Vector2(panel.Left + 60, panel.Top + 160), panel.Width - 120, Theme.Muted, 1f,
            TextAlign.Centre);
        batch.Text(Context.Assets.Font("small"), Loc.T("common.backHint"),
            new Vector2(panel.Center.X, panel.Bottom - 70), Theme.Muted, 1f, TextAlign.Centre);
    }

    /// <summary>True when the screen has nothing to show and should skip its update.</summary>
    protected bool MissingCareer(InputState input)
    {
        if (Context.State is not null)
        {
            return false;
        }

        if (input.BackPressed)
        {
            Context.Screens.Pop();
        }

        return true;
    }

    public virtual void OnEnter() => Elapsed = 0f;

    public virtual void OnExit()
    {
    }

    public abstract void Update(GameTime time, InputState input);

    public abstract void Draw(SpriteBatch batch, GameTime time);

    internal void Tick(GameTime time) => Elapsed += (float)time.ElapsedGameTime.TotalSeconds;
}

/// <summary>A small screen stack with a fade between screens.</summary>
public sealed class ScreenManager
{
    private readonly GameContext _context;
    private readonly List<Screen> _stack = new();
    private float _fade;

    public ScreenManager(GameContext context)
    {
        _context = context;
        _context.Screens = this;
    }

    public Screen? Current => _stack.Count > 0 ? _stack[^1] : null;

    /// <summary>How many screens are stacked up. The sweep needs it: a pop changes
    /// nothing else when the screen below looks the same.</summary>
    public int Depth => _stack.Count;

    public void Push(Screen screen)
    {
        screen.OnEnter();
        _stack.Add(screen);
        _fade = 1f;
        _context.Trace?.Invoke($"push {screen.GetType().Name} -> {_stack.Count} deep");
    }

    public void Pop()
    {
        if (_stack.Count <= 1)
        {
            return;
        }

        var leaving = _stack[^1];
        leaving.OnExit();
        _stack.RemoveAt(_stack.Count - 1);
        _fade = 1f;
        _context.Trace?.Invoke($"pop {leaving.GetType().Name} -> {_stack.Count} deep");
    }

    /// <summary>Replaces everything with a single screen.</summary>
    public void SetRoot(Screen screen)
    {
        foreach (var existing in _stack)
        {
            existing.OnExit();
        }

        _stack.Clear();
        screen.OnEnter();
        _stack.Add(screen);
        _fade = 1f;
        _context.Trace?.Invoke($"root {screen.GetType().Name}");
    }

    public void Update(GameTime time, InputState input)
    {
        _fade = Math.Max(0f, _fade - (float)time.ElapsedGameTime.TotalSeconds * 3.5f);

        // Whatever is on top asks for its track every frame; the player ignores
        // the request unless it is a different one, and crossfades when it is.
        _context.Music.Play(Current?.MusicTrack);
        _context.Music.Update((float)time.ElapsedGameTime.TotalSeconds);

        Current?.Tick(time);
        Current?.Update(time, input);
    }

    public void Draw(SpriteBatch batch, GameTime time)
    {
        Current?.Draw(batch, time);

        if (_fade > 0.001f)
        {
            // The sprite batch is already begun in virtual space by the game.
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
                Color.Black * _fade);
        }
    }
}

public static class GameSettings
{
    public const int VirtualWidth = 1920;
    public const int VirtualHeight = 1080;
    public const float TextScale = 1f;
    public const string DisplayName = "SuperSlapper";
    public const string Copyright = "© 2026 Paul F. Johnson";
}
