using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rankings;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Titles;
using SuperSlapper.Core.Training;
using SuperSlapper.Core.Units;

namespace SuperSlapper.Core.Career;

/// <summary>Everything that happens outside the ring: days, camps, offers, results.</summary>
public sealed class CareerService
{
    private readonly TrainingEngine _training;
    private readonly RankingPoints _points;

    public CareerService(TrainingEngine? training = null, RankingPoints? points = null)
    {
        _training = training ?? new TrainingEngine();
        _points = points ?? new RankingPoints();
    }

    public TrainingEngine Training => _training;

    public IRandomSource Rng(CareerState state)
    {
        var rng = new RandomSource(state.RngState0, state.RngState1) { Seed = state.Seed };
        return rng;
    }

    public void PersistRng(CareerState state, RandomSource rng)
    {
        state.RngState0 = rng.State0;
        state.RngState1 = rng.State1;
    }

    /// <summary>Books the next bout for the player. Called at career start and after every result.</summary>
    public FightOffer BookNextFight(CareerState state, IRandomSource rng, int leadDays = 21)
    {
        var league = Leagues.Leagues.Get(state.PlayerLeagueId);
        var player = state.Player;

        // Professionals fight on the league's monthly card instead of taking a
        // bout whenever one is offered.
        if (league.RunsMonthlyCards && league.Sanctions(state.PlayerDivision))
        {
            return TournamentService.Book(state, rng, leadDays);
        }

        if (!league.Sanctions(state.PlayerDivision))
        {
            // Move the player to a division the league actually runs.
            state.PlayerDivision = league.Divisions.OrderBy(d => (int)d).First();
            player.Division = state.PlayerDivision;
        }

        var table = state.Rankings.Get(league.Id, state.PlayerDivision);
        var playerPosition = table.PositionOf(player.Id);

        var pool = state.Fighters
            .Where(f => f.Id != player.Id && f.LeagueId == league.Id && f.Division == state.PlayerDivision)
            .ToList();

        // Never book the same opponent twice in a row if anyone else is free.
        var lastOpponentId = state.History
            .Where(h => h.InvolvesPlayer)
            .OrderByDescending(h => h.Day)
            .Select(h => h.FighterAId == player.Id ? h.FighterBId : h.FighterAId)
            .FirstOrDefault();

        var targetPosition = Math.Min(playerPosition, league.RankedContenders + 1);
        var rested = pool
            .Where(f => f.LastFightDay < 0 || state.Day - f.LastFightDay >= 10)
            .Where(f => f.Id != lastOpponentId)
            .ToList();

        var candidates = rested.Count > 0
            ? rested
            : pool.Where(f => f.Id != lastOpponentId).ToList();
        if (candidates.Count == 0)
        {
            candidates = pool;
        }

        var opponent = candidates
            .OrderBy(f => Math.Abs(table.PositionOf(f.Id) - targetPosition))
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .First();

        var champion = state.Belts.Get(league.Id, state.PlayerDivision, BeltKind.World);
        var isChampion = champion.HolderId == player.Id;
        var isNumberOne = playerPosition <= 1;
        var titleFight = isChampion || (isNumberOne && !champion.IsVacant);

        if (isNumberOne && !champion.IsVacant && champion.HolderId != player.Id)
        {
            var beltHolder = state.FindFighter(champion.HolderId);
            if (beltHolder is not null)
            {
                opponent = beltHolder;
            }
        }

        var purse = league.PurseFor(titleFight, won: false) + (titleFight ? league.TitlePurse / 2 : 0) + player.Popularity * 25;

        var offer = new FightOffer
        {
            LeagueId = league.Id,
            Division = state.PlayerDivision,
            OpponentId = opponent.Id,
            OpponentName = opponent.DisplayName,
            FightDay = state.Day + leadDays,
            TitleFight = titleFight,
            Rounds = league.Ruleset.Rounds,
            Purse = purse,
            Venue = WorldGenerator.RandomVenue(rng),
            IsMainEvent = titleFight || player.Popularity >= 45,
            BeltKind = BeltKind.World
        };

        state.Offer = offer;
        return offer;
    }

    /// <summary>Schedules a bout between two named fighters (used by the career screen).</summary>
    public FightOffer CreateOfferFor(CareerState state, string opponentId, int leadDays, bool titleFight)
    {
        var league = Leagues.Leagues.Get(state.PlayerLeagueId);
        var opponent = state.FindFighter(opponentId)
                       ?? throw new ArgumentException("Unknown opponent.", nameof(opponentId));

        return new FightOffer
        {
            LeagueId = league.Id,
            Division = state.PlayerDivision,
            OpponentId = opponent.Id,
            OpponentName = opponent.DisplayName,
            FightDay = state.Day + leadDays,
            TitleFight = titleFight,
            Rounds = league.Ruleset.Rounds,
            Purse = league.PurseFor(titleFight, false),
            Venue = WorldGenerator.RandomVenue(new RandomSource(state.Seed + state.Day)),
            IsMainEvent = titleFight
        };
    }

    /// <summary>
    /// Runs one training day. The player cannot train through a fight day, and
    /// every activity is gated by energy.
    /// </summary>
    public TrainingResult TrainDay(CareerState state, TrainingActivity activity)
    {
        if (state.Offer is not null && state.Day >= state.Offer.FightDay)
        {
            throw new InvalidOperationException("It is fight day. Step on the scale first.");
        }

        var rng = (RandomSource)Rng(state);
        var player = state.Player;
        var lastEntry = state.TrainingLog.LastOrDefault();
        var repeat = lastEntry?.ActivityId == activity.Id
            ? state.TrainingLog.TakeLast(3).Count(e => e.ActivityId == activity.Id)
            : 0;

        var result = _training.Execute(player, activity, rng, lastEntry?.ActivityId, repeat);

        if (result.Injury is not null)
        {
            result.Injury.InjuryDay = state.Day;
        }

        _training.EndOfDay(player);
        AdvanceWorldDay(state, skipPlayer: true);

        state.TrainingLog.Add(new TrainingLogEntry
        {
            Day = state.Day,
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            GainSummary = result.GainSummary,
            Injury = result.Injury?.DisplayName,
            Overtrained = result.Overtrained
        });

        state.Day++;
        PersistRng(state, rng);
        return result;
    }

    /// <summary>Moves the world on by one day for everyone except the player.</summary>
    private void AdvanceWorldDay(CareerState state, bool skipPlayer)
    {
        foreach (var fighter in state.Fighters)
        {
            if (skipPlayer && fighter.Id == state.PlayerId)
            {
                continue;
            }

            fighter.PassDay();
        }
    }

    /// <summary>Skips a day without training (travelling, media, rest).</summary>
    public void RestDay(CareerState state)
    {
        TrainDay(state, TrainingCatalog.Rest);
    }

    /// <summary>Weigh-in on the morning of the bout.</summary>
    public WeighInResult WeighIn(CareerState state, WeightUnit unit = WeightUnit.Metric)
    {
        var offer = state.Offer ?? throw new InvalidOperationException("No fight booked.");
        var league = Leagues.Leagues.Get(offer.LeagueId);
        var rules = league.Ruleset;

        var result = Fight.WeighIn.Evaluate(state.Player, state.PlayerDivision, rules, offer.TitleFight, unit);

        state.WeighIns.Add(new WeighInRecord
        {
            Day = state.Day,
            Division = state.PlayerDivision,
            ActualPounds = result.ActualPounds,
            LimitPounds = result.LimitPounds,
            AllowancePounds = result.AllowancePounds,
            MadeWeight = result.MadeWeight,
            TitleFight = offer.TitleFight,
            CutPercent = result.CutPercent
        });

        // Step on the scale, then rehydrate.
        _training.Rehydrate(state.Player);
        return result;
    }

    /// <summary>
    /// Builds the fight engine for the booked bout. Fighters are cloned so an
    /// abandoned bout cannot corrupt the career: only a completed result is
    /// written back.
    /// </summary>
    public FightEngine CreateFightEngine(CareerState state)
    {
        var offer = state.Offer ?? throw new InvalidOperationException("No fight booked.");
        var league = Leagues.Leagues.Get(offer.LeagueId);
        var opponent = state.FindFighter(offer.OpponentId)
                       ?? throw new InvalidOperationException("Opponent has left the league.");

        var rng = Rng(state);

        return new FightEngine(state.Player.Clone(), opponent.Clone(), league.Ruleset, rng,
            offer.TitleFight, offer.LeagueId, state.Day);
    }

    /// <summary>Runs the whole bout with both sides on AI, for background bouts.</summary>
    public FightResult SimulateBout(Fighter a, Fighter b, Ruleset rules, IRandomSource rng,
        bool titleFight, string leagueId, int day, double skillA = 0.55, double skillB = 0.55)
    {
        var engine = new FightEngine(a.Clone(), b.Clone(), rules, rng, titleFight, leagueId, day);

        var guard = 0;
        while (engine.Phase != FightPhase.Finished && guard++ < 200)
        {
            switch (engine.Phase)
            {
                case FightPhase.AwaitingSlap:
                {
                    var skill = engine.StrikerIsA ? skillA : skillB;
                    var attempt = FightEngine.GenerateAiAttempt(engine.Striker, skill, rng);
                    attempt.DefenderBraced = rng.NextDouble() < 0.35;
                    engine.DeliverSlap(attempt);
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

        if (engine.Phase != FightPhase.Finished || engine.Result is null)
        {
            throw new InvalidOperationException("Bout failed to reach a result.");
        }

        return engine.Result;
    }

    /// <summary>
    /// Writes a completed bout into the career: records, ranking points, belts,
    /// purse, injuries, popularity and the move to the next league. This is the
    /// single place where a fight propagates into everything that follows.
    /// </summary>
    public CareerOutcome ApplyResult(CareerState state, FightResult result, WeighInResult? weighIn = null)
    {
        var offer = state.Offer;
        var league = Leagues.Leagues.Get(offer?.LeagueId ?? state.PlayerLeagueId);
        var player = state.Player;
        var opponent = state.FindFighter(result.FighterAId == player.Id ? result.FighterBId : result.FighterAId)
                       ?? state.FindFighter(offer?.OpponentId);

        if (opponent is null)
        {
            throw new InvalidOperationException("Could not find the opponent for this result.");
        }

        var playerIsA = result.FighterAId == player.Id;
        var titleFight = result.WasTitleFight;

        var playerWon = result.WonBy(player.Id);
        var isDraw = result.IsDraw;
        var playerKo = playerWon && result.Kind is FightOutcomeKind.Knockout or FightOutcomeKind.TechnicalKnockout;
        var playerLostKo = !playerWon && !isDraw && result.Kind is FightOutcomeKind.Knockout or FightOutcomeKind.TechnicalKnockout;

        // Records.
        if (isDraw)
        {
            player.Record.Draws++;
            opponent.Record.Draws++;
            player.Streak = 0;
            opponent.Streak = 0;
        }
        else if (playerWon)
        {
            player.Record.Wins++;
            opponent.Record.Losses++;
            player.Streak = Math.Max(0, player.Streak) + 1;
            opponent.Streak = Math.Min(0, opponent.Streak) - 1;
            if (playerKo)
            {
                player.Record.KnockoutWins++;
                opponent.Record.KnockoutLosses++;
            }
        }
        else
        {
            opponent.Record.Wins++;
            player.Record.Losses++;
            player.Streak = Math.Min(0, player.Streak) - 1;
            opponent.Streak = Math.Max(0, opponent.Streak) + 1;
            if (playerLostKo)
            {
                opponent.Record.KnockoutWins++;
                player.Record.KnockoutLosses++;
            }
        }

        player.LastFightDay = state.Day;
        opponent.LastFightDay = state.Day;

        // Ranking points for both fighters.
        var playerTitleHolder = state.Belts.HeldBy(player.Id).Any();
        var tableForPlayer = state.Rankings.Get(league.Id, state.PlayerDivision);
        var playerPreviousPosition = tableForPlayer.PositionOf(player.Id);
        var pointsBefore = tableForPlayer.Find(player.Id)?.Points ?? 0;

        AssignPoints(state, league, player, result, playerWon, isDraw, titleFight, playerTitleHolder);
        AssignPoints(state, league, opponent, result, !playerWon && !isDraw, isDraw, titleFight,
            state.Belts.HeldBy(opponent.Id).Any());

        // Belts.
        var beltChanges = UpdateBelts(state, result, league, titleFight, weighIn);

        // Purse.
        var purse = ComputePurse(league, offer, playerWon, isDraw, titleFight, weighIn);
        state.Money += purse;

        // Popularity.
        player.Popularity = Math.Clamp(player.Popularity + (playerWon ? 4 : isDraw ? 1 : -1) + (titleFight ? 5 : 0), 1, 100);

        // Fight damage and injuries carried into the next camp.
        var damageTaken = playerIsA ? result.TotalDamageB : result.TotalDamageA;
        var knockdownsTaken = playerIsA ? result.FighterBKnockdowns : result.FighterAKnockdowns;
        ApplyFightToll(state, player, damageTaken, knockdownsTaken, playerLostKo);
        ApplyFightToll(state, opponent, playerIsA ? result.TotalDamageA : result.TotalDamageB,
            playerIsA ? result.FighterAKnockdowns : result.FighterBKnockdowns, playerKo);

        state.History.Add(new FightHistoryEntry
        {
            Day = state.Day,
            LeagueId = league.Id,
            Division = state.PlayerDivision,
            WinnerId = result.WinnerId,
            LoserId = result.LoserId,
            FighterAId = result.FighterAId,
            FighterBId = result.FighterBId,
            Kind = result.Kind,
            Round = result.EndRound,
            TitleFight = titleFight,
            Headline = result.Headline,
            Scorecards = result.ScorecardText,
            InvolvesPlayer = true,
            KnockdownsA = result.FighterAKnockdowns,
            KnockdownsB = result.FighterBKnockdowns
        });

        // The player's bout is one line on the tournament card; the rest of the
        // card can still be watched afterwards.
        TournamentService.AttachPlayerResult(state, result);
        TournamentService.Prune(state);

        // Republish both divisions so the movement arrows are meaningful.
        state.Rankings.Republish(league.Id, state.PlayerDivision, league.RankedContenders);

        if (playerWon)
        {
            state.BoutsWonAtCurrentLeague++;
        }

        var pointsAfter = state.Rankings.Get(league.Id, state.PlayerDivision).Find(player.Id)?.Points ?? 0;
        var newPosition = state.Rankings.Get(league.Id, state.PlayerDivision).PositionOf(player.Id);
        var promotion = MaybePromote(state, playerWon, playerPreviousPosition);

        // Close out the bout and book the next one.
        state.LastResultHeadline = result.Headline;
        state.Offer = null;
        state.Day = Math.Max(state.Day, (offer?.FightDay ?? state.Day) + 1);
        player.Energy = Math.Max(10, player.Energy - 35);

        var rng = (RandomSource)Rng(state);
        SimulateUndercard(state, league, rng);
        var next = BookNextFight(state, rng);
        PersistRng(state, rng);

        return new CareerOutcome
        {
            Result = result,
            Purse = purse,
            PlayerWon = playerWon,
            IsDraw = isDraw,
            Promoted = promotion is not null,
            NewLeagueId = promotion?.Id,
            BeltChanges = beltChanges,
            NextFight = next,
            Money = state.Money,
            RankingPointsGained = Math.Max(0, pointsAfter - pointsBefore),
            PreviousPosition = playerPreviousPosition == int.MaxValue ? 0 : playerPreviousPosition,
            NewPosition = newPosition == int.MaxValue ? 0 : newPosition,
            WasChampion = playerTitleHolder,
            IsNowChampion = state.Belts.Get(league.Id, state.PlayerDivision, BeltKind.World).HolderId == player.Id
        };
    }

    private void AssignPoints(CareerState state, League league, Fighter fighter, FightResult result,
        bool won, bool draw, bool titleFight, bool wasChampion)
    {
        var table = state.Rankings.Get(league.Id, fighter.Division);
        var entry = table.Find(fighter.Id);
        if (entry is null)
        {
            entry = new RankingEntry
            {
                FighterId = fighter.Id,
                FighterName = fighter.DisplayName,
                Points = 0,
                Position = 0,
                PreviousPosition = 0
            };
            table.Entries.Add(entry);
        }

        var streakAfter = won ? Math.Max(1, fighter.Streak) : 0;
        var award = RankingCalculator.Award(league, result.Kind, won, draw, titleFight, wasChampion,
            streakAfter, _points);

        entry.Points = Math.Max(0, entry.Points + award);
        entry.Record = fighter.Record.Display;
        entry.Streak = fighter.Streak;
        entry.FighterName = fighter.DisplayName;
    }

    private List<string> UpdateBelts(CareerState state, FightResult result, League league,
        bool titleFight, WeighInResult? weighIn)
    {
        var changes = new List<string>();
        if (!titleFight || result.IsDraw)
        {
            return changes;
        }

        var belt = state.Belts.Get(league.Id, state.PlayerDivision, BeltKind.World);
        var winner = state.FindFighter(result.WinnerId);
        if (winner is null)
        {
            return changes;
        }

        // Missing weight forfeits the right to the belt.
        var playerWon = winner.Id == state.PlayerId;
        if (playerWon && weighIn is { MadeWeight: false })
        {
            changes.Add(Loc.T("belt.missedWeight"));
            return changes;
        }

        if (belt.HolderId == winner.Id)
        {
            belt.RecordDefence();
            changes.Add(Loc.T("belt.defends", winner.ShortName,
                WeightClasses.Get(state.PlayerDivision).Name));
        }
        else
        {
            var previous = belt.HolderId is null ? Loc.T("belt.vacantTitle") :
                state.FindFighter(belt.HolderId)?.ShortName ?? Loc.T("belt.champion");
            belt.Award(winner.Id, winner.DisplayName, state.Day);
            belt.RecordDefence();
            changes.Add(Loc.T("belt.takes", winner.ShortName,
                WeightClasses.Get(state.PlayerDivision).Name, previous));
        }

        var table = state.Rankings.Get(league.Id, state.PlayerDivision);
        table.ChampionId = belt.HolderId;
        return changes;
    }

    private int ComputePurse(League league, FightOffer? offer, bool won, bool draw, bool titleFight,
        WeighInResult? weighIn)
    {
        var basePurse = offer?.Purse ?? league.BasePurse;
        var winBonus = won ? basePurse / 2 : draw ? basePurse / 4 : 0;
        var purse = basePurse + winBonus;

        if (titleFight && won)
        {
            purse += league.TitlePurse / 3;
        }

        if (weighIn is { MadeWeight: false })
        {
            purse = (int)(purse * (1 - weighIn.PurseFinePercent / 100m));
        }

        return Math.Max(0, purse);
    }

    /// <summary>Converts the punishment of a bout into fatigue, damage and injuries.</summary>
    private void ApplyFightToll(CareerState state, Fighter fighter, int damageTaken, int knockdowns, bool stopped)
    {
        fighter.Damage = Math.Min(100, fighter.Damage + damageTaken / 2);
        fighter.Energy = Math.Max(5, fighter.Energy - 40 - damageTaken / 4);

        var rng = new RandomSource(state.Seed + state.Day * 7919 + fighter.Id.GetHashCode(StringComparison.Ordinal));

        if (knockdowns > 0 || damageTaken > 45)
        {
            var severity = knockdowns >= 2 || damageTaken > 80
                ? InjurySeverity.Serious
                : knockdowns == 1 || damageTaken > 55
                    ? InjurySeverity.Moderate
                    : InjurySeverity.Niggle;

            var type = stopped ? InjuryType.Concussion : InjuryType.RibBruising;
            if (rng.NextDouble() < 0.4)
            {
                type = InjuryType.HandContusion;
            }

            var days = Injury.DaysFor(severity);
            fighter.AddInjury(new Injury
            {
                Type = type,
                Severity = severity,
                DaysRemaining = days,
                TotalDays = days
            }, state.Day);
        }
    }

    private League? MaybePromote(CareerState state, bool won, int previousPosition)
    {
        if (!won)
        {
            return null;
        }

        var current = Leagues.Leagues.Get(state.PlayerLeagueId);
        if (current.Prestige >= 5)
        {
            return null;
        }

        var table = state.Rankings.Get(current.Id, state.PlayerDivision);
        var position = table.PositionOf(state.PlayerId);
        var isChampion = table.ChampionId == state.PlayerId;

        var earned = state.BoutsWonAtCurrentLeague >= 2 && position <= 3;

        if (!earned)
        {
            return null;
        }

        var next = Leagues.Leagues.All
            .Where(l => l.Prestige > current.Prestige)
            .OrderBy(l => l.Prestige)
            .FirstOrDefault(l => l.Sanctions(state.PlayerDivision));

        if (next is null)
        {
            return null;
        }

        state.PlayerLeagueId = next.Id;
        state.Player.Division = state.PlayerDivision;
        state.Player.LeagueId = next.Id;
        state.BoutsWonAtCurrentLeague = 0;
        state.Season++;

        // A promoted fighter enters the new league unranked but with reputation.
        var newTable = state.Rankings.Get(next.Id, state.PlayerDivision);
        if (newTable.Find(state.PlayerId) is null)
        {
            newTable.Entries.Add(new RankingEntry
            {
                FighterId = state.PlayerId,
                FighterName = state.Player.DisplayName,
                Points = 10,
                Position = 0,
                PreviousPosition = 0,
                Record = state.Player.Record.Display,
                Streak = state.Player.Streak
            });
        }

        return next;
    }

    /// <summary>
    /// Writes an AI-vs-AI bout into the career: history, records, streaks, ranking
    /// points and - for a championship bout - the belt. Shared by the background
    /// undercard and the tournament cards the player can watch.
    /// </summary>
    public void ApplyBackgroundResult(CareerState state, League league, Fighter a, Fighter b,
        FightResult result)
    {
        state.History.Add(new FightHistoryEntry
        {
            Day = result.Day,
            LeagueId = league.Id,
            Division = a.Division,
            WinnerId = result.WinnerId,
            LoserId = result.LoserId,
            FighterAId = result.FighterAId,
            FighterBId = result.FighterBId,
            Kind = result.Kind,
            Round = result.EndRound,
            TitleFight = result.WasTitleFight,
            Headline = result.Headline,
            Scorecards = result.ScorecardText,
            InvolvesPlayer = false,
            KnockdownsA = result.FighterAKnockdowns,
            KnockdownsB = result.FighterBKnockdowns
        });

        a.LastFightDay = result.Day;
        b.LastFightDay = result.Day;

        if (result.IsDraw)
        {
            a.Record.Draws++;
            b.Record.Draws++;
            a.Streak = 0;
            b.Streak = 0;
        }
        else
        {
            var winner = result.WinnerId == a.Id ? a : b;
            var loser = winner == a ? b : a;

            winner.Record.Wins++;
            loser.Record.Losses++;
            winner.Streak = Math.Max(0, winner.Streak) + 1;
            loser.Streak = Math.Min(0, loser.Streak) - 1;

            if (result.Kind is FightOutcomeKind.Knockout or FightOutcomeKind.TechnicalKnockout)
            {
                winner.Record.KnockoutWins++;
                loser.Record.KnockoutLosses++;
            }

            AssignPoints(state, league, winner, result, true, false, result.WasTitleFight,
                state.Belts.HeldBy(winner.Id).Any());
            AssignPoints(state, league, loser, result, false, false, result.WasTitleFight,
                state.Belts.HeldBy(loser.Id).Any());
        }

        if (result.WasTitleFight)
        {
            UpdateBelts(state, result, league, true, null);
        }

        state.Rankings.Republish(league.Id, a.Division, league.RankedContenders);
    }

    /// <summary>Keeps the rest of the card alive so the ranked list keeps moving.</summary>
    private void SimulateUndercard(CareerState state, League league, RandomSource rng)
    {
        var pool = state.Fighters
            .Where(f => f.LeagueId == league.Id && f.Division == state.PlayerDivision && !f.IsPlayer)
            .ToList();

        if (pool.Count < 2)
        {
            return;
        }

        var bouts = Math.Min(2, pool.Count / 2);
        for (var i = 0; i < bouts; i++)
        {
            var a = pool[rng.Next(0, pool.Count)];
            var b = pool[rng.Next(0, pool.Count)];
            if (a.Id == b.Id)
            {
                continue;
            }

            var result = SimulateBout(a, b, league.Ruleset, rng, false, league.Id, state.Day,
                0.35 + a.Overall / 250.0, 0.35 + b.Overall / 250.0);

            ApplyBackgroundResult(state, league, a, b, result);
        }
    }
}

/// <summary>What a completed bout changed. Drives the post-fight screen.</summary>
public sealed class CareerOutcome
{
    public FightResult Result { get; set; } = new();
    public int Purse { get; set; }
    public bool PlayerWon { get; set; }
    public bool IsDraw { get; set; }
    public bool Promoted { get; set; }
    public string? NewLeagueId { get; set; }
    public List<string> BeltChanges { get; set; } = new();
    public FightOffer? NextFight { get; set; }
    public int Money { get; set; }
    public int RankingPointsGained { get; set; }
    public int PreviousPosition { get; set; }
    public int NewPosition { get; set; }
    public bool WasChampion { get; set; }
    public bool IsNowChampion { get; set; }
}
