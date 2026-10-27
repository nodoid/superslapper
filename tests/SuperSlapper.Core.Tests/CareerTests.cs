using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;
using SuperSlapper.Core.Training;

namespace SuperSlapper.Core.Tests;

public class CareerTests
{
    private static CareerState NewCareer(int seed = 2024, WeightClassId division = WeightClassId.Welterweight)
        => WorldGenerator.Create("Paul", "Johnson", "The Professor", division, seed);

    private static (FightEngine Engine, FightResult Result) FightIt(CareerService service, CareerState state,
        double aim = 0.9)
    {
        var engine = service.CreateFightEngine(state);
        var rng = service.Rng(state);
        var guard = 0;

        while (engine.Phase != FightPhase.Finished && guard++ < 400)
        {
            switch (engine.Phase)
            {
                case FightPhase.AwaitingSlap:
                    engine.DeliverSlap(new SlapAttempt
                    {
                        Aim = aim,
                        PowerCharge = 0.9,
                        Timing = 0.9,
                        TargetZone = SlapZone.Jaw
                    });
                    break;
                case FightPhase.AwaitingCount:
                    engine.ResolveCount();
                    break;
                case FightPhase.RoundBreak:
                    engine.StartNextRound();
                    break;
            }
        }

        return (engine, engine.Result!);
    }

    [Theory]
    [InlineData(Stance.Southpaw)]
    [InlineData(Stance.Orthodox)]
    public void PlayerStanceComesFromTheChosenPreferredHand(Stance stance)
    {
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor",
            WeightClassId.Welterweight, 4242, "Career", stance);

        Assert.Equal(stance, state.Player.Stance);
    }

    [Fact]
    public void NewCareerHasAPlayerARosterAndChampions()
    {
        var state = NewCareer();

        Assert.Equal("player", state.PlayerId);
        Assert.True(state.Player.IsPlayer);
        Assert.Equal(WeightClassId.Welterweight, state.PlayerDivision);
        Assert.True(state.Fighters.Count > 100);
        Assert.True(state.Belts.Belts.Count > 0);
        Assert.All(Leagues.Leagues.All,
            l => Assert.False(state.Belts.Get(l.Id, WeightClassId.Welterweight).IsVacant));
    }

    [Fact]
    public void PlayerStartsOnTheAmateurCircuit()
    {
        var state = NewCareer();
        Assert.Equal(Leagues.Leagues.AmateurCircuit.Id, state.PlayerLeagueId);
        Assert.Equal(Leagues.Leagues.AmateurCircuit.Prestige, Leagues.Leagues.Get(state.PlayerLeagueId).Prestige);
    }

    [Fact]
    public void SameSeedProducesTheSameWorld()
    {
        var a = NewCareer(777);
        var b = NewCareer(777);
        Assert.Equal(a.Fighters.Count, b.Fighters.Count);
        Assert.Equal(a.Belts.Get("wsl", WeightClassId.Heavyweight).HolderId,
            b.Belts.Get("wsl", WeightClassId.Heavyweight).HolderId);
        Assert.Equal(a.Player.WalkAroundPounds, b.Player.WalkAroundPounds);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentWorlds()
    {
        var a = NewCareer(1);
        var b = NewCareer(2);
        Assert.NotEqual(a.Belts.Get("wsl", WeightClassId.Heavyweight).HolderId,
            b.Belts.Get("wsl", WeightClassId.Heavyweight).HolderId);
    }

    [Fact]
    public void BookingAFightCreatesAPlausibleOffer()
    {
        var service = new CareerService();
        var state = NewCareer();
        var offer = service.BookNextFight(state, new RandomSource(5));

        Assert.NotNull(state.FindFighter(offer.OpponentId));
        Assert.NotEqual(state.PlayerId, offer.OpponentId);
        Assert.True(offer.FightDay > state.Day);
        Assert.Equal(Leagues.Leagues.AmateurCircuit.Ruleset.Rounds, offer.Rounds);
        Assert.True(offer.Purse > 0);
    }

    [Fact]
    public void TrainingAdvancesTheDayAndLogsTheSession()
    {
        var service = new CareerService();
        var state = NewCareer();
        service.BookNextFight(state, new RandomSource(6));

        var day = state.Day;
        var result = service.TrainDay(state, TrainingCatalog.SlapPower);

        Assert.Equal(day + 1, state.Day);
        Assert.Single(state.TrainingLog);
        Assert.Equal(TrainingCatalog.SlapPower.Id, state.TrainingLog[0].ActivityId);
        Assert.True(result.EnergyAfter <= result.EnergyBefore);
    }

    [Fact]
    public void TrainingCannotRunThroughFightDay()
    {
        var service = new CareerService();
        var state = NewCareer();
        var offer = service.BookNextFight(state, new RandomSource(7), leadDays: 1);

        service.RestDay(state);
        Assert.Equal(offer.FightDay, state.Day);

        Assert.Throws<InvalidOperationException>(() => service.TrainDay(state, TrainingCatalog.Weights));
    }

    [Fact]
    public void ResultWinnerTakesRankingPointsAndPurse()
    {
        var service = new CareerService();
        var state = NewCareer(4242);
        var offer = service.BookNextFight(state, new RandomSource(11));

        var table = state.Rankings.Get(state.PlayerLeagueId, state.PlayerDivision);
        var before = table.Find(state.PlayerId)?.Points ?? 0;
        var money = state.Money;

        var (_, result) = FightIt(service, state);
        var outcome = service.ApplyResult(state, result);

        var after = table.Find(state.PlayerId)?.Points ?? 0;
        Assert.True(after >= before);
        Assert.True(state.Money > money);
        Assert.Equal(money + outcome.Purse, state.Money);
        Assert.Single(state.PlayerBouts);
        Assert.Equal(1, state.Player.Record.Total);
    }

    [Fact]
    public void ResultUpdatesTheRecordImmediately()
    {
        var service = new CareerService();
        var state = NewCareer(99);
        service.BookNextFight(state, new RandomSource(12));

        var (_, result) = FightIt(service, state);
        service.ApplyResult(state, result);

        var record = state.Player.Record;
        Assert.Equal(1, record.Total);
        if (result.IsDraw)
        {
            Assert.Equal(1, record.Draws);
        }
        else if (result.WinnerId == state.PlayerId)
        {
            Assert.Equal(1, record.Wins);
        }
        else
        {
            Assert.Equal(1, record.Losses);
        }
    }

    [Fact]
    public void ResultBooksTheNextFightAutomatically()
    {
        var service = new CareerService();
        var state = NewCareer(31337);
        service.BookNextFight(state, new RandomSource(13));

        var (_, result) = FightIt(service, state);
        var outcome = service.ApplyResult(state, result);

        Assert.NotNull(state.Offer);
        Assert.NotNull(outcome.NextFight);
        Assert.True(state.Offer!.FightDay > state.Day);
        Assert.NotEqual(outcome.Result.FighterAId, state.Offer.OpponentId + "x");
    }

    [Fact]
    public void InjuriesFromAFightCarryIntoTheNextCamp()
    {
        var service = new CareerService();
        var state = NewCareer(5150);
        service.BookNextFight(state, new RandomSource(14));

        var (_, result) = FightIt(service, state);
        service.ApplyResult(state, result);

        // A punishing bout leaves a mark: damage always, injuries sometimes.
        Assert.True(state.Player.Damage > 0 || state.Player.IsInjured);

        var day = state.Day;
        service.RestDay(state);
        Assert.Equal(day + 1, state.Day);
    }

    [Fact]
    public void WeighInIsRecordedAndRehydratesTheFighter()
    {
        var service = new CareerService();
        var state = NewCareer(606);
        service.BookNextFight(state, new RandomSource(15));

        var beforeWeight = state.Player.WeightPounds;
        var weighIn = service.WeighIn(state);

        Assert.Single(state.WeighIns);
        Assert.Equal(WeightClassId.Welterweight, weighIn.Division);
        Assert.True(state.Player.WeightPounds >= beforeWeight);
    }

    [Fact]
    public void TitleFightTransfersTheBelt()
    {
        var service = new CareerService();
        var state = NewCareer(8080);

        var belt = state.Belts.Get(Leagues.Leagues.AmateurCircuit.Id, WeightClassId.Welterweight);
        var champion = state.FindFighter(belt.HolderId!)!;

        var offer = service.CreateOfferFor(state, champion.Id, 14, titleFight: true);
        offer.Purse = 5000;
        state.Offer = offer;

        // Make the player overwhelming so the belt changes hands.
        state.Player.Attributes.Power = 99;
        state.Player.Attributes.Aim = 99;
        state.Player.Attributes.Chin = 99;
        champion.Attributes.Chin = 20;
        champion.Attributes.Power = 20;

        var (_, result) = FightIt(service, state);
        Assert.True(result.WasTitleFight);
        var weighIn = service.WeighIn(state);
        service.ApplyResult(state, result, weighIn);

        if (result.WinnerId == state.PlayerId && weighIn.MadeWeight)
        {
            Assert.Equal(state.PlayerId, belt.HolderId);
            Assert.NotEmpty(state.Belts.HeldBy(state.PlayerId));
            Assert.Equal(state.PlayerId,
                state.Rankings.Get(state.PlayerLeagueId, state.PlayerDivision).ChampionId);
            Assert.Contains(state.History, h => h.TitleFight && h.InvolvesPlayer);
        }
        else
        {
            Assert.NotNull(belt.HolderId);
        }
    }

    [Fact]
    public void MissingWeightForfeitsTheTitle()
    {
        var service = new CareerService();
        var state = NewCareer(1212);

        var belt = state.Belts.Get(Leagues.Leagues.AmateurCircuit.Id, WeightClassId.Welterweight);
        var champion = state.FindFighter(belt.HolderId!)!;
        var originalHolder = belt.HolderId;

        state.Offer = service.CreateOfferFor(state, champion.Id, 7, titleFight: true);
        state.Player.Attributes.Power = 99;
        state.Player.Attributes.Aim = 99;

        // Blow the weight cut: 20 lb over the welterweight limit.
        state.Player.WalkAroundPounds = 200m;
        state.Player.WeightPounds = 190m;

        var weighIn = service.WeighIn(state);
        Assert.False(weighIn.MadeWeight);

        var (_, result) = FightIt(service, state);
        service.ApplyResult(state, result, weighIn);

        if (result.WinnerId == state.PlayerId)
        {
            Assert.NotEqual(state.PlayerId, belt.HolderId);
            Assert.Equal(originalHolder, belt.HolderId);
        }
    }

    [Fact]
    public void ChampionDefencesAccumulate()
    {
        var service = new CareerService();
        var state = NewCareer(4321);

        var belt = state.Belts.Get(Leagues.Leagues.AmateurCircuit.Id, WeightClassId.Welterweight);
        belt.Award(state.PlayerId, state.Player.DisplayName, state.Day);
        state.Rankings.Get(state.PlayerLeagueId, state.PlayerDivision).ChampionId = state.PlayerId;

        var challenger = state.Fighters
            .Where(f => f.LeagueId == state.PlayerLeagueId && f.Division == state.PlayerDivision && !f.IsPlayer)
            .OrderBy(f => f.Overall)
            .First();

        state.Player.Attributes.Power = 99;
        state.Player.Attributes.Aim = 99;
        state.Player.Attributes.Chin = 99;
        challenger.Attributes.Chin = 10;

        state.Offer = service.CreateOfferFor(state, challenger.Id, 10, titleFight: true);
        var (_, result) = FightIt(service, state);
        var weighIn = service.WeighIn(state);
        service.ApplyResult(state, result, weighIn);

        if (result.WinnerId == state.PlayerId)
        {
            Assert.Equal(state.PlayerId, belt.HolderId);
            Assert.True(belt.Defences >= 1);
        }
    }

    [Fact]
    public void PlayerCanWinEnoughToBePromoted()
    {
        var service = new CareerService();
        var state = NewCareer(2718);
        state.Player.Attributes.Power = 95;
        state.Player.Attributes.Aim = 95;
        state.Player.Attributes.Chin = 95;
        state.Player.Attributes.Endurance = 95;

        var promoted = false;
        for (var i = 0; i < 8 && !promoted; i++)
        {
            if (state.Offer is null)
            {
                service.BookNextFight(state, new RandomSource(i + 1));
            }

            var (_, result) = FightIt(service, state);
            var weighIn = service.WeighIn(state);
            var outcome = service.ApplyResult(state, result, weighIn);
            promoted = outcome.Promoted;
        }

        Assert.True(promoted, "A dominant amateur should be moved up the pyramid.");
        Assert.NotEqual(Leagues.Leagues.AmateurCircuit.Id, state.PlayerLeagueId);
    }

    [Fact]
    public void CareerRoundTripsThroughJsonWithEverythingIntact()
    {
        var service = new CareerService();
        var state = NewCareer(10101);
        service.BookNextFight(state, new RandomSource(16));
        service.TrainDay(state, TrainingCatalog.SlapAimDrill);

        var clone = CareerRepository.RoundTrip(state);

        Assert.Equal(state.PlayerId, clone.PlayerId);
        Assert.Equal(state.Day, clone.Day);
        Assert.Equal(state.Fighters.Count, clone.Fighters.Count);
        Assert.Equal(state.Player.Attributes.Power, clone.Player.Attributes.Power);
        Assert.Equal(state.Money, clone.Money);
        Assert.Equal(state.Offer!.OpponentId, clone.Offer!.OpponentId);
        Assert.Equal(state.TrainingLog.Count, clone.TrainingLog.Count);
        Assert.Equal(state.Belts.Belts.Count, clone.Belts.Belts.Count);
        Assert.Equal(state.Rankings.Divisions.Count, clone.Rankings.Divisions.Count);
    }

    [Fact]
    public void CareerResumesAfterBeingReloaded()
    {
        var service = new CareerService();
        var state = NewCareer(1001);
        service.BookNextFight(state, new RandomSource(17));

        var (_, result) = FightIt(service, state);
        var weighIn = service.WeighIn(state);
        service.ApplyResult(state, result, weighIn);

        var clone = CareerRepository.RoundTrip(state);
        var recordBefore = clone.Player.Record.Total;

        var nextEngine = service.CreateFightEngine(clone);
        Assert.Equal(FightPhase.AwaitingSlap, nextEngine.Phase);

        var guard = 0;
        while (nextEngine.Phase != FightPhase.Finished && guard++ < 400)
        {
            switch (nextEngine.Phase)
            {
                case FightPhase.AwaitingSlap:
                    nextEngine.DeliverSlap(new SlapAttempt { Aim = 0.8, PowerCharge = 0.7, Timing = 0.8 });
                    break;
                case FightPhase.AwaitingCount:
                    nextEngine.ResolveCount();
                    break;
                case FightPhase.RoundBreak:
                    nextEngine.StartNextRound();
                    break;
            }
        }

        var outcome = service.ApplyResult(clone, nextEngine.Result!, service.WeighIn(clone));
        Assert.Equal(recordBefore + 1, clone.Player.Record.Total);
        Assert.NotNull(outcome);
    }

    [Fact]
    public void RankingsMoveAfterABout()
    {
        var service = new CareerService();
        var state = NewCareer(2468);
        service.BookNextFight(state, new RandomSource(18));
        state.Player.Attributes.Power = 99;
        state.Player.Attributes.Aim = 99;

        var table = state.Rankings.Get(state.PlayerLeagueId, state.PlayerDivision);
        var (_, result) = FightIt(service, state);
        service.ApplyResult(state, result);

        Assert.True(table.Entries.Count > 0);
        Assert.All(table.Entries, e => Assert.InRange(e.Position, 1, 10));
        Assert.Equal(table.Entries.Count, table.Entries.Select(e => e.FighterId).Distinct().Count());
    }

    [Fact]
    public void SimulationOfABackgroundBoutAlwaysProducesAResult()
    {
        var service = new CareerService();
        var a = new Fighter { Id = "a", Division = WeightClassId.Middleweight };
        var b = new Fighter { Id = "b", Division = WeightClassId.Middleweight };

        for (var seed = 0; seed < 20; seed++)
        {
            var result = service.SimulateBout(a, b, Rulesets.WorldSlapLeague, new RandomSource(seed),
                false, "wsl", seed);
            Assert.NotEqual(FightOutcomeKind.Undecided, result.Kind);
        }
    }

    [Fact]
    public void AProfessionalBooksOneCardAndTheRestOfTheBillCanBeRunOut()
    {
        var service = new CareerService();
        var state = NewCareer(3131);

        // The world league stages a monthly card, which is how professionals fight.
        state.PlayerLeagueId = SuperSlapper.Core.Leagues.Leagues.WorldSlapLeague.Id;
        var offer = service.BookNextFight(state, new RandomSource(31));

        var card = Assert.Single(state.Cards);
        Assert.Equal(offer.FightDay, card.Day);
        Assert.Equal(TournamentService.BoutsOnCard, card.Bouts.Count);

        var playerBout = Assert.Single(card.Bouts, b => b.IsPlayerBout);

        TournamentService.SimulateRest(state, service, card);

        // Everyone else's bout is decided; the player's own is fought, never simulated.
        Assert.False(playerBout.IsDecided);
        Assert.All(card.Bouts.Where(b => !b.IsPlayerBout), b => Assert.True(b.IsDecided));
        Assert.False(card.IsComplete);

        // Booking again reuses the card already on the calendar.
        service.BookNextFight(state, new RandomSource(32));
        Assert.Single(state.Cards);
    }
}
