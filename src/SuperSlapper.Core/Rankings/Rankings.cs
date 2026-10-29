using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Rankings;

public enum RankMovement
{
    Same = 0,
    Up = 1,
    Down = 2,
    New = 3
}

public sealed class RankingEntry
{
    public string FighterId { get; set; } = "";
    public string FighterName { get; set; } = "";
    public int Points { get; set; }
    public int Position { get; set; }

    /// <summary>Position in the previous published ranking (0 when unranked).</summary>
    public int PreviousPosition { get; set; }

    public string Record { get; set; } = "0-0-0";
    public int Streak { get; set; }

    public RankMovement Movement => PreviousPosition switch
    {
        0 when Position > 0 => RankMovement.New,
        0 => RankMovement.Same,
        _ when Position < PreviousPosition => RankMovement.Up,
        _ when Position > PreviousPosition => RankMovement.Down,
        _ => RankMovement.Same
    };

    /// <summary>"#3", "C" for champion, "NR" for unranked.</summary>
    public string PositionText => Position <= 0
        ? Loc.T("ranking.unranked")
        : Loc.T("ranking.position", Position);

    public string MovementText => Movement switch
    {
        RankMovement.Up => Loc.T("ranking.up", PreviousPosition - Position),
        RankMovement.Down => Loc.T("ranking.down", Position - PreviousPosition),
        RankMovement.New => Loc.T("ranking.new"),
        _ => Loc.T("ranking.same")
    };
}

/// <summary>Rankings for one league's division, including the champion.</summary>
public sealed class DivisionRanking
{
    public string LeagueId { get; set; } = "wsl";
    public WeightClassId Division { get; set; } = WeightClassId.Welterweight;

    /// <summary>Champion is ranked above the list and holds the belt.</summary>
    public string? ChampionId { get; set; }

    public List<RankingEntry> Entries { get; set; } = new();

    public void SnapshotForNextPeriod()
    {
        foreach (var entry in Entries)
        {
            entry.PreviousPosition = entry.Position;
        }
    }

    public RankingEntry? Find(string fighterId) => Entries.FirstOrDefault(e => e.FighterId == fighterId);

    public int PositionOf(string fighterId)
    {
        if (ChampionId == fighterId)
        {
            return 0;
        }

        return Find(fighterId)?.Position ?? int.MaxValue;
    }
}

/// <summary>
/// Owning object for every league/division ranking. Points are awarded from
/// fight results by <see cref="RankingCalculator"/> and the book re-sorts and
/// re-numbers on demand, keeping the previous positions so movement arrows work.
/// </summary>
public sealed class RankingBook
{
    public List<DivisionRanking> Divisions { get; set; } = new();

    public DivisionRanking Get(string leagueId, WeightClassId division)
    {
        var existing = Divisions.FirstOrDefault(
            d => string.Equals(d.LeagueId, leagueId, StringComparison.OrdinalIgnoreCase) && d.Division == division);
        if (existing is not null)
        {
            return existing;
        }

        existing = new DivisionRanking { LeagueId = leagueId, Division = division };
        Divisions.Add(existing);
        return existing;
    }

    /// <summary>Re-sorts entries by points and re-numbers positions 1..N.</summary>
    public void Republish(string leagueId, WeightClassId division, int rankedContenders)
    {
        var table = Get(leagueId, division);
        var ordered = table.Entries
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.Streak)
            .ThenBy(e => e.FighterName, StringComparer.OrdinalIgnoreCase)
            .Take(rankedContenders)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Position = i + 1;
        }

        table.Entries = ordered;
    }

    public IEnumerable<DivisionRanking> ForLeague(string leagueId) =>
        Divisions.Where(d => string.Equals(d.LeagueId, leagueId, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Point values. Kept in one place so the rulebook can be tuned and tested.</summary>
public sealed class RankingPoints
{
    public int KnockoutWin { get; set; } = 100;
    public int DecisionWin { get; set; } = 75;
    public int DisqualificationWin { get; set; } = 60;
    public int Draw { get; set; } = 25;
    public int Loss { get; set; } = 5;
    public int TitleWinBonus { get; set; } = 50;
    public int TitleDefenceBonus { get; set; } = 40;
    public int StreakBonusPerWin { get; set; } = 5;
    public int MaxStreakBonus { get; set; } = 30;
}

public static class RankingCalculator
{
    public static int Award(League league, FightOutcomeKind kind, bool won, bool draw, bool titleFight,
        bool defendingChampion, int streakAfter, RankingPoints? points = null)
    {
        points ??= new RankingPoints();

        var basePoints = draw
            ? points.Draw
            : won
                ? kind switch
                {
                    FightOutcomeKind.Knockout or FightOutcomeKind.TechnicalKnockout => points.KnockoutWin,
                    FightOutcomeKind.Disqualification => points.DisqualificationWin,
                    _ => points.DecisionWin
                }
                : points.Loss;

        if (titleFight && won)
        {
            basePoints += defendingChampion ? points.TitleDefenceBonus : points.TitleWinBonus;
        }

        var streakBonus = won
            ? Math.Min(points.MaxStreakBonus, Math.Max(0, streakAfter - 1) * points.StreakBonusPerWin)
            : 0;

        var total = (basePoints + streakBonus) * league.PointsMultiplier;
        return (int)Math.Round(total, MidpointRounding.AwayFromZero);
    }
}
