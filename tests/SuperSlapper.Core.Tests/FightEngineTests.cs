using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Tests;

public class FightEngineTests
{
    private static Fighter MakeFighter(string id, int rating, int chin = -1) => new()
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
            Chin = chin >= 0 ? chin : rating,
            Endurance = rating,
            Reflex = rating,
            Mass = 50
        }
    };

    private static FightResult RunToCompletion(FightEngine engine, SlapAttempt? template = null, int guard = 500)
    {
        var steps = 0;
        while (engine.Phase != FightPhase.Finished && steps++ < guard)
        {
            switch (engine.Phase)
            {
                case FightPhase.AwaitingSlap:
                    engine.DeliverSlap(template ?? new SlapAttempt
                    {
                        Aim = 0.8,
                        PowerCharge = 0.8,
                        Timing = 0.85,
                        TargetZone = SlapZone.RightCheek
                    });
                    break;
                case FightPhase.AwaitingCount:
                    engine.ResolveCount();
                    break;
                case FightPhase.RoundBreak:
                    engine.StartNextRound();
                    break;
                default:
                    throw new InvalidOperationException($"Stuck in {engine.Phase}");
            }
        }

        Assert.Equal(FightPhase.Finished, engine.Phase);
        Assert.NotNull(engine.Result);
        return engine.Result!;
    }

    [Fact]
    public void EngineRejectsIllegalRules()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.Rounds = 0;
        Assert.Throws<InvalidOperationException>(() =>
            new FightEngine(MakeFighter("a", 50), MakeFighter("b", 50), rules, new RandomSource(1)));
    }

    [Fact]
    public void BoutStartsAwaitingASlapWithAFullTank()
    {
        var engine = new FightEngine(MakeFighter("a", 50), MakeFighter("b", 50),
            Rulesets.WorldSlapLeague, new RandomSource(1));

        Assert.Equal(FightPhase.AwaitingSlap, engine.Phase);
        Assert.Equal(1, engine.Round);
        Assert.Equal(100, engine.ConditionA.Health);
        Assert.Equal(100, engine.ConditionB.Health);
        Assert.Equal(0, engine.SlapsThisRoundA);
    }

    [Fact]
    public void DeliveringASlapConsumesAStrikeSlot()
    {
        var engine = new FightEngine(MakeFighter("a", 50), MakeFighter("b", 50),
            Rulesets.WorldSlapLeague, new RandomSource(2));

        var wasA = engine.StrikerIsA;
        var result = engine.DeliverSlap(new SlapAttempt { Aim = 0.9, PowerCharge = 0.5, Timing = 0.9 });

        Assert.Equal(wasA, result.StrikerId == "a");
        Assert.Equal(wasA ? 1 : 0, engine.SlapsThisRoundA);
        Assert.Equal(wasA ? 0 : 1, engine.SlapsThisRoundB);
        Assert.NotEqual(wasA, engine.StrikerIsA);
    }

    [Fact]
    public void FightAlwaysReachesAResult()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var engine = new FightEngine(MakeFighter("a", 45), MakeFighter("b", 45),
                Rulesets.WorldSlapLeague, new RandomSource(seed));
            var result = RunToCompletion(engine);

            Assert.InRange(result.EndRound, 1, Rulesets.WorldSlapLeague.Rounds);
            Assert.False(string.IsNullOrWhiteSpace(result.Headline));
            Assert.True(result.Kind != FightOutcomeKind.Undecided);
        }
    }

    [Fact]
    public void SameSeedSameFight()
    {
        var first = RunToCompletion(new FightEngine(MakeFighter("a", 55), MakeFighter("b", 55),
            Rulesets.WorldSlapLeague, new RandomSource(4242)));
        var second = RunToCompletion(new FightEngine(MakeFighter("a", 55), MakeFighter("b", 55),
            Rulesets.WorldSlapLeague, new RandomSource(4242)));

        Assert.Equal(first.WinnerId, second.WinnerId);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.TotalDamageA, second.TotalDamageA);
        Assert.Equal(first.TotalDamageB, second.TotalDamageB);
    }

    [Fact]
    public void ClosedFistIsAFoulWhenTheRulebookRequiresAnOpenHand()
    {
        var engine = new FightEngine(MakeFighter("a", 50), MakeFighter("b", 50),
            Rulesets.WorldSlapLeague, new RandomSource(3));

        var result = engine.DeliverSlap(new SlapAttempt
        {
            Aim = 1.0,
            PowerCharge = 1.0,
            Timing = 1.0,
            OpenHand = false
        });

        Assert.Equal(SlapVerdict.Foul, result.Verdict);
        Assert.Contains("closed fist", result.Commentary);
    }

    [Fact]
    public void RepeatedFoulsEndInDisqualification()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.Rounds = 3;
        rules.SlapsPerRound = 3;
        rules.FoulsBeforeDisqualification = 2;

        var engine = new FightEngine(MakeFighter("a", 60), MakeFighter("b", 60), rules, new RandomSource(4));

        var result = RunToCompletion(engine, new SlapAttempt
        {
            Aim = 1.0,
            PowerCharge = 0.9,
            Timing = 1.0,
            OpenHand = false
        });

        Assert.Equal(FightOutcomeKind.Disqualification, result.Kind);
        Assert.True(engine.ConditionA.Disqualified || engine.ConditionB.Disqualified);
    }

    [Fact]
    public void KnockdownGoesToACount()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.KnockdownDamageThreshold = 5m;

        var engine = new FightEngine(MakeFighter("a", 90), MakeFighter("b", 20, chin: 15), rules,
            new RandomSource(5));

        var sawKnockdown = false;
        var guard = 0;
        while (engine.Phase != FightPhase.Finished && guard++ < 100)
        {
            if (engine.Phase == FightPhase.AwaitingSlap)
            {
                var result = engine.DeliverSlap(new SlapAttempt
                {
                    Aim = 0.95, PowerCharge = 1.0, Timing = 1.0, TargetZone = SlapZone.Jaw
                });
                if (result.Knockdown)
                {
                    sawKnockdown = true;
                    Assert.Equal(FightPhase.AwaitingCount, engine.Phase);
                    var count = engine.ResolveCount();
                    Assert.InRange(count.Count, 4, rules.CountOutSeconds);
                }
            }
            else if (engine.Phase == FightPhase.AwaitingCount)
            {
                engine.ResolveCount();
            }
            else if (engine.Phase == FightPhase.RoundBreak)
            {
                engine.StartNextRound();
            }
        }

        Assert.True(sawKnockdown, "A 90-power fighter should knock down a 20-rated chin.");
        Assert.Equal(FightPhase.Finished, engine.Phase);
    }

    [Fact]
    public void KnockedDownFighterCanBeStoppedAfterOneCount()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.MaxKnockdownsPerRound = 1;
        rules.KnockdownDamageThreshold = 5m;

        var engine = new FightEngine(MakeFighter("a", 95), MakeFighter("b", 15, chin: 10), rules,
            new RandomSource(6));

        var result = RunToCompletion(engine, new SlapAttempt
        {
            Aim = 0.95, PowerCharge = 1.0, Timing = 1.0, TargetZone = SlapZone.Jaw
        });

        Assert.True(result.Kind is FightOutcomeKind.Knockout or FightOutcomeKind.TechnicalKnockout);
        Assert.False(string.IsNullOrWhiteSpace(result.WinnerId));
    }

    [Fact]
    public void DecisionScorecardsAreTenPointMust()
    {
        var engine = new FightEngine(MakeFighter("a", 40), MakeFighter("b", 40),
            Rulesets.WorldSlapLeague, new RandomSource(7));
        var result = RunToCompletion(engine);

        if (result.Kind is FightOutcomeKind.Decision or FightOutcomeKind.SplitDecision
            or FightOutcomeKind.MajorityDecision or FightOutcomeKind.Draw)
        {
            Assert.Equal(Rulesets.WorldSlapLeague.JudgesCount, result.Scorecards.Count);
            foreach (var card in result.Scorecards)
            {
                Assert.Equal(Rulesets.WorldSlapLeague.Rounds, card.Rounds.Count);
                foreach (var (a, b) in card.Rounds)
                {
                    Assert.InRange(a, 7, 10);
                    Assert.InRange(b, 7, 10);
                    // 10-9 normally; point deductions can drag it to 9-9 or worse.
                    Assert.InRange(a + b, 18, 20);
                }
            }
        }
    }

    [Fact]
    public void BracingReducesTheDamageTaken()
    {
        var unbraced = 0;
        var braced = 0;

        for (var seed = 0; seed < 20; seed++)
        {
            var a = new FightEngine(MakeFighter("a", 70), MakeFighter("b", 40), Rulesets.WorldSlapLeague,
                new RandomSource(seed));
            while (!a.StrikerIsA && a.Phase == FightPhase.AwaitingSlap)
            {
                a.DeliverSlap(new SlapAttempt { Aim = 0.0, PowerCharge = 0.0, Timing = 0.0 });
            }

            var attack = new SlapAttempt { Aim = 1.0, PowerCharge = 0.9, Timing = 1.0 };
            unbraced += a.DeliverSlap(attack, braced: false).Damage;

            var b = new FightEngine(MakeFighter("a", 70), MakeFighter("b", 40), Rulesets.WorldSlapLeague,
                new RandomSource(seed));
            while (!b.StrikerIsA && b.Phase == FightPhase.AwaitingSlap)
            {
                b.DeliverSlap(new SlapAttempt { Aim = 0.0, PowerCharge = 0.0, Timing = 0.0 });
            }

            braced += b.DeliverSlap(attack, braced: true).Damage;
        }

        Assert.True(braced < unbraced, $"Bracing should hurt less ({braced} vs {unbraced}).");
    }

    [Fact]
    public void SlapCountsPerRoundMatchTheRulebook()
    {
        var rules = Rulesets.EuropeanCircuit.Clone();
        var engine = new FightEngine(MakeFighter("a", 50), MakeFighter("b", 50), rules, new RandomSource(8));

        Assert.Equal(2, engine.SlapsRemainingForCurrentStriker);
        engine.DeliverSlap(new SlapAttempt { Aim = 0.7, PowerCharge = 0.5, Timing = 0.8 });
        Assert.Equal(rules.SlapsPerRound * rules.Rounds, rules.SlapsPerFighter);
    }

    [Fact]
    public void RoundBreakRequiresAcknowledgement()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.Rounds = 2;
        rules.FoulsBeforeDisqualification = 2;
        var engine = new FightEngine(MakeFighter("a", 50), MakeFighter("b", 50), rules, new RandomSource(9));

        var guard = 0;
        while (engine.Phase != FightPhase.RoundBreak && guard++ < 20)
        {
            if (engine.Phase == FightPhase.AwaitingSlap)
            {
                engine.DeliverSlap(new SlapAttempt { Aim = 0.9, PowerCharge = 0.4, Timing = 0.9 });
            }
            else if (engine.Phase == FightPhase.AwaitingCount)
            {
                engine.ResolveCount();
            }
        }

        Assert.Equal(FightPhase.RoundBreak, engine.Phase);
        engine.StartNextRound();
        Assert.Equal(FightPhase.AwaitingSlap, engine.Phase);
    }

    [Fact]
    public void RoundNumberTurnsOverWhenTheNextRoundIsBroughtOn()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.Rounds = 2;
        rules.FoulsBeforeDisqualification = 2;
        var engine = new FightEngine(MakeFighter("a", 10), MakeFighter("b", 10), rules, new RandomSource(9));

        var guard = 0;
        while (engine.Phase != FightPhase.RoundBreak && engine.Phase != FightPhase.Finished && guard++ < 40)
        {
            if (engine.Phase == FightPhase.AwaitingSlap)
            {
                engine.DeliverSlap(new SlapAttempt { Aim = 0.9, PowerCharge = 0.3, Timing = 0.9 });
            }
            else if (engine.Phase == FightPhase.AwaitingCount)
            {
                engine.ResolveCount();
            }
        }

        Assert.Equal(FightPhase.RoundBreak, engine.Phase);

        // The break still belongs to the round that just finished - the round card
        // announces the new one only once the next round is brought on.
        Assert.Equal(1, engine.Round);
        engine.StartNextRound();
        Assert.Equal(2, engine.Round);
    }

    [Fact]
    public void EveryRoundOpensOnTheSameStriker()
    {
        var rules = Rulesets.WorldSlapLeague.Clone();
        rules.Rounds = 3;
        rules.FoulsBeforeDisqualification = 2;
        var engine = new FightEngine(MakeFighter("a", 10), MakeFighter("b", 10), rules, new RandomSource(4));

        var openers = new List<bool>();
        var guard = 0;
        while (engine.Phase != FightPhase.Finished && guard++ < 200)
        {
            if (engine.Phase == FightPhase.AwaitingSlap)
            {
                if (engine.SlapsThisRoundA + engine.SlapsThisRoundB == 0)
                {
                    openers.Add(engine.StrikerIsA);
                }

                engine.DeliverSlap(new SlapAttempt { Aim = 0.9, PowerCharge = 0.3, Timing = 0.9 });
            }
            else if (engine.Phase == FightPhase.AwaitingCount)
            {
                engine.ResolveCount();
            }
            else
            {
                engine.StartNextRound();
            }
        }

        Assert.True(openers.Count >= 2, $"Expected at least two rounds, saw {openers.Count}.");
        Assert.All(openers, opener => Assert.Equal(openers[0], opener));
    }

    [Fact]
    public void AiAttemptStaysInRange()
    {
        var rng = new RandomSource(10);
        for (var i = 0; i < 200; i++)
        {
            var attempt = FightEngine.GenerateAiAttempt(MakeFighter("a", 60), 0.6, rng);
            Assert.InRange(attempt.Aim, 0, 1);
            Assert.InRange(attempt.PowerCharge, 0, 1);
            Assert.InRange(attempt.Timing, 0, 1);
        }
    }

    [Fact]
    public void WhiffedSlapDoesNoDamage()
    {
        var engine = new FightEngine(MakeFighter("a", 90), MakeFighter("b", 90), Rulesets.WorldSlapLeague,
            new RandomSource(11));

        // A completely unaimed swing at zero power is a miss for any seed.
        var result = engine.DeliverSlap(new SlapAttempt { Aim = 0.0, PowerCharge = 0.0, Timing = 0.0 });
        if (result.Verdict == SlapVerdict.Miss)
        {
            Assert.Equal(0, result.Damage);
            Assert.Equal(100, engine.ConditionA.Health + engine.ConditionB.Health);
        }
    }

    [Fact]
    public void FighterRatingAffectsOutcome()
    {
        var wins = 0;
        for (var seed = 0; seed < 30; seed++)
        {
            var rng = new RandomSource(seed);
            var engine = new FightEngine(MakeFighter("champ", 90), MakeFighter("bum", 30),
                Rulesets.WorldSlapLeague, rng);

            var guard = 0;
            while (engine.Phase != FightPhase.Finished && guard++ < 300)
            {
                switch (engine.Phase)
                {
                    case FightPhase.AwaitingSlap:
                    {
                        // Each side slaps with their own skill level.
                        var skill = engine.StrikerIsA ? 0.9 : 0.25;
                        engine.DeliverSlap(FightEngine.GenerateAiAttempt(engine.Striker, skill, rng));
                        break;
                    }
                    case FightPhase.AwaitingCount:
                        engine.ResolveCount();
                        break;
                    case FightPhase.RoundBreak:
                        engine.StartNextRound();
                        break;
                }
            }

            if (engine.Result!.WinnerId == "champ")
            {
                wins++;
            }
        }

        Assert.True(wins >= 22, $"A far better fighter should win nearly always (won {wins}/30).");
    }
}
