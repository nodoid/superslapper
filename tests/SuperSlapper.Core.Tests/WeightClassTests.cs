using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Units;

namespace SuperSlapper.Core.Tests;

public class WeightClassTests
{
    [Theory]
    [InlineData(125, 56.7)]
    [InlineData(135, 61.2)]
    [InlineData(145, 65.8)]
    [InlineData(155, 70.3)]
    [InlineData(170, 77.1)]
    [InlineData(185, 83.9)]
    [InlineData(205, 93.0)]
    [InlineData(265, 120.2)]
    public void PublishedMetricLimitsMatchImperialLimits(double pounds, double kilos)
    {
        var fromImperial = Weight.FromPounds((decimal)pounds);
        var fromMetric = Weight.FromKilograms((decimal)kilos);

        Assert.Equal((decimal)pounds, Math.Round(fromImperial.Pounds, 1));
        Assert.InRange(fromMetric.Pounds, (decimal)pounds - Weight.ComparisonTolerancePounds,
            (decimal)pounds + Weight.ComparisonTolerancePounds);
    }

    [Fact]
    public void EveryDivisionPublishesBothUnitLimits()
    {
        foreach (var division in WeightClasses.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(division.MetricLimitText));
            Assert.False(string.IsNullOrWhiteSpace(division.ImperialLimitText));
            Assert.Contains(division.IsOpen ? "limit" : "kg", division.MetricLimitText,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FlyweightUpperBoundIsInclusive()
    {
        Assert.Equal(WeightClassId.Flyweight, WeightClasses.Classify(Weight.FromPounds(125m)).Id);
        Assert.Equal(WeightClassId.Flyweight, WeightClasses.Classify(Weight.FromPounds(125.1m)).Id);
        Assert.Equal(WeightClassId.Bantamweight, WeightClasses.Classify(Weight.FromPounds(125.4m)).Id);
    }

    [Fact]
    public void MetricEntryAtThePublishedLimitStillFitsTheDivision()
    {
        // 56.7 kg is 125.002 lb, which a naive comparison would reject.
        var weight = Weight.FromKilograms(56.7m);
        Assert.True(weight.Pounds > 125m);
        Assert.Equal(WeightClassId.Flyweight, WeightClasses.Classify(weight).Id);
    }

    [Theory]
    [InlineData(110, WeightClassId.Flyweight)]
    [InlineData(125, WeightClassId.Flyweight)]
    [InlineData(126, WeightClassId.Bantamweight)]
    [InlineData(135, WeightClassId.Bantamweight)]
    [InlineData(136, WeightClassId.Featherweight)]
    [InlineData(145, WeightClassId.Featherweight)]
    [InlineData(146, WeightClassId.Lightweight)]
    [InlineData(155, WeightClassId.Lightweight)]
    [InlineData(156, WeightClassId.Welterweight)]
    [InlineData(170, WeightClassId.Welterweight)]
    [InlineData(171, WeightClassId.Middleweight)]
    [InlineData(185, WeightClassId.Middleweight)]
    [InlineData(186, WeightClassId.LightHeavyweight)]
    [InlineData(205, WeightClassId.LightHeavyweight)]
    [InlineData(206, WeightClassId.Heavyweight)]
    [InlineData(265, WeightClassId.Heavyweight)]
    [InlineData(266, WeightClassId.SuperHeavyweight)]
    [InlineData(400, WeightClassId.SuperHeavyweight)]
    public void ClassifyWalksTheBoundariesCorrectly(double pounds, WeightClassId expected)
    {
        Assert.Equal(expected, WeightClasses.Classify(Weight.FromPounds((decimal)pounds)).Id);
    }

    [Fact]
    public void AllowanceMovesTheBoundary()
    {
        var overByOne = Weight.FromPounds(126m);
        Assert.Equal(WeightClassId.Bantamweight, WeightClasses.Classify(overByOne).Id);
        Assert.Equal(WeightClassId.Flyweight, WeightClasses.Classify(overByOne, allowancePounds: 1m).Id);

        Assert.True(WeightClasses.Fits(WeightClassId.Flyweight, overByOne, 1m));
        Assert.False(WeightClasses.Fits(WeightClassId.Flyweight, overByOne, 0m));
    }

    [Theory]
    [InlineData(WeightClassId.Flyweight, 127, 1, 1.0)]
    [InlineData(WeightClassId.Flyweight, 127, 0, 2.0)]
    [InlineData(WeightClassId.Heavyweight, 268, 1, 2.0)]
    [InlineData(WeightClassId.Heavyweight, 265, 1, 0.0)]
    [InlineData(WeightClassId.SuperHeavyweight, 400, 0, 0.0)]
    public void OverByReportsPoundsOverTheAllowedLimit(WeightClassId division, double weight,
        double allowance, double expected)
    {
        var over = WeightClasses.OverBy(division, Weight.FromPounds((decimal)weight), (decimal)allowance);
        Assert.Equal((decimal)expected, over);
    }

    [Fact]
    public void SuperHeavyweightHasNoLimit()
    {
        var division = WeightClasses.Get(WeightClassId.SuperHeavyweight);
        Assert.True(division.IsOpen);
        Assert.Null(division.Limit);
        Assert.Equal("No limit", division.ImperialLimitText);
        Assert.Equal(0m, WeightClasses.OverBy(WeightClassId.SuperHeavyweight, Weight.FromPounds(600m)));
    }

    [Fact]
    public void WeightRoundTripsBetweenUnits()
    {
        var weight = Weight.FromKilograms(83.9m);
        var display = weight.ToDisplayString(WeightUnit.Metric);
        Assert.Equal("83.9 kg", display);
        Assert.Equal("185.0 lb", weight.ToDisplayString(WeightUnit.Imperial));

        var parsedKg = Weight.Parse("83.9kg");
        var parsedLb = Weight.Parse("185 lb");
        Assert.Equal(weight.Kilograms, parsedKg.Kilograms);
        Assert.Equal(185m, Math.Round(parsedLb.Pounds, 1));
    }

    [Theory]
    [InlineData("83.9 kg")]
    [InlineData("83.9kg")]
    [InlineData("83.9 kilos")]
    [InlineData("83.9 kilograms")]
    public void ParseAcceptsMetricSpellings(string text)
    {
        Assert.Equal(83.9m, Weight.Parse(text).In(WeightUnit.Metric));
    }

    [Theory]
    [InlineData("185 lb")]
    [InlineData("185lb")]
    [InlineData("185 pounds")]
    [InlineData("185lbs")]
    public void ParseAcceptsImperialSpellings(string text)
    {
        Assert.Equal(185m, Weight.Parse(text).In(WeightUnit.Imperial));
    }

    [Fact]
    public void ParseRejectsNonsense()
    {
        Assert.Throws<FormatException>(() => Weight.Parse("heavy"));
        Assert.Throws<FormatException>(() => Weight.Parse(""));
    }

    [Fact]
    public void ZeroWeightCannotBeClassified()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeightClasses.Classify(Weight.Zero));
    }

    [Fact]
    public void NegativeAllowanceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeightClasses.Classify(Weight.FromPounds(150m), -1m));
    }

    [Fact]
    public void DivisionsAreOrderedLightestFirst()
    {
        var ordered = WeightClasses.All.Select(c => c.Id).ToList();
        Assert.Equal(ordered.OrderBy(id => (int)id), ordered);
        Assert.Equal(WeightClasses.Count, ordered.Distinct().Count());
    }

    [Fact]
    public void TypicalRangeStaysInsideTheDivision()
    {
        foreach (var division in WeightClasses.All)
        {
            var (min, max) = WeightClasses.TypicalRange(division.Id);
            Assert.True(min.Pounds < max.Pounds);
            Assert.Equal(division.Id, WeightClasses.Classify(min).Id);
            if (division.Limit is not null)
            {
                Assert.Equal(division.Id, WeightClasses.Classify(max).Id);
            }
        }
    }
}
