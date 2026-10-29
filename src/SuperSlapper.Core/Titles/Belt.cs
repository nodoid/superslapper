using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Titles;

public enum BeltKind
{
    /// <summary>The full championship of the league in that division.</summary>
    World = 0,

    /// <summary>Held while the champion is inactive or injured.</summary>
    Interim = 1,

    /// <summary>Regional stepping stone title.</summary>
    Continental = 2
}

/// <summary>A single reign. The history list on a belt is what "belts" mean in the game.</summary>
public sealed class TitleReign
{
    public string FighterId { get; set; } = "";
    public string FighterName { get; set; } = "";
    public int WonDay { get; set; }
    public int LostDay { get; set; } = -1;
    public int Defences { get; set; }
    public bool Vacated { get; set; }

    public bool IsActive => LostDay < 0 && !Vacated;

    public int LengthDays(int currentDay) => (LostDay < 0 ? currentDay : LostDay) - WonDay;

    public string Display => Loc.T(IsActive
            ? Defences == 1 ? "belt.reign.current.one" : "belt.reign.current.many"
            : Defences == 1 ? "belt.reign.past.one" : "belt.reign.past.many",
        FighterName, Defences);
}

/// <summary>
/// A championship belt for one league and one division. The belt keeps its full
/// lineage so the title picture survives from bout to bout.
/// </summary>
public sealed class Belt
{
    public string Id { get; set; } = "";
    public string LeagueId { get; set; } = "wsl";
    public WeightClassId Division { get; set; } = WeightClassId.Welterweight;
    public BeltKind Kind { get; set; } = BeltKind.World;
    public string Name { get; set; } = "";

    /// <summary>Current holder, or null when vacant.</summary>
    public string? HolderId { get; set; }

    public List<TitleReign> Lineage { get; set; } = new();

    public bool IsVacant => HolderId is null;

    public TitleReign? CurrentReign => Lineage.LastOrDefault(r => r.IsActive);

    public int Defences => CurrentReign?.Defences ?? 0;

    public void Award(string fighterId, string fighterName, int day)
    {
        CloseCurrent(day, vacated: false);
        Lineage.Add(new TitleReign
        {
            FighterId = fighterId,
            FighterName = fighterName,
            WonDay = day,
            Defences = 0
        });
        HolderId = fighterId;
    }

    /// <summary>Records a successful defence by the current holder.</summary>
    public void RecordDefence()
    {
        var current = CurrentReign;
        if (current is not null)
        {
            current.Defences++;
        }
    }

    public void Vacate(int day)
    {
        CloseCurrent(day, vacated: true);
        HolderId = null;
    }

    private void CloseCurrent(int day, bool vacated)
    {
        var current = CurrentReign;
        if (current is null)
        {
            return;
        }

        current.LostDay = day;
        current.Vacated = vacated;
    }

    public string DisplayName => Loc.T(Kind switch
    {
        BeltKind.World => "belt.name.world",
        BeltKind.Interim => "belt.name.interim",
        _ => "belt.name.continental"
    }, Leagues.Leagues.Get(LeagueId).ShortName, GetDivisionName());

    private string GetDivisionName() => WeightClasses.Get(Division).Name;
}

/// <summary>Every belt in the game world, one per league, division and tier.</summary>
public sealed class BeltBook
{
    public List<Belt> Belts { get; set; } = new();

    public Belt Get(string leagueId, WeightClassId division, BeltKind kind = BeltKind.World)
    {
        var belt = Belts.FirstOrDefault(b =>
            string.Equals(b.LeagueId, leagueId, StringComparison.OrdinalIgnoreCase) &&
            b.Division == division && b.Kind == kind);
        if (belt is not null)
        {
            return belt;
        }

        belt = new Belt
        {
            Id = $"{leagueId}-{division}-{kind}".ToLowerInvariant(),
            LeagueId = leagueId,
            Division = division,
            Kind = kind
        };
        belt.Name = belt.DisplayName;
        Belts.Add(belt);
        return belt;
    }

    public Belt? FindByHolder(string fighterId, BeltKind kind = BeltKind.World) =>
        Belts.FirstOrDefault(b => b.Kind == kind && b.HolderId == fighterId);

    public IEnumerable<Belt> HeldBy(string fighterId) =>
        Belts.Where(b => b.HolderId == fighterId);

    /// <summary>Creates every world and continental belt the league sanctions.</summary>
    public void SeedForLeague(Leagues.League league)
    {
        foreach (var division in league.Divisions)
        {
            Get(league.Id, division, BeltKind.World);
            Get(league.Id, division, BeltKind.Interim);
            Get(league.Id, division, BeltKind.Continental);
        }
    }
}
