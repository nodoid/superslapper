using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Leagues;

/// <summary>
/// A sanctioning body. Each league runs its own rulebook, its own purse
/// structure, its own title for every division and its own rankings.
/// </summary>
public sealed class League
{
    public string Id { get; set; } = "wsl";
    public string Name { get; set; } = "World Slap League";
    public string ShortName { get; set; } = "WSL";
    public string Region { get; set; } = "Worldwide";
    public string RulesetId { get; set; } = "wsl-world";

    /// <summary>Prestige 1 (regional) to 5 (world). Gates the purse and the ranking points.</summary>
    public int Prestige { get; set; } = 3;

    /// <summary>Purse for a win on the bottom of the card.</summary>
    public int BasePurse { get; set; } = 2000;

    /// <summary>Purse for a championship bout.</summary>
    public int TitlePurse { get; set; } = 25000;

    /// <summary>Ranking points multiplier applied to every result in the league.</summary>
    public decimal PointsMultiplier { get; set; } = 1.0m;

    /// <summary>How many contenders are ranked beneath the champion.</summary>
    public int RankedContenders { get; set; } = 10;

    /// <summary>Days a champion may be inactive before an interim title is created.</summary>
    public int InterimTitleAfterDays { get; set; } = 270;

    /// <summary>Prestige 3 and up runs a tournament card every month.</summary>
    public bool RunsMonthlyCards => Prestige >= 3;

    /// <summary>Weekday the monthly card lands on.</summary>
    public DayOfWeek EventWeekday { get; set; } = DayOfWeek.Saturday;

    /// <summary>Which of that weekday's occurrences in the month (1-5).</summary>
    public int EventWeekOfMonth { get; set; } = 3;

    /// <summary>Divisions this league sanctions.</summary>
    public List<WeightClassId> Divisions { get; set; } = new();

    public Ruleset Ruleset => Rulesets.Get(RulesetId);

    public bool Sanctions(WeightClassId division) => Divisions.Contains(division);

    /// <summary>Purse for a bout, before the win bonus that career mode pays.</summary>
    public int PurseFor(bool titleFight, bool won) =>
        (titleFight ? TitlePurse : BasePurse) * (won ? 1 : 0) +
        (titleFight ? TitlePurse : BasePurse) / 4;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("League id is required.");
        }

        if (Prestige is < 1 or > 5)
        {
            errors.Add("Prestige must be between 1 and 5.");
        }

        if (BasePurse <= 0 || TitlePurse <= 0)
        {
            errors.Add("Purses must be positive.");
        }

        if (TitlePurse < BasePurse)
        {
            errors.Add("A title purse cannot be smaller than a base purse.");
        }

        if (Divisions.Count == 0)
        {
            errors.Add("A league must sanction at least one division.");
        }

        if (Divisions.Distinct().Count() != Divisions.Count)
        {
            errors.Add("A division cannot be sanctioned twice.");
        }

        try
        {
            _ = Ruleset.Validate();
        }
        catch (KeyNotFoundException)
        {
            errors.Add($"Unknown ruleset '{RulesetId}'.");
        }

        return errors;
    }
}

/// <summary>The leagues that exist in the game world.</summary>
public static class Leagues
{
    public static League WorldSlapLeague { get; } = new()
    {
        Id = "wsl",
        Name = "World Slap League",
        ShortName = "WSL",
        Region = "Worldwide",
        RulesetId = "wsl-world",
        Prestige = 5,
        BasePurse = 12000,
        TitlePurse = 90000,
        PointsMultiplier = 1.25m,
        RankedContenders = 10,
        InterimTitleAfterDays = 240,
        EventWeekOfMonth = 4,
        Divisions = WeightClasses.All.Select(c => c.Id).ToList()
    };

    public static League EuropeanCircuit { get; } = new()
    {
        Id = "esc",
        Name = "European Slap Circuit",
        ShortName = "ESC",
        Region = "Europe",
        RulesetId = "esc-continental",
        Prestige = 3,
        BasePurse = 4000,
        TitlePurse = 24000,
        PointsMultiplier = 1.0m,
        RankedContenders = 10,
        InterimTitleAfterDays = 300,
        EventWeekOfMonth = 2,
        Divisions = WeightClasses.All.Select(c => c.Id).ToList()
    };

    public static League AtlanticFederation { get; } = new()
    {
        Id = "asf",
        Name = "Atlantic Slap Federation",
        ShortName = "ASF",
        Region = "North America",
        RulesetId = "asf-open",
        Prestige = 4,
        BasePurse = 8000,
        TitlePurse = 55000,
        PointsMultiplier = 1.15m,
        RankedContenders = 12,
        InterimTitleAfterDays = 210,
        EventWeekOfMonth = 3,
        Divisions = new List<WeightClassId>
        {
            WeightClassId.Lightweight,
            WeightClassId.Welterweight,
            WeightClassId.Middleweight,
            WeightClassId.LightHeavyweight,
            WeightClassId.Heavyweight,
            WeightClassId.SuperHeavyweight
        }
    };

    /// <summary>The amateur circuit the player starts on.</summary>
    public static League AmateurCircuit { get; } = new()
    {
        Id = "amateur",
        Name = "Regional Amateur Circuit",
        ShortName = "RAC",
        Region = "Local",
        RulesetId = "amateur",
        Prestige = 1,
        BasePurse = 400,
        TitlePurse = 1500,
        PointsMultiplier = 0.5m,
        RankedContenders = 8,
        InterimTitleAfterDays = 365,
        Divisions = WeightClasses.All.Select(c => c.Id).ToList()
    };

    public static IReadOnlyList<League> All { get; } = new[]
    {
        AmateurCircuit, EuropeanCircuit, AtlanticFederation, WorldSlapLeague
    };

    public static League Get(string id) =>
        All.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Unknown league '{id}'.");

    /// <summary>Hierarchy used by career mode to decide when a fighter is promoted.</summary>
    public static IReadOnlyList<League> ByPrestige => All.OrderBy(l => l.Prestige).ToList();
}
