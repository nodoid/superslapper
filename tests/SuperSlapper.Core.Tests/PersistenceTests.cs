using SuperSlapper.Core.Career;
using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "superslapper-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CareerState Career() =>
        WorldGenerator.Create("Test", "Fighter", "Tester", WeightClassId.Lightweight, 12345);

    [Fact]
    public void SavingWritesAReadableFile()
    {
        var repo = new CareerRepository(_dir);
        repo.Save(Career());

        Assert.True(File.Exists(repo.SlotPath("career")));
        Assert.Contains("\"PlayerId\": \"player\"", File.ReadAllText(repo.SlotPath("career")));
    }

    [Fact]
    public void LoadReturnsWhatWasSaved()
    {
        var repo = new CareerRepository(_dir);
        var original = Career();
        original.Money = 123456;
        repo.Save(original);

        var loaded = repo.Load();
        Assert.NotNull(loaded);
        Assert.Equal(123456, loaded!.Money);
        Assert.Equal(original.Fighters.Count, loaded.Fighters.Count);
        Assert.Equal(original.Player.Division, loaded.Player.Division);
    }

    [Fact]
    public void LoadMissingSlotReturnsNull()
    {
        Assert.Null(new CareerRepository(_dir).Load("nothing-here"));
    }

    [Fact]
    public void SlotsAreIndependent()
    {
        var repo = new CareerRepository(_dir);
        var a = Career();
        a.CareerName = "Alpha";
        var b = Career();
        b.CareerName = "Bravo";

        repo.Save(a, "alpha");
        repo.Save(b, "bravo");

        Assert.Equal("Alpha", repo.Load("alpha")!.CareerName);
        Assert.Equal("Bravo", repo.Load("bravo")!.CareerName);
        Assert.Equal(2, repo.ListSlots().Count);
    }

    [Fact]
    public void SaveIsAtomicAndLeavesNoTempFile()
    {
        var repo = new CareerRepository(_dir);
        repo.Save(Career(), "atomic");
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));

        repo.Save(Career(), "atomic");
        Assert.Single(Directory.GetFiles(_dir, "*.json"));
    }

    [Fact]
    public void DeleteRemovesTheSlot()
    {
        var repo = new CareerRepository(_dir);
        repo.Save(Career(), "gone");
        Assert.True(repo.Exists("gone"));
        repo.Delete("gone");
        Assert.False(repo.Exists("gone"));
    }

    [Fact]
    public void SlotNamesAreSanitised()
    {
        Assert.Equal("career", CareerRepository.SanitiseSlot(""));
        Assert.Equal("mycareer1", CareerRepository.SanitiseSlot("my career 1"));
        Assert.Equal("etcpasswd", CareerRepository.SanitiseSlot("../../etc/passwd"));
        Assert.Equal("career_final", CareerRepository.SanitiseSlot("career_final"));
    }

    [Fact]
    public void OldSavesAreMigratedToTheCurrentSchema()
    {
        var old = Career();
        old.SchemaVersion = 1;
        var migrated = CareerRepository.Migrate(old);
        Assert.Equal(CareerState.CurrentSchemaVersion, migrated.SchemaVersion);
    }

    [Fact]
    public void SavedCareerKeepsRngStateSoTheFutureIsStable()
    {
        var repo = new CareerRepository(_dir);
        var state = Career();
        state.RngState0 = 42;
        state.RngState1 = 99;
        repo.Save(state);

        var loaded = repo.Load()!;
        Assert.Equal(42UL, loaded.RngState0);
        Assert.Equal(99UL, loaded.RngState1);
    }

    [Fact]
    public void PlayerAndBeltStateSurviveTheRoundTrip()
    {
        var repo = new CareerRepository(_dir);
        var state = Career();
        var belt = state.Belts.Get("wsl", WeightClassId.Lightweight);
        belt.Award("player", "Test Fighter", 10);
        state.Player.Record.Wins = 7;
        state.Player.Record.KnockoutWins = 5;
        repo.Save(state);

        var loaded = repo.Load()!;
        Assert.Equal("player", loaded.Belts.Get("wsl", WeightClassId.Lightweight).HolderId);
        Assert.Equal(7, loaded.Player.Record.Wins);
        Assert.Equal(5, loaded.Player.Record.KnockoutWins);
    }
}
