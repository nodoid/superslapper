using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Rankings;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;
using SuperSlapper.Core.Training;

namespace SuperSlapper.Core.Career;

/// <summary>A booked bout. Everything the fight night screen needs is here.</summary>
public sealed class FightOffer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string LeagueId { get; set; } = "amateur";
    public WeightClassId Division { get; set; } = WeightClassId.Welterweight;
    public string OpponentId { get; set; } = "";
    public string OpponentName { get; set; } = "";
    public int FightDay { get; set; }
    public bool TitleFight { get; set; }
    public BeltKind BeltKind { get; set; } = BeltKind.World;

    /// <summary>Rounds on the poster - always taken from the league's ruleset.</summary>
    public int Rounds { get; set; } = 3;

    public int Purse { get; set; }
    public string Venue { get; set; } = "The Slaplantic";
    public bool IsMainEvent { get; set; }

    public int DaysUntilFight(int currentDay) => FightDay - currentDay;

    public string Billing => TitleFight
        ? $"CHAMPIONSHIP · {Rounds} rounds"
        : $"{Rounds} rounds · {(IsMainEvent ? "Main event" : "Undercard")}";
}

public sealed class TrainingLogEntry
{
    public int Day { get; set; }
    public string ActivityId { get; set; } = "";
    public string ActivityName { get; set; } = "";
    public string GainSummary { get; set; } = "";
    public string? Injury { get; set; }
    public bool Overtrained { get; set; }
}

public sealed class FightHistoryEntry
{
    public int Day { get; set; }
    public string LeagueId { get; set; } = "";
    public WeightClassId Division { get; set; }
    public string WinnerId { get; set; } = "";
    public string LoserId { get; set; } = "";
    public string FighterAId { get; set; } = "";
    public string FighterBId { get; set; } = "";
    public FightOutcomeKind Kind { get; set; }
    public int Round { get; set; }
    public bool TitleFight { get; set; }
    public string Headline { get; set; } = "";
    public string Scorecards { get; set; } = "";
    public bool InvolvesPlayer { get; set; }
    public int KnockdownsA { get; set; }
    public int KnockdownsB { get; set; }
}

public sealed class WeighInRecord
{
    public int Day { get; set; }
    public WeightClassId Division { get; set; }
    public decimal ActualPounds { get; set; }
    public decimal LimitPounds { get; set; }
    public decimal AllowancePounds { get; set; }
    public bool MadeWeight { get; set; }
    public bool TitleFight { get; set; }
    public decimal CutPercent { get; set; }
}

/// <summary>
/// The whole career in one serialisable object. Rank, belts, records, purses,
/// injuries and camp history all live here, which is what makes them carry into
/// the next bout and the next session.
/// </summary>
public sealed class CareerState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string CareerName { get; set; } = "Career";
    public long Seed { get; set; }
    public ulong RngState0 { get; set; }
    public ulong RngState1 { get; set; }

    /// <summary>Days since the career started. Bouts and injuries are dated from here.</summary>
    public int Day { get; set; }

    public int Season { get; set; } = 1;

    public string PlayerId { get; set; } = "";
    public string PlayerLeagueId { get; set; } = "amateur";
    public WeightClassId PlayerDivision { get; set; } = WeightClassId.Welterweight;

    public int Money { get; set; } = 500;
    public int CareerFights => History.Count(h => h.InvolvesPlayer && h.WinnerId.Length > 0);

    public List<Fighter> Fighters { get; set; } = new();
    public BeltBook Belts { get; set; } = new();
    public RankingBook Rankings { get; set; } = new();
    public List<FightHistoryEntry> History { get; set; } = new();
    public List<TrainingLogEntry> TrainingLog { get; set; } = new();
    public List<WeighInRecord> WeighIns { get; set; } = new();

    /// <summary>Professional tournament cards, oldest first. Amateur bouts do not use these.</summary>
    public List<TournamentCard> Cards { get; set; } = new();

    public FightOffer? Offer { get; set; }

    public string LastResultHeadline { get; set; } = "";
    public int BoutsWonAtCurrentLeague { get; set; }

    public Fighter? FindFighter(string? id) =>
        id is null ? null : Fighters.FirstOrDefault(f => f.Id == id);

    public Fighter Player => FindFighter(PlayerId) ?? throw new InvalidOperationException("Player fighter missing.");

    public Fighter? OpponentOf(FightOffer? offer) => offer is null ? null : FindFighter(offer.OpponentId);

    public IReadOnlyList<FightHistoryEntry> PlayerBouts =>
        History.Where(h => h.InvolvesPlayer).OrderByDescending(h => h.Day).ToList();
}
