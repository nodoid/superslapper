using SuperSlapper.Core.Units;

namespace SuperSlapper.Core.Rules;

/// <summary>The divisions sanctioned by the slap federations modelled by the game.</summary>
public enum WeightClassId
{
    Flyweight = 0,
    Bantamweight = 1,
    Featherweight = 2,
    Lightweight = 3,
    Welterweight = 4,
    Middleweight = 5,
    LightHeavyweight = 6,
    Heavyweight = 7,
    SuperHeavyweight = 8
}

/// <summary>
/// One weight division. The upper limit is published in pounds (as every slap
/// federation does) and the metric figure is the converted, rounded equivalent.
/// A limit of <c>null</c> means "no upper limit" (Super Heavyweight).
/// </summary>
public sealed record WeightClass(
    WeightClassId Id,
    string Name,
    string ShortName,
    Weight? Limit,
    string Blurb)
{
    /// <summary>True when the division has no upper weight limit.</summary>
    public bool IsOpen => Limit is null;

    /// <summary>Published metric limit, e.g. "56.7 kg" — "No limit" for Super Heavyweight.</summary>
    public string MetricLimitText => Limit is null
        ? "No limit"
        : Limit.Value.ToDisplayString(WeightUnit.Metric);

    /// <summary>Published imperial limit, e.g. "125 lb" — "No limit" for Super Heavyweight.</summary>
    public string ImperialLimitText => Limit is null
        ? "No limit"
        : Limit.Value.ToDisplayString(WeightUnit.Imperial);

    public string LimitText(WeightUnit unit) => unit == WeightUnit.Metric ? MetricLimitText : ImperialLimitText;
}

public static class WeightClasses
{
    /// <summary>Ordered lightest first.</summary>
    public static IReadOnlyList<WeightClass> All { get; } = new[]
    {
        new WeightClass(WeightClassId.Flyweight, "Flyweight", "FLY",
            Weight.FromPounds(125m), "The smallest division. Speed and chin decide everything."),
        new WeightClass(WeightClassId.Bantamweight, "Bantamweight", "BAN",
            Weight.FromPounds(135m), "Fast hands and short, brutal exchanges."),
        new WeightClass(WeightClassId.Featherweight, "Featherweight", "FEA",
            Weight.FromPounds(145m), "Sharp technique meets real power."),
        new WeightClass(WeightClassId.Lightweight, "Lightweight", "LIG",
            Weight.FromPounds(155m), "The deepest talent pool in the sport."),
        new WeightClass(WeightClassId.Welterweight, "Welterweight", "WEL",
            Weight.FromPounds(170m), "Where knockout artists are made."),
        new WeightClass(WeightClassId.Middleweight, "Middleweight", "MID",
            Weight.FromPounds(185m), "Big frames, bigger slaps."),
        new WeightClass(WeightClassId.LightHeavyweight, "Light Heavyweight", "LHW",
            Weight.FromPounds(205m), "Bone-rattling power, no margin for error."),
        new WeightClass(WeightClassId.Heavyweight, "Heavyweight", "HEA",
            Weight.FromPounds(265m), "The glamour division. One slap ends careers."),
        new WeightClass(WeightClassId.SuperHeavyweight, "Super Heavyweight", "SHW",
            null, "No limit. Giants only.")
    };

    public static WeightClass Get(WeightClassId id) => All[(int)id];

    public static int Count => All.Count;

    /// <summary>
    /// The division a weight falls into when every division's published limit
    /// (plus the weigh-in allowance) has been applied.
    /// </summary>
    public static WeightClass Classify(Weight weight, decimal allowancePounds = 0m)
    {
        if (!weight.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be greater than zero.");
        }

        if (allowancePounds < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(allowancePounds), "Allowance cannot be negative.");
        }

        foreach (var division in All)
        {
            if (division.Limit is null)
            {
                return division;
            }

            var limit = Weight.FromPounds(division.Limit.Value.Pounds + allowancePounds);
            if (weight.IsAtOrUnder(limit))
            {
                return division;
            }
        }

        return All[^1];
    }

    /// <summary>True when <paramref name="weight"/> can be made for the division.</summary>
    public static bool Fits(WeightClassId division, Weight weight, decimal allowancePounds = 0m)
        => Classify(weight, allowancePounds).Id == division;

    public static bool HasLimit(WeightClassId division) => Get(division).Limit is not null;

    /// <summary>Pounds a fighter is over the limit for the division (0 when they make weight).</summary>
    public static decimal OverBy(WeightClassId division, Weight weight, decimal allowancePounds = 0m)
    {
        var limit = Get(division).Limit;
        if (limit is null)
        {
            return 0m;
        }

        var allowed = limit.Value.Pounds + allowancePounds;
        return weight.Pounds <= allowed + Weight.ComparisonTolerancePounds
            ? 0m
            : Math.Round(weight.Pounds - allowed, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Typical competition range of a division: from just above the previous
    /// division's limit (110 lb for flyweight) up to this division's limit.
    /// </summary>
    public static (Weight Min, Weight Max) TypicalRange(WeightClassId division)
    {
        var index = (int)division;
        var lower = index == 0
            ? Weight.FromPounds(110m)
            : Weight.FromPounds(All[index - 1].Limit!.Value.Pounds + 3m);
        var upper = Get(division).Limit ?? Weight.FromPounds(330m);
        return (lower, upper);
    }
}
