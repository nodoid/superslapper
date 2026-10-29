using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Units;

namespace SuperSlapper.Core.Fight;

/// <summary>The outcome of stepping on the scale against a division limit.</summary>
public sealed class WeighInResult
{
    public WeightClassId Division { get; set; }
    public decimal ActualPounds { get; set; }
    public decimal LimitPounds { get; set; }
    public decimal AllowancePounds { get; set; }
    public bool TitleFight { get; set; }
    public WeightUnit Unit { get; set; } = WeightUnit.Metric;

    public Weight Actual => Weight.FromPounds(ActualPounds);

    public Weight LimitWithAllowance => Weight.FromPounds(LimitPounds + AllowancePounds);

    public bool IsUnlimited => LimitPounds <= 0m;

    public bool MadeWeight { get; set; }

    /// <summary>Pounds over the allowed limit; zero when the fighter made weight.</summary>
    public decimal OverPounds { get; set; }

    /// <summary>Championship belts cannot be won or defended when a fighter misses weight.</summary>
    public bool ChampionshipAtRisk => !MadeWeight;

    /// <summary>Percentage of the walk-around weight that was cut to reach this number.</summary>
    public decimal CutPercent { get; set; }

    public decimal PurseFinePercent => OverPounds switch
    {
        <= 0m => 0m,
        <= 1m => 10m,
        <= 3m => 20m,
        _ => 30m
    };

    public string VerdictText => IsUnlimited
        ? Loc.T("weighIn.noLimit")
        : MadeWeight
            ? Loc.T("weighIn.made")
            : Loc.T("weighIn.missedBy", Weight.FromPounds(OverPounds).ToDisplayString(Unit));

    public string ScaleText => Loc.T("weighIn.scale", Actual.ToDisplayString(Unit),
        LimitWithAllowance.ToDisplayString(Unit));

    public string Summary => IsUnlimited
        ? Loc.T("weighIn.summaryNoLimit", Actual.ToDisplayString(Unit))
        : Loc.T("weighIn.summary", Actual.ToDisplayString(Unit),
            LimitWithAllowance.ToDisplayString(Unit), VerdictText);
}

public static class WeighIn
{
    /// <summary>
    /// Applies the division limit and the rulebook's allowance (tighter for
    /// championship bouts). A fighter who has cut more than the rulebook allows
    /// is flagged through <see cref="WeighInResult.CutPercent"/>.
    /// </summary>
    public static WeighInResult Evaluate(Fighter fighter, WeightClassId division, Ruleset rules,
        bool titleFight, WeightUnit unit = WeightUnit.Metric)
    {
        var weightClass = WeightClasses.Get(division);
        var allowance = rules.AllowancePounds(titleFight);
        var actual = fighter.WeightPounds;

        var result = new WeighInResult
        {
            Division = division,
            ActualPounds = actual,
            AllowancePounds = allowance,
            TitleFight = titleFight,
            Unit = unit,
            LimitPounds = weightClass.Limit?.Pounds ?? 0m
        };

        if (weightClass.Limit is null)
        {
            result.MadeWeight = true;
            result.OverPounds = 0m;
        }
        else
        {
            var over = WeightClasses.OverBy(division, Weight.FromPounds(actual), allowance);
            result.OverPounds = over;
            result.MadeWeight = over <= 0m;

            // A fighter above the division limit entirely is in the wrong class.
            var actualClass = WeightClasses.Classify(Weight.FromPounds(actual), allowance);
            if (actualClass.Id != division && actualClass.Id > division)
            {
                result.MadeWeight = false;
                var classLimit = weightClass.Limit.Value.Pounds + allowance;
                result.OverPounds = Math.Max(result.OverPounds,
                    Math.Round(actual - classLimit, 1, MidpointRounding.AwayFromZero));
            }
        }

        if (fighter.WalkAroundPounds > 0m)
        {
            result.CutPercent = Math.Round(
                Math.Max(0m, fighter.WalkAroundPounds - actual) / fighter.WalkAroundPounds * 100m, 1,
                MidpointRounding.AwayFromZero);
        }

        return result;
    }
}
