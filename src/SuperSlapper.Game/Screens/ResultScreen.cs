using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// The award ceremony that closes a bout: both fighters in the ring, the
/// winner's hand raised, the belt brought out, the cards read out and the purse
/// and ranking points handed over.
/// </summary>
public sealed class ResultScreen : Screen
{
    private readonly List<Button> _buttons = new();
    private CareerOutcome _outcome;
    private float _beat;
    private bool _beltRevealed;
    private float _confettiTimer;

    public ResultScreen(GameContext context, CareerOutcome outcome) : base(context)
    {
        _outcome = outcome;
    }

    private CareerState State => Context.State!;

    public override void OnEnter()
    {
        base.OnEnter();
        _buttons.Clear();
        _buttons.Add(new Button(new Rectangle(740, 890, 440, 120), "CONTINUE",
            () => Context.Screens.SetRoot(new HubScreen(Context)), ButtonStyle.Normal));

        Context.Assets.Play(_outcome.PlayerWon ? "fanfare" : "crowd_boo", 0.75f);
        if (_outcome.PlayerWon)
        {
            Context.Assets.Play("applause_wild", 0.6f);
        }
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        var dt = (float)time.ElapsedGameTime.TotalSeconds;
        _beat += dt;
        _confettiTimer += dt;

        if (!_beltRevealed && Elapsed > 1.2f)
        {
            _beltRevealed = true;
            if (_outcome.IsNowChampion || _outcome.BeltChanges.Count > 0)
            {
                Context.Assets.Play("crowd_cheer", 0.8f);
            }
        }

        foreach (var button in _buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.5f));
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        if (!HasCareer || _outcome is null)
        {
            DrawNoCareer(batch);
            return;
        }

        var result = _outcome.Result;
        var player = State.Player;

        batch.Draw(Context.Assets.Tex("arena_world"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White * 0.55f);
        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
            new Color(5, 8, 16) * 0.45f);

        DrawConfetti(batch);

        // Fighters in the ring.
        var playerFrame = _outcome.PlayerWon ? FighterSheet.Victory : FighterSheet.Stagger;
        var rivalFrame = _outcome.PlayerWon ? FighterSheet.Stagger : FighterSheet.Victory;

        Gfx.Frame(batch, Context.Assets.Tex("fighter_player"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, playerFrame, new Vector2(620, 900), Color.White, 1.15f);
        Gfx.Frame(batch, Context.Assets.Tex("fighter_rival"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, rivalFrame, new Vector2(1300, 900), Color.White, 1.15f,
            SpriteEffects.FlipHorizontally);

        // Referee raising the winner's hand.
        var referee = Context.Assets.Tex("referee");
        batch.Draw(referee, new Vector2(960, 900), new Rectangle(340 * 3, 0, 340, 500),
            Color.White, 0f, new Vector2(170, 500), 1.1f, SpriteEffects.None, 0f);

        // Result banner.
        var banner = new Rectangle(360, 60, 1200, 200);
        batch.NineSlice(Context.Assets.Tex("panel"), banner, 40, Color.White * 0.96f);

        var headline = result.IsDraw ? "DRAW" : _outcome.PlayerWon ? "YOU WIN" : "YOU LOSE";
        var colour = result.IsDraw ? Theme.Amber : _outcome.PlayerWon ? Theme.Gold : Theme.Red;

        var pulse = 1f + 0.02f * (float)Math.Sin(_beat * 3f);
        batch.Text(Context.Assets.Font("huge"), headline,
            new Vector2(banner.Center.X, banner.Top + 34), colour, pulse, TextAlign.Centre);
        batch.Text(Context.Assets.Font("medium"), result.Summary,
            new Vector2(banner.Center.X, banner.Top + 140), Theme.White, 1f, TextAlign.Centre);

        // Award panel: what this bout paid.
        var award = new Rectangle(1330, 300, 520, 620);
        batch.NineSlice(Context.Assets.Tex("panel"), award, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), "AWARD", new Vector2(award.Left + 30, award.Top + 24), Theme.Gold);

        var y = award.Top + 70;
        DrawAwardRow(batch, award, ref y, "Purse", $"${_outcome.Purse:N0}", Theme.Green);
        DrawAwardRow(batch, award, ref y, "Ranking points", $"+{_outcome.RankingPointsGained}", Theme.Gold);
        DrawAwardRow(batch, award, ref y, "Record", player.Record.DisplayWithKo, Theme.White);

        var position = _outcome.NewPosition > 0 ? $"#{_outcome.NewPosition}" : "unranked";
        DrawAwardRow(batch, award, ref y, "Ranking", position, Theme.White);

        if (_outcome.Promoted)
        {
            DrawAwardRow(batch, award, ref y, "Promoted", "NEW LEAGUE", Theme.Gold);
        }

        foreach (var change in _outcome.BeltChanges)
        {
            batch.WrappedText(Context.Assets.Font("small"), change, new Vector2(award.Left + 30, y),
                award.Width - 60, Theme.GoldLight);
            y += 60;
        }

        if (State.Player.IsInjured)
        {
            var injury = State.Player.ActiveInjuries.OrderByDescending(i => i.DaysRemaining).First();
            DrawAwardRow(batch, award, ref y, "Injury", $"{injury.DisplayName} ({injury.DaysRemaining}d)", Theme.RedLight);
        }

        if (_beltRevealed && (player.Id == State.Belts.Get(State.PlayerLeagueId, State.PlayerDivision).HolderId))
        {
            var belt = Context.Assets.Tex("belt");
            var rise = Gfx.EaseOutCubic(Math.Min(1f, (Elapsed - 1.2f) / 0.8f));
            var beltY = award.Bottom + 60 - rise * 160;
            batch.Draw(belt, new Vector2(award.Center.X, beltY), null, Color.White, 0f,
                new Vector2(belt.Width / 2f, belt.Height / 2f), 0.46f, SpriteEffects.None, 0f);
        }

        // Official cards.
        if (result.Scorecards.Count > 0)
        {
            var cards = new Rectangle(80, 300, 500, 300);
            batch.NineSlice(Context.Assets.Tex("panel"), cards, 40, Color.White);
            batch.Text(Context.Assets.Font("small"), "JUDGES' SCORECARDS",
                new Vector2(cards.Left + 30, cards.Top + 24), Theme.Gold);

            var cy = cards.Top + 70;
            foreach (var card in result.Scorecards)
            {
                batch.Text(Context.Assets.Font("small"), card.Judge, new Vector2(cards.Left + 30, cy), Theme.Muted);
                batch.Text(Context.Assets.Font("body"), $"{card.First} - {card.Second}",
                    new Vector2(cards.Right - 30, cy - 6), Theme.White, 1f, TextAlign.Right);
                cy += 44;
            }
        }

        // Fouls and knockdowns.
        var facts = new Rectangle(80, 630, 500, 260);
        batch.NineSlice(Context.Assets.Tex("panel"), facts, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), "FIGHT FACTS",
            new Vector2(facts.Left + 30, facts.Top + 24), Theme.Gold);

        var fy = facts.Top + 70;
        batch.Text(Context.Assets.Font("small"), "Knockdowns", new Vector2(facts.Left + 30, fy), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), $"{result.FighterAKnockdowns} - {result.FighterBKnockdowns}",
            new Vector2(facts.Right - 30, fy), Theme.White, 1f, TextAlign.Right);
        fy += 40;
        batch.Text(Context.Assets.Font("small"), "Fouls", new Vector2(facts.Left + 30, fy), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), $"{result.FighterAFouls} - {result.FighterBFouls}",
            new Vector2(facts.Right - 30, fy), Theme.White, 1f, TextAlign.Right);
        fy += 40;
        batch.Text(Context.Assets.Font("small"), "Damage dealt", new Vector2(facts.Left + 30, fy), Theme.Muted);
        batch.Text(Context.Assets.Font("small"), $"{result.TotalDamageA} - {result.TotalDamageB}",
            new Vector2(facts.Right - 30, fy), Theme.White, 1f, TextAlign.Right);

        foreach (var button in _buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }

    private void DrawAwardRow(SpriteBatch batch, Rectangle panel, ref int y, string label, string value,
        Color colour)
    {
        batch.Text(Context.Assets.Font("small"), label, new Vector2(panel.Left + 30, y), Theme.Muted);
        batch.Text(Context.Assets.Font("body"), value, new Vector2(panel.Right - 30, y - 6), colour, 1f,
            TextAlign.Right);
        y += 46;
    }

    private void DrawConfetti(SpriteBatch batch)
    {
        if (!_outcome.PlayerWon)
        {
            return;
        }

        var rng = new Random(7);
        for (var i = 0; i < 90; i++)
        {
            var x = (rng.Next(0, GameSettings.VirtualWidth) + _confettiTimer * rng.Next(20, 90)) %
                    GameSettings.VirtualWidth;
            var y = (rng.Next(0, GameSettings.VirtualHeight) + _confettiTimer * rng.Next(60, 180)) %
                    GameSettings.VirtualHeight;
            var colour = i % 3 == 0 ? Theme.Gold : i % 3 == 1 ? Theme.White : Theme.BlueLight;
            batch.Fill(x, y, 10, 18, colour * 0.9f);
        }
    }
}
