namespace SuperSlapper.Core.Rules;

/// <summary>
/// The rulebook of a sanctioning body: round structure, foul policy, weigh-in
/// tolerance and the count. Every league points at one of these, and every
/// disciplinary decision taken during a fight is judged against it.
/// </summary>
public sealed class Ruleset
{
    public string Id { get; set; } = "standard";

    public string Name { get; set; } = "Standard Slap Rules";

    /// <summary>Scored rounds, 1-5.</summary>
    public int Rounds { get; set; } = 3;

    /// <summary>Slaps each fighter lands per round, 1-3.</summary>
    public int SlapsPerRound { get; set; } = 1;

    /// <summary>Seconds the referee gives a knocked-down fighter to recover.</summary>
    public int KnockdownRecoverySeconds { get; set; } = 30;

    /// <summary>Knockdowns in a single round before the fight is stopped, 2-4.</summary>
    public int MaxKnockdownsPerRound { get; set; } = 3;

    /// <summary>Seconds to beat the count.</summary>
    public int CountOutSeconds { get; set; } = 10;

    /// <summary>Weigh-in allowance for a championship fight, in pounds.</summary>
    public decimal TitleWeighInAllowancePounds { get; set; }

    /// <summary>Weigh-in allowance for a normal bout, in pounds.</summary>
    public decimal NonTitleWeighInAllowancePounds { get; set; } = 1m;

    /// <summary>Open hand only: a closed fist is a foul.</summary>
    public bool OpenHandRequired { get; set; } = true;

    /// <summary>
    /// Fouls a fighter may commit before disqualification. Two in a standard
    /// three-round bout, three in a five-round championship bout.
    /// </summary>
    public int FoulsBeforeDisqualification { get; set; } = 2;

    /// <summary>Moving the head away from the slap is illegal - fighters must stand still.</summary>
    public bool FlinchingIsFoul { get; set; } = true;

    /// <summary>Stepping off the mark while slapping or being slapped is illegal.</summary>
    public bool SteppingIsFoul { get; set; } = true;

    /// <summary>Both fighters weighed on the morning of the fight.</summary>
    public bool SameDayWeighIn { get; set; }

    /// <summary>Maximum percentage of body weight a fighter may cut in camp.</summary>
    public decimal MaxWeightCutPercent { get; set; } = 8m;

    /// <summary>Number of judges scoring the bout.</summary>
    public int JudgesCount { get; set; } = 3;

    /// <summary>10-point-must scoring (the only system in sanctioning bodies today).</summary>
    public bool TenPointMust { get; set; } = true;

    /// <summary>Seconds a fighter has to deliver a slap once the referee starts them.</summary>
    public int SlapTimeLimitSeconds { get; set; } = 30;

    /// <summary>Damage a slap must beat to count as a knockdown (before modifiers).</summary>
    public decimal KnockdownDamageThreshold { get; set; } = 34m;

    /// <summary>Zones not sanctioned by this body.</summary>
    public List<SlapZone> IllegalZones { get; set; } = SlapZones.Illegal.ToList();

    public bool IsIllegalZone(SlapZone zone) => IllegalZones.Contains(zone);

    public decimal AllowancePounds(bool titleFight) =>
        titleFight ? TitleWeighInAllowancePounds : NonTitleWeighInAllowancePounds;

    /// <summary>Total slaps available to a fighter over the whole bout.</summary>
    public int SlapsPerFighter => Rounds * SlapsPerRound;

    /// <summary>Returns every rule that is out of bounds. Empty means a legal rulebook.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("Ruleset id is required.");
        }

        if (Rounds is < 1 or > 5)
        {
            errors.Add("Rounds must be between 1 and 5.");
        }

        if (SlapsPerRound is < 1 or > 3)
        {
            errors.Add("Slaps per round must be between 1 and 3.");
        }

        if (KnockdownRecoverySeconds is < 5 or > 300)
        {
            errors.Add("Knockdown recovery must be between 5 and 300 seconds.");
        }

        if (MaxKnockdownsPerRound is < 1 or > 4)
        {
            errors.Add("Max knockdowns per round must be between 1 and 4.");
        }

        if (CountOutSeconds is < 5 or > 10)
        {
            errors.Add("The count must be between 5 and 10 seconds.");
        }

        if (TitleWeighInAllowancePounds < 0m || NonTitleWeighInAllowancePounds < 0m)
        {
            errors.Add("Weigh-in allowances cannot be negative.");
        }

        if (TitleWeighInAllowancePounds > NonTitleWeighInAllowancePounds)
        {
            errors.Add("A title fight cannot allow more weight than a normal bout.");
        }

        if (JudgesCount is < 1 or > 5)
        {
            errors.Add("Judge count must be between 1 and 5.");
        }

        if (MaxWeightCutPercent is < 0m or > 15m)
        {
            errors.Add("Weight cut limit must be between 0 and 15 percent.");
        }

        if (SlapTimeLimitSeconds is < 5 or > 120)
        {
            errors.Add("Slap time limit must be between 5 and 120 seconds.");
        }

        if (FoulsBeforeDisqualification is < 1 or > 5)
        {
            errors.Add("Fouls before disqualification must be between 1 and 5.");
        }

        // The rulebook pairs the foul limit to the length of the bout:
        // championships go five rounds and get one extra foul.
        var expectedFouls = Rounds >= 4 ? 3 : 2;
        if (FoulsBeforeDisqualification != expectedFouls)
        {
            errors.Add($"A {Rounds}-round bout must allow {expectedFouls} fouls before disqualification.");
        }

        if (KnockdownDamageThreshold <= 0m)
        {
            errors.Add("Knockdown damage threshold must be positive.");
        }

        if (IllegalZones.Count == 0)
        {
            errors.Add("At least one illegal zone must be listed.");
        }

        return errors;
    }

    public Ruleset Clone() => new()
    {
        Id = Id,
        Name = Name,
        Rounds = Rounds,
        SlapsPerRound = SlapsPerRound,
        KnockdownRecoverySeconds = KnockdownRecoverySeconds,
        MaxKnockdownsPerRound = MaxKnockdownsPerRound,
        CountOutSeconds = CountOutSeconds,
        TitleWeighInAllowancePounds = TitleWeighInAllowancePounds,
        NonTitleWeighInAllowancePounds = NonTitleWeighInAllowancePounds,
        OpenHandRequired = OpenHandRequired,
        FoulsBeforeDisqualification = FoulsBeforeDisqualification,
        FlinchingIsFoul = FlinchingIsFoul,
        SteppingIsFoul = SteppingIsFoul,
        SameDayWeighIn = SameDayWeighIn,
        MaxWeightCutPercent = MaxWeightCutPercent,
        JudgesCount = JudgesCount,
        TenPointMust = TenPointMust,
        SlapTimeLimitSeconds = SlapTimeLimitSeconds,
        KnockdownDamageThreshold = KnockdownDamageThreshold,
        IllegalZones = IllegalZones.ToList()
    };
}

/// <summary>The rulebooks shipped with the game.</summary>
public static class Rulesets
{
    /// <summary>WSL championship rules: five rounds, three fouls and out.</summary>
    public static Ruleset WorldSlapLeague { get; } = new()
    {
        Id = "wsl-world",
        Name = "WSL World Rules",
        Rounds = 5,
        SlapsPerRound = 1,
        KnockdownRecoverySeconds = 30,
        MaxKnockdownsPerRound = 3,
        CountOutSeconds = 10,
        TitleWeighInAllowancePounds = 0m,
        NonTitleWeighInAllowancePounds = 1m,
        FoulsBeforeDisqualification = 3,
        SameDayWeighIn = true,
        MaxWeightCutPercent = 8m,
        JudgesCount = 3,
        SlapTimeLimitSeconds = 30,
        KnockdownDamageThreshold = 34m
    };

    /// <summary>European circuit: longer bouts, more forgiving weigh-ins.</summary>
    public static Ruleset EuropeanCircuit { get; } = new()
    {
        Id = "esc-continental",
        Name = "ESC Continental Rules",
        Rounds = 3,
        SlapsPerRound = 2,
        KnockdownRecoverySeconds = 45,
        MaxKnockdownsPerRound = 3,
        CountOutSeconds = 10,
        TitleWeighInAllowancePounds = 0.5m,
        NonTitleWeighInAllowancePounds = 2m,
        FoulsBeforeDisqualification = 2,
        SameDayWeighIn = false,
        MaxWeightCutPercent = 10m,
        JudgesCount = 3,
        SlapTimeLimitSeconds = 20,
        KnockdownDamageThreshold = 36m
    };

    /// <summary>Atlantic federation: three rounds, strict officials, five judges.</summary>
    public static Ruleset AtlanticFederation { get; } = new()
    {
        Id = "asf-open",
        Name = "ASF Open Rules",
        Rounds = 3,
        SlapsPerRound = 1,
        KnockdownRecoverySeconds = 60,
        MaxKnockdownsPerRound = 4,
        CountOutSeconds = 10,
        TitleWeighInAllowancePounds = 0m,
        NonTitleWeighInAllowancePounds = 1m,
        FoulsBeforeDisqualification = 2,
        SameDayWeighIn = false,
        MaxWeightCutPercent = 6m,
        JudgesCount = 5,
        SlapTimeLimitSeconds = 45,
        KnockdownDamageThreshold = 32m
    };

    /// <summary>Amateur development circuit used for the first career bouts.</summary>
    public static Ruleset Amateur { get; } = new()
    {
        Id = "amateur",
        Name = "Amateur Rules",
        Rounds = 2,
        SlapsPerRound = 1,
        KnockdownRecoverySeconds = 60,
        MaxKnockdownsPerRound = 2,
        CountOutSeconds = 8,
        TitleWeighInAllowancePounds = 0m,
        NonTitleWeighInAllowancePounds = 3m,
        FoulsBeforeDisqualification = 2,
        SameDayWeighIn = false,
        MaxWeightCutPercent = 4m,
        JudgesCount = 3,
        SlapTimeLimitSeconds = 20,
        KnockdownDamageThreshold = 40m
    };

    public static IReadOnlyList<Ruleset> All { get; } = new[]
    {
        WorldSlapLeague, EuropeanCircuit, AtlanticFederation, Amateur
    };

    public static Ruleset Get(string id) =>
        All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Unknown ruleset '{id}'.");
}
