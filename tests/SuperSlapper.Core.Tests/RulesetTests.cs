using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Tests;

public class RulesetTests
{
    public static TheoryData<string> RulebookIds => new()
    {
        "wsl-world", "esc-continental", "asf-open", "amateur"
    };

    [Theory]
    [MemberData(nameof(RulebookIds))]
    public void ShippedRulebooksAreLegal(string id)
    {
        var rules = Rulesets.Get(id);
        Assert.Empty(rules.Validate());
    }

    [Theory]
    [MemberData(nameof(RulebookIds))]
    public void ShippedRulebooksOnlySanctionCheekAndJaw(string id)
    {
        var rules = Rulesets.Get(id);
        Assert.Contains(SlapZone.LeftCheek, rules.IllegalZones.Concat(SlapZones.Legal).Except(rules.IllegalZones));
        foreach (var illegal in new[]
                 {
                     SlapZone.Eye, SlapZone.Throat, SlapZone.BackOfHead, SlapZone.Temple, SlapZone.Ear
                 })
        {
            Assert.True(rules.IsIllegalZone(illegal), $"{id} should treat {illegal} as a foul.");
        }
    }

    [Fact]
    public void EveryRulebookYieldsAtLeastOneSlapPerFighter()
    {
        foreach (var rules in Rulesets.All)
        {
            Assert.True(rules.SlapsPerFighter >= 1);
            Assert.Equal(rules.Rounds * rules.SlapsPerRound, rules.SlapsPerFighter);
        }
    }

    [Fact]
    public void TitleFightsAllowLessWeightThanNormalBouts()
    {
        foreach (var rules in Rulesets.All)
        {
            Assert.True(rules.AllowancePounds(true) <= rules.AllowancePounds(false));
        }
    }

    [Fact]
    public void WorldTitleRulesRefuseAnyAllowance()
    {
        Assert.Equal(0m, Rulesets.WorldSlapLeague.AllowancePounds(true));
        Assert.Equal(1m, Rulesets.WorldSlapLeague.AllowancePounds(false));
    }

    [Fact]
    public void InvalidRoundCountIsRejected()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.Rounds = 0;
        Assert.Contains(rules.Validate(), e => e.Contains("Rounds", StringComparison.OrdinalIgnoreCase));

        rules.Rounds = 6;
        Assert.Contains(rules.Validate(), e => e.Contains("Rounds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidSlapCountIsRejected()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.SlapsPerRound = 4;
        Assert.Contains(rules.Validate(), e => e.Contains("Slaps per round", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TitleAllowanceCannotExceedNormalAllowance()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.TitleWeighInAllowancePounds = 5m;
        rules.NonTitleWeighInAllowancePounds = 1m;
        Assert.NotEmpty(rules.Validate());
    }

    [Fact]
    public void EmptyIllegalZoneListIsRejected()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.IllegalZones.Clear();
        Assert.NotEmpty(rules.Validate());
    }

    [Fact]
    public void CloneIsIndependent()
    {
        var clone = Rulesets.WorldSlapLeague.Clone();
        clone.Rounds = 1;
        clone.IllegalZones.Clear();
        Assert.Equal(5, Rulesets.WorldSlapLeague.Rounds);
        Assert.NotEmpty(Rulesets.WorldSlapLeague.IllegalZones);
    }

    [Fact]
    public void UnknownRulebookThrows()
    {
        Assert.Throws<KeyNotFoundException>(() => Rulesets.Get("does-not-exist"));
    }

    [Fact]
    public void EveryLeagueIsLegalAndHasARulebook()
    {
        foreach (var league in Leagues.Leagues.All)
        {
            Assert.Empty(league.Validate());
            Assert.NotNull(league.Ruleset);
            Assert.NotEmpty(league.Divisions);
        }
    }

    [Fact]
    public void LeaguePrestigeOrdersThePyramid()
    {
        var ordered = Leagues.Leagues.ByPrestige;
        Assert.Equal(Leagues.Leagues.AmateurCircuit.Id, ordered[0].Id);
        Assert.Equal(Leagues.Leagues.WorldSlapLeague.Id, ordered[^1].Id);
    }

    [Fact]
    public void TitlePurseBeatsBasePurse()
    {
        foreach (var league in Leagues.Leagues.All)
        {
            Assert.True(league.PurseFor(true, true) > league.PurseFor(false, true));
        }
    }

    [Fact]
    public void ZoneLegalityMatchesTheSport()
    {
        Assert.True(SlapZones.IsLegal(SlapZone.LeftCheek));
        Assert.True(SlapZones.IsLegal(SlapZone.RightCheek));
        Assert.True(SlapZones.IsLegal(SlapZone.Jaw));
        Assert.False(SlapZones.IsLegal(SlapZone.Temple));
        Assert.False(SlapZones.IsLegal(SlapZone.Eye));
        Assert.False(SlapZones.IsLegal(SlapZone.Throat));
        Assert.Equal(3, SlapZones.Legal.Count);
    }
}
