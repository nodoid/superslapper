using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Units;

namespace SuperSlapper.Core.Tests;

public class WeighInTests
{
    private static Fighter FighterAt(decimal pounds) => new()
    {
        Id = "f1",
        WalkAroundPounds = pounds + 6m,
        WeightPounds = pounds
    };

    [Fact]
    public void FighterOnTheLimitMakesWeight()
    {
        var result = WeighIn.Evaluate(FighterAt(155m), WeightClassId.Lightweight, Rulesets.WorldSlapLeague, false);
        Assert.True(result.MadeWeight);
        Assert.Equal(0m, result.OverPounds);
        Assert.Equal("Made weight", result.VerdictText);
    }

    [Fact]
    public void PoundOverFitsANormalBoutButNotATitleFight()
    {
        var rules = Rulesets.WorldSlapLeague;
        var under = WeighIn.Evaluate(FighterAt(156m), WeightClassId.Lightweight, rules, titleFight: false);
        var title = WeighIn.Evaluate(FighterAt(156m), WeightClassId.Lightweight, rules, titleFight: true);

        Assert.True(under.MadeWeight);
        Assert.False(title.MadeWeight);
        Assert.Equal(1m, title.OverPounds);
        Assert.True(title.ChampionshipAtRisk);
        Assert.Equal(10m, title.PurseFinePercent);
    }

    [Theory]
    [InlineData(157, 20)]
    [InlineData(158, 20)]
    [InlineData(159, 30)]
    [InlineData(165, 30)]
    public void PurseFinesStepUpWithTheMiss(double pounds, decimal expectedFine)
    {
        var result = WeighIn.Evaluate(FighterAt((decimal)pounds), WeightClassId.Lightweight,
            Rulesets.WorldSlapLeague, titleFight: true);
        Assert.False(result.MadeWeight);
        Assert.Equal(expectedFine, result.PurseFinePercent);
    }

    [Fact]
    public void SuperHeavyweightAlwaysMakesWeight()
    {
        var result = WeighIn.Evaluate(FighterAt(420m), WeightClassId.SuperHeavyweight,
            Rulesets.WorldSlapLeague, true);
        Assert.True(result.MadeWeight);
        Assert.True(result.IsUnlimited);
        Assert.Equal("No limit", result.VerdictText);
    }

    [Fact]
    public void CutPercentIsMeasuredAgainstWalkAroundWeight()
    {
        var fighter = new Fighter { WalkAroundPounds = 180m, WeightPounds = 165m };
        var result = WeighIn.Evaluate(fighter, WeightClassId.Welterweight, Rulesets.WorldSlapLeague, false);
        Assert.Equal(8.3m, result.CutPercent);
    }

    [Fact]
    public void ScaleTextShowsBothSidesOfTheLimit()
    {
        var result = WeighIn.Evaluate(FighterAt(170m), WeightClassId.Welterweight,
            Rulesets.WorldSlapLeague, false, WeightUnit.Imperial);
        Assert.Contains("170.0 lb", result.ScaleText);
        Assert.Contains("171.0 lb", result.ScaleText);
    }

    [Fact]
    public void MetricAndImperialVerdictsAgree()
    {
        var fighter = FighterAt(155m);
        var metric = WeighIn.Evaluate(fighter, WeightClassId.Lightweight, Rulesets.AtlanticFederation, false,
            WeightUnit.Metric);
        var imperial = WeighIn.Evaluate(fighter, WeightClassId.Lightweight, Rulesets.AtlanticFederation, false,
            WeightUnit.Imperial);

        Assert.Equal(metric.MadeWeight, imperial.MadeWeight);
        Assert.Contains("70.3 kg", metric.ScaleText);
        Assert.Contains("156.0 lb", imperial.ScaleText);
    }

    [Fact]
    public void FighterClearlyOutsideTheDivisionMissesWeight()
    {
        var result = WeighIn.Evaluate(FighterAt(178m), WeightClassId.Lightweight,
            Rulesets.WorldSlapLeague, false);
        Assert.False(result.MadeWeight);
        Assert.True(result.OverPounds >= 22m);
    }
}
