using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Localisation;
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

    private Tab _tab;
    private int _leagueIndex;
    private int _divisionIndex;

    public RecordsScreen(GameContext context, Tab tab) : base(context) => _tab = tab;

    /// <summary>The tab and the two chips' positions, for the button sweep.</summary>
    public override string ViewState => $"{_tab}|{_leagueIndex}|{_divisionIndex}";

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
    /// <summary>
    /// The three tab chips, laid out left to right under the heading. They are wide
    /// and tall on purpose: this is the row a thumb has to hit on a phone, and a
    /// tap that lands one chip over is a tap that changes the wrong page.
    /// </summary>
    private static Rectangle TabRect(int index) => new(80 + index * 360, 145, 340, 104);

    private void BuildButtons()
    {
        ClearControls();

        var tabs = new[]
        {
            (Loc.T("hub.rankings"), Tab.Rankings),
            (Loc.T("hub.belts"), Tab.Belts),
            (Loc.T("hub.rules"), Tab.Rules)
        };
        for (var i = 0; i < tabs.Length; i++)
        {
            var target = tabs[i].Item2;
            Add(new Button(TabRect(i), tabs[i].Item1,
                () => Open(target), i == (int)_tab ? ButtonStyle.Normal : ButtonStyle.Ghost));
        }

        Add(new Button(new Rectangle(1420, 150, 200, 84), League.ShortName,
            () =>
            {
                _leagueIndex = (_leagueIndex + 1) % Leagues.All.Count;
                _divisionIndex = 0;
                BuildButtons();
            }, ButtonStyle.Ghost));

        Add(new Button(new Rectangle(1640, 150, 200, 84), Division.ShortName,
            () =>
            {
                _divisionIndex = (_divisionIndex + 1) % League.Divisions.Count;
                BuildButtons();
            }, ButtonStyle.Ghost));

        // Big, framed and clear of the panels, because it is the only way out of the
        // book on a phone.
        Add(new Button(new Rectangle(1520, 860, 340, 140), Loc.T("common.back"), Leave,
            ButtonStyle.Normal));
    }

    /// <summary>
    /// Turns to a page. The chips are rebuilt as well as the page: the frame on the
    /// open tab is the only thing telling a finger which page it just opened.
    /// </summary>
    private void Open(Tab tab)
    {
        _tab = tab;
        _divisionIndex = 0;
        BuildButtons();
    }

    /// <summary>Shuts the book. Always leaves it, even with nothing behind it.</summary>
    private void Leave()
    {
        Context.Persist();

        if (Context.Screens.Depth > 1)
        {
            Context.Screens.Pop();
            return;
        }

        Context.Screens.SetRoot(Context.State is null
            ? new TitleScreen(Context)
            : new HubScreen(Context));
    }

    public override void Update(GameTime time, InputState input)
    {
        foreach (var button in Buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Text(Context.Assets.Font("large"), Loc.T("records.title"),
            new Vector2(80, 40), Theme.Gold);
        batch.Text(Context.Assets.Font("small"),
            Loc.T("records.subtitle", League.DisplayName, Division.Name,
                Division.LimitText(Context.Units), Context.UnitLabel),
            new Vector2(80, 120), Theme.Muted);

        // The page first, then the controls over it. The panels run to the foot of
        // the screen, and a BACK button drawn underneath one is a button that looks
        // half cut off - so the tabs, the chips and BACK all sit on top.
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

        foreach (var button in Buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }

        // The open page wears a gold frame, so which tab is showing is never a guess.
        batch.Outline(TabRect((int)_tab), Theme.Gold, 4);
    }

    private void DrawRankings(SpriteBatch batch)
    {
        var state = State;
        var table = state.Rankings.Get(League.Id, Division.Id);
        var rect = new Rectangle(80, 260, 1180, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        batch.Text(Context.Assets.Font("small"), Loc.T("records.champion"),
            new Vector2(rect.Left + 40, rect.Top + 24), Theme.Gold);

        var champion = state.FindFighter(table.ChampionId);
        batch.Text(Context.Assets.Font("medium"),
            champion is null ? Loc.T("records.vacantCapital") : Loc.T("records.championLine", champion.DisplayName),
            new Vector2(rect.Left + 40, rect.Top + 56), champion is null ? Theme.Muted : Theme.Gold);

        if (champion is not null)
        {
            batch.Text(Context.Assets.Font("small"),
                Loc.T("hub.opponentLine", champion.Record.DisplayWithKo, champion.Overall),
                new Vector2(rect.Right - 40, rect.Top + 70), Theme.Muted, 1f, TextAlign.Right);
        }

        batch.Fill(new Rectangle(rect.Left + 30, rect.Top + 110, rect.Width - 60, 3), Theme.GoldDark);

        var y = rect.Top + 130;
        batch.Text(Context.Assets.Font("small"), Loc.T("records.rank"),
            new Vector2(rect.Left + 40, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), Loc.T("records.fighter"),
            new Vector2(rect.Left + 150, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), Loc.T("records.record"),
            new Vector2(rect.Left + 660, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), Loc.T("records.points"),
            new Vector2(rect.Left + 860, y), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), Loc.T("records.move"),
            new Vector2(rect.Left + 980, y), Theme.Muted);

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
            batch.Text(Context.Assets.Font("body"), Loc.T("records.noContenders"),
                new Vector2(rect.Left + 40, y), Theme.Muted);
        }

        DrawDivisionLadder(batch, 1300);
    }

    private void DrawDivisionLadder(SpriteBatch batch, int x)
    {
        var rect = new Rectangle(x, 260, 540, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), Loc.T("records.divisions"),
            new Vector2(rect.Left + 34, rect.Top + 24), Theme.Gold);

        var y = rect.Top + 64;
        foreach (var division in WeightClasses.All)
        {
            var sanctioned = League.Sanctions(division.Id);
            var current = division.Id == Division.Id;
            batch.Text(Context.Assets.Font("small"),
                Loc.T("records.divisionLine", division.Name, division.LimitText(Context.Units)),
                new Vector2(rect.Left + 34, y),
                current ? Theme.Gold : sanctioned ? Theme.White : new Color(90, 100, 125));
            if (!sanctioned)
            {
                batch.Text(Context.Assets.Font("tiny"), Loc.T("records.notSanctioned"),
                    new Vector2(rect.Right - 34, y + 4), new Color(90, 100, 125), 1f, TextAlign.Right);
            }

            y += 36;
        }

        batch.Text(Context.Assets.Font("tiny"), Loc.T("records.chipHint"),
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

        DrawBeltSummary(batch, state, belt, rect.Left + 44, rect.Top + 330, Loc.T("records.world"));
        DrawBeltSummary(batch, state, interim, rect.Left + 44, rect.Top + 400, Loc.T("records.interim"));
        DrawBeltSummary(batch, state, continental, rect.Left + 44, rect.Top + 470,
            Loc.T("records.continental"));

        // Lineage.
        batch.Text(Context.Assets.Font("small"), Loc.T("records.lineage"),
            new Vector2(rect.Left + 660, rect.Top + 90), Theme.Gold);

        var y = rect.Top + 130;
        foreach (var reign in Enumerable.Reverse(belt.Lineage).Take(8))
        {
            var active = reign.IsActive;
            batch.Text(Context.Assets.Font("small"),
                Loc.T("records.reign", reign.FighterName, reign.WonDay, reign.Defences),
                new Vector2(rect.Left + 660, y), active ? Theme.Gold : Theme.White);
            if (!active && reign.LostDay >= 0)
            {
                batch.Text(Context.Assets.Font("tiny"), Loc.T("records.lostDay", reign.LostDay),
                    new Vector2(rect.Right - 60, y + 4), Theme.Muted, 1f, TextAlign.Right);
            }

            y += 34;
        }

        if (belt.Lineage.Count == 0)
        {
            batch.Text(Context.Assets.Font("body"), Loc.T("records.neverContested"),
                new Vector2(rect.Left + 660, y), Theme.Muted);
        }

        batch.Text(Context.Assets.Font("small"), Loc.T("records.defences"),
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
            holder is null ? Loc.T("records.vacant") : holder.DisplayName,
            new Vector2(x + 200, y - 8), holder is null ? Theme.Muted : Theme.White);
    }

    private void DrawRules(SpriteBatch batch)
    {
        var rect = new Rectangle(80, 260, 900, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        var rules = League.Ruleset;
        batch.Text(Context.Assets.Font("medium"), rules.DisplayName.ToUpperInvariant(),
            new Vector2(rect.Left + 40, rect.Top + 26), Theme.Gold);

        var lines = new (string Label, string Value)[]
        {
            (Loc.T("records.rule.rounds"), rules.Rounds.ToString()),
            (Loc.T("records.rule.slapsPerRound"), rules.SlapsPerRound.ToString()),
            (Loc.T("records.rule.slapsPerFighter"), rules.SlapsPerFighter.ToString()),
            (Loc.T("records.rule.fouls"), Loc.T(rules.Rounds >= 4
                ? "records.rule.foulsChampionship"
                : "records.rule.foulsStandard", rules.FoulsBeforeDisqualification)),
            (Loc.T("records.rule.countOut"), Loc.T("common.seconds", rules.CountOutSeconds)),
            (Loc.T("records.rule.recovery"), Loc.T("common.seconds", rules.KnockdownRecoverySeconds)),
            (Loc.T("records.rule.knockdowns"), rules.MaxKnockdownsPerRound.ToString()),
            (Loc.T("records.rule.judges"), rules.JudgesCount.ToString()),
            (Loc.T("records.rule.titleAllowance"),
                Context.FormatWeightPair(rules.TitleWeighInAllowancePounds)),
            (Loc.T("records.rule.allowance"),
                Context.FormatWeightPair(rules.NonTitleWeighInAllowancePounds)),
            (Loc.T("records.rule.sameDay"), Loc.T(rules.SameDayWeighIn ? "common.yes" : "common.no")),
            (Loc.T("records.rule.maxCut"), Loc.T("common.percent", rules.MaxWeightCutPercent.ToString("0.#"))),
            (Loc.T("records.rule.openHand"), Loc.T(rules.OpenHandRequired
                ? "records.rule.required"
                : "records.rule.optional")),
            (Loc.T("records.rule.flinching"), Loc.T(rules.FlinchingIsFoul
                ? "records.rule.foul"
                : "records.rule.allowed")),
            (Loc.T("records.rule.stepping"), Loc.T(rules.SteppingIsFoul
                ? "records.rule.foul"
                : "records.rule.allowed"))
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
        batch.Text(Context.Assets.Font("small"), Loc.T("records.legalTargets"),
            new Vector2(targetX, rect.Top + 90), Theme.Gold);
        var targetY = rect.Top + 130;
        foreach (var zone in SlapZones.Legal)
        {
            batch.Text(Context.Assets.Font("small"), "✔ " + SlapZones.DisplayName(zone),
                new Vector2(targetX, targetY), Theme.Green);
            targetY += 34;
        }

        batch.Text(Context.Assets.Font("small"), Loc.T("records.illegalTargets"),
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
        batch.Text(Context.Assets.Font("small"), Loc.T("records.weightClasses"),
            new Vector2(table.Left + 36, table.Top + 24), Theme.Gold);

        var ty = table.Top + 60;
        batch.Text(Context.Assets.Font("tiny"), Loc.T("records.division"),
            new Vector2(table.Left + 36, ty), Theme.Muted);
        batch.Text(Context.Assets.Font("tiny"), Loc.T("records.metric"),
            new Vector2(table.Left + 400, ty), Theme.Muted);
        batch.Text(Context.Assets.Font("tiny"), Loc.T("records.imperial"),
            new Vector2(table.Left + 620, ty), Theme.Muted);
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

        batch.WrappedText(Context.Assets.Font("tiny"), Loc.T("records.weightNote"),
            new Vector2(table.Left + 36, table.Bottom - 72), 760, Theme.Muted);
    }
}
