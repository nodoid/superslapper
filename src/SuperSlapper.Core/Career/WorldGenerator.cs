using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rankings;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;

namespace SuperSlapper.Core.Career;

/// <summary>
/// Builds a complete slap world: a roster for every league and division, a
/// champion in every division, and published rankings. Deterministic from the
/// career seed so the same seed always produces the same world.
/// </summary>
public static class WorldGenerator
{
    public const int FightersPerDivision = 10;

    private static readonly string[] FirstNames =
    {
        "Dmitri", "Kazim", "Rico", "Bobby", "Jarrah", "Tomas", "Andrei", "Liam", "Carlos", "Sasha",
        "Malik", "Viktor", "Noah", "Kwame", "Ivan", "Hamish", "Diego", "Yusuf", "Ollie", "Mateo",
        "Brandon", "Dainius", "Sean", "Aapo", "Rafa", "Kenji", "Milos", "Tariq", "Eddie", "Ronan",
        "Goran", "Pieter", "Sami", "Jock", "Anton", "Bruno", "Caleb", "Fabio", "Gethin", "Hugo"
    };

    private static readonly string[] LastNames =
    {
        "Volkov", "Okafor", "Marchetti", "Kelly", "Nakamura", "Silva", "Petrov", "Doyle", "Mbeki", "Horvat",
        "Kovacs", "Berg", "Alvarez", "Nielsen", "Rahman", "Ferrari", "Kavanagh", "Sorensen", "Baptiste", "Oduya",
        "Novak", "Lindqvist", "Dimitrov", "Rossi", "Fletcher", "Aziz", "Morrow", "Baldwin", "Duarte", "Hale",
        "Iversen", "MacLeod", "Bianchi", "Sato", "Farrell", "Nagy", "Whitlock", "Costa", "Achterberg", "Toure"
    };

    private static readonly string[] Nicknames =
    {
        "The Hammer", "Brick Hands", "The Anvil", "Open Palm", "The Surgeon", "Thunderclap", "The Wall",
        "Half-Ton", "The Metronome", "Sledge", "Icicle", "The Butcher", "Velvet Hand", "The Metronome",
        "Stone Chin", "The Whirlwind", "Nightmare", "The Architect", "Cinderblock", "The Professor"
    };

    private static readonly string[] Nationalities =
    {
        "GB", "US", "CA", "AU", "NZ", "IE", "PL", "RU", "UA", "LV", "LT", "NL", "DE", "FR", "ES", "PT",
        "IT", "RS", "HR", "BA", "TR", "ZA", "NG", "BR", "AR", "MX", "JP", "KR", "PH", "IN"
    };

    private static readonly string[] Venues =
    {
        "The Slaplantic", "Palms Arena", "Echo Hall", "The Steelyard", "Palm Gardens",
        "Bricklayers' Union Hall", "Northside Dome", "The Iron Ring", "Civic Coliseum", "The Slap Pit"
    };

    public static CareerState Create(string firstName, string lastName, string nickname,
        WeightClassId division, int seed, string careerName = "Career", Stance? stance = null)
    {
        var rng = new RandomSource(seed);
        var state = new CareerState
        {
            CareerName = careerName,
            Seed = seed,
            PlayerDivision = division,
            PlayerLeagueId = Leagues.Leagues.AmateurCircuit.Id,
            Day = 0,
            Season = 1
        };

        var player = CreatePlayer(firstName, lastName, nickname, division, rng, stance);
        state.PlayerId = player.Id;
        state.Fighters.Add(player);

        foreach (var league in Leagues.Leagues.All)
        {
            state.Belts.SeedForLeague(league);
            foreach (var sanctionedDivision in league.Divisions)
            {
                for (var i = 0; i < FightersPerDivision; i++)
                {
                    state.Fighters.Add(CreateNpc(league, sanctionedDivision, rng, i));
                }
            }
        }

        SeedChampions(state, rng);
        SeedRankings(state);

        state.RngState0 = rng.State0;
        state.RngState1 = rng.State1;

        return state;
    }

    /// <summary>
    /// Builds the player. The stance is what the new-career screen picks as the
    /// preferred hand; leave it null for a random one.
    /// </summary>
    public static Fighter CreatePlayer(string firstName, string lastName, string nickname,
        WeightClassId division, IRandomSource rng, Stance? stance = null)
    {
        var (min, max) = WeightClasses.TypicalRange(division);
        var walkAround = Math.Round(max.Pounds - 4m + (decimal)rng.NextDouble() * 6m, 1);

        var player = new Fighter
        {
            Id = "player",
            FirstName = string.IsNullOrWhiteSpace(firstName) ? "New" : firstName,
            LastName = string.IsNullOrWhiteSpace(lastName) ? "Signing" : lastName,
            Nickname = nickname,
            Nationality = "GB",
            Age = 22,
            Stance = stance ?? (rng.NextDouble() < 0.25 ? Stance.Southpaw : Stance.Orthodox),
            LeagueId = Leagues.Leagues.AmateurCircuit.Id,
            Division = division,
            WalkAroundPounds = walkAround,
            WeightPounds = walkAround,
            Popularity = 5,
            IsPlayer = true,
            Energy = 100
        };

        // A rookie with a bit of natural pop and a lot to learn.
        player.Attributes.Power = 34 + rng.Next(0, 8);
        player.Attributes.Aim = 30 + rng.Next(0, 10);
        player.Attributes.Chin = 34 + rng.Next(0, 10);
        player.Attributes.Endurance = 30 + rng.Next(0, 10);
        player.Attributes.Reflex = 28 + rng.Next(0, 10);
        player.Attributes.Mass = 45 + rng.Next(0, 10);
        player.Attributes.Clamp();
        return player;
    }

    public static Fighter CreateNpc(League league, WeightClassId division, IRandomSource rng, int index = 0)
    {
        var (min, max) = WeightClasses.TypicalRange(division);
        var span = Math.Max(0.1m, max.Pounds - min.Pounds);
        var weight = Math.Round(min.Pounds + (decimal)rng.NextDouble() * span * 0.9m, 1);

        // Higher prestige leagues get better fighters.
        var baseRating = league.Prestige switch
        {
            1 => 25,
            2 => 40,
            3 => 50,
            4 => 60,
            _ => 68
        };

        // The amateur circuit is the shallow end: beatable for a raw rookie.
        var spread = league.Prestige == 1 ? 13 : 18;
        var rating = Math.Clamp(baseRating + rng.Next(0, spread), 20, 94);

        var fighter = new Fighter
        {
            Id = $"{league.Id}-{division}-{index:D2}",
            FirstName = FirstNames[rng.Next(0, FirstNames.Length)],
            LastName = LastNames[rng.Next(0, LastNames.Length)],
            Nickname = rng.NextDouble() < 0.45 ? Nicknames[rng.Next(0, Nicknames.Length)] : "",
            Nationality = Nationalities[rng.Next(0, Nationalities.Length)],
            Age = 21 + rng.Next(0, 15),
            Stance = rng.NextDouble() < 0.22 ? Stance.Southpaw : Stance.Orthodox,
            LeagueId = league.Id,
            Division = division,
            WalkAroundPounds = Math.Round(weight + 3m + (decimal)rng.NextDouble() * 5m, 1),
            WeightPounds = weight,
            Popularity = Math.Clamp(league.Prestige * 8 + rng.Next(0, 20), 1, 100),
            Energy = 100
        };

        // Spread attributes around the rating so no two fighters fight alike.
        fighter.Attributes.Power = Math.Clamp(rating + rng.Next(-9, 11), 15, 99);
        fighter.Attributes.Aim = Math.Clamp(rating + rng.Next(-9, 11), 15, 99);
        fighter.Attributes.Chin = Math.Clamp(rating + rng.Next(-9, 11), 15, 99);
        fighter.Attributes.Endurance = Math.Clamp(rating + rng.Next(-9, 11), 15, 99);
        fighter.Attributes.Reflex = Math.Clamp(rating + rng.Next(-9, 11), 15, 99);
        fighter.Attributes.Mass = Math.Clamp(40 + rng.Next(0, 45), 20, 100);
        fighter.Attributes.Clamp();

        fighter.Record.Wins = rng.Next(0, 14);
        fighter.Record.Losses = rng.Next(0, 6);
        fighter.Record.Draws = rng.Next(0, 2);
        fighter.Record.KnockoutWins = rng.Next(0, Math.Max(1, fighter.Record.Wins));
        fighter.LastFightDay = rng.Next(0, 120);
        return fighter;
    }

    /// <summary>The best fighter in each division is the inaugural champion.</summary>
    public static void SeedChampions(CareerState state, IRandomSource rng)
    {
        foreach (var league in Leagues.Leagues.All)
        {
            foreach (var division in league.Divisions)
            {
                var contenders = state.Fighters
                    .Where(f => f.LeagueId == league.Id && f.Division == division && !f.IsPlayer)
                    .OrderByDescending(f => f.Overall)
                    .ThenBy(f => f.Id, StringComparer.Ordinal)
                    .ToList();

                if (contenders.Count == 0)
                {
                    continue;
                }

                var world = state.Belts.Get(league.Id, division, BeltKind.World);
                var champion = contenders[0];
                world.Award(champion.Id, champion.DisplayName, 0);

                if (contenders.Count > 1)
                {
                    var continental = state.Belts.Get(league.Id, division, BeltKind.Continental);
                    var holder = contenders[1];
                    continental.Award(holder.Id, holder.DisplayName, 0);
                }
            }
        }
    }

    /// <summary>Initial ranking points derived from standing so the order looks earned.</summary>
    public static void SeedRankings(CareerState state)
    {
        foreach (var league in Leagues.Leagues.All)
        {
            foreach (var division in league.Divisions)
            {
                var table = state.Rankings.Get(league.Id, division);
                var belt = state.Belts.Get(league.Id, division, BeltKind.World);
                table.ChampionId = belt.HolderId;

                var ranked = state.Fighters
                    .Where(f => f.LeagueId == league.Id && f.Division == division)
                    .Where(f => f.Id != belt.HolderId)
                    .OrderByDescending(f => f.Overall)
                    .ThenBy(f => f.Id, StringComparer.Ordinal)
                    .Take(league.RankedContenders)
                    .ToList();

                var points = 90;
                table.Entries.Clear();
                foreach (var fighter in ranked)
                {
                    table.Entries.Add(new RankingEntry
                    {
                        FighterId = fighter.Id,
                        FighterName = fighter.DisplayName,
                        Points = Math.Max(5, points),
                        Position = table.Entries.Count + 1,
                        PreviousPosition = table.Entries.Count + 1,
                        Record = fighter.Record.Display,
                        Streak = fighter.Streak
                    });
                    points -= 8;
                }

                state.Rankings.Republish(league.Id, division, league.RankedContenders);
            }
        }
    }

    public static string RandomVenue(IRandomSource rng) => Venues[rng.Next(0, Venues.Length)];
}
