using SuperSlapper.Core.Localisation;

namespace SuperSlapper.Core.Training;

public enum TrainingFocus
{
    Strength = 0,
    Endurance = 1,
    Aim = 2,
    SlapPower = 3,
    Reflex = 4,
    Recovery = 5,
    WeightCut = 6,
    Study = 7
}

/// <summary>
/// One trainable session. Gains are expressed in attribute points before the
/// diminishing-returns curve, so a beginner at Power 30 gains far more from a
/// session than a veteran at Power 85.
/// </summary>
public sealed class TrainingActivity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>The session's name as the player reads it.</summary>
    public string DisplayName => Loc.T($"camp.{Id}.name");

    /// <summary>What the session does, in one line.</summary>
    public string DisplayDescription => Loc.T($"camp.{Id}.desc");
    public TrainingFocus Focus { get; set; } = TrainingFocus.Strength;

    /// <summary>Attribute -> base points earned per session.</summary>
    public Dictionary<string, double> Gains { get; set; } = new();

    /// <summary>Energy consumed. Cannot start a session without it.</summary>
    public int EnergyCost { get; set; } = 20;

    /// <summary>Base chance (0-1) of picking up an injury during the session.</summary>
    public double InjuryRisk { get; set; } = 0.02;

    /// <summary>Injury types this activity can cause, in priority order.</summary>
    public List<Fighters.InjuryType> PossibleInjuries { get; set; } = new();

    /// <summary>Pounds of walking weight gained (positive) or lost (negative).</summary>
    public decimal MassChangePounds { get; set; }

    /// <summary>Injury days healed by the session (recovery work only).</summary>
    public int HealsInjuryDays { get; set; }

    /// <summary>Pounds of water cut from the scale weight.</summary>
    public decimal WeightCutPounds { get; set; }

    /// <summary>Popularity gained (media work).</summary>
    public int PopularityGain { get; set; }

    /// <summary>Set for sessions that only restore energy.</summary>
    public bool IsRest => Focus == TrainingFocus.Recovery && EnergyCost <= 0;

    public string IconKey => Focus switch
    {
        TrainingFocus.Strength => "icon_weights",
        TrainingFocus.Endurance => "icon_run",
        TrainingFocus.Aim => "icon_target",
        TrainingFocus.SlapPower => "icon_hand",
        TrainingFocus.Reflex => "icon_reflex",
        TrainingFocus.Recovery => "icon_medkit",
        TrainingFocus.WeightCut => "icon_water",
        _ => "icon_video"
    };
}

/// <summary>The full training catalogue. Every session the player can pick from.</summary>
public static class TrainingCatalog
{
    public static TrainingActivity Weights { get; } = new()
    {
        Id = "weights",
        Name = "Heavy Weights",
        Description = "Squats, pulls and grip work. Adds mass and raw power, but drains you.",
        Focus = TrainingFocus.Strength,
        Gains = new() { ["Power"] = 1.8, ["Mass"] = 0.6 },
        EnergyCost = 35,
        InjuryRisk = 0.05,
        PossibleInjuries = new() { Fighters.InjuryType.BackSpasm, Fighters.InjuryType.ShoulderStrain },
        MassChangePounds = 0.6m
    };

    public static TrainingActivity Roadwork { get; } = new()
    {
        Id = "roadwork",
        Name = "Roadwork",
        Description = "Early-morning miles. Builds the engine that keeps your slap honest in round three.",
        Focus = TrainingFocus.Endurance,
        Gains = new() { ["Endurance"] = 2.0 },
        EnergyCost = 30,
        InjuryRisk = 0.03,
        PossibleInjuries = new() { Fighters.InjuryType.RibBruising, Fighters.InjuryType.NeckStrain },
        MassChangePounds = -0.4m
    };

    public static TrainingActivity SlapAimDrill { get; } = new()
    {
        Id = "aim",
        Name = "Slap Aim Drill",
        Description = "Pad work on a swinging target. Keeps the open hand on the cheek and away from the temple.",
        Focus = TrainingFocus.Aim,
        Gains = new() { ["Aim"] = 2.2 },
        EnergyCost = 22,
        InjuryRisk = 0.03,
        PossibleInjuries = new() { Fighters.InjuryType.WristSprain, Fighters.InjuryType.HandContusion }
    };

    public static TrainingActivity SlapPower { get; } = new()
    {
        Id = "slap-power",
        Name = "Slap Power Pads",
        Description = "Full-speed open-hand strikes into a heavy pad. The most direct route to a knockout.",
        Focus = TrainingFocus.SlapPower,
        Gains = new() { ["Power"] = 2.4 },
        EnergyCost = 35,
        InjuryRisk = 0.09,
        PossibleInjuries = new() { Fighters.InjuryType.HandContusion, Fighters.InjuryType.WristSprain, Fighters.InjuryType.ShoulderStrain }
    };

    public static TrainingActivity BagWork { get; } = new()
    {
        Id = "bag",
        Name = "Heavy Bag",
        Description = "Volume work on the bag for chin strength and conditioning.",
        Focus = TrainingFocus.Endurance,
        Gains = new() { ["Endurance"] = 1.4, ["Chin"] = 0.5 },
        EnergyCost = 30,
        InjuryRisk = 0.06,
        PossibleInjuries = new() { Fighters.InjuryType.HandContusion, Fighters.InjuryType.RibBruising }
    };

    public static TrainingActivity NeckAndChin { get; } = new()
    {
        Id = "neck",
        Name = "Neck & Chin Work",
        Description = "Harness work and isometrics. A thick neck survives a heavyweight's best slap.",
        Focus = TrainingFocus.Strength,
        Gains = new() { ["Chin"] = 2.0 },
        EnergyCost = 18,
        InjuryRisk = 0.04,
        PossibleInjuries = new() { Fighters.InjuryType.NeckStrain }
    };

    public static TrainingActivity SlipDrill { get; } = new()
    {
        Id = "reflex",
        Name = "Slip & Brace Drills",
        Description = "Reaction work so the brace goes up before the hand arrives.",
        Focus = TrainingFocus.Reflex,
        Gains = new() { ["Reflex"] = 2.2 },
        EnergyCost = 22,
        InjuryRisk = 0.02,
        PossibleInjuries = new() { Fighters.InjuryType.NeckStrain }
    };

    public static TrainingActivity Sparring { get; } = new()
    {
        Id = "sparring",
        Name = "Live Sparring",
        Description = "Controlled slap rounds. Best all-round gains in the game and the highest injury risk.",
        Focus = TrainingFocus.Reflex,
        Gains = new() { ["Reflex"] = 1.2, ["Aim"] = 0.8, ["Chin"] = 0.8, ["Power"] = 0.5 },
        EnergyCost = 40,
        InjuryRisk = 0.16,
        PossibleInjuries = new()
        {
            Fighters.InjuryType.Concussion, Fighters.InjuryType.BrokenJaw,
            Fighters.InjuryType.HandContusion, Fighters.InjuryType.RibBruising
        }
    };

    public static TrainingActivity Recovery { get; } = new()
    {
        Id = "recovery",
        Name = "Recovery Session",
        Description = "Ice baths, physio and sleep. Heals injuries faster and refills your tank.",
        Focus = TrainingFocus.Recovery,
        Gains = new() { ["Endurance"] = 0.3 },
        EnergyCost = 10,
        InjuryRisk = 0,
        HealsInjuryDays = 1
    };

    public static TrainingActivity Rest { get; } = new()
    {
        Id = "rest",
        Name = "Rest Day",
        Description = "Sleep, eat and do nothing. The cheapest way to get your energy back.",
        Focus = TrainingFocus.Recovery,
        Gains = new(),
        EnergyCost = 0,
        InjuryRisk = 0
    };

    public static TrainingActivity WeightCut { get; } = new()
    {
        Id = "cut",
        Name = "Weight Cut",
        Description = "Sauna and water loading. Takes pounds off the scale and your energy with it.",
        Focus = TrainingFocus.WeightCut,
        Gains = new(),
        EnergyCost = 30,
        InjuryRisk = 0.02,
        WeightCutPounds = 2.0m
    };

    public static TrainingActivity FilmStudy { get; } = new()
    {
        Id = "film",
        Name = "Film Study",
        Description = "Watch your opponent's last bout. Sharper aim and a little more buzz around your name.",
        Focus = TrainingFocus.Study,
        Gains = new() { ["Aim"] = 1.0, ["Reflex"] = 0.5 },
        EnergyCost = 5,
        InjuryRisk = 0,
        PopularityGain = 1
    };

    public static IReadOnlyList<TrainingActivity> All { get; } = new[]
    {
        Weights, Roadwork, SlapAimDrill, SlapPower, BagWork, NeckAndChin,
        SlipDrill, Sparring, Recovery, Rest, WeightCut, FilmStudy
    };

    public static TrainingActivity Get(string id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Unknown training activity '{id}'.");
}
