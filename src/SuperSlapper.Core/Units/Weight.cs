namespace SuperSlapper.Core.Units;

/// <summary>Which system the player / official weighing machine is using.</summary>
public enum WeightUnit
{
    Imperial,
    Metric
}

/// <summary>
/// A body mass held canonically in pounds. All weight-class boundaries in slap
/// fighting are published in pounds and mirrored in kilos, so pounds is the
/// source of truth and kilos are derived. Decimal is used so boundary
/// comparisons never suffer floating point drift.
/// </summary>
public readonly record struct Weight : IComparable<Weight>
{
    /// <summary>Exact international avoirdupois pound in kilograms.</summary>
    public const decimal KilogramsPerPound = 0.45359237m;

    /// <summary>
    /// Slack allowed when comparing weights. A weigh-in display is rounded to
    /// 0.1 kg (~0.22 lb), so a fighter shown as 56.7 kg must still be accepted
    /// for a 125 lb limit.
    /// </summary>
    public const decimal ComparisonTolerancePounds = 0.25m;

    public Weight(decimal pounds) => Pounds = pounds;

    public decimal Pounds { get; }

    public decimal Kilograms => Math.Round(Pounds * KilogramsPerPound, 3, MidpointRounding.AwayFromZero);

    public static Weight FromPounds(decimal pounds) => new(pounds);

    public static Weight FromKilograms(decimal kilograms) => new(kilograms / KilogramsPerPound);

    public static Weight Zero => new(0m);

    public bool IsPositive => Pounds > 0m;

    /// <summary>Value in the requested unit, rounded the way scales display it.</summary>
    public decimal In(WeightUnit unit) => unit == WeightUnit.Metric
        ? Math.Round(Kilograms, 1, MidpointRounding.AwayFromZero)
        : Math.Round(Pounds, 1, MidpointRounding.AwayFromZero);

    /// <summary>True when this weight is at or under <paramref name="limit"/> (within display tolerance).</summary>
    public bool IsAtOrUnder(Weight limit) => Pounds <= limit.Pounds + ComparisonTolerancePounds;

    public bool IsOver(Weight limit) => !IsAtOrUnder(limit);

    public int CompareTo(Weight other) => Pounds.CompareTo(other.Pounds);

    public static bool operator <(Weight left, Weight right) => left.CompareTo(right) < 0;
    public static bool operator >(Weight left, Weight right) => left.CompareTo(right) > 0;
    public static bool operator <=(Weight left, Weight right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Weight left, Weight right) => left.CompareTo(right) >= 0;

    public static Weight operator +(Weight left, Weight right) => new(left.Pounds + right.Pounds);
    public static Weight operator -(Weight left, Weight right) => new(left.Pounds - right.Pounds);

    public string ToDisplayString(WeightUnit unit, bool withUnit = true)
    {
        var value = In(unit).ToString("0.0");
        if (!withUnit)
        {
            return value;
        }

        return unit == WeightUnit.Metric ? value + " kg" : value + " lb";
    }

    /// <summary>e.g. "56.7 kg (125.0 lb)".</summary>
    public string ToDualString() =>
        $"{ToDisplayString(WeightUnit.Metric)} ({ToDisplayString(WeightUnit.Imperial)})";

    public override string ToString() => ToDisplayString(WeightUnit.Imperial);

    public static Weight Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("Weight text was empty.");
        }

        var cleaned = text.Trim().ToLowerInvariant()
            .Replace("kilogrammes", string.Empty)
            .Replace("kilograms", string.Empty)
            .Replace("kilogram", string.Empty)
            .Replace("kilos", string.Empty)
            .Replace("kilo", string.Empty)
            .Replace("kgs", string.Empty)
            .Replace("kg", string.Empty)
            .Replace("pounds", string.Empty)
            .Replace("pound", string.Empty)
            .Replace("lbs", string.Empty)
            .Replace("lb", string.Empty)
            .Replace("stones", string.Empty)
            .Replace("stone", string.Empty)
            .Trim();

        var isMetric = text.Contains("kg", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("kilo", StringComparison.OrdinalIgnoreCase);

        if (!decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"'{text}' is not a valid weight.");
        }

        return isMetric ? FromKilograms(value) : FromPounds(value);
    }
}
