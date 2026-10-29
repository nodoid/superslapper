using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Random;
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
    private static readonly (string Key, Stance Stance)[] Hands =
    {
        ("create.hand.orthodox", Stance.Orthodox),
        ("create.hand.southpaw", Stance.Southpaw)
    };

    private int _firstIndex;
    private int _lastIndex;
    private int _nicknameIndex = 2;
    private int _handIndex;
    private int _divisionIndex = (int)WeightClassId.Welterweight;

    public CreateCareerScreen(GameContext context) : base(context)
    {
    }

    private WeightClass Division => WeightClasses.All[_divisionIndex];

    /// <summary>Every picker's position, for the button sweep: tapping a field that
    /// changes none of this has done nothing.</summary>
    public override string ViewState =>
        $"{_firstIndex}|{_lastIndex}|{_nicknameIndex}|{_handIndex}|{_divisionIndex}";

    public override void OnEnter()
    {
        base.OnEnter();
        ClearControls();

        // Bottom row: tall enough for a thumb and clear of the home-indicator
        // band along the bottom edge of a phone. The career button owns the
        // whole right half and the exit sits well to its left, so a thumb aimed
        // at START CAREER can never land on BACK by mistake.
        Add(new Button(new Rectangle(700, 860, 300, 150), Loc.T("common.back"),
            () => Context.Screens.Pop(), ButtonStyle.Ghost));

        Add(new Button(new Rectangle(1040, 860, 820, 150), Loc.T("create.start"),
            Start, ButtonStyle.Normal));

        Add(new Button(new Rectangle(1600, 60, 240, 90),
            Context.Units == WeightUnit.Metric ? "KG" : "LB",
            () => Context.ToggleUnits(), ButtonStyle.Ghost));

        // The five pickers are tapped rather than typed, so they are tap targets
        // rather than buttons - declared with the very rectangles the screen
        // hit-tests in Update.
        AddTarget("first name", FirstField);
        AddTarget("last name", LastField);
        AddTarget("nickname", NicknameField);
        AddTarget("preferred hand", HandField);
        AddTarget("division", DivisionField);
    }

    private void Start()
    {
        var seed = Environment.TickCount;
        Context.State = WorldGenerator.Create(FirstNames[_firstIndex], LastNames[_lastIndex],
            Nicknames[_nicknameIndex], Division.Id, seed, $"{FirstNames[_firstIndex]} {LastNames[_lastIndex]}",
            Hands[_handIndex].Stance);
        Context.Career.BookNextFight(Context.State, new RandomSource(seed + 17));
        Context.Persist();
        Context.Assets.Play("ui_confirm");
        Context.Screens.SetRoot(new HubScreen(Context));
    }

    public override void Update(GameTime time, InputState input)
    {
        foreach (var button in Buttons.ToArray())
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

        batch.Text(Context.Assets.Font("huge"), Loc.T("create.title"), new Vector2(110, 90), Theme.Gold);
        batch.Text(Context.Assets.Font("body"), Loc.T("create.subtitle"),
            new Vector2(112, 200), Theme.Muted);

        DrawPanel(batch, new Rectangle(90, 270, 820, 370), Loc.T("create.yourFighter"));
        DrawField(batch, Loc.T("create.firstName"), FirstNames[_firstIndex], FirstField);
        DrawField(batch, Loc.T("create.lastName"), LastNames[_lastIndex], LastField);
        DrawField(batch, Loc.T("create.nickname"), Nicknames[_nicknameIndex], NicknameField);
        DrawField(batch, Loc.T("create.hand"), Loc.T(Hands[_handIndex].Key), HandField);

        DrawPanel(batch, new Rectangle(950, 270, 880, 560), Loc.T("create.division"));

        var division = Division;
        batch.Text(Context.Assets.Font("large"), division.Name.ToUpperInvariant(),
            new Vector2(1390, 360), Theme.Gold, 1f, TextAlign.Centre);

        batch.Text(Context.Assets.Font("medium"), Loc.T("create.limit", division.LimitText(Context.Units)),
            new Vector2(1390, 440), Theme.White, 1f, TextAlign.Centre);
        batch.Text(Context.Assets.Font("small"),
            division.Limit is null
                ? Loc.T("create.openDivision", WeightClasses.Get(WeightClassId.Heavyweight)
                    .LimitText(Context.Units))
                : Loc.T("create.bothUnits", division.MetricLimitText, division.ImperialLimitText),
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

        batch.Text(Context.Assets.Font("small"), Loc.T("create.hint"),
            new Vector2(90, 1045), Theme.Muted);

        foreach (var button in Buttons.ToArray())
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
