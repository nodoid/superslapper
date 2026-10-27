using Microsoft.Xna.Framework;
using SuperSlapper.Core.Career;
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
                return Report(log);
            }

            log.Add($"bout {bout + 1}: entered the ring against {state.Offer?.OpponentName}");

            var fightFrames = 0;
            while (context.Screens.Current is FightScreen && guard++ < 40000)
            {
                input.SimulateTap(new Vector2(960, 900));
                context.Screens.Update(time, input);
                fightFrames++;

                if (fightFrames % 2 == 0)
                {
                    input.SimulateIdle();
                    context.Screens.Update(time, input);
                }
            }

            if (context.Screens.Current is not ResultScreen)
            {
                log.Add($"FAIL: fight never finished ({context.Screens.Current?.GetType().Name}, " +
                        $"{fightFrames} frames)");
                return Report(log);
            }

            log.Add($"bout {bout + 1}: result = {state.LastResultHeadline} " +
                    $"(record {state.Player.Record.Display}, money {state.Money})");

            Tap(context, input, time, new Vector2(960, 990), log, 6);

            if (context.Screens.Current is not HubScreen)
            {
                log.Add($"FAIL: result did not return to the hub " +
                        $"({context.Screens.Current?.GetType().Name})");
                return Report(log);
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
        return Report(log);
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

    private static int Report(List<string> log)
    {
        foreach (var line in log)
        {
            Console.WriteLine("[play] " + line);
        }

        var ok = log.Contains("PLAYTHROUGH OK");
        Console.WriteLine(ok ? "[play] PASS" : "[play] FAIL");
        return ok ? 0 : 1;
    }
}
