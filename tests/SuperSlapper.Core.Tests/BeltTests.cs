using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;

namespace SuperSlapper.Core.Tests;

public class BeltTests
{
    [Fact]
    public void NewBeltIsVacant()
    {
        var book = new BeltBook();
        var belt = book.Get("wsl", WeightClassId.Welterweight);
        Assert.True(belt.IsVacant);
        Assert.Null(belt.HolderId);
        Assert.Empty(belt.Lineage);
    }

    [Fact]
    public void AwardingCreatesAReign()
    {
        var belt = new BeltBook().Get("wsl", WeightClassId.Welterweight);
        belt.Award("f1", "Rico Marchetti", 0);

        Assert.Equal("f1", belt.HolderId);
        Assert.Single(belt.Lineage);
        Assert.True(belt.CurrentReign!.IsActive);
        Assert.Equal(0, belt.Defences);
    }

    [Fact]
    public void DefencesAccumulate()
    {
        var belt = new BeltBook().Get("wsl", WeightClassId.Middleweight);
        belt.Award("f1", "Rico", 0);
        belt.RecordDefence();
        belt.RecordDefence();
        Assert.Equal(2, belt.Defences);
        Assert.Equal(2, belt.CurrentReign!.Defences);
    }

    [Fact]
    public void NewChampionClosesThePreviousReign()
    {
        var belt = new BeltBook().Get("wsl", WeightClassId.Heavyweight);
        belt.Award("f1", "Old Champ", 0);
        belt.RecordDefence();
        belt.Award("f2", "New Champ", 100);

        Assert.Equal(2, belt.Lineage.Count);
        Assert.Equal("f2", belt.HolderId);
        Assert.False(belt.Lineage[0].IsActive);
        Assert.Equal(100, belt.Lineage[0].LostDay);
        Assert.Equal(100, belt.Lineage[0].LengthDays(200));
        Assert.True(belt.Lineage[1].IsActive);
    }

    [Fact]
    public void VacatedBeltIsHeldByNobody()
    {
        var belt = new BeltBook().Get("wsl", WeightClassId.Lightweight);
        belt.Award("f1", "Rico", 0);
        belt.Vacate(50);

        Assert.True(belt.IsVacant);
        Assert.Null(belt.HolderId);
        Assert.True(belt.Lineage[0].Vacated);
        Assert.False(belt.Lineage[0].IsActive);
    }

    [Fact]
    public void ReAwardingAVacantBeltStartsAFreshReign()
    {
        var belt = new BeltBook().Get("wsl", WeightClassId.Flyweight);
        belt.Award("f1", "A", 0);
        belt.Vacate(10);
        belt.Award("f2", "B", 20);

        Assert.Equal(2, belt.Lineage.Count);
        Assert.Equal("f2", belt.HolderId);
        Assert.Equal(0, belt.Defences);
    }

    [Fact]
    public void BeltBookTracksHolders()
    {
        var book = new BeltBook();
        book.Get("wsl", WeightClassId.Welterweight).Award("f1", "Rico", 0);
        book.Get("wsl", WeightClassId.Middleweight).Award("f1", "Rico", 0);
        book.Get("esc", WeightClassId.Welterweight).Award("f2", "Other", 0);

        Assert.Equal(2, book.HeldBy("f1").Count());
        Assert.NotNull(book.FindByHolder("f2"));
        Assert.Null(book.FindByHolder("nobody"));
    }

    [Fact]
    public void SeedingCreatesEveryTierForEverySanctionedDivision()
    {
        var book = new BeltBook();
        var league = Leagues.Leagues.EuropeanCircuit;
        book.SeedForLeague(league);

        foreach (var division in league.Divisions)
        {
            Assert.NotNull(book.Belts.FirstOrDefault(b => b.Division == division && b.Kind == BeltKind.World));
            Assert.NotNull(book.Belts.FirstOrDefault(b => b.Division == division && b.Kind == BeltKind.Interim));
            Assert.NotNull(book.Belts.FirstOrDefault(b => b.Division == division && b.Kind == BeltKind.Continental));
        }
    }

    [Fact]
    public void BeltNamesReadLikeRealChampionships()
    {
        var book = new BeltBook();
        var belt = book.Get("wsl", WeightClassId.Heavyweight);
        Assert.Equal("WSL Heavyweight World Championship", belt.DisplayName);

        var interim = book.Get("wsl", WeightClassId.Heavyweight, BeltKind.Interim);
        Assert.Contains("Interim", interim.DisplayName);
    }

    [Fact]
    public void SameBeltIsReturnedForTheSameKeys()
    {
        var book = new BeltBook();
        var first = book.Get("wsl", WeightClassId.Welterweight);
        var second = book.Get("wsl", WeightClassId.Welterweight);
        Assert.Same(first, second);
        Assert.Single(book.Belts);
    }
}
