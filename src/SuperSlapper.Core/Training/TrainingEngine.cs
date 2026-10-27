using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Random;

namespace SuperSlapper.Core.Training;

/// <summary>Tuning knobs for the camp simulation, kept testable.</summary>
public sealed class TrainingBalance
{
    /// <summary>Energy below which a session counts as overtrained.</summary>
    public int OvertrainingEnergyThreshold { get; set; } = 20;

    /// <summary>Gain multiplier while overtrained.</summary>
    public double OvertrainingGainFactor { get; set; } = 0.5;

    /// <summary>Injury chance multiplier while overtrained.</summary>
    public double OvertrainingRiskFactor { get; set; } = 2.0;

    /// <summary>Energy restored at the end of every day.</summary>
    public int DailyEnergyRecovery { get; set; } = 15;

    /// <summary>Attribute value above which gains fall away sharply.</summary>
    public int SoftCap { get; set; } = 80;

    /// <summary>Attribute hard cap.</summary>
    public int HardCap { get; set; } = 100;

    /// <summary>Multiplier applied per consecutive session of the same activity (spam penalty).</summary>
    public double RepetitionPenalty { get; set; } = 0.12;

    /// <summary>Mass in pounds per point of the Mass attribute.</summary>
    public decimal PoundsPerMassPoint { get; set; } = 1.6m;

    /// <summary>Fraction of the cut weight regained before the first slap.</summary>
    public decimal RehydrationFraction { get; set; } = 0.9m;
}

public sealed class TrainingResult
{
    public string ActivityId { get; set; } = "";
    public string ActivityName { get; set; } = "";
    public Dictionary<string, int> AttributeGains { get; set; } = new();
    public int EnergyBefore { get; set; }
    public int EnergyAfter { get; set; }
    public bool Overtrained { get; set; }
    public Injury? Injury { get; set; }
    public bool InjuryHealed { get; set; }
    public decimal MassChangePounds { get; set; }
    public int PopularityGain { get; set; }
    public List<string> Messages { get; set; } = new();

    public bool MadeProgress => AttributeGains.Values.Any(v => v > 0);

    public string GainSummary
    {
        get
        {
            var parts = AttributeGains.Where(kv => kv.Value > 0)
                .Select(kv => $"+{kv.Value} {kv.Key}")
                .ToList();
            return parts.Count == 0 ? "No measurable gain" : string.Join(", ", parts);
        }
    }
}

/// <summary>
/// Runs a single training day on a fighter. Everything the camp does - gains,
/// fatigue, injury and healing - funnels through here so it can be unit tested
/// without a render loop.
/// </summary>
public sealed class TrainingEngine
{
    public TrainingEngine(TrainingBalance? balance = null) => Balance = balance ?? new TrainingBalance();

    public TrainingBalance Balance { get; }

    /// <summary>Attribute gain curve: big early, tiny near the soft cap, zero at the hard cap.</summary>
    public double GainMultiplier(int currentValue)
    {
        if (currentValue >= Balance.HardCap)
        {
            return 0;
        }

        if (currentValue <= Balance.SoftCap)
        {
            // 1.0 at 1, tapering to 0.45 at the soft cap.
            var span = Math.Max(1, Balance.SoftCap - 1);
            return 1.0 - 0.55 * (currentValue - 1) / (double)span;
        }

        var remaining = Balance.HardCap - currentValue;
        return Math.Max(0.02, 0.45 * remaining / (double)(Balance.HardCap - Balance.SoftCap));
    }

    /// <summary>True when the fighter is too tired to run this session.</summary>
    public bool CanTrain(Fighter fighter, TrainingActivity activity) =>
        activity.EnergyCost == 0 || fighter.Energy >= Math.Max(1, activity.EnergyCost / 2);

    /// <summary>Executes the session. The caller is responsible for advancing the calendar day.</summary>
    public TrainingResult Execute(Fighter fighter, TrainingActivity activity, IRandomSource rng,
        string? previousActivityId = null, int consecutiveRepeat = 0)
    {
        var result = new TrainingResult
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            EnergyBefore = fighter.Energy
        };

        if (!CanTrain(fighter, activity))
        {
            result.Messages.Add("Too exhausted to train. Take a rest day.");
            result.EnergyAfter = fighter.Energy;
            return result;
        }

        var energyFactor = 0.55 + 0.45 * Math.Clamp(fighter.Energy, 0, 100) / 100.0;
        var overtrained = fighter.Energy < Balance.OvertrainingEnergyThreshold;
        result.Overtrained = overtrained;

        if (overtrained)
        {
            energyFactor *= Balance.OvertrainingGainFactor;
            result.Messages.Add("Overtrained: gains cut in half today.");
        }

        var repetitionFactor = previousActivityId == activity.Id
            ? Math.Max(0.4, 1.0 - Balance.RepetitionPenalty * Math.Max(1, consecutiveRepeat))
            : 1.0;

        foreach (var (attribute, baseGain) in activity.Gains)
        {
            var member = FighterAttributes.Members.FirstOrDefault(m => m.Name == attribute);
            if (member.Name is null)
            {
                continue;
            }

            var current = member.Get(fighter.Attributes);
            var multiplier = GainMultiplier(current);
            if (multiplier <= 0)
            {
                continue;
            }

            var variance = 0.85 + rng.NextDouble() * 0.3;
            var raw = baseGain * multiplier * energyFactor * repetitionFactor * variance;
            var whole = (int)Math.Floor(raw + rng.NextDouble());
            if (whole > 0)
            {
                member.Set(fighter.Attributes, Math.Min(Balance.HardCap, current + whole));
                result.AttributeGains[attribute] = whole;
            }
        }

        if (activity.MassChangePounds != 0m)
        {
            var massDelta = activity.MassChangePounds;
            if (massDelta > 0m)
            {
                // Size follows the Mass attribute, and Mass is what drives the scale.
                massDelta *= (decimal)Math.Max(0.2, GainMultiplier(fighter.Attributes.Mass));
            }

            fighter.WalkAroundPounds = Math.Max(100m, Math.Round(fighter.WalkAroundPounds + massDelta, 1));
            fighter.WeightPounds = Math.Max(100m, Math.Round(fighter.WeightPounds + massDelta, 1));
            result.MassChangePounds = massDelta;
        }

        if (activity.WeightCutPounds > 0m && fighter.WeightPounds > 100m + activity.WeightCutPounds)
        {
            fighter.WeightPounds = Math.Round(fighter.WeightPounds - activity.WeightCutPounds, 1);
        }

        if (activity.HealsInjuryDays > 0)
        {
            var active = fighter.ActiveInjuries.ToList();
            if (active.Count > 0)
            {
                var worst = active.OrderByDescending(i => i.DaysRemaining).First();
                worst.DaysRemaining = Math.Max(0, worst.DaysRemaining - activity.HealsInjuryDays);
                result.InjuryHealed = worst.DaysRemaining == 0;
                if (result.InjuryHealed)
                {
                    fighter.Injuries.Remove(worst);
                    result.Messages.Add($"{worst.DisplayName} cleared.");
                }
            }
        }

        if (activity.PopularityGain > 0)
        {
            fighter.Popularity = Math.Min(100, fighter.Popularity + activity.PopularityGain);
            result.PopularityGain = activity.PopularityGain;
        }

        var risk = activity.InjuryRisk * (overtrained ? Balance.OvertrainingRiskFactor : 1.0);
        risk *= Math.Max(0.3, 1.0 - fighter.Energy / 250.0);

        if (activity.InjuryRisk > 0 && rng.NextDouble() < risk)
        {
            var injury = RollInjury(fighter, activity, rng);
            fighter.AddInjury(injury, 0);
            result.Injury = injury;
            result.Messages.Add($"Injured: {injury.DisplayName} ({injury.SeverityText}) - out {injury.DaysRemaining} days.");
        }

        fighter.Energy = Math.Clamp(fighter.Energy - activity.EnergyCost, 0, 100);
        result.EnergyAfter = fighter.Energy;
        return result;
    }

    /// <summary>
    /// Picks an injury appropriate to the session. Lower energy means a worse
    /// injury, and serious injuries are always possibility for sparring.
    /// </summary>
    public Injury RollInjury(Fighter fighter, TrainingActivity activity, IRandomSource rng)
    {
        var candidates = activity.PossibleInjuries.Count > 0
            ? activity.PossibleInjuries
            : new List<InjuryType> { InjuryType.HandContusion };

        var type = candidates[rng.Next(0, candidates.Count)];
        var fatigueBias = (100 - fighter.Energy) / 100.0;
        var roll = rng.NextDouble() + fatigueBias * 0.35;

        var severity = roll switch
        {
            < 0.7 => InjurySeverity.Niggle,
            < 0.95 => InjurySeverity.Moderate,
            _ => InjurySeverity.Serious
        };

        var days = Injury.DaysFor(severity);
        return new Injury
        {
            Type = type,
            Severity = severity,
            DaysRemaining = days,
            TotalDays = days
        };
    }

    /// <summary>End-of-day housekeeping: rest, heal, decay damage.</summary>
    public void EndOfDay(Fighter fighter)
    {
        foreach (var injury in fighter.Injuries)
        {
            if (injury.DaysRemaining > 0)
            {
                injury.DaysRemaining--;
            }
        }

        fighter.Injuries.RemoveAll(i => i.DaysRemaining <= 0);
        fighter.Energy = Math.Min(100, fighter.Energy + Balance.DailyEnergyRecovery);
        fighter.Damage = Math.Max(0, fighter.Damage - 2);
    }

    /// <summary>
    /// Rehydrates after the weigh-in. The fighter fights heavier than they
    /// weighed and hits harder for it, which is exactly why they cut in the
    /// first place.
    /// </summary>
    public decimal Rehydrate(Fighter fighter)
    {
        var cut = Math.Max(0m, fighter.WalkAroundPounds - fighter.WeightPounds);
        var restored = Math.Round(cut * Balance.RehydrationFraction, 1);
        fighter.WeightPounds = Math.Round(fighter.WeightPounds + restored, 1);
        return restored;
    }

    /// <summary>Attack bonus earned by fighting above the weigh-in weight, 0.7-1.08.</summary>
    public decimal RehydrationPowerFactor(Fighter fighter, decimal weighInPounds)
    {
        var regained = Math.Max(0m, fighter.WeightPounds - weighInPounds);
        return 0.7m + Math.Min(0.38m, regained / 30m * 0.38m);
    }
}
