using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>The career dashboard: who you are, where you are ranked, who is next.</summary>
public sealed class HubScreen : Screen
{
    private readonly SoundToggle _music;
    private readonly SoundToggle _effects;
    private float _time;

    public HubScreen(GameContext context) : base(context)
    {
        // In the top bar, clear of the career details on the left and of the purse,
        // which is right-aligned and grows with the career.
        _music = new SoundToggle(new Rectangle(700, 24, 240, 66), context, AudioChannel.Music);
        _effects = new SoundToggle(new Rectangle(950, 24, 240, 66), context, AudioChannel.Effects);
    }

    private CareerState State => Context.State!;

    public override void OnEnter()
    {
        base.OnEnter();
        ClearControls();

        // One row across the foot of the screen, tall enough to hit with a
        // thumb and lifted clear of the phone's home-indicator band.
        const int rowY = 890;
        const int rowH = 120;
        const int buttonW = 276;
        const int gap = 20;
        var x = 80;

        Button Footer(string text, Action click, ButtonStyle style, string? icon = null)
        {
            var button = new Button(new Rectangle(x, rowY, buttonW, rowH), text, click, style)
            {
                IconKey = icon
            };
            x += buttonW + gap;
            return button;
        }

        Add(Footer(Loc.T("hub.training"), () => Context.Screens.Push(new TrainingScreen(Context)),
            ButtonStyle.Normal, "icon_weights"));

        Add(Footer(Loc.T("hub.rankings"),
            () => Context.Screens.Push(new RecordsScreen(Context, RecordsScreen.Tab.Rankings)),
            ButtonStyle.Ghost));

        Add(Footer(Loc.T("hub.belts"),
            () => Context.Screens.Push(new RecordsScreen(Context, RecordsScreen.Tab.Belts)),
            ButtonStyle.Ghost));

        Add(Footer(Loc.T("hub.rules"),
            () => Context.Screens.Push(new RecordsScreen(Context, RecordsScreen.Tab.Rules)),
            ButtonStyle.Ghost));

        Add(Footer(Loc.T("hub.calendar"), () => Context.Screens.Push(new CalendarScreen(Context)),
            ButtonStyle.Ghost));

        Add(Footer(Loc.T("hub.weighIn"), () => Context.Screens.Push(new WeighInScreen(Context)),
            ButtonStyle.Danger, "icon_hand"));

        // The sheet itself sits in the top bar: the footer row is full, and this is
        // the only corner of it nothing else claims.
        Add(new Button(new Rectangle(1540, 24, 280, 66), Loc.T("hub.settings"),
            () => Context.Screens.Push(new SettingsScreen(Context)), ButtonStyle.Ghost));

        // The sound chips handle their own presses, so they are declared as regions.
        AddTarget(_music.ChipLabel, _music.Bounds);
        AddTarget(_effects.ChipLabel, _effects.Bounds);
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        _time += (float)time.ElapsedGameTime.TotalSeconds;
        foreach (var button in Buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.5f));
        }

        _music.Update(time, input);
        _effects.Update(time, input);
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        if (!HasCareer)
        {
            DrawNoCareer(batch);
            return;
        }

        var state = State;
        var player = state.Player;
        var league = Leagues.Get(state.PlayerLeagueId);

        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        DrawTopBar(batch, state, league);

        // ---- Player card.
        var card = new Rectangle(80, 150, 900, 700);
        batch.NineSlice(Context.Assets.Tex("panel"), card, 40, Color.White);

        var head = new Rectangle(card.Left + 30, card.Top + 40, 300, 340);
        batch.Fill(head, Theme.Blue * 0.35f);
        Gfx.Frame(batch, Context.Assets.Tex("fighter_player"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, FighterSheet.Idle(_time), new Vector2(head.Center.X, head.Bottom + 20),
            Color.White, 0.72f);

        var textX = card.Left + 360;
        batch.Text(Context.Assets.Font("small"), Loc.T("hub.yourFighter"),
            new Vector2(card.Left + 40, card.Top + 26), Theme.Gold);
        batch.Text(Context.Assets.Font("large"), player.LastName.ToUpperInvariant(),
            new Vector2(textX, card.Top + 40), Theme.White);
        batch.Text(Context.Assets.Font("body"), $"\"{player.Nickname}\"", new Vector2(textX + 4, card.Top + 130),
            Theme.Muted);
        batch.Text(Context.Assets.Font("medium"), player.Record.DisplayWithKo,
            new Vector2(textX, card.Top + 200), Theme.Gold);

        var division = WeightClasses.Get(state.PlayerDivision);
        batch.Text(Context.Assets.Font("body"),
            Loc.T("hub.weightLine", division.Name, Context.FormatWeight(player.WeightPounds)),
            new Vector2(textX, card.Top + 260), Theme.White);

        var table = state.Rankings.Get(league.Id, state.PlayerDivision);
        var belt = state.Belts.Get(league.Id, state.PlayerDivision, BeltKind.World);
        var isChampion = belt.HolderId == player.Id;
        var position = table.PositionOf(player.Id);

        var rankText = isChampion ? Loc.T("hub.worldChampion")
            : position == int.MaxValue ? Loc.T("hub.unranked")
            : Loc.T("hub.ranked", position);
        batch.Text(Context.Assets.Font("body"), rankText, new Vector2(textX, card.Top + 310),
            isChampion ? Theme.Gold : Theme.White);

        if (belt.HolderId == player.Id)
        {
            var beltTex = Context.Assets.Tex("belt");
            batch.Draw(beltTex, new Rectangle(card.Right - 300, card.Top + 20, 260, 108), Color.White);
        }

        // Attribute bars.
        var stats = new[]
        {
            (Loc.T("attribute.Power").ToUpperInvariant(), player.Attributes.Power),
            (Loc.T("attribute.Aim").ToUpperInvariant(), player.Attributes.Aim),
            (Loc.T("attribute.Chin").ToUpperInvariant(), player.Attributes.Chin),
            (Loc.T("attribute.Endurance").ToUpperInvariant(), player.Attributes.Endurance),
            (Loc.T("attribute.Reflex").ToUpperInvariant(), player.Attributes.Reflex),
            (Loc.T("attribute.Mass").ToUpperInvariant(), player.Attributes.Mass)
        };

        for (var i = 0; i < stats.Length; i++)
        {
            var x = card.Left + 60 + (i % 3) * 280;
            var y = card.Top + 420 + (i / 3) * 96;
            StatBar.Draw(batch, Context.Assets, new Rectangle(x, y, 240, 26), stats[i].Item1,
                stats[i].Item2, i % 3 == 0 ? Theme.Red : i % 3 == 1 ? Theme.Blue : Theme.Green);
        }

        // Condition row.
        var energyY = card.Bottom - 90;
        StatBar.Draw(batch, Context.Assets, new Rectangle(card.Left + 60, energyY, 240, 22),
            Loc.T("hub.energy"), player.Energy, Theme.Green);
        StatBar.Draw(batch, Context.Assets, new Rectangle(card.Left + 340, energyY, 240, 22),
            Loc.T("hub.headDamage"), player.Damage, Theme.Amber);
        StatBar.Draw(batch, Context.Assets, new Rectangle(card.Left + 620, energyY, 240, 22),
            Loc.T("hub.popularity"), player.Popularity, Theme.Gold);

        if (player.IsInjured)
        {
            var injury = player.ActiveInjuries.OrderByDescending(i => i.DaysRemaining).First();
            batch.Text(Context.Assets.Font("small"),
                Loc.T("hub.injured", injury.DisplayName, injury.SeverityText, injury.DaysRemaining),
                new Vector2(card.Left + 60, energyY + 36), Theme.RedLight);
        }
        else
        {
            batch.Text(Context.Assets.Font("small"), Loc.T("hub.noInjuries"),
                new Vector2(card.Left + 60, energyY + 36), Theme.Green);
        }

        DrawFightCard(batch, state, league);

        foreach (var button in Buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }

        _music.Draw(batch);
        _effects.Draw(batch);
    }

    private void DrawTopBar(SpriteBatch batch, CareerState state, League league)
    {
        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, 110), new Color(8, 12, 22) * 0.9f);

        batch.Text(Context.Assets.Font("medium"), state.CareerName.ToUpperInvariant(),
            new Vector2(80, 26), Theme.Gold);
        batch.Text(Context.Assets.Font("small"),
            Loc.T("hub.leagueLine", league.DisplayName, league.Prestige, state.Season),
            new Vector2(80, 72), Theme.Muted);

        batch.Text(Context.Assets.Font("medium"), Loc.T("hub.money", state.Money.ToString("N0")),
            new Vector2(1500, 26), Theme.Green, 1f, TextAlign.Right);
        batch.Text(Context.Assets.Font("small"),
            Loc.T("hub.dayLine", state.Day, Context.UnitLabel),
            new Vector2(1500, 72), Theme.Muted, 1f, TextAlign.Right);
    }

    private void DrawFightCard(SpriteBatch batch, CareerState state, League league)
    {
        var rect = new Rectangle(1010, 150, 830, 700);
        batch.NineSlice(Context.Assets.Tex("panel"), rect, 40, Color.White);

        var offer = state.Offer;
        if (offer is null)
        {
            batch.Text(Context.Assets.Font("medium"), Loc.T("hub.noBout"),
                new Vector2(rect.Center.X, rect.Center.Y), Theme.Muted, 1f, TextAlign.Centre);
            return;
        }

        batch.Text(Context.Assets.Font("small"), Loc.T("hub.nextBout"),
            new Vector2(rect.Left + 40, rect.Top + 26), Theme.Gold);

        if (offer.TitleFight)
        {
            var badge = new Rectangle(rect.Right - 300, rect.Top + 24, 260, 52);
            batch.Fill(badge, Theme.Gold);
            batch.Text(Context.Assets.Font("small"), Loc.T("hub.championship"),
                new Vector2(badge.Center.X, badge.Top + 12),
                Theme.Ink, 1f, TextAlign.Centre, shadow: false);
        }

        // The portrait is the backdrop; every label is drawn after it so the
        // head can never swallow the name or the record line.
        var opponent = state.FindFighter(offer.OpponentId);
        Gfx.Frame(batch, Context.Assets.Tex("fighter_rival"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, FighterSheet.Idle(_time + 0.4f),
            new Vector2(rect.Center.X, rect.Top + 510), Color.White * 0.55f, 0.9f,
            SpriteEffects.FlipHorizontally);

        batch.Text(Context.Assets.Font("large"), offer.OpponentName.Split(' ').Last().ToUpperInvariant(),
            new Vector2(rect.Center.X, rect.Top + 90), Theme.White, 0.9f, TextAlign.Centre);

        if (opponent is not null)
        {
            // Below the portrait and the ranking rows, so it never collides.
            batch.Text(Context.Assets.Font("body"),
                Loc.T("hub.opponentLine", opponent.Record.DisplayWithKo, opponent.Overall),
                new Vector2(rect.Center.X, rect.Top + 580), Theme.Muted, 1f, TextAlign.Centre);
        }

        var days = offer.DaysUntilFight(state.Day);
        batch.Text(Context.Assets.Font("medium"),
            days <= 0 ? Loc.T("hub.fightDay") : Loc.T("hub.days", days),
            new Vector2(rect.Left + 50, rect.Top + 120), days <= 0 ? Theme.RedLight : Theme.Gold);

        batch.Text(Context.Assets.Font("body"), offer.Billing,
            new Vector2(rect.Left + 50, rect.Top + 180), Theme.White);
        batch.Text(Context.Assets.Font("body"), offer.Venue,
            new Vector2(rect.Left + 50, rect.Top + 226), Theme.Muted);
        batch.Text(Context.Assets.Font("body"), Loc.T("hub.purse", offer.Purse.ToString("N0")),
            new Vector2(rect.Left + 50, rect.Top + 272), Theme.Green);

        var rules = league.Ruleset;
        batch.Text(Context.Assets.Font("small"),
            Loc.T("hub.rulesLine", rules.Rounds, rules.SlapsPerRound, rules.FoulsBeforeDisqualification),
            new Vector2(rect.Left + 50, rect.Top + 320), Theme.Muted);

        // Ranking ladder around the player.
        var table = state.Rankings.Get(league.Id, state.PlayerDivision);
        batch.Text(Context.Assets.Font("small"), Loc.T("hub.top5"),
            new Vector2(rect.Left + 50, rect.Top + 380), Theme.Gold);

        var rows = table.Entries.Take(5).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var y = rect.Top + 414 + i * 30;
            var row = rows[i];
            var isPlayer = row.FighterId == state.PlayerId;
            batch.Text(Context.Assets.Font("small"), $"{row.PositionText} {row.FighterName}",
                new Vector2(rect.Left + 60, y), isPlayer ? Theme.Gold : Theme.White);
            batch.Text(Context.Assets.Font("small"), row.Points.ToString(),
                new Vector2(rect.Right - 70, y), Theme.Muted, 1f, TextAlign.Right);
        }

        DrawRewardPreview(batch, state, offer, rect);
    }

    private void DrawRewardPreview(SpriteBatch batch, CareerState state, FightOffer offer, Rectangle rect)
    {
        var days = offer.DaysUntilFight(state.Day);
        if (days > 0)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * (float)Math.Sin(Elapsed * 3f);
        batch.Fill(new Rectangle(rect.Left + 40, rect.Bottom - 96, rect.Width - 80, 60),
            Theme.Red * (0.25f + 0.25f * pulse));
        batch.Text(Context.Assets.Font("body"), Loc.T("hub.stepOnScale"),
            new Vector2(rect.Center.X, rect.Bottom - 80), Theme.White, 1f, TextAlign.Centre);
    }
}
