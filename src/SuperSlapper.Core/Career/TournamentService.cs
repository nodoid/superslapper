using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;

namespace SuperSlapper.Core.Career;

/// <summary>
/// Professional tournaments. A league stages one card a month in each division:
/// three undercard bouts, a co-main and a main event. The player is placed by
/// standing - champion or top contender closes the show, everyone else works the
/// undercard - and every other bout on the card can be watched.
/// </summary>
public static class TournamentService
{
    public const int BoutsOnCard = 5;

    private static readonly CardSlot[] Slots =
    {
        CardSlot.Undercard, CardSlot.Undercard, CardSlot.Undercard,
        CardSlot.CoMain, CardSlot.MainEvent
    };

    /// <summary>
    /// Puts the player on the next card at least <paramref name="leadDays"/> out
    /// and returns the offer. Reuses a card that is already booked, so calling
    /// this twice cannot duplicate an event.
    /// </summary>
    public static FightOffer Book(CareerState state, IRandomSource rng, int leadDays = 21)
    {
        var league = Leagues.Leagues.Get(state.PlayerLeagueId);

        var card = state.Cards
            .Where(c => c.LeagueId == league.Id && c.Division == state.PlayerDivision)
            .Where(c => c.Day >= state.Day + leadDays)
            .OrderBy(c => c.Day)
            .FirstOrDefault();

        if (card is null)
        {
            var day = CareerCalendar.NextEventDay(league, state.Day + leadDays);
            card = Build(state, league, rng, day);
            state.Cards.Add(card);
            state.Cards.Sort((a, b) => a.Day.CompareTo(b.Day));
        }

        var playerBout = card.Bouts.First(b => b.IsPlayerBout);
        var opponentId = playerBout.FighterAId == state.PlayerId ? playerBout.FighterBId : playerBout.FighterAId;

        var offer = new FightOffer
        {
            LeagueId = league.Id,
            Division = state.PlayerDivision,
            OpponentId = opponentId,
            OpponentName = state.FindFighter(opponentId)?.DisplayName ?? "",
            FightDay = card.Day,
            TitleFight = playerBout.TitleFight,
            Rounds = league.Ruleset.Rounds,
            Purse = league.PurseFor(playerBout.TitleFight, false) + state.Player.Popularity * 25,
            Venue = card.Venue,
            IsMainEvent = playerBout.Slot == CardSlot.MainEvent,
            BeltKind = BeltKind.World
        };

        state.Offer = offer;
        return offer;
    }

    /// <summary>The card the player is watching: fight night or later, still unfinished.</summary>
    public static TournamentCard? CardOnNight(CareerState state)
    {
        return state.Cards
            .Where(c => c.Day <= state.Day)
            .Where(c => !c.IsComplete)
            .Where(c => c.Division == state.PlayerDivision)
            .OrderByDescending(c => c.Day)
            .FirstOrDefault();
    }

    /// <summary>The player's next card, whether or not it has been built yet.</summary>
    public static TournamentCard? UpcomingCard(CareerState state)
    {
        return state.Cards
            .Where(c => c.Day > state.Day)
            .Where(c => c.Division == state.PlayerDivision)
            .OrderBy(c => c.Day)
            .FirstOrDefault();
    }

    /// <summary>Plays the next AI bout on the card and writes it into the career.</summary>
    public static BoutReplay Watch(CareerState state, CareerService service, TournamentCard card, int index)
    {
        if (index < 0 || index >= card.Bouts.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var bout = card.Bouts[index];
        if (bout.IsDecided)
        {
            throw new InvalidOperationException("That bout has already been fought.");
        }

        if (bout.IsPlayerBout)
        {
            throw new InvalidOperationException("The player's own bout is fought, not watched.");
        }

        var a = state.FindFighter(bout.FighterAId)
                ?? throw new InvalidOperationException($"Missing fighter {bout.FighterAId}.");
        var b = state.FindFighter(bout.FighterBId)
                ?? throw new InvalidOperationException($"Missing fighter {bout.FighterBId}.");

        var league = Leagues.Leagues.Get(card.LeagueId);
        var rng = service.Rng(state);

        var replay = BoutReplay.Record(a, b, league.Ruleset, rng, bout.TitleFight, league.Id, card.Day,
            0.35 + a.Overall / 250.0, 0.35 + b.Overall / 250.0);

        bout.Result = replay.Result;
        bout.Replay = replay;
        bout.Watched = true;

        service.ApplyBackgroundResult(state, league, a, b, replay.Result);
        service.PersistRng(state, (RandomSource)rng);
        return replay;
    }

    /// <summary>Runs out the rest of the card without a replay.</summary>
    public static void SimulateRest(CareerState state, CareerService service, TournamentCard card)
    {
        for (var i = 0; i < card.Bouts.Count; i++)
        {
            if (!card.Bouts[i].IsDecided && !card.Bouts[i].IsPlayerBout)
            {
                Watch(state, service, card, i);
            }
        }
    }

    /// <summary>Records the player's own result on the card.</summary>
    public static void AttachPlayerResult(CareerState state, FightResult result)
    {
        var card = state.Cards.FirstOrDefault(c =>
            c.Day == result.Day && c.LeagueId == result.LeagueId &&
            c.Bouts.Any(b => b.IsPlayerBout && !b.IsDecided));

        var bout = card?.Bouts.FirstOrDefault(b => b.IsPlayerBout && !b.IsDecided);
        if (bout is null)
        {
            return;
        }

        bout.Result = result;
        bout.Watched = true;
    }

    /// <summary>Keeps the card list from growing without bound over a long career.</summary>
    public static void Prune(CareerState state)
    {
        state.Cards.RemoveAll(c => c.IsComplete && c.Day < state.Day - 120);
    }

    private static TournamentCard Build(CareerState state, League league, IRandomSource rng, int day)
    {
        var division = state.PlayerDivision;
        var champion = state.Belts.Get(league.Id, division, BeltKind.World);
        var table = state.Rankings.Get(league.Id, division);
        var player = state.Player;
        var playerPosition = table.PositionOf(player.Id);

        // Champion first, then the published rankings, then anyone unranked.
        var order = state.Fighters
            .Where(f => f.LeagueId == league.Id && f.Division == division)
            .OrderBy(f => f.Id == champion.HolderId ? 0 : 1)
            .ThenBy(f => table.PositionOf(f.Id))
            .ThenByDescending(f => f.Overall)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .ToList();

        var lastOpponentId = state.History
            .Where(h => h.InvolvesPlayer)
            .OrderByDescending(h => h.Day)
            .Select(h => h.FighterAId == player.Id ? h.FighterBId : h.FighterAId)
            .FirstOrDefault();

        var isChampion = champion.HolderId == player.Id;
        var isNumberOne = playerPosition <= 1 && !isChampion;
        var titleFight = isChampion || (isNumberOne && !champion.IsVacant);

        // Champion in a title fight, otherwise the nearest ranked rival.
        Fighter opponent;
        if (isChampion && !champion.IsVacant)
        {
            opponent = order.FirstOrDefault(f => f.Id != player.Id && f.Id != champion.HolderId)
                       ?? order.First(f => f.Id != player.Id);
        }
        else if (isNumberOne && !champion.IsVacant &&
                 state.FindFighter(champion.HolderId) is { } beltHolder)
        {
            opponent = beltHolder;
        }
        else
        {
            var pool = order.Where(f => f.Id != player.Id).ToList();
            var rested = pool
                .Where(f => f.LastFightDay < 0 || state.Day - f.LastFightDay >= 10)
                .Where(f => f.Id != lastOpponentId)
                .ToList();
            var candidates = rested.Count > 0 ? rested : pool;
            opponent = candidates
                .OrderBy(f => Math.Abs(table.PositionOf(f.Id) - Math.Min(playerPosition, league.RankedContenders + 1)))
                .ThenBy(f => f.Id, StringComparer.Ordinal)
                .First();
        }

        var playerSlot = titleFight || playerPosition <= 3
            ? CardSlot.MainEvent
            : playerPosition <= 6
                ? CardSlot.CoMain
                : CardSlot.Undercard;

        // Pair the rest of the division down the card, champion bout first.
        var remaining = order
            .Where(f => f.Id != player.Id && f.Id != opponent.Id)
            .ToList();

        var others = new List<CardBout>();
        for (var i = 0; i + 1 < remaining.Count && others.Count < BoutsOnCard; i += 2)
        {
            var a = remaining[i];
            var b = remaining[i + 1];
            others.Add(new CardBout
            {
                FighterAId = a.Id,
                FighterBId = b.Id,
                FighterAName = a.DisplayName,
                FighterBName = b.DisplayName,
                TitleFight = a.Id == champion.HolderId || b.Id == champion.HolderId
            });
        }

        var playerBout = new CardBout
        {
            FighterAId = player.Id,
            FighterBId = opponent.Id,
            FighterAName = player.DisplayName,
            FighterBName = opponent.DisplayName,
            TitleFight = titleFight,
            IsPlayerBout = true,
            Slot = playerSlot
        };

        var slots = Slots.ToList();
        var playerSlotIndex = slots.IndexOf(playerSlot);
        if (playerSlotIndex < 0)
        {
            playerSlotIndex = 0;
        }

        var card = new TournamentCard
        {
            LeagueId = league.Id,
            Division = division,
            Day = day,
            Venue = WorldGenerator.RandomVenue(rng)
        };

        var next = 0;
        for (var i = 0; i < BoutsOnCard; i++)
        {
            if (i == playerSlotIndex)
            {
                playerBout.Slot = slots[i];
                card.Bouts.Add(playerBout);
            }
            else if (next < others.Count)
            {
                var bout = others[next++];
                bout.Slot = slots[i];
                card.Bouts.Add(bout);
            }
        }

        return card;
    }
}
