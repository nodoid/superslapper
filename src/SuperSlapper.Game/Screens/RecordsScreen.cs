using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Rankings;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>Rankings, belts and the rulebook, all in one tabbed reference screen.</summary>
public sealed class RecordsScreen : Screen
{
    public enum Tab
    {
        Rankings = 0,
        Belts = 1,
        Rules = 2
    }

    private readonly List<Button> _buttons = new();
    private Tab _tab;
    private int _leagueIndex;
    private int _divisionIndex;

    public RecordsScreen(GameContext context, Tab tab) : base(context) => _tab = tab;

    /// <summary>
    /// A stand-in world used when the book is opened from the title screen, before
    /// a career exists. It is never saved and never becomes the career.
    /// </summary>
    private CareerState? _referenceWorld;

    private bool IsReference => Context.State is null;

    private CareerState State => Context.State ?? (_referenceWorld ??= WorldGenerator.Create(
        "Reference", "World", string.Empty, WeightClassId.Welterweight, 20260101));

    public override void OnEnter()
    {
        base.OnEnter();
        Rebuild();
    }

    private League League => Leagues.All[Math.Clamp(_leagueIndex, 0, Leagues.All.Count - 1)];

    private WeightClass Division =>
        WeightClasses.Get(League.Divisions[Math.Clamp(_divisionIndex, 0, League.Divisions.Count - 1)]);

    private void Rebuild()
    {
        // Start on the player's own league and division.
        if (Context.State is { } state)
        {
            for (var i = 0; i < Leagues.All.Count; i++)
            {
                if (Leagues.All[i].Id == state.PlayerLeagueId)
                {
                    _leagueIndex = i;
                }
            }
        }

        _divisionIndex = 0;
        BuildButtons();
    }

    /// <summary>
    /// Rebuilt after every chip press so the two chips always name the league and
    /// division the tables below are actually showing.
    /// </summary>
    private void BuildButtons()
    {
        _buttons.Clear();

        var tabs = new[] { ("RANKINGS", Tab.Rankings), ("BELTS", Tab.Belts), ("RULES", Tab.Rules) };
        for (var i = 0; i < tabs.Length; i++)
        {
            var target = tabs[i].Item2;
            _buttons.Add(new Button(new Rectangle(80 + i * 320, 150, 300, 84), tabs[i].Item1,
                () =>
                {
                    _tab = target;
                    _divisionIndex = 0;
                }, i == (int)_tab ? ButtonStyle.Normal : ButtonStyle.Ghost));
        }

        _buttons.Add(new Button(new Rectangle(1420, 150, 200, 84), League.ShortName,
            () =>
            {
                _leagueIndex = (_leagueIndex + 1) % Leagues.All.Count;
                _divisionIndex = 0;
                BuildButtons();
            }, ButtonStyle.Ghost));

        _buttons.Add(new Button(new Rectangle(1640, 150, 200, 84), Division.ShortName,
            () =>
            {
                _divisionIndex = (_divisionIndex + 1) % League.Divisions.Count;
                BuildButtons();
            }, ButtonStyle.Ghost));

        _buttons.Add(new Button(new Rectangle(1620, 890, 240, 120), "BACK",
            () => Context.Screens.Pop(), ButtonStyle.Ghost));
    }

    public override void Update(GameTime time, InputState input)
    {
        foreach (var button in _buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Text(Context.Assets.Font("large"), "THE RECORD BOOK", new Vector2(80, 40), Theme.Gold);
        batch.Text(Context.Assets.Font("small"),
            $"{League.Name} · {Division.Name} ({Division.LimitText(Context.Units)}) · {Context.UnitLabel}",
            new Vector2(80, 120), Theme.Muted);

        foreach (var button in _buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }

        switch (_tab)
        {
            case Tab.Rankings:
                DrawRankings(batch);
                break;
            case Tab.Belts:
                DrawBelts(batch);
                break;
            default:
                DrawRules(batch);
                break;
        }
    }

    private void DrawRankings(SpriteBatch batch)
    {
        var state = State;
        var table = state.Rankings.Get(League.Id, Division.Id);
        var rect = new Rectangle(80, 260, 1180, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        batch.Text(Context.Assets.Font("small"), "CHAMPION", new Vector2(rect.Left + 40, rect.Top + 24), Theme.Gold);

        var champion = state.FindFighter(table.ChampionId);
        batch.Text(Context.Assets.Font("medium"),
            champion is null ? "VACANT" : $"C  {champion.DisplayName}",
            new Vector2(rect.Left + 40, rect.Top + 56), champion is null ? Theme.Muted : Theme.Gold);

        if (champion is not null)
        {
            batch.Text(Context.Assets.Font("small"), champion.Record.DisplayWithKo + " · Overall " + champion.Overall,
                new Vector2(rect.Right - 40, rect.Top + 70), Theme.Muted, 1f, TextAlign.Right);
        }

        batch.Fill(new Rectangle(rect.Left + 30, rect.Top + 110, rect.Width - 60, 3), Theme.GoldDark);

        var y = rect.Top + 130;
        batch.Text(Context.Assets.Font("small"), "RANK", new Vector2(rect.Left + 40, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), "FIGHTER", new Vector2(rect.Left + 150, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), "RECORD", new Vector2(rect.Left + 660, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), "PTS", new Vector2(rect.Left + 860, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), "MOVE", new Vector2(rect.Left + 980, y), Theme.Muted);

        // The reference world has a placeholder player; it is not a contender the
        // visitor can look up, so it stays out of the published table.
        var entries = IsReference
            ? table.Entries.Where(e => e.FighterId != state.PlayerId)
            : table.Entries;

        y += 34;
        foreach (var entry in entries.Take(10))
        {
            var isPlayer = !IsReference && entry.FighterId == state.PlayerId;
            var colour = isPlayer ? Theme.Gold : Theme.White;

            batch.Text(Context.Assets.Font("body"), entry.PositionText,
                new Vector2(rect.Left + 40, y), colour);
            batch.Text(Context.Assets.Font("body"), entry.FighterName,
                new Vector2(rect.Left + 150, y), colour);
            batch.Text(Context.Assets.Font("body"), entry.Record,
                new Vector2(rect.Left + 660, y), Theme.Muted);
            batch.Text(Context.Assets.Font("body"), entry.Points.ToString(),
                new Vector2(rect.Left + 860, y), Theme.White);

            var arrow = entry.Movement switch
            {
                RankMovement.Up => Context.Assets.Tex("arrow_up"),
                RankMovement.Down => Context.Assets.Tex("arrow_down"),
                _ => null
            };
            if (arrow is not null)
            {
                batch.Draw(arrow, new Vector2(rect.Left + 1000, y + 18), null, Color.White, 0f,
                    new Vector2(arrow.Width / 2f, arrow.Height / 2f), 0.4f, SpriteEffects.None, 0f);
            }

            batch.Text(Context.Assets.Font("tiny"), entry.MovementText,
                new Vector2(rect.Left + 1040, y + 6), Theme.Muted);

            y += 46;
        }

        if (table.Entries.Count == 0)
        {
            batch.Text(Context.Assets.Font("body"), "No ranked contenders yet.",
                new Vector2(rect.Left + 40, y), Theme.Muted);
        }

        DrawDivisionLadder(batch, 1300);
    }

    private void DrawDivisionLadder(SpriteBatch batch, int x)
    {
        var rect = new Rectangle(x, 260, 540, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), "DIVISIONS", new Vector2(rect.Left + 34, rect.Top + 24), Theme.Gold);

        var y = rect.Top + 64;
        foreach (var division in WeightClasses.All)
        {
            var sanctioned = League.Sanctions(division.Id);
            var current = division.Id == Division.Id;
            batch.Text(Context.Assets.Font("small"),
                $"{division.Name} · {division.LimitText(Context.Units)}",
                new Vector2(rect.Left + 34, y),
                current ? Theme.Gold : sanctioned ? Theme.White : new Color(90, 100, 125));
            if (!sanctioned)
            {
                batch.Text(Context.Assets.Font("tiny"), "not sanctioned",
                    new Vector2(rect.Right - 34, y + 4), new Color(90, 100, 125), 1f, TextAlign.Right);
            }

            y += 36;
        }

        batch.Text(Context.Assets.Font("tiny"),
            "Tap a division chip above to inspect a different table.",
            new Vector2(rect.Left + 34, rect.Bottom - 40), Theme.Muted);
    }

    private void DrawBelts(SpriteBatch batch)
    {
        var state = State;
        var rect = new Rectangle(80, 260, 1760, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        var belt = state.Belts.Get(League.Id, Division.Id, BeltKind.World);
        var interim = state.Belts.Get(League.Id, Division.Id, BeltKind.Interim);
        var continental = state.Belts.Get(League.Id, Division.Id, BeltKind.Continental);

        batch.Text(Context.Assets.Font("medium"), belt.DisplayName.ToUpperInvariant(),
            new Vector2(rect.Left + 44, rect.Top + 30), Theme.Gold);

        var beltTex = Context.Assets.Tex("belt");
        batch.Draw(beltTex, new Rectangle(rect.Left + 44, rect.Top + 90, 520, 214), Color.White);

        DrawBeltSummary(batch, state, belt, rect.Left + 44, rect.Top + 330, "WORLD");
        DrawBeltSummary(batch, state, interim, rect.Left + 44, rect.Top + 400, "INTERIM");
        DrawBeltSummary(batch, state, continental, rect.Left + 44, rect.Top + 470, "CONTINENTAL");

        // Lineage.
        batch.Text(Context.Assets.Font("small"), "TITLE LINEAGE",
            new Vector2(rect.Left + 660, rect.Top + 90), Theme.Gold);

        var y = rect.Top + 130;
        foreach (var reign in Enumerable.Reverse(belt.Lineage).Take(8))
        {
            var active = reign.IsActive;
            batch.Text(Context.Assets.Font("small"),
                $"{reign.FighterName}  ·  won day {reign.WonDay}  ·  {reign.Defences} def.",
                new Vector2(rect.Left + 660, y), active ? Theme.Gold : Theme.White);
            if (!active && reign.LostDay >= 0)
            {
                batch.Text(Context.Assets.Font("tiny"), $"lost day {reign.LostDay}",
                    new Vector2(rect.Right - 60, y + 4), Theme.Muted, 1f, TextAlign.Right);
            }

            y += 34;
        }

        if (belt.Lineage.Count == 0)
        {
            batch.Text(Context.Assets.Font("body"), "Never contested.",
                new Vector2(rect.Left + 660, y), Theme.Muted);
        }

        batch.Text(Context.Assets.Font("small"), "DEFENCES",
            new Vector2(rect.Left + 1320, rect.Top + 90), Theme.Gold);
        batch.Text(Context.Assets.Font("huge"), belt.Defences.ToString(),
            new Vector2(rect.Left + 1320, rect.Top + 130), Theme.White);

        var holder = state.FindFighter(belt.HolderId);
        if (holder is not null)
        {
            batch.Text(Context.Assets.Font("body"), holder.DisplayName,
                new Vector2(rect.Left + 1320, rect.Top + 270), Theme.Gold);
            batch.Text(Context.Assets.Font("small"), holder.Record.DisplayWithKo,
                new Vector2(rect.Left + 1320, rect.Top + 320), Theme.Muted);
            Gfx.Frame(batch, Context.Assets.Tex("fighter_champion"), FighterSheet.FrameWidth,
                FighterSheet.FrameHeight, FighterSheet.Taunt, new Vector2(rect.Left + 1420, rect.Top + 640),
                Color.White, 0.85f);
        }
    }

    private void DrawBeltSummary(SpriteBatch batch, CareerState state, Belt belt, int x, int y, string tier)
    {
        var holder = state.FindFighter(belt.HolderId);
        batch.Text(Context.Assets.Font("tiny"), tier, new Vector2(x, y), Theme.Muted);
        batch.Text(Context.Assets.Font("body"),
            holder is null ? "Vacant" : holder.DisplayName,
            new Vector2(x + 200, y - 8), holder is null ? Theme.Muted : Theme.White);
    }

    private void DrawRules(SpriteBatch batch)
    {
        var rect = new Rectangle(80, 260, 900, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        var rules = League.Ruleset;
        batch.Text(Context.Assets.Font("medium"), rules.Name.ToUpperInvariant(),
            new Vector2(rect.Left + 40, rect.Top + 26), Theme.Gold);

        var lines = new (string Label, string Value)[]
        {
            ("Rounds", rules.Rounds.ToString()),
            ("Slaps per round", rules.SlapsPerRound.ToString()),
            ("Slaps per fighter", rules.SlapsPerFighter.ToString()),
            ("Fouls before DQ", $"{rules.FoulsBeforeDisqualification} ({(rules.Rounds >= 4 ? "championship" : "standard")})"),
            ("Count out", $"{rules.CountOutSeconds} seconds"),
            ("Knockdown recovery", $"{rules.KnockdownRecoverySeconds} seconds"),
            ("Knockdowns allowed", rules.MaxKnockdownsPerRound.ToString()),
            ("Judges", rules.JudgesCount.ToString()),
            ("Title weigh-in allowance", Context.FormatWeightPair(rules.TitleWeighInAllowancePounds)),
            ("Weigh-in allowance", Context.FormatWeightPair(rules.NonTitleWeighInAllowancePounds)),
            ("Same-day weigh-in", rules.SameDayWeighIn ? "Yes" : "No"),
            ("Max weight cut", $"{rules.MaxWeightCutPercent:0.#}%"),
            ("Open hand", rules.OpenHandRequired ? "Required" : "Optional"),
            ("Flinching", rules.FlinchingIsFoul ? "Foul" : "Allowed"),
            ("Stepping off the mark", rules.SteppingIsFoul ? "Foul" : "Allowed")
        };

        // Two columns: the rule rows keep their values inside the left half so
        // they cannot run into the target lists.
        var y = rect.Top + 90;
        var valueRight = rect.Left + 470;
        for (var i = 0; i < lines.Length; i++)
        {
            var row = lines[i];
            batch.Text(Context.Assets.Font("small"), row.Label, new Vector2(rect.Left + 44, y), Theme.Muted);
            batch.Text(Context.Assets.Font("small"), row.Value,
                new Vector2(valueRight, y), Theme.White, 1f, TextAlign.Right);
            y += 36;
        }

        // Legal targets.
        var targetX = rect.Left + 520;
        batch.Text(Context.Assets.Font("small"), "LEGAL TARGETS",
            new Vector2(targetX, rect.Top + 90), Theme.Gold);
        var targetY = rect.Top + 130;
        foreach (var zone in SlapZones.Legal)
        {
            batch.Text(Context.Assets.Font("small"), "✔ " + SlapZones.DisplayName(zone),
                new Vector2(targetX, targetY), Theme.Green);
            targetY += 34;
        }

        batch.Text(Context.Assets.Font("small"), "ILLEGAL TARGETS",
            new Vector2(targetX, targetY + 20), Theme.Gold);
        targetY += 58;
        foreach (var zone in SlapZones.Illegal)
        {
            batch.Text(Context.Assets.Font("tiny"), "✖ " + SlapZones.DisplayName(zone),
                new Vector2(targetX, targetY), Theme.RedLight);
            targetY += 26;
        }

        // Weight table.
        var table = new Rectangle(1000, 260, 840, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), table, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), "WEIGHT CLASSES",
            new Vector2(table.Left + 36, table.Top + 24), Theme.Gold);

        var ty = table.Top + 60;
        batch.Text(Context.Assets.Font("tiny"), "DIVISION", new Vector2(table.Left + 36, ty), Theme.Muted);
        batch.Text(Context.Assets.Font("tiny"), "METRIC", new Vector2(table.Left + 400, ty), Theme.Muted);
        batch.Text(Context.Assets.Font("tiny"), "IMPERIAL", new Vector2(table.Left + 620, ty), Theme.Muted);
        ty += 26;

        foreach (var division in WeightClasses.All)
        {
            var current = division.Id == Division.Id;
            var sanctioned = League.Sanctions(division.Id);
            var colour = current ? Theme.Gold : sanctioned ? Theme.White : new Color(95, 105, 130);

            batch.Text(Context.Assets.Font("small"), division.Name,
                new Vector2(table.Left + 36, ty), colour);
            batch.Text(Context.Assets.Font("small"), division.MetricLimitText,
                new Vector2(table.Left + 400, ty), colour);
            batch.Text(Context.Assets.Font("small"), division.ImperialLimitText,
                new Vector2(table.Left + 620, ty), colour);
            ty += 40;
        }

        batch.WrappedText(Context.Assets.Font("tiny"),
            "Limits are published in pounds; kilos are the rounded equivalent. A 0.25 lb tolerance absorbs scale rounding.",
            new Vector2(table.Left + 36, table.Bottom - 72), 760, Theme.Muted);
    }
}
