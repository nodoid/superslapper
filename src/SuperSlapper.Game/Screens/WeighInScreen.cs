using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Rules;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>Morning of the bout: step on the scale, then go and fight.</summary>
public sealed class WeighInScreen : Screen
{
    private readonly List<Button> _buttons = new();
    private WeighInResult? _result;
    private bool _revealed;
    private float _revealTimer;

    public WeighInScreen(GameContext context) : base(context)
    {
    }

    private CareerState State => Context.State!;

    public override void OnEnter()
    {
        base.OnEnter();
        _result = null;
        _revealed = false;
        _revealTimer = 0f;

        _buttons.Clear();
        _buttons.Add(new Button(new Rectangle(760, 880, 440, 120), "STEP ON THE SCALE",
            DoWeighIn, ButtonStyle.Normal));

        _buttons.Add(new Button(new Rectangle(1220, 880, 300, 120), "BACK",
            () => Context.Screens.Pop(), ButtonStyle.Ghost));
    }

    private void DoWeighIn()
    {
        if (_result is not null || State.Offer is null)
        {
            return;
        }

        _result = Context.Career.WeighIn(State, Context.Units);
        _revealed = true;
        _revealTimer = 0f;
        Context.Persist();
        Context.Assets.Play(_result.MadeWeight ? "bell" : "crowd_boo", 0.7f);

        _buttons.Clear();
        _buttons.Add(new Button(new Rectangle(760, 880, 440, 120), "ENTER THE RING",
            () => Context.Screens.Push(new FightScreen(Context)), ButtonStyle.Danger)
        {
            IconKey = "icon_hand"
        });
        _buttons.Add(new Button(new Rectangle(1220, 880, 300, 120), "BACK",
            () => Context.Screens.Pop(), ButtonStyle.Ghost));
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        _revealTimer += (float)time.ElapsedGameTime.TotalSeconds;
        foreach (var button in _buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        if (!HasCareer)
        {
            DrawNoCareer(batch);
            return;
        }

        var state = State;
        var offer = state.Offer;

        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Text(Context.Assets.Font("large"), "OFFICIAL WEIGH-IN", new Vector2(80, 40), Theme.Gold);

        if (offer is null)
        {
            batch.Text(Context.Assets.Font("body"), "No bout booked.",
                new Vector2(120, 300), Theme.Muted);
            foreach (var button in _buttons.ToArray())
            {
                button.Draw(batch, Context.Assets);
            }
            return;
        }

        batch.Text(Context.Assets.Font("small"),
            $"{offer.OpponentName} · {offer.Venue} · {offer.Billing}",
            new Vector2(80, 130), Theme.Muted);

        var panel = new Rectangle(80, 190, 1080, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), panel, 40, Color.White);

        var scaleTex = Context.Assets.Tex("scale");
        batch.Draw(scaleTex, new Rectangle(panel.Left + 50, panel.Top + 90, 560, 350), Color.White);

        var division = WeightClasses.Get(state.PlayerDivision);
        var player = state.Player;

        batch.Text(Context.Assets.Font("small"), "DIVISION LIMIT",
            new Vector2(panel.Left + 60, panel.Top + 460), Theme.Muted);
        batch.Text(Context.Assets.Font("large"), division.LimitText(Context.Units),
            new Vector2(panel.Left + 60, panel.Top + 492), Theme.Gold);

        batch.Text(Context.Assets.Font("small"), "YOUR WEIGHT",
            new Vector2(panel.Left + 360, panel.Top + 460), Theme.Muted);
        batch.Text(Context.Assets.Font("large"),
            Context.FormatWeight(_result?.ActualPounds ?? player.WeightPounds),
            new Vector2(panel.Left + 360, panel.Top + 492), Theme.White);

        var rules = SuperSlapper.Core.Leagues.Leagues.Get(offer.LeagueId).Ruleset;
        batch.Text(Context.Assets.Font("tiny"),
            $"Allowance: {Context.FormatWeightPair(offer.TitleFight ? rules.TitleWeighInAllowancePounds : rules.NonTitleWeighInAllowancePounds)}" +
            (offer.TitleFight ? " (title)" : " (standard)"),
            new Vector2(panel.Left + 660, panel.Top + 464), Theme.Muted);

        batch.Text(Context.Assets.Font("small"),
            $"Walk-around: {Context.FormatWeight(player.WalkAroundPounds)}",
            new Vector2(panel.Left + 660, panel.Top + 500), Theme.Muted);

        // Verdict.
        if (_result is not null && _revealed && _revealTimer > 0.2f)
        {
            var colour = _result.MadeWeight ? Theme.Green : Theme.Red;
            batch.Fill(new Rectangle(panel.Left + 50, panel.Top + 540, panel.Width - 100, 70),
                colour * 0.22f);
            batch.Text(Context.Assets.Font("medium"), _result.VerdictText.ToUpperInvariant(),
                new Vector2(panel.Left + 70, panel.Top + 550), colour);

            batch.Text(Context.Assets.Font("small"), _result.Summary,
                new Vector2(panel.Left + 70, panel.Top + 590), Theme.White);

            if (!_result.MadeWeight)
            {
                batch.Text(Context.Assets.Font("small"),
                    $"Purse fine {_result.PurseFinePercent:0}% · championship not at stake",
                    new Vector2(panel.Right - 70, panel.Top + 590), Theme.RedLight, 1f, TextAlign.Right);
            }
            else if (_result.CutPercent > rules.MaxWeightCutPercent)
            {
                batch.Text(Context.Assets.Font("small"),
                    $"Cut {_result.CutPercent:0.#}% - above the {rules.MaxWeightCutPercent:0.#}% limit",
                    new Vector2(panel.Right - 70, panel.Top + 590), Theme.Amber, 1f, TextAlign.Right);
            }
        }

        // Opponent card.
        var rivalPanel = new Rectangle(1200, 190, 640, 640);
        batch.NineSlice(Context.Assets.Tex("panel"), rivalPanel, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), "OPPONENT",
            new Vector2(rivalPanel.Left + 36, rivalPanel.Top + 24), Theme.Gold);

        Gfx.Frame(batch, Context.Assets.Tex("fighter_rival"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, FighterSheet.Taunt,
            new Vector2(rivalPanel.Center.X, rivalPanel.Top + 520), Color.White, 0.8f,
            SpriteEffects.FlipHorizontally);

        var opponent = state.FindFighter(offer.OpponentId);
        if (opponent is not null)
        {
            batch.Text(Context.Assets.Font("medium"), opponent.DisplayName,
                new Vector2(rivalPanel.Center.X, rivalPanel.Top + 60), Theme.White, 0.9f, TextAlign.Centre);
            batch.Text(Context.Assets.Font("body"),
                $"{opponent.Record.DisplayWithKo} · Overall {opponent.Overall}",
                new Vector2(rivalPanel.Center.X, rivalPanel.Top + 130), Theme.Muted, 1f, TextAlign.Centre);
            batch.Text(Context.Assets.Font("body"),
                $"{Context.FormatWeight(opponent.WeightPounds)}",
                new Vector2(rivalPanel.Center.X, rivalPanel.Top + 176), Theme.White, 1f, TextAlign.Centre);
        }

        foreach (var button in _buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }
}
