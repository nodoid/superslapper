using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// One tournament card: every bout on the night, undercard first, with the
/// player's own bout marked. The rest of the card can be run out from here.
/// </summary>
public sealed class TournamentScreen : Screen
{
    private readonly TournamentCard _card;

    public TournamentScreen(GameContext context, TournamentCard card) : base(context)
    {
        _card = card;
    }

    private CareerState State => Context.State!;

    private DateTime Date => CareerCalendar.DateOf(_card.Day);

    /// <summary>True once the player's own bout on this card is the one they are due to fight.</summary>
    private bool PlayerBoutIsDue =>
        State.Offer is { } offer && offer.FightDay == _card.Day &&
        _card.Bouts.Any(b => b.IsPlayerBout && !b.IsDecided);

    private bool HasBoutsToRun =>
        _card.Bouts.Any(b => !b.IsPlayerBout && !b.IsDecided);

    public override void OnEnter()
    {
        base.OnEnter();
        Build();
    }

    private void Build()
    {
        ClearControls();

        // With no career the card has no career to sit in; the stand-in panel draws.
        if (Context.State is null)
        {
            return;
        }

        Add(new Button(new Rectangle(80, 890, 320, 120), Loc.T("common.back"),
            () => Context.Screens.Pop(), ButtonStyle.Ghost));

        // Straight from the card to the month it sits in, so a night's results
        // can be read against the rest of the calendar.
        Add(new Button(new Rectangle(460, 890, 320, 120), Loc.T("hub.calendar"),
            () => Context.Screens.Push(new CalendarScreen(Context)), ButtonStyle.Ghost));

        if (HasBoutsToRun)
        {
            Add(new Button(new Rectangle(1180, 890, 400, 120), Loc.T("card.runRest"),
                RunRest, ButtonStyle.Ghost));
        }

        if (PlayerBoutIsDue)
        {
            Add(new Button(new Rectangle(1600, 890, 260, 120), Loc.T("hub.weighIn"),
                () => Context.Screens.Push(new WeighInScreen(Context)), ButtonStyle.Danger)
            {
                IconKey = "icon_hand"
            });
        }
    }

    private void RunRest()
    {
        TournamentService.SimulateRest(State, Context.Career, _card);
        Context.Persist();
        Context.Assets.Play("bell", 0.5f);
        Build();
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        foreach (var button in Buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }

        if (input.BackPressed)
        {
            Context.Screens.Pop();
        }
    }

    public override void Draw(SpriteBatch batch, GameTime time)
    {
        if (!HasCareer)
        {
            DrawNoCareer(batch);
            return;
        }

        var league = Leagues.Get(_card.LeagueId);
        var division = WeightClasses.Get(_card.Division);

        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, 150), new Color(8, 12, 22) * 0.9f);
        batch.Text(Context.Assets.Font("large"),
            Loc.T("card.header", league.ShortName, division.Name.ToUpperInvariant()),
            new Vector2(80, 40), Theme.Gold);
        batch.Text(Context.Assets.Font("body"),
            Loc.T("card.dateLine", Date.ToString("dddd d MMMM yyyy"), _card.Venue),
            new Vector2(82, 100), Theme.Muted);

        var days = _card.Day - State.Day;
        var when = _card.IsComplete ? Loc.T("card.complete")
            : days > 0 ? Loc.T(days == 1 ? "card.nightInOne" : "card.nightInMany", days)
            : Loc.T("card.fightNight");

        batch.Text(Context.Assets.Font("medium"), when, new Vector2(1840, 46),
            _card.IsComplete ? Theme.Green : Theme.Gold, 1f, TextAlign.Right);
        batch.Text(Context.Assets.Font("small"),
            Loc.T("card.progress", _card.Bouts.Count(b => b.IsDecided), _card.Bouts.Count),
            new Vector2(1840, 100), Theme.Muted, 1f, TextAlign.Right);

        for (var i = 0; i < _card.Bouts.Count; i++)
        {
            DrawBout(batch, _card.Bouts[i], new Rectangle(80, 180 + i * 134, 1760, 122));
        }

        foreach (var button in Buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }

    private void DrawBout(SpriteBatch batch, CardBout bout, Rectangle rect)
    {
        var played = bout.IsDecided;
        batch.Fill(rect, bout.IsPlayerBout ? new Color(38, 32, 16) * 0.95f : new Color(18, 25, 42) * 0.92f);
        batch.Outline(rect, bout.IsPlayerBout ? Theme.Gold : new Color(38, 50, 78), bout.IsPlayerBout ? 3 : 1);

        batch.Text(Context.Assets.Font("small"), bout.SlotName,
            new Vector2(rect.Left + 28, rect.Top + 18), bout.Slot == CardSlot.MainEvent ? Theme.Gold : Theme.Muted);

        if (bout.TitleFight)
        {
            var badge = new Rectangle(rect.Left + 200, rect.Top + 16, 190, 30);
            batch.Fill(badge, Theme.Gold);
            batch.Text(Context.Assets.Font("tiny"), Loc.T("hub.championship"),
                new Vector2(badge.Center.X, badge.Top + 7), Theme.Ink, 1f, TextAlign.Centre, shadow: false);
        }

        batch.Text(Context.Assets.Font("medium"),
            Loc.T("card.names", bout.FighterAName, bout.FighterBName),
            new Vector2(rect.Left + 28, rect.Top + 50), played ? Theme.White : Theme.White * 0.9f);

        if (bout.IsPlayerBout)
        {
            batch.Text(Context.Assets.Font("tiny"), Loc.T("card.yourBout"),
                new Vector2(rect.Left + 28, rect.Top + 94), Theme.Gold);
        }

        // Result on the right: the winner and how, or the booking still to come.
        var resultColour = played ? Theme.Green : Theme.Muted;
        batch.Text(Context.Assets.Font("medium"), bout.ResultText,
            new Vector2(rect.Right - 28, rect.Top + 40), resultColour, 0.9f, TextAlign.Right);

        if (played && bout.Result is { } result)
        {
            var line = result.ScorecardText.Length > 0 ? result.ScorecardText : result.Summary;
            batch.Text(Context.Assets.Font("small"), line,
                new Vector2(rect.Right - 28, rect.Top + 84), Theme.Muted, 1f, TextAlign.Right);
        }
        else if (bout.IsPlayerBout && PlayerBoutIsDue)
        {
            var pulse = 0.5f + 0.5f * (float)Math.Sin(Elapsed * 3f);
            batch.Text(Context.Assets.Font("small"), Loc.T("card.ready"),
                new Vector2(rect.Right - 28, rect.Top + 84), Theme.RedLight * (0.6f + 0.4f * pulse), 1f,
                TextAlign.Right);
        }
        else
        {
            batch.Text(Context.Assets.Font("small"), Loc.T("card.toBeContested"),
                new Vector2(rect.Right - 28, rect.Top + 84), Theme.Muted, 1f, TextAlign.Right);
        }
    }
}
