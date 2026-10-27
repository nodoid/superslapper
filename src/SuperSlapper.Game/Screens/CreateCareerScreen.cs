using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Units;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// Career setup: name, nickname and weight division. Pickers are tapped rather
/// than typed so the same screen works on a phone with no soft keyboard.
/// </summary>
public sealed class CreateCareerScreen : Screen
{
    private static readonly string[] FirstNames =
    {
        "Paul", "Rico", "Dmitri", "Kwame", "Jarrah", "Sean", "Mateo", "Ivan", "Kenji", "Hamish"
    };

    private static readonly string[] LastNames =
    {
        "Johnson", "Volkov", "Okafor", "Marchetti", "Doyle", "Nakamura", "Silva", "Novak", "Kelly", "Costa"
    };

    private static readonly string[] Nicknames =
    {
        "The Professor", "Brick Hands", "Open Palm", "The Anvil", "Thunderclap", "The Surgeon",
        "Sledge", "Stone Chin", "The Metronome", "Cinderblock"
    };

    // The pickers draw from these same rectangles, so a tap always lands on the
    // field it looks like it landed on.
    private static readonly Rectangle FirstField = new(140, 314, 340, 104);
    private static readonly Rectangle LastField = new(490, 314, 340, 104);
    private static readonly Rectangle NicknameField = new(140, 414, 690, 104);
    private static readonly Rectangle HandField = new(140, 518, 690, 104);
    private static readonly Rectangle DivisionField = new(1000, 330, 780, 200);

    /// <summary>The hand the fighter slaps with: right is orthodox, left southpaw.</summary>
    private static readonly (string Label, Stance Stance)[] Hands =
    {
        ("Right · Orthodox", Stance.Orthodox),
        ("Left · Southpaw", Stance.Southpaw)
    };

    private readonly List<Button> _buttons = new();
    private int _firstIndex;
    private int _lastIndex;
    private int _nicknameIndex = 2;
    private int _handIndex;
    private int _divisionIndex = (int)WeightClassId.Welterweight;

    public CreateCareerScreen(GameContext context) : base(context)
    {
    }

    private WeightClass Division => WeightClasses.All[_divisionIndex];

    public override void OnEnter()
    {
        base.OnEnter();
        _buttons.Clear();

        // Bottom row: tall enough for a thumb and clear of the home-indicator
        // band along the bottom edge of a phone. The career button owns the
        // whole right half and the exit sits well to its left, so a thumb aimed
        // at START CAREER can never land on BACK by mistake.
        _buttons.Add(new Button(new Rectangle(700, 860, 300, 150), "BACK",
            () => Context.Screens.Pop(), ButtonStyle.Ghost));

        _buttons.Add(new Button(new Rectangle(1040, 860, 820, 150), "START CAREER",
            Start, ButtonStyle.Normal));

        _buttons.Add(new Button(new Rectangle(1600, 60, 240, 90),
            Context.Units == WeightUnit.Metric ? "KG" : "LB",
            () => Context.ToggleUnits(), ButtonStyle.Ghost));
    }

    private void Start()
    {
        var seed = Environment.TickCount;
        Context.State = WorldGenerator.Create(FirstNames[_firstIndex], LastNames[_lastIndex],
            Nicknames[_nicknameIndex], Division.Id, seed, $"{FirstNames[_firstIndex]} {LastNames[_lastIndex]}",
            Hands[_handIndex].Stance);
        Context.Career.BookNextFight(Context.State, new SuperSlapper.Core.Random.RandomSource(seed + 17));
        Context.Persist();
        Context.Assets.Play("ui_confirm");
        Context.Screens.SetRoot(new HubScreen(Context));
    }

    public override void Update(GameTime time, InputState input)
    {
        foreach (var button in _buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.5f));
        }

        if (input.LeftPressed)
        {
            var p = input.Pointer.ToPoint();

            if (FirstField.Contains(p)) _firstIndex = (_firstIndex + 1) % FirstNames.Length;
            if (LastField.Contains(p)) _lastIndex = (_lastIndex + 1) % LastNames.Length;
            if (NicknameField.Contains(p)) _nicknameIndex = (_nicknameIndex + 1) % Nicknames.Length;
            if (HandField.Contains(p)) _handIndex = (_handIndex + 1) % Hands.Length;
            if (DivisionField.Contains(p)) _divisionIndex = (_divisionIndex + 1) % WeightClasses.Count;
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Text(Context.Assets.Font("huge"), "NEW CAREER", new Vector2(110, 90), Theme.Gold);
        batch.Text(Context.Assets.Font("body"),
            "Every division, belt and ranking in this save starts here.",
            new Vector2(112, 200), Theme.Muted);

        DrawPanel(batch, new Rectangle(90, 270, 820, 370), "YOUR FIGHTER");
        DrawField(batch, "First name", FirstNames[_firstIndex], FirstField);
        DrawField(batch, "Last name", LastNames[_lastIndex], LastField);
        DrawField(batch, "Nickname", Nicknames[_nicknameIndex], NicknameField);
        DrawField(batch, "Preferred hand", Hands[_handIndex].Label, HandField);

        DrawPanel(batch, new Rectangle(950, 270, 880, 560), "DIVISION");

        var division = Division;
        batch.Text(Context.Assets.Font("large"), division.Name.ToUpperInvariant(),
            new Vector2(1390, 360), Theme.Gold, 1f, TextAlign.Centre);

        batch.Text(Context.Assets.Font("medium"), "Limit: " + division.LimitText(Context.Units),
            new Vector2(1390, 440), Theme.White, 1f, TextAlign.Centre);
        batch.Text(Context.Assets.Font("small"),
            division.Limit is null
                ? "Anything over 265 lb"
                : division.MetricLimitText + "  ·  " + division.ImperialLimitText,
            new Vector2(1390, 500), Theme.Muted, 1f, TextAlign.Centre);

        batch.WrappedText(Context.Assets.Font("body"), division.Blurb,
            new Vector2(1030, 560), 720, Theme.White);

        // Division ladder in two columns so it stays inside the panel.
        const int perColumn = 5;
        for (var i = 0; i < WeightClasses.Count; i++)
        {
            var x = 1030 + (i / perColumn) * 360;
            var y = 640 + (i % perColumn) * 28;
            var current = i == _divisionIndex;
            var wc = WeightClasses.All[i];
            batch.Text(Context.Assets.Font("small"),
                $"{wc.Name,-18} {wc.LimitText(Context.Units)}",
                new Vector2(x, y), current ? Theme.Gold : Theme.Muted,
                current ? 1.05f : 1f);
        }

        // Preview fighter, below the form card so it cannot hide the fields.
        Gfx.Frame(batch, Context.Assets.Tex("fighter_player"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, FighterSheet.IdleA, new Vector2(400, 1000), Color.White, 0.72f);

        batch.Text(Context.Assets.Font("small"),
            "Tap a picker to cycle. Units switch metric and imperial everywhere in the game.",
            new Vector2(90, 1045), Theme.Muted);

        foreach (var button in _buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }

    private void DrawPanel(SpriteBatch batch, Rectangle rect, string title)
    {
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), title, new Vector2(rect.Left + 40, rect.Top + 26), Theme.Gold);
    }

    private void DrawField(SpriteBatch batch, string label, string value, Rectangle rect)
    {
        batch.Text(Context.Assets.Font("small"), label.ToUpperInvariant(),
            new Vector2(rect.Left, rect.Top + 6), Theme.Muted);
        batch.Text(Context.Assets.Font("medium"), value, new Vector2(rect.Left + 14, rect.Top + 38), Theme.White);

        var arrow = Context.Assets.Tex("arrow_up");
        batch.Draw(arrow, new Vector2(rect.Right - 24, rect.Top + 44), null, Theme.Gold,
            (float)Math.PI / 2f, new Vector2(arrow.Width / 2f, arrow.Height / 2f), 0.7f,
            SpriteEffects.None, 0f);
    }
}
