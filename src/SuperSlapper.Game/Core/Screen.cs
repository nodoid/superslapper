using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
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
    public GameContext(Assets assets, GraphicsDevice device)
    {
        Assets = assets;
        Device = device;
        Repository = new CareerRepository(SaveDirectory());
    }

    public Assets Assets { get; }
    public GraphicsDevice Device { get; }
    public CareerService Career { get; } = new();
    public CareerState? State { get; set; }
    public CareerRepository Repository { get; }
    public WeightUnit Units { get; set; } = WeightUnit.Metric;
    public ScreenManager Screens { get; internal set; } = null!;

    /// <summary>Virtual-pixels-to-screen matrix, refreshed every frame by the game.</summary>
    public Matrix VirtualTransform { get; set; } = Matrix.Identity;

    /// <summary>Toggle between metric and imperial everywhere at once.</summary>
    public string UnitLabel => Units == WeightUnit.Metric ? "KILOGRAMS" : "POUNDS";

    public void ToggleUnits() =>
        Units = Units == WeightUnit.Metric ? WeightUnit.Imperial : WeightUnit.Metric;

    public string FormatWeight(decimal pounds) => SuperSlapper.Core.Units.Weight.FromPounds(pounds).ToDisplayString(Units);

    /// <summary>Small allowances are rulebook constants, so show both systems: "1.4 kg / 3 lb".</summary>
    public string FormatWeightPair(decimal pounds)
    {
        var weight = SuperSlapper.Core.Units.Weight.FromPounds(pounds);
        var metric = weight.In(WeightUnit.Metric).ToString("0.#") + " kg";
        var imperial = weight.In(WeightUnit.Imperial).ToString("0.#") + " lb";

        return Units == WeightUnit.Metric ? $"{metric} / {imperial}" : $"{imperial} / {metric}";
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

public abstract class Screen
{
    protected Screen(GameContext context) => Context = context;

    protected GameContext Context { get; }

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
        batch.Text(Context.Assets.Font("large"), "NO CAREER LOADED",
            new Vector2(panel.Center.X, panel.Top + 70), Theme.Gold, 1f, TextAlign.Centre);
        batch.WrappedText(Context.Assets.Font("body"),
            "Start a new career from the title screen to see this page.",
            new Vector2(panel.Left + 60, panel.Top + 160), panel.Width - 120, Theme.Muted, 1f,
            TextAlign.Centre);
        batch.Text(Context.Assets.Font("small"), "ESC / BACK TO RETURN",
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

    public void Push(Screen screen)
    {
        screen.OnEnter();
        _stack.Add(screen);
        _fade = 1f;
    }

    public void Pop()
    {
        if (_stack.Count <= 1)
        {
            return;
        }

        _stack[^1].OnExit();
        _stack.RemoveAt(_stack.Count - 1);
        _fade = 1f;
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
    }

    public void Update(GameTime time, InputState input)
    {
        _fade = Math.Max(0f, _fade - (float)time.ElapsedGameTime.TotalSeconds * 3.5f);
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
