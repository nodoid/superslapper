using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Rankings;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Tests;

public class RankingTests
{
    private static RankingEntry Entry(string id, int points, int position = 0) => new()
    {
        FighterId = id,
        FighterName = id,
        Points = points,
        Position = position,
        PreviousPosition = position
    };

    [Fact]
    public void KnockoutWinScoresMoreThanADecision()
    {
        var league = Leagues.Leagues.WorldSlapLeague;
        var ko = RankingCalculator.Award(league, FightOutcomeKind.Knockout, true, false, false, false, 1);
        var decision = RankingCalculator.Award(league, FightOutcomeKind.Decision, true, false, false, false, 1);
        var loss = RankingCalculator.Award(league, FightOutcomeKind.Decision, false, false, false, false, 0);
        var draw = RankingCalculator.Award(league, FightOutcomeKind.Draw, false, true, false, false, 0);

        Assert.True(ko > decision);
        Assert.True(decision > loss);
        Assert.True(draw > loss);
    }

    [Fact]
    public void PrestigeMultiplierScalesTheAward()
    {
        var award = RankingCalculator.Award(Leagues.Leagues.WorldSlapLeague,
            FightOutcomeKind.Decision, true, false, false, false, 1);
        var amateur = RankingCalculator.Award(Leagues.Leagues.AmateurCircuit,
            FightOutcomeKind.Decision, true, false, false, false, 1);
        Assert.True(award > amateur);
    }

    [Fact]
    public void TitleWinsPayABonusAndDefencesPayMore()
    {
        var league = Leagues.Leagues.WorldSlapLeague;
        var normal = RankingCalculator.Award(league, FightOutcomeKind.Decision, true, false, false, false, 1);
        var title = RankingCalculator.Award(league, FightOutcomeKind.Decision, true, false, true, false, 1);
        var defence = RankingCalculator.Award(league, FightOutcomeKind.Decision, true, false, true, true, 1);
        Assert.True(title > normal);
        Assert.True(defence > normal);
    }

    [Fact]
    public void StreakBonusIsCapped()
    {
        var league = Leagues.Leagues.WorldSlapLeague;
        var seven = RankingCalculator.Award(league, FightOutcomeKind.Decision, true, false, false, false, 7);
        var fifty = RankingCalculator.Award(league, FightOutcomeKind.Decision, true, false, false, false, 50);
        var five = RankingCalculator.Award(league, FightOutcomeKind.Decision, true, false, false, false, 5);

        Assert.Equal(seven, fifty);
        Assert.True(five < fifty, "A five-fight streak should not yet be at the cap.");
    }

    [Fact]
    public void RepublishOrdersByPointsAndNumbersPositions()
    {
        var book = new RankingBook();
        var table = book.Get("wsl", WeightClassId.Welterweight);
        table.Entries.Add(Entry("b", 50));
        table.Entries.Add(Entry("a", 90));
        table.Entries.Add(Entry("c", 70));

        book.Republish("wsl", WeightClassId.Welterweight, 10);

        Assert.Equal(new[] { "a", "c", "b" }, table.Entries.Select(e => e.FighterId));
        Assert.Equal(new[] { 1, 2, 3 }, table.Entries.Select(e => e.Position));
    }

    [Fact]
    public void RepublishTrimsToTheRankedContenderCount()
    {
        var book = new RankingBook();
        var table = book.Get("wsl", WeightClassId.Middleweight);
        for (var i = 0; i < 20; i++)
        {
            table.Entries.Add(Entry($"f{i}", i * 5));
        }

        book.Republish("wsl", WeightClassId.Middleweight, 10);
        Assert.Equal(10, table.Entries.Count);
        Assert.Equal("f19", table.Entries[0].FighterId);
    }

    [Fact]
    public void MovementArrowsDescribeTheChange()
    {
        var up = new RankingEntry { Position = 2, PreviousPosition = 5 };
        var down = new RankingEntry { Position = 6, PreviousPosition = 3 };
        var same = new RankingEntry { Position = 4, PreviousPosition = 4 };
        var fresh = new RankingEntry { Position = 9, PreviousPosition = 0 };

        Assert.Equal(RankMovement.Up, up.Movement);
        Assert.Equal("▲3", up.MovementText);
        Assert.Equal(RankMovement.Down, down.Movement);
        Assert.Equal("▼3", down.MovementText);
        Assert.Equal(RankMovement.Same, same.Movement);
        Assert.Equal("–", same.MovementText);
        Assert.Equal(RankMovement.New, fresh.Movement);
        Assert.Equal("NEW", fresh.MovementText);
    }

    [Fact]
    public void ChampionIsRatedAheadOfTheList()
    {
        var table = new DivisionRanking { ChampionId = "champ" };
        table.Entries.Add(Entry("champ", 10, 1));
        table.Entries.Add(Entry("other", 20, 2));

        Assert.Equal(0, table.PositionOf("champ"));
        Assert.Equal(2, table.PositionOf("other"));
        Assert.Equal(int.MaxValue, table.PositionOf("unranked"));
    }

    [Fact]
    public void SnapshotPreservesTheOldPositions()
    {
        var table = new DivisionRanking();
        table.Entries.Add(Entry("a", 10, 1));
        table.SnapshotForNextPeriod();

        table.Entries[0].Position = 3;
        Assert.Equal(1, table.Entries[0].PreviousPosition);
        Assert.Equal(RankMovement.Down, table.Entries[0].Movement);
    }

    [Fact]
    public void TiesAreBrokenByStreakThenName()
    {
        var book = new RankingBook();
        var table = book.Get("wsl", WeightClassId.Flyweight);
        table.Entries.Add(new RankingEntry { FighterId = "1", FighterName = "Zed", Points = 50, Streak = 1 });
        table.Entries.Add(new RankingEntry { FighterId = "2", FighterName = "Abe", Points = 50, Streak = 1 });
        table.Entries.Add(new RankingEntry { FighterId = "3", FighterName = "Mia", Points = 50, Streak = 4 });

        book.Republish("wsl", WeightClassId.Flyweight, 10);
        Assert.Equal("3", table.Entries[0].FighterId);
        Assert.Equal("2", table.Entries[1].FighterId);
    }
}
