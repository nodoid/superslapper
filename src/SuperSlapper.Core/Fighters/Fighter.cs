using System.Text.Json.Serialization;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Fighters;

public enum Stance
{
    Southpaw = 0,
    Orthodox = 1
}

/// <summary>
/// The trainable attributes. All are 1-100; the fight engine converts them to
/// damage, accuracy and durability multipliers.
/// </summary>
public sealed class FighterAttributes
{
    /// <summary>Raw hitting power of an open hand.</summary>
    public int Power { get; set; } = 40;

    /// <summary>Ability to place the slap on the cheek instead of the temple.</summary>
    public int Aim { get; set; } = 40;

    /// <summary>Durability: resistance to knockdowns and knockouts.</summary>
    public int Chin { get; set; } = 40;

    /// <summary>Conditioning: how long power survives a long bout.</summary>
    public int Endurance { get; set; } = 40;

    /// <summary>Reaction and slip: how well the fighter braces for a slap.</summary>
    public int Reflex { get; set; } = 40;

    /// <summary>Walk-around body mass. Rises with weight training.</summary>
    public int Mass { get; set; } = 40;

    public int Total => Power + Aim + Chin + Endurance + Reflex + Mass;

    public FighterAttributes Clone() => new()
    {
        Power = Power,
        Aim = Aim,
        Chin = Chin,
        Endurance = Endurance,
        Reflex = Reflex,
        Mass = Mass
    };

    public static FighterAttributes FromOverall(int overall) => new()
    {
        Power = overall,
        Aim = overall,
        Chin = overall,
        Endurance = overall,
        Reflex = overall,
        Mass = overall
    };

    public static IReadOnlyList<(string Name, Func<FighterAttributes, int> Get, Action<FighterAttributes, int> Set)> Members { get; } =
        new List<(string, Func<FighterAttributes, int>, Action<FighterAttributes, int>)>
        {
            ("Power", a => a.Power, (a, v) => a.Power = v),
            ("Aim", a => a.Aim, (a, v) => a.Aim = v),
            ("Chin", a => a.Chin, (a, v) => a.Chin = v),
            ("Endurance", a => a.Endurance, (a, v) => a.Endurance = v),
            ("Reflex", a => a.Reflex, (a, v) => a.Reflex = v),
            ("Mass", a => a.Mass, (a, v) => a.Mass = v)
        };

    public void Clamp()
    {
        foreach (var member in Members)
        {
            var value = Math.Clamp(member.Get(this), 1, 100);
            member.Set(this, value);
        }
    }
}

public enum InjurySeverity
{
    Niggle = 1,
    Moderate = 2,
    Serious = 3
}

public enum InjuryType
{
    HandContusion = 0,
    WristSprain = 1,
    ShoulderStrain = 2,
    BackSpasm = 3,
    RibBruising = 4,
    Concussion = 5,
    NeckStrain = 6,
    BrokenJaw = 7
}

/// <summary>A twist, a break or a concussion, and the days it keeps a fighter out.</summary>
public sealed class Injury
{
    public InjuryType Type { get; set; }
    public InjurySeverity Severity { get; set; } = InjurySeverity.Niggle;
    public int DaysRemaining { get; set; }
    public int TotalDays { get; set; }
    public int InjuryDay { get; set; }

    [JsonIgnore]
    public bool IsHealed => DaysRemaining <= 0;

    /// <summary>Attribute penalty while the injury is live, in attribute points.</summary>
    public int AttributePenalty => Severity switch
    {
        InjurySeverity.Niggle => 3,
        InjurySeverity.Moderate => 7,
        _ => 14
    };

    /// <summary>Which attributes the injury drags down.</summary>
    public IReadOnlyList<string> AffectedAttributes => Type switch
    {
        InjuryType.HandContusion or InjuryType.WristSprain => new[] { "Power", "Aim" },
        InjuryType.ShoulderStrain => new[] { "Power" },
        InjuryType.BackSpasm => new[] { "Power", "Endurance" },
        InjuryType.RibBruising => new[] { "Endurance" },
        InjuryType.Concussion => new[] { "Reflex", "Chin", "Aim" },
        InjuryType.NeckStrain => new[] { "Reflex" },
        InjuryType.BrokenJaw => new[] { "Chin", "Endurance" },
        _ => Array.Empty<string>()
    };

    public string DisplayName => Loc.T($"injury.{Type}");

    public string SeverityText => Loc.T($"injury.severity.{Severity}");

    public Injury Clone() => new()
    {
        Type = Type,
        Severity = Severity,
        DaysRemaining = DaysRemaining,
        TotalDays = TotalDays,
        InjuryDay = InjuryDay
    };

    /// <summary>Healing days for a severity, used by the injury generator.</summary>
    public static int DaysFor(InjurySeverity severity) => severity switch
    {
        InjurySeverity.Niggle => 4,
        InjurySeverity.Moderate => 11,
        _ => 28
    };
}

public sealed class FightRecord
{
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int KnockoutWins { get; set; }
    public int KnockoutLosses { get; set; }

    [JsonIgnore]
    public int Total => Wins + Losses + Draws;

    [JsonIgnore]
    public decimal WinRatio => Total == 0 ? 0m : Math.Round((decimal)Wins / Total, 3);

    public string Display => $"{Wins}-{Losses}-{Draws}";

    public string DisplayWithKo =>
        Loc.T("record.withKo", Wins, Losses, Draws, KnockoutWins, Loc.T("outcome.ko"));

    public FightRecord Clone() => new()
    {
        Wins = Wins,
        Losses = Losses,
        Draws = Draws,
        KnockoutWins = KnockoutWins,
        KnockoutLosses = KnockoutLosses
    };
}

/// <summary>
/// A slap fighter: identity, attributes, current condition (weight, energy,
/// accumulated damage) and injuries. Condition values are what propagate into
/// the next bout, so they live here rather than in the fight engine.
/// </summary>
public sealed class Fighter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = "New";
    public string LastName { get; set; } = "Signing";
    public string Nickname { get; set; } = "";
    public string Nationality { get; set; } = "GB";
    public int Age { get; set; } = 27;
    public Stance Stance { get; set; } = Stance.Orthodox;
    public string LeagueId { get; set; } = "amateur";
    public WeightClassId Division { get; set; } = WeightClassId.Welterweight;

    /// <summary>Walk-around weight in pounds.</summary>
    public decimal WalkAroundPounds { get; set; } = 165m;

    /// <summary>Body weight the fighter actually weighs in at (after the cut).</summary>
    public decimal WeightPounds { get; set; } = 165m;

    public FighterAttributes Attributes { get; set; } = new();
    public FightRecord Record { get; set; } = new();
    public List<Injury> Injuries { get; set; } = new();

    /// <summary>0-100 conditioning left for today's session. Refills overnight.</summary>
    public int Energy { get; set; } = 100;

    /// <summary>0-100 accumulated head damage. Decays with rest.</summary>
    public int Damage { get; set; } = 0;

    /// <summary>Popularity 0-100; drives purse and main-event status.</summary>
    public int Popularity { get; set; } = 10;

    /// <summary>Consecutive wins (negative means losses).</summary>
    public int Streak { get; set; }

    /// <summary>Day number of the last bout.</summary>
    public int LastFightDay { get; set; } = -1;

    public bool IsPlayer { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Nickname)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} \"{Nickname}\" {LastName}";

    public string ShortName => $"{FirstName[0]}. {LastName}";

    public int Overall
    {
        get
        {
            var a = Attributes;
            var weighted = a.Power * 0.28m + a.Aim * 0.2m + a.Chin * 0.2m +
                           a.Endurance * 0.14m + a.Reflex * 0.12m + a.Mass * 0.06m;
            return (int)Math.Round(weighted, MidpointRounding.AwayFromZero);
        }
    }

    public IEnumerable<Injury> ActiveInjuries => Injuries.Where(i => !i.IsHealed);

    public bool IsInjured => ActiveInjuries.Any();

    public int InjuryPenalty => ActiveInjuries.Sum(i => i.AttributePenalty);

    /// <summary>Attributes after every live injury has been applied, floored at 1.</summary>
    public FighterAttributes EffectiveAttributes()
    {
        var copy = Attributes.Clone();
        foreach (var injury in ActiveInjuries)
        {
            foreach (var name in injury.AffectedAttributes)
            {
                var member = FighterAttributes.Members.FirstOrDefault(m => m.Name == name);
                if (member.Name is null)
                {
                    continue;
                }

                member.Set(copy, Math.Max(1, member.Get(copy) - injury.AttributePenalty));
            }
        }

        return copy;
    }

    /// <summary>Energy expressed as a 0.65-1.0 multiplier applied to output.</summary>
    public decimal EnergyFactor => 0.65m + 0.35m * Math.Clamp(Energy, 0, 100) / 100m;

    public void AddInjury(Injury injury, int day)
    {
        injury.InjuryDay = day;
        injury.TotalDays = Math.Max(injury.TotalDays, injury.DaysRemaining);
        Injuries.Add(injury);
    }

    /// <summary>Advance one calendar day: healing, energy, damage decay.</summary>
    public void PassDay()
    {
        foreach (var injury in Injuries)
        {
            if (injury.DaysRemaining > 0)
            {
                injury.DaysRemaining--;
            }
        }

        Injuries.RemoveAll(i => i.DaysRemaining <= 0);
        Energy = Math.Min(100, Energy + 15);
        Damage = Math.Max(0, Damage - 2);
        Age = Age;
    }

    /// <summary>Deep copy, used when a fight needs a frozen snapshot of both fighters.</summary>
    public Fighter Clone()
    {
        var clone = (Fighter)MemberwiseClone();
        clone.Attributes = Attributes.Clone();
        clone.Record = Record.Clone();
        clone.Injuries = Injuries.Select(i => i.Clone()).ToList();
        return clone;
    }
}
