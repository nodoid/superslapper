using Microsoft.Xna.Framework;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// Drives the real screens - hub, weigh-in, fight, result - without a human.
/// Used by the platform smoke tests to prove the whole loop works on a device.
/// </summary>
public static class Playthrough
{
    public static int Run(GameContext context, int bouts = 3)
    {
        var log = new List<string>();
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor",
            WeightClassId.Welterweight, 20261026);
        context.State = state;
        context.Career.BookNextFight(state, new RandomSource(7));

        var time = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 30.0));
        var input = new InputState();
        var guard = 0;

        for (var bout = 0; bout < bouts; bout++)
        {
            context.Screens.SetRoot(new HubScreen(context));
            Pump(context, time, input, 4, log);

            context.Screens.Push(new WeighInScreen(context));
            Pump(context, time, input, 4, log);

            // Step on the scale, then enter the ring.
            Tap(context, input, time, new Vector2(970, 935), log, 3);
            Tap(context, input, time, new Vector2(970, 935), log, 3);

            if (context.Screens.Current is not FightScreen)
            {
                log.Add($"FAIL: expected FightScreen, got {context.Screens.Current?.GetType().Name}");
                return Report(log, "PLAYTHROUGH OK");
            }

            log.Add($"bout {bout + 1}: entered the ring against {state.Offer?.OpponentName}");

            var fightFrames = 0;
            while (context.Screens.Current is FightScreen screen && guard++ < 40000)
            {
                input.SimulateIdle();
                PlayFightFrame(screen, input, fightFrames);
                context.Screens.Update(time, input);
                fightFrames++;
            }

            if (context.Screens.Current is not ResultScreen)
            {
                log.Add($"FAIL: fight never finished ({context.Screens.Current?.GetType().Name}, " +
                        $"{fightFrames} frames)");
                return Report(log, "PLAYTHROUGH OK");
            }

            log.Add($"bout {bout + 1}: result = {state.LastResultHeadline} " +
                    $"(record {state.Player.Record.Display}, money {state.Money})");

            Tap(context, input, time, new Vector2(960, 990), log, 6);

            if (context.Screens.Current is not HubScreen)
            {
                log.Add($"FAIL: result did not return to the hub " +
                        $"({context.Screens.Current?.GetType().Name})");
                return Report(log, "PLAYTHROUGH OK");
            }

            // Train for a few days before the next bout.
            context.Screens.Push(new TrainingScreen(context));
            for (var day = 0; day < 4; day++)
            {
                Tap(context, input, time, new Vector2(280, 260), log, 2);
            }

            context.Screens.Pop();
            log.Add($"camp done: energy {state.Player.Energy}, day {state.Day}, " +
                    $"injuries {state.Player.ActiveInjuries.Count()}");
        }

        log.Add("PLAYTHROUGH OK");
        return Report(log, "PLAYTHROUGH OK");
    }

    /// <summary>
    /// One frame of the bout, played the way a person plays it: call the hand, call the
    /// count, then measure - walk the palm onto the cheek and set it - before the ritual
    /// count, and brace when the rival measures. The screen names its own beat in its
    /// view state, so nothing here has to guess where the bout is.
    /// </summary>
    private static void PlayFightFrame(FightScreen screen, InputState input, int frame)
    {
        switch (screen.ViewState.Split('|')[0])
        {
            case "PlayerDeclare":
                // A hand first, then the number it will land on.
                input.SimulateTap(frame % 2 == 0
                    ? new Vector2(700, 748)
                    : new Vector2(1030, 878));
                break;

            case "PlayerMeasure":
                // Walk the measured palm onto the cheek, then set it.
                var view = screen.ViewState;
                input.SimulateTap(view.Contains("zone Temple") ? new Vector2(1590, 902)
                    : view.Contains("zone Jaw") ? new Vector2(730, 902)
                    : new Vector2(1160, 902));
                break;

            case "OpponentCharge":
                input.SimulateTap(new Vector2(955, 808));
                break;
        }
    }

    /// <summary>
    /// Plays a whole season: one year of the career calendar, taking every bout the
    /// league books, training between them and checking the night's card when there
    /// is one. The screens are the real ones - hub, calendar, card, camp, scale,
    /// ring, result - so what it proves is the year a player actually lives, not a
    /// shortcut through the simulation.
    /// </summary>
    public static int RunSeason(GameContext context, int maxBouts = 24, int seasonDays = 365,
        string leagueId = "esc")
    {
        var log = new List<string>();
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor",
            WeightClassId.Welterweight, 20261027, leagueId: leagueId);
        context.State = state;

        var time = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 30.0));
        var input = new InputState();

        var openingLeague = state.PlayerLeagueId;
        var cardsRun = 0;
        var boutsWatched = 0;
        var bouts = 0;

        log.Add($"a season is one year of the calendar: day {state.Day} to {seasonDays}");
        log.Add($"opening in {Leagues.Get(openingLeague).Name}, division " +
                $"{WeightClasses.Get(state.PlayerDivision).Name} (prestige " +
                $"{Leagues.Get(openingLeague).Prestige}, monthly cards " +
                $"{(Leagues.Get(openingLeague).RunsMonthlyCards ? "yes" : "no")})");

        for (; bouts < maxBouts; bouts++)
        {
            var offer = state.Offer ??= context.Career.BookNextFight(state,
                new RandomSource(state.Seed + state.Day));

            context.Screens.SetRoot(new HubScreen(context));
            Pump(context, time, input, 3, log);

            // The calendar, and the night's card when there is one to run.
            context.Screens.Push(new CalendarScreen(context));
            Pump(context, time, input, 2, log);

            var card = TournamentService.UpcomingCard(state) ?? TournamentService.CardOnNight(state);
            if (card is not null)
            {
                context.Screens.Push(new TournamentScreen(context, card));
                Pump(context, time, input, 2, log);

                var others = card.Bouts.Count(b => !b.IsPlayerBout);
                if (card.Bouts.Any(b => !b.IsPlayerBout && !b.IsDecided))
                {
                    Tap(context, input, time, new Vector2(1380, 950), log, 3);

                    var decided = card.Bouts.Count(b => !b.IsPlayerBout && b.IsDecided);
                    if (decided != others)
                    {
                        log.Add($"FAIL: the card left {others - decided} bout(s) unbooked");
                        return Report(log, "SEASON OK");
                    }

                    cardsRun++;
                    boutsWatched += decided;
                }

                context.Screens.Pop();
            }

            context.Screens.Pop();
            if (context.Screens.Current is not HubScreen)
            {
                log.Add($"FAIL: the calendar did not come back to the hub " +
                        $"({context.Screens.Current?.GetType().Name})");
                return Report(log, "SEASON OK");
            }

            // Camp: train while fresh, rest when tired, until fight day.
            context.Screens.Push(new TrainingScreen(context));
            var campDay = state.Day;
            var campGuard = 0;
            while (state.Day < offer.FightDay && campGuard++ < 200)
            {
                Tap(context, input, time,
                    state.Player.Energy < 45 ? new Vector2(280, 950) : new Vector2(280, 260), log, 1);

                if (state.Day == campDay)
                {
                    break; // The session was blocked, so no day passed.
                }

                campDay = state.Day;
            }

            if (state.Day < offer.FightDay)
            {
                log.Add($"FAIL: the camp stalled on day {state.Day} of {offer.FightDay} " +
                        $"(energy {state.Player.Energy})");
                return Report(log, "SEASON OK");
            }

            if (state.Player.Energy is < 0 or > 100)
            {
                log.Add($"FAIL: energy went out of range ({state.Player.Energy})");
                return Report(log, "SEASON OK");
            }

            context.Screens.Pop();

            // The scale, then the ring.
            context.Screens.Push(new WeighInScreen(context));
            Pump(context, time, input, 2, log);
            Tap(context, input, time, new Vector2(970, 935), log, 2);
            Tap(context, input, time, new Vector2(970, 935), log, 2);

            if (context.Screens.Current is not FightScreen)
            {
                log.Add($"FAIL: the weigh-in did not open the ring " +
                        $"({context.Screens.Current?.GetType().Name})");
                return Report(log, "SEASON OK");
            }

            var fightFrames = 0;
            while (context.Screens.Current is FightScreen screen && fightFrames++ < 40000)
            {
                input.SimulateIdle();
                PlayFightFrame(screen, input, fightFrames);
                context.Screens.Update(time, input);
            }

            if (context.Screens.Current is not ResultScreen)
            {
                log.Add($"FAIL: the bout never finished " +
                        $"({context.Screens.Current?.GetType().Name}, {fightFrames} frames)");
                return Report(log, "SEASON OK");
            }

            if (state.Money < 0)
            {
                log.Add($"FAIL: the purse went negative (${state.Money})");
                return Report(log, "SEASON OK");
            }

            log.Add($"bout {bouts + 1}: day {state.Day} in {state.PlayerLeagueId} - " +
                    $"{state.LastResultHeadline} ({state.Player.Record.Display}, ${state.Money})");

            Tap(context, input, time, new Vector2(960, 990), log, 4);
            if (context.Screens.Current is not HubScreen)
            {
                log.Add($"FAIL: the result did not return to the hub " +
                        $"({context.Screens.Current?.GetType().Name})");
                return Report(log, "SEASON OK");
            }

            if (state.PlayerLeagueId != openingLeague)
            {
                log.Add($"promoted to {state.PlayerLeagueId} on day {state.Day}, season {state.Season}");
                openingLeague = state.PlayerLeagueId;
            }

            if (state.Day >= seasonDays)
            {
                log.Add($"SEASON OK: the year ran out on day {state.Day} - " +
                        $"{bouts + 1} bouts, {cardsRun} cards run, {boutsWatched} undercard bouts watched, " +
                        $"record {state.Player.Record.DisplayWithKo}, ${state.Money}, " +
                        $"{state.History.Count(h => !h.InvolvesPlayer)} background bouts in the book");
                return Report(log, "SEASON OK");
            }
        }

        log.Add($"FAIL: the season did not finish within {maxBouts} bouts " +
                $"(day {state.Day}, season {state.Season})");
        return Report(log, "SEASON OK");
    }

    private static void Tap(GameContext context, InputState input, GameTime time, Vector2 position,
        List<string> log, int settleFrames)
    {
        input.SimulateTap(position);
        context.Screens.Update(time, input);
        Pump(context, time, input, settleFrames, log);
    }

    private static void Pump(GameContext context, GameTime time, InputState input, int frames,
        List<string> log)
    {
        for (var i = 0; i < frames; i++)
        {
            input.SimulateIdle();
            context.Screens.Update(time, input);
        }

        _ = log;
    }

    private static int Report(List<string> log, string marker)
    {
        foreach (var line in log)
        {
            Console.WriteLine("[play] " + line);
        }

        var ok = log.Any(line => line.StartsWith(marker, StringComparison.Ordinal));
        Console.WriteLine(ok ? "[play] PASS" : "[play] FAIL");
        return ok ? 0 : 1;
    }
}
