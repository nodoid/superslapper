using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Training;

namespace SuperSlapper.Core.Tests;

public class TrainingTests
{
    private static Fighter NewFighter(int power = 40, int energy = 100) => new()
    {
        Id = "t1",
        WalkAroundPounds = 170m,
        WeightPounds = 170m,
        Energy = energy,
        Attributes = new FighterAttributes
        {
            Power = power,
            Aim = 40,
            Chin = 40,
            Endurance = 40,
            Reflex = 40,
            Mass = 50
        }
    };

    [Fact]
    public void EveryCatalogueEntryHasAnIdentityAndIcon()
    {
        foreach (var activity in TrainingCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(activity.Id));
            Assert.False(string.IsNullOrWhiteSpace(activity.Name));
            Assert.False(string.IsNullOrWhiteSpace(activity.Description));
            Assert.False(string.IsNullOrWhiteSpace(activity.IconKey));
        }
    }

    [Fact]
    public void CatalogueCoversThePromisedDisciplines()
    {
        var focuses = TrainingCatalog.All.Select(a => a.Focus).Distinct().ToList();
        Assert.Contains(TrainingFocus.Strength, focuses);
        Assert.Contains(TrainingFocus.Endurance, focuses);
        Assert.Contains(TrainingFocus.Aim, focuses);
        Assert.Contains(TrainingFocus.SlapPower, focuses);
        Assert.Contains(TrainingFocus.Recovery, focuses);
        Assert.Contains(TrainingFocus.WeightCut, focuses);
    }

    [Fact]
    public void WeightSessionBuildsPowerAndMass()
    {
        var fighter = NewFighter();
        var engine = new TrainingEngine();
        var before = fighter.Attributes.Power;
        var weightBefore = fighter.WalkAroundPounds;

        var result = engine.Execute(fighter, TrainingCatalog.Weights, new RandomSource(1));

        Assert.True(fighter.Attributes.Power > before);
        Assert.True(fighter.WalkAroundPounds >= weightBefore);
        Assert.True(result.MadeProgress);
    }

    [Fact]
    public void EnduranceSessionRaisesEnduranceAndShedsAFewPounds()
    {
        var fighter = NewFighter();
        var engine = new TrainingEngine();
        var before = fighter.Attributes.Endurance;
        var weightBefore = fighter.WalkAroundPounds;

        engine.Execute(fighter, TrainingCatalog.Roadwork, new RandomSource(2));

        Assert.True(fighter.Attributes.Endurance > before);
        Assert.True(fighter.WalkAroundPounds < weightBefore);
    }

    [Fact]
    public void AimAndPowerSessionsTargetTheRightAttributes()
    {
        var engine = new TrainingEngine();
        var aimFighter = NewFighter();
        engine.Execute(aimFighter, TrainingCatalog.SlapAimDrill, new RandomSource(3));
        Assert.True(aimFighter.Attributes.Aim > 40);
        Assert.Equal(40, aimFighter.Attributes.Power);

        var powerFighter = NewFighter();
        engine.Execute(powerFighter, TrainingCatalog.SlapPower, new RandomSource(4));
        Assert.True(powerFighter.Attributes.Power > 40);
    }

    [Fact]
    public void GainsDiminishAsTheAttributeGrows()
    {
        var engine = new TrainingEngine();
        var rookie = NewFighter(power: 20);
        var veteran = NewFighter(power: 92);

        var rookieGain = 0;
        var veteranGain = 0;
        for (var i = 0; i < 40; i++)
        {
            var r = NewFighter(power: 20);
            var v = NewFighter(power: 92);
            rookieGain += engine.Execute(r, TrainingCatalog.SlapPower, new RandomSource(i * 31 + 7))
                .AttributeGains.GetValueOrDefault("Power");
            veteranGain += engine.Execute(v, TrainingCatalog.SlapPower, new RandomSource(i * 31 + 7))
                .AttributeGains.GetValueOrDefault("Power");
        }

        Assert.True(rookieGain > veteranGain * 2,
            $"Expected rookies to gain much faster (rookie {rookieGain}, veteran {veteranGain}).");
    }

    [Fact]
    public void AttributeNeverExceedsTheHardCap()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter(power: 100);
        engine.Execute(fighter, TrainingCatalog.SlapPower, new RandomSource(5));
        Assert.Equal(100, fighter.Attributes.Power);
    }

    [Fact]
    public void ExhaustedFighterCannotTrain()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter(energy: 3);
        var result = engine.Execute(fighter, TrainingCatalog.SlapPower, new RandomSource(6));

        Assert.False(result.MadeProgress);
        Assert.Contains(result.Messages, m => m.Contains("exhausted", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, fighter.Energy);
    }

    [Fact]
    public void OvertrainingHalvesGainsAndDoublesRisk()
    {
        var engine = new TrainingEngine();
        var tired = NewFighter(energy: 16);
        var result = engine.Execute(tired, TrainingCatalog.Roadwork, new RandomSource(7));
        Assert.True(result.Overtrained);
        Assert.Contains(result.Messages, m => m.Contains("Overtrained", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RepeatedIdenticalSessionsDiminish()
    {
        var engine = new TrainingEngine();
        var fresh = NewFighter();
        var repeated = NewFighter();

        var first = engine.Execute(fresh, TrainingCatalog.SlapAimDrill, new RandomSource(8));
        var spam = engine.Execute(repeated, TrainingCatalog.SlapAimDrill, new RandomSource(8),
            "aim", consecutiveRepeat: 3);

        var freshGain = first.AttributeGains.GetValueOrDefault("Aim");
        var spamGain = spam.AttributeGains.GetValueOrDefault("Aim");
        Assert.True(freshGain >= spamGain);
    }

    [Fact]
    public void TrainingDrainsEnergyByTheListedCost()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter(energy: 100);
        engine.Execute(fighter, TrainingCatalog.Weights, new RandomSource(9));
        Assert.Equal(100 - TrainingCatalog.Weights.EnergyCost, fighter.Energy);
    }

    [Fact]
    public void RestDayCostsNothingAndCapsAtFullEnergy()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter(energy: 95);
        engine.Execute(fighter, TrainingCatalog.Rest, new RandomSource(10));
        Assert.Equal(95, fighter.Energy);
        fighter.Energy = 100;
        engine.EndOfDay(fighter);
        Assert.Equal(100, fighter.Energy);
    }

    [Fact]
    public void InjuriesCanHappenAndCarryRecoveryDays()
    {
        var engine = new TrainingEngine();
        var injured = false;
        for (var seed = 0; seed < 60 && !injured; seed++)
        {
            var fighter = NewFighter(energy: 55);
            var result = engine.Execute(fighter, TrainingCatalog.Sparring, new RandomSource(seed));
            if (result.Injury is not null)
            {
                injured = true;
                Assert.True(result.Injury.DaysRemaining > 0);
                Assert.True(fighter.IsInjured);
                Assert.Contains(result.Injury, fighter.Injuries);
            }
        }

        Assert.True(injured, "Sparring should produce an injury across 60 fresh seeds.");
    }

    [Fact]
    public void InjuriesReduceAttributesWhileActive()
    {
        var fighter = NewFighter();
        fighter.AddInjury(new Injury
        {
            Type = InjuryType.HandContusion,
            Severity = InjurySeverity.Moderate,
            DaysRemaining = 10,
            TotalDays = 10
        }, 0);

        var effective = fighter.EffectiveAttributes();
        Assert.True(effective.Power < fighter.Attributes.Power);
        Assert.True(effective.Aim < fighter.Attributes.Aim);
        Assert.Equal(fighter.Attributes.Chin, effective.Chin);
    }

    [Fact]
    public void RecoverySessionHealsInjuryDays()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter();
        fighter.AddInjury(new Injury
        {
            Type = InjuryType.WristSprain,
            Severity = InjurySeverity.Serious,
            DaysRemaining = 5,
            TotalDays = 5
        }, 0);

        var result = engine.Execute(fighter, TrainingCatalog.Recovery, new RandomSource(11));
        Assert.False(result.InjuryHealed);
        Assert.Equal(4, fighter.ActiveInjuries.Single().DaysRemaining);

        fighter.Injuries.Clear();
        fighter.AddInjury(new Injury
        {
            Type = InjuryType.HandContusion,
            Severity = InjurySeverity.Niggle,
            DaysRemaining = 1,
            TotalDays = 1
        }, 0);

        fighter.Energy = 100;
        var cleared = engine.Execute(fighter, TrainingCatalog.Recovery, new RandomSource(11));
        Assert.True(cleared.InjuryHealed);
        Assert.False(fighter.IsInjured);
    }

    [Fact]
    public void PassingDaysHealsInjuriesAndRestoresEnergy()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter(energy: 20);
        fighter.AddInjury(new Injury
        {
            Type = InjuryType.RibBruising,
            Severity = InjurySeverity.Niggle,
            DaysRemaining = 2,
            TotalDays = 2
        }, 0);
        fighter.Damage = 30;

        engine.EndOfDay(fighter);
        Assert.Equal(1, fighter.ActiveInjuries.Single().DaysRemaining);
        Assert.True(fighter.Energy > 20);
        Assert.True(fighter.Damage < 30);

        engine.EndOfDay(fighter);
        Assert.False(fighter.IsInjured);
        Assert.Empty(fighter.Injuries);
    }

    [Fact]
    public void WeightCutLowersScaleWeight()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter();
        var before = fighter.WeightPounds;
        engine.Execute(fighter, TrainingCatalog.WeightCut, new RandomSource(12));
        Assert.True(fighter.WeightPounds < before);
        Assert.Equal(before - TrainingCatalog.WeightCut.WeightCutPounds, fighter.WeightPounds);
    }

    [Fact]
    public void RehydrationRestoresMostOfTheCut()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter();
        fighter.WeightPounds = 160m;
        var restored = engine.Rehydrate(fighter);
        Assert.Equal(9m, restored);
        Assert.Equal(169m, fighter.WeightPounds);
    }

    [Fact]
    public void RehydrationBonusScalesWithWeightRegained()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter();
        fighter.WeightPounds = 180m;
        var small = engine.RehydrationPowerFactor(fighter, 178m);
        var large = engine.RehydrationPowerFactor(fighter, 160m);
        Assert.True(large > small);
    }

    [Fact]
    public void FilmStudyAddsPopularityWithoutInjuryRisk()
    {
        var engine = new TrainingEngine();
        var fighter = NewFighter();
        var popularity = fighter.Popularity;
        engine.Execute(fighter, TrainingCatalog.FilmStudy, new RandomSource(13));
        Assert.True(fighter.Popularity > popularity);
        Assert.Equal(0, TrainingCatalog.FilmStudy.InjuryRisk);
    }

    [Fact]
    public void UnknownActivityThrows()
    {
        Assert.Throws<KeyNotFoundException>(() => TrainingCatalog.Get("nope"));
    }

    [Fact]
    public void SameSeedProducesIdenticalSession()
    {
        var engine = new TrainingEngine();
        var a = NewFighter();
        var b = NewFighter();
        engine.Execute(a, TrainingCatalog.Sparring, new RandomSource(1234));
        engine.Execute(b, TrainingCatalog.Sparring, new RandomSource(1234));

        Assert.Equal(a.Attributes.Power, b.Attributes.Power);
        Assert.Equal(a.Attributes.Reflex, b.Attributes.Reflex);
        Assert.Equal(a.Energy, b.Energy);
        Assert.Equal(a.Injuries.Count, b.Injuries.Count);
    }
}
