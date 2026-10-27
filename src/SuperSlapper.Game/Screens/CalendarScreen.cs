using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// The career calendar. Real months, the leagues' monthly card nights and the
/// player's own booked bout, which is one tap from the card itself.
/// </summary>
public sealed class CalendarScreen : Screen
{
    private static readonly string[] Weekdays = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };

    private const int GridLeft = 80;
    private const int GridTop = 190;
    private const int CellWidth = 157;
    private const int CellHeight = 112;

    private readonly List<Button> _buttons = new();
    private readonly List<(Rectangle Rect, int Day)> _cells = new();
    private DateTime _month;
    private string _note = string.Empty;

    public CalendarScreen(GameContext context) : base(context)
    {
    }

    private CareerState State => Context.State!;

    public override void OnEnter()
    {
        base.OnEnter();

        var today = CareerCalendar.DateOf(State.Day);
        _month = new DateTime(today.Year, today.Month, 1);
        Build();
    }

    /// <summary>Moves the displayed month by <paramref name="delta"/> and rebuilds the taps.</summary>
    private void Shift(int delta)
    {
        _month = _month.AddMonths(delta);
        _note = string.Empty;
        Build();
    }

    private void GoToToday()
    {
        var today = CareerCalendar.DateOf(State.Day);
        _month = new DateTime(today.Year, today.Month, 1);
        _note = string.Empty;
        Build();
    }

    private void Build()
    {
        _buttons.Clear();

        _buttons.Add(new Button(new Rectangle(1000, 42, 84, 72), "<", () => Shift(-1), ButtonStyle.Ghost));
        _buttons.Add(new Button(new Rectangle(1100, 42, 84, 72), ">", () => Shift(1), ButtonStyle.Ghost));
        _buttons.Add(new Button(new Rectangle(80, 890, 260, 120), "TODAY", GoToToday, ButtonStyle.Ghost));
        _buttons.Add(new Button(new Rectangle(1620, 890, 240, 120), "BACK",
            () => Context.Screens.Pop(), ButtonStyle.Ghost));

        var card = PlayerCardInMonth();
        if (card is not null)
        {
            _buttons.Add(new Button(new Rectangle(1180, 890, 400, 120), "OPEN THE CARD",
                () => Context.Screens.Push(new TournamentScreen(Context, card)), ButtonStyle.Normal));
        }

        BuildCells();
    }

    private void BuildCells()
    {
        _cells.Clear();

        var days = DateTime.DaysInMonth(_month.Year, _month.Month);
        var offset = ((int)_month.DayOfWeek + 6) % 7;

        for (var day = 1; day <= days; day++)
        {
            var cell = offset + day - 1;
            var rect = new Rectangle(GridLeft + (cell % 7) * CellWidth,
                GridTop + (cell / 7) * CellHeight, CellWidth - 6, CellHeight - 6);
            _cells.Add((rect, day));
        }
    }

    /// <summary>The player's card, if one lands in the month on screen.</summary>
    private TournamentCard? PlayerCardInMonth()
    {
        foreach (var card in State.Cards)
        {
            if (card.Division != State.PlayerDivision)
            {
                continue;
            }

            var date = CareerCalendar.DateOf(card.Day);
            if (date.Year == _month.Year && date.Month == _month.Month)
            {
                return card;
            }
        }

        return null;
    }

    /// <summary>Every league staging a card in the month on screen, and the day it lands.</summary>
    private List<(League League, int Day)> CardNights()
    {
        var nights = new List<(League, int)>();
        foreach (var league in Leagues.All)
        {
            if (!league.RunsMonthlyCards || !league.Sanctions(State.PlayerDivision))
            {
                continue;
            }

            var date = CareerCalendar.EventDate(league, _month.Year, _month.Month);
            nights.Add((league, date.Day));
        }

        return nights;
    }

    /// <summary>The player's booked bout, which exists whether or not a card does.</summary>
    private (int Day, string Label)? PlayerNight()
    {
        var card = PlayerCardInMonth();
        if (card is not null)
        {
            return (CareerCalendar.DateOf(card.Day).Day, "YOUR CARD");
        }

        var offer = State.Offer;
        if (offer is null)
        {
            return null;
        }

        var date = CareerCalendar.DateOf(offer.FightDay);
        return date.Year == _month.Year && date.Month == _month.Month
            ? (date.Day, "YOUR BOUT")
            : null;
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        foreach (var button in _buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }

        if (input.BackPressed)
        {
            Context.Screens.Pop();
            return;
        }

        if (!input.LeftPressed)
        {
            return;
        }

        var point = input.Pointer.ToPoint();
        foreach (var (rect, day) in _cells)
        {
            if (!rect.Contains(point))
            {
                continue;
            }

            var card = PlayerCardInMonth();
            if (card is not null && CareerCalendar.DateOf(card.Day).Day == day)
            {
                Context.Assets.Play("ui_click", 0.5f);
                Context.Screens.Push(new TournamentScreen(Context, card));
                return;
            }

            var nights = CardNights();
            var night = nights.FirstOrDefault(n => n.Day == day);
            _note = night.League is null
                ? string.Empty
                : $"{night.League.ShortName} stages a card on this day. Open it from the hub once you are booked on it.";
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
        var today = CareerCalendar.DateOf(state.Day);

        batch.Draw(Context.Assets.Tex("menu_bg"), new Rectangle(0, 0, GameSettings.VirtualWidth,
            GameSettings.VirtualHeight), Color.White);

        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, 130), new Color(8, 12, 22) * 0.9f);
        batch.Text(Context.Assets.Font("large"), "CALENDAR", new Vector2(80, 26), Theme.Gold);
        batch.Text(Context.Assets.Font("body"),
            $"{today:dddd d MMMM yyyy} · day {state.Day} of the career",
            new Vector2(82, 96), Theme.Muted);

        batch.Text(Context.Assets.Font("large"), _month.ToString("MMMM yyyy").ToUpperInvariant(),
            new Vector2(1400, 40), Theme.White, 0.9f, TextAlign.Centre);

        for (var i = 0; i < Weekdays.Length; i++)
        {
            batch.Text(Context.Assets.Font("small"), Weekdays[i],
                new Vector2(GridLeft + i * CellWidth + (CellWidth - 6) / 2f, 156), Theme.Muted, 1f,
                TextAlign.Centre);
        }

        DrawGrid(batch, today);
        DrawMonthPanel(batch);
        DrawNote(batch);

        foreach (var button in _buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }

    private void DrawGrid(SpriteBatch batch, DateTime today)
    {
        var nights = CardNights();
        var playerNight = PlayerNight();

        foreach (var (rect, day) in _cells)
        {
            var isToday = today.Year == _month.Year && today.Month == _month.Month && today.Day == day;
            var isPlayer = playerNight?.Day == day;

            batch.Fill(rect, isToday ? new Color(36, 52, 86) : new Color(18, 25, 42) * 0.92f);
            batch.Outline(rect, isPlayer ? Theme.Gold : new Color(38, 50, 78), isPlayer ? 3 : 1);

            batch.Text(Context.Assets.Font("small"), day.ToString(),
                new Vector2(rect.Left + 14, rect.Top + 10), isToday ? Theme.Gold : Theme.White);

            if (isPlayer)
            {
                batch.Text(Context.Assets.Font("tiny"), playerNight!.Value.Label,
                    new Vector2(rect.Left + 14, rect.Top + 50), Theme.Gold);
            }

            var chips = 0;
            foreach (var (league, nightDay) in nights)
            {
                if (nightDay != day)
                {
                    continue;
                }

                var chip = new Rectangle(rect.Left + 12, rect.Bottom - 34 - chips * 30, rect.Width - 24, 26);
                batch.Fill(chip, LeagueColour(league.Id) * 0.85f);
                batch.Text(Context.Assets.Font("tiny"), league.ShortName,
                    new Vector2(chip.Left + 8, chip.Top + 6), Theme.Ink, 1f, TextAlign.Left, shadow: false);
                chips++;
            }
        }
    }

    private void DrawMonthPanel(SpriteBatch batch)
    {
        var panel = new Rectangle(1210, 150, 630, 700);
        batch.NineSlice(Context.Assets.Tex("panel"), panel, 40, Color.White);
        batch.Text(Context.Assets.Font("small"), "CARD NIGHTS", new Vector2(panel.Left + 40, panel.Top + 26),
            Theme.Gold);

        var y = panel.Top + 80;
        var nights = CardNights();
        foreach (var (league, nightDay) in nights)
        {
            var date = new DateTime(_month.Year, _month.Month, nightDay);
            batch.Fill(new Rectangle(panel.Left + 36, y + 6, 12, 46), LeagueColour(league.Id));
            batch.Text(Context.Assets.Font("medium"), league.ShortName,
                new Vector2(panel.Left + 62, y), Theme.White);
            batch.Text(Context.Assets.Font("small"), $"{date:ddd d MMM} · {league.Region}",
                new Vector2(panel.Left + 62, y + 44), Theme.Muted);
            y += 96;
        }

        var card = PlayerCardInMonth();
        var offer = State.Offer;
        var box = new Rectangle(panel.Left + 30, panel.Bottom - 260, panel.Width - 60, 220);
        batch.Fill(box, new Color(24, 34, 56));
        batch.Outline(box, Theme.Gold, 2);

        if (card is not null)
        {
            var date = CareerCalendar.DateOf(card.Day);
            batch.Text(Context.Assets.Font("small"), "YOUR CARD", new Vector2(box.Left + 24, box.Top + 20),
                Theme.Gold);
            batch.Text(Context.Assets.Font("medium"), date.ToString("dddd d MMMM"),
                new Vector2(box.Left + 24, box.Top + 54), Theme.White);
            batch.Text(Context.Assets.Font("body"), card.Venue,
                new Vector2(box.Left + 24, box.Top + 104), Theme.Muted);
            batch.Text(Context.Assets.Font("small"),
                $"{Leagues.Get(card.LeagueId).ShortName} · {card.Bouts.Count} bouts · " +
                $"{card.Bouts.Count(b => b.IsDecided)} decided",
                new Vector2(box.Left + 24, box.Top + 148), Theme.Muted);
        }
        else if (offer is not null)
        {
            batch.Text(Context.Assets.Font("small"), "YOUR NEXT BOUT",
                new Vector2(box.Left + 24, box.Top + 20), Theme.Gold);
            batch.Text(Context.Assets.Font("medium"),
                CareerCalendar.DateOf(offer.FightDay).ToString("dddd d MMMM"),
                new Vector2(box.Left + 24, box.Top + 54), Theme.White);
            batch.Text(Context.Assets.Font("body"), offer.OpponentName,
                new Vector2(box.Left + 24, box.Top + 104), Theme.White);
            batch.Text(Context.Assets.Font("small"), offer.Venue,
                new Vector2(box.Left + 24, box.Top + 148), Theme.Muted);
        }
        else
        {
            batch.Text(Context.Assets.Font("body"), "No bout booked.",
                new Vector2(box.Left + 24, box.Top + 90), Theme.Muted);
        }
    }

    private void DrawNote(SpriteBatch batch)
    {
        if (_note.Length == 0)
        {
            return;
        }

        batch.WrappedText(Context.Assets.Font("small"), _note, new Vector2(360, 936), 780, Theme.Muted);
    }

    private static Color LeagueColour(string leagueId) => leagueId switch
    {
        "wsl" => Theme.Gold,
        "asf" => Theme.BlueLight,
        "esc" => Theme.Green,
        _ => Theme.Muted
    };
}
