using SuperSlapper.Core.Leagues;

namespace SuperSlapper.Core.Career;

/// <summary>
/// Maps the career's day counter onto real calendar dates.
///
/// Careers are dated in whole days from 1 January 2026 so the calendar screen
/// can show proper months, weekdays and month lengths instead of a fake
/// 30-day block. Professional leagues stage one card a month, on their own
/// Saturday, which is what gives the calendar its rhythm.
/// </summary>
public static class CareerCalendar
{
    public static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public static DateTime DateOf(int day) => Epoch.AddDays(day);

    public static int DayOf(DateTime date) => (date.Date - Epoch).Days;

    /// <summary>The nth (1-based) weekday of a month, e.g. the third Saturday of March.</summary>
    public static DateTime NthWeekday(int year, int month, DayOfWeek weekday, int n)
    {
        n = Math.Clamp(n, 1, 5);
        var first = new DateTime(year, month, 1);
        var offset = ((int)weekday - (int)first.DayOfWeek + 7) % 7;
        var day = 1 + offset + (n - 1) * 7;

        // A fifth Saturday does not exist in every month; fall back a week.
        while (day > DateTime.DaysInMonth(year, month))
        {
            day -= 7;
        }

        return new DateTime(year, month, day);
    }

    /// <summary>The date a league stages its card in a given month.</summary>
    public static DateTime EventDate(League league, int year, int month) =>
        NthWeekday(year, month, league.EventWeekday, league.EventWeekOfMonth);

    /// <summary>The first card day strictly after <paramref name="fromDay"/>.</summary>
    public static int NextEventDay(League league, int fromDay)
    {
        var date = DateOf(fromDay).Date;
        var candidate = EventDate(league, date.Year, date.Month);
        if (DayOf(candidate) <= fromDay)
        {
            var next = date.AddMonths(1);
            candidate = EventDate(league, next.Year, next.Month);
        }

        return DayOf(candidate);
    }

    /// <summary>Days from <paramref name="fromDay"/> to the next card, never negative.</summary>
    public static int DaysToNextEvent(League league, int fromDay) =>
        Math.Max(0, NextEventDay(league, fromDay) - fromDay);
}
