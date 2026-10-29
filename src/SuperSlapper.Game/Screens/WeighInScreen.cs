using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>Morning of the bout: step on the scale, then go and fight.</summary>
public sealed class WeighInScreen : Screen
{
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

        ClearControls();
        Add(new Button(new Rectangle(760, 880, 440, 120), Loc.T("weighInScreen.stepOnScale"),
            DoWeighIn, ButtonStyle.Normal));

        Add(new Button(new Rectangle(1220, 880, 300, 120), Loc.T("common.back"),
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

        ClearControls();
        Add(new Button(new Rectangle(760, 880, 440, 120), Loc.T("weighInScreen.enterRing"),
            () => Context.Screens.Push(new FightScreen(Context)), ButtonStyle.Danger)
        {
            IconKey = "icon_hand"
        });
        Add(new Button(new Rectangle(1220, 880, 300, 120), Loc.T("common.back"),
            () => Context.Screens.Pop(), ButtonStyle.Ghost));
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        _revealTimer += (float)time.ElapsedGameTime.TotalSeconds;
        foreach (var button in Buttons.ToArray())
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

        batch.Text(Context.Assets.Font("large"), Loc.T("weighInScreen.title"),
            new Vector2(80, 40), Theme.Gold);

        if (offer is null)
        {
            batch.Text(Context.Assets.Font("body"), Loc.T("weighInScreen.noBout"),
                new Vector2(120, 300), Theme.Muted);
            foreach (var button in Buttons.ToArray())
            {
                button.Draw(batch, Context.Assets);
            }
            return;
        }

        batch.Text(Context.Assets.Font("small"),
            Loc.T("weighInScreen.header", offer.OpponentName, offer.Venue, offer.Billing),
            new Vector2(80, 130), Theme.Muted);

        var panel = new Rectangle(80, 190, 1080, 660);
        batch.NineSlice(Context.Assets.Tex("panel"), panel, 40, Color.White);

        var scaleTex = Context.Assets.Tex("scale");
        batch.Draw(scaleTex, new Rectangle(panel.Left + 50, panel.Top + 90, 560, 350), Color.White);

        var division = WeightClasses.Get(state.PlayerDivision);
        var player = state.Player;

        batch.Text(Context.Assets.Font("small"), Loc.T("weighInScreen.divisionLimit"),
            new Vector2(panel.Left + 60, panel.Top + 460), Theme.Muted);
        batch.Text(Context.Assets.Font("large"), division.LimitText(Context.Units),
            new Vector2(panel.Left + 60, panel.Top + 492), Theme.Gold);

        batch.Text(Context.Assets.Font("small"), Loc.T("weighInScreen.yourWeight"),
            new Vector2(panel.Left + 360, panel.Top + 460), Theme.Muted);
        batch.Text(Context.Assets.Font("large"),
            Context.FormatWeight(_result?.ActualPounds ?? player.WeightPounds),
            new Vector2(panel.Left + 360, panel.Top + 492), Theme.White);

        var rules = Leagues.Get(offer.LeagueId).Ruleset;
        batch.Text(Context.Assets.Font("tiny"),
            Loc.T(offer.TitleFight ? "weighInScreen.allowanceTitle" : "weighInScreen.allowanceStandard",
                Context.FormatWeightPair(offer.TitleFight
                    ? rules.TitleWeighInAllowancePounds
                    : rules.NonTitleWeighInAllowancePounds)),
            new Vector2(panel.Left + 660, panel.Top + 464), Theme.Muted);

        batch.Text(Context.Assets.Font("small"),
            Loc.T("weighInScreen.walkAround", Context.FormatWeight(player.WalkAroundPounds)),
            new Vector2(panel.Left + 660, panel.Top + 500), Theme.Muted);

        // Verdict. It sits below the two big readings, which are drawn in the large
        // face and run past the baseline of their own row.
        if (_result is not null && _revealed && _revealTimer > 0.2f)
        {
            var colour = _result.MadeWeight ? Theme.Green : Theme.Red;
            batch.Fill(new Rectangle(panel.Left + 50, panel.Top + 566, panel.Width - 100, 76),
                colour * 0.22f);
            batch.Text(Context.Assets.Font("medium"), _result.VerdictText.ToUpperInvariant(),
                new Vector2(panel.Left + 70, panel.Top + 572), colour);

            batch.Text(Context.Assets.Font("small"), _result.Summary,
                new Vector2(panel.Left + 70, panel.Top + 612), Theme.White);

            if (!_result.MadeWeight)
            {
                batch.Text(Context.Assets.Font("small"),
                    Loc.T("weighInScreen.purseFine", _result.PurseFinePercent.ToString("0")),
                    new Vector2(panel.Right - 70, panel.Top + 612), Theme.RedLight, 1f, TextAlign.Right);
            }
            else if (_result.CutPercent > rules.MaxWeightCutPercent)
            {
                batch.Text(Context.Assets.Font("small"),
                    Loc.T("weighInScreen.cutTooMuch", _result.CutPercent.ToString("0.#"),
                        rules.MaxWeightCutPercent.ToString("0.#")),
                    new Vector2(panel.Right - 70, panel.Top + 612), Theme.Amber, 1f, TextAlign.Right);
            }
        }

        // Opponent card.
        var rivalPanel = new Rectangle(1200, 190, 640, 660);
        batch.NineSlice(Context.Assets.Tex("panel"), rivalPanel, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), Loc.T("weighInScreen.opponent"),
            new Vector2(rivalPanel.Left + 36, rivalPanel.Top + 24), Theme.Gold);

        // Standing on the floor of his own card, and small enough that his head stays
        // clear of the record and the weight printed above him.
        Gfx.Frame(batch, Context.Assets.Tex("fighter_rival"), FighterSheet.FrameWidth,
            FighterSheet.FrameHeight, FighterSheet.Taunt,
            new Vector2(rivalPanel.Center.X, rivalPanel.Bottom - 28), Color.White, 0.58f,
            SpriteEffects.FlipHorizontally);

        var opponent = state.FindFighter(offer.OpponentId);
        if (opponent is not null)
        {
            batch.Text(Context.Assets.Font("medium"), opponent.DisplayName,
                new Vector2(rivalPanel.Center.X, rivalPanel.Top + 60), Theme.White, 0.9f, TextAlign.Centre);
            batch.Text(Context.Assets.Font("body"),
                Loc.T("hub.opponentLine", opponent.Record.DisplayWithKo, opponent.Overall),
                new Vector2(rivalPanel.Center.X, rivalPanel.Top + 130), Theme.Muted, 1f, TextAlign.Centre);
            batch.Text(Context.Assets.Font("body"),
                $"{Context.FormatWeight(opponent.WeightPounds)}",
                new Vector2(rivalPanel.Center.X, rivalPanel.Top + 176), Theme.White, 1f, TextAlign.Centre);
        }

        foreach (var button in Buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }
}
