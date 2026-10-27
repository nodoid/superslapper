using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Tests;

public class FoulTests
{
    private static Fighter MakeFighter(string id, int rating = 55) => new()
    {
        Id = id,
        FirstName = id,
        LastName = "Test",
        Division = WeightClassId.Welterweight,
        WalkAroundPounds = 170m,
        WeightPounds = 170m,
        Energy = 100,
        Attributes = new FighterAttributes
        {
            Power = rating,
            Aim = rating,
            Chin = rating,
            Endurance = rating,
            Reflex = rating,
            Mass = 50
        }
    };

    private static SlapAttempt CleanSlap() => new()
    {
        Aim = 0.95,
        PowerCharge = 0.7,
        Timing = 0.95,
        TargetZone = SlapZone.RightCheek
    };

    [Fact]
    public void StandardBoutAllowsTwoFoulsBeforeDisqualification()
    {
        var rules = Rulesets.EuropeanCircuit;
        Assert.Equal(3, rules.Rounds);
        Assert.Equal(2, rules.FoulsBeforeDisqualification);
    }

    [Fact]
    public void ChampionshipBoutAllowsThreeFoulsBeforeDisqualification()
    {
        var rules = Rulesets.WorldSlapLeague;
        Assert.Equal(5, rules.Rounds);
        Assert.Equal(3, rules.FoulsBeforeDisqualification);
    }

    [Fact]
    public void FoulLimitMustMatchTheBoutLength()
    {
        var threeRounds = Rulesets.EuropeanCircuit.Clone();
        threeRounds.FoulsBeforeDisqualification = 3;
        Assert.Contains(threeRounds.Validate(), e => e.Contains("fouls", StringComparison.OrdinalIgnoreCase));

        var fiveRounds = Rulesets.WorldSlapLeague.Clone();
        fiveRounds.FoulsBeforeDisqualification = 2;
        Assert.Contains(fiveRounds.Validate(), e => e.Contains("fouls", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FlinchingIsAFoulAgainstTheDefender()
    {
        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), Rulesets.EuropeanCircuit,
            new RandomSource(3));

        var attempt = CleanSlap();
        attempt.DefenderFlinched = true;
        var result = engine.DeliverSlap(attempt);

        var foul = Assert.Single(result.Fouls);
        Assert.Equal(FoulKind.Flinching, foul.Kind);
        Assert.Equal(engine.ConditionB.FighterId, foul.FighterId);
        Assert.Equal(1, engine.ConditionB.Fouls);
        Assert.Equal(0, engine.ConditionA.Fouls);
        Assert.Contains("flinched", result.Commentary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SteppingOutIsAFoulAndTheSlapCannotLand()
    {
        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), Rulesets.EuropeanCircuit,
            new RandomSource(4));

        var attempt = CleanSlap();
        attempt.DefenderSteppedOut = true;
        var result = engine.DeliverSlap(attempt);

        Assert.Equal(SlapVerdict.Miss, result.Verdict);
        Assert.Equal(0, result.Damage);
        Assert.Equal(FoulKind.Stepping, result.Fouls.Single().Kind);
        Assert.Equal(1, engine.ConditionB.Fouls);
    }

    [Fact]
    public void SteppingInWithTheSlapIsAFoulAgainstTheStriker()
    {
        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), Rulesets.EuropeanCircuit,
            new RandomSource(5));

        var attempt = CleanSlap();
        attempt.SteppedForward = true;
        var result = engine.DeliverSlap(attempt);

        Assert.Contains(result.Fouls, f => f.Kind == FoulKind.Stepping);
        Assert.Equal(1, engine.ConditionA.Fouls);
    }

    [Fact]
    public void ClosedFistIsAFoul()
    {
        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), Rulesets.EuropeanCircuit,
            new RandomSource(6));

        var attempt = CleanSlap();
        attempt.OpenHand = false;
        var result = engine.DeliverSlap(attempt);

        Assert.Equal(SlapVerdict.Foul, result.Verdict);
        Assert.Equal(FoulKind.ClosedFist, result.Fouls.Single().Kind);
    }

    /// <summary>Runs a bout where fighter A fouls on every one of its own slaps.</summary>
    private static FightEngine RunFoulBout(Ruleset rules, int seed, int guard = 60)
    {
        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), rules, new RandomSource(seed));
        var steps = 0;

        while (engine.Phase != FightPhase.Finished && steps++ < guard)
        {
            switch (engine.Phase)
            {
                case FightPhase.RoundBreak:
                    engine.StartNextRound();
                    break;
                case FightPhase.AwaitingCount:
                    engine.ResolveCount();
                    break;
                case FightPhase.AwaitingSlap:
                {
                    var attempt = CleanSlap();
                    if (engine.StrikerIsA)
                    {
                        attempt.OpenHand = false;
                    }

                    engine.DeliverSlap(attempt);
                    break;
                }
            }
        }

        return engine;
    }

    [Fact]
    public void TwoFoulsInAStandardBoutEndsInDisqualification()
    {
        var engine = RunFoulBout(Rulesets.EuropeanCircuit, seed: 7);

        Assert.Equal(FightPhase.Finished, engine.Phase);
        Assert.Equal(FightOutcomeKind.Disqualification, engine.Result!.Kind);
        Assert.Equal(2, engine.ConditionA.Fouls);
        Assert.True(engine.ConditionA.Disqualified);
    }

    [Fact]
    public void ChampionshipBoutSurvivesTwoFoulsButNotThree()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), rules, new RandomSource(8));

        var fouls = 0;
        var steps = 0;
        while (engine.Phase != FightPhase.Finished && steps++ < 60)
        {
            if (engine.Phase == FightPhase.RoundBreak)
            {
                engine.StartNextRound();
                continue;
            }

            if (engine.Phase == FightPhase.AwaitingCount)
            {
                engine.ResolveCount();
                continue;
            }

            var attempt = CleanSlap();
            if (engine.StrikerIsA)
            {
                attempt.OpenHand = false;
            }

            var result = engine.DeliverSlap(attempt);
            if (result.Fouls.Any(f => f.FighterId == engine.FighterA.Id))
            {
                fouls++;
                if (fouls <= 2)
                {
                    Assert.False(engine.ConditionA.Disqualified,
                        $"Two fouls must not end a five-round championship bout (foul {fouls}).");
                }
            }
        }

        Assert.Equal(FightPhase.Finished, engine.Phase);
        Assert.Equal(3, fouls);
        Assert.Equal(FightOutcomeKind.Disqualification, engine.Result!.Kind);
    }

    [Fact]
    public void EveryFoulCostsAPointOnTheCards()
    {
        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), Rulesets.EuropeanCircuit,
            new RandomSource(9));

        engine.DeliverSlap(new SlapAttempt { Aim = 1.0, PowerCharge = 0.5, Timing = 1.0, OpenHand = false });

        var striker = engine.ConditionA.Fouls > 0 ? engine.ConditionA : engine.ConditionB;
        Assert.Equal(1, striker.Fouls);
        Assert.Equal(1, striker.PointDeductions);
        Assert.Contains(FoulKind.ClosedFist, striker.FoulLog);
    }

    [Fact]
    public void FoulLogIsReportedOnTheResult()
    {
        var engine = RunFoulBout(Rulesets.EuropeanCircuit, seed: 10);

        Assert.NotNull(engine.Result);
        Assert.Equal(2, engine.Result!.FighterAFouls);
        Assert.Equal(2, engine.Result.FighterAFoulLog.Count);
        Assert.All(engine.Result.FighterAFoulLog, f => Assert.Equal(FoulKind.ClosedFist, f));
    }

    [Fact]
    public void FoulsCanBeSwitchedOffByTheRulebook()
    {
        var rules = Rulesets.EuropeanCircuit.Clone();
        rules.FlinchingIsFoul = false;
        rules.SteppingIsFoul = false;

        var engine = new FightEngine(MakeFighter("a"), MakeFighter("b"), rules, new RandomSource(11));
        var attempt = CleanSlap();
        attempt.DefenderFlinched = true;
        var result = engine.DeliverSlap(attempt);

        Assert.Empty(result.Fouls);
        Assert.Equal(0, engine.ConditionB.Fouls);
    }

    [Fact]
    public void FoulLimitMatchesRoundsForEveryShippedRulebook()
    {
        foreach (var rules in Rulesets.All)
        {
            var expected = rules.Rounds >= 4 ? 3 : 2;
            Assert.Equal(expected, rules.FoulsBeforeDisqualification);
            Assert.Empty(rules.Validate());
        }
    }
}
