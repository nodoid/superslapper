using Microsoft.Xna.Framework;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// Renders every screen to a PNG so the build can be verified without a human
/// sitting in front of it. Used by the Windows and iPhone smoke tests.
/// </summary>
public sealed class SelfTestRunner
{
    private readonly string _directory;
    private readonly List<(string Name, Action<GameContext> Enter, int Ticks, Vector2? TapAt)> _scenarios;
    private int _index;
    private bool _entered;

    public SelfTestRunner(string directory, Assets assets)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        _scenarios = new List<(string, Action<GameContext>, int, Vector2?)>
        {
            ("01_title", _ => { }, 2, null),
            ("02_create", ctx => ctx.Screens.SetRoot(new CreateCareerScreen(ctx)), 3, null),
            // Taps the real START CAREER button, so a dead button shows up here.
            ("03_new_career", ctx => ctx.Screens.SetRoot(new CreateCareerScreen(ctx)), 8,
                new Vector2(1640, 980)),
            ("04_hub", CreateCareer, 3, null),
            ("05_training", ctx => ctx.Screens.SetRoot(new TrainingScreen(ctx)), 3, null),
            ("06_rankings", ctx => ctx.Screens.SetRoot(new RecordsScreen(ctx, RecordsScreen.Tab.Rankings)), 3, null),
            ("07_belts", ctx => ctx.Screens.SetRoot(new RecordsScreen(ctx, RecordsScreen.Tab.Belts)), 3, null),
            ("08_rules", ctx => ctx.Screens.SetRoot(new RecordsScreen(ctx, RecordsScreen.Tab.Rules)), 3, null),
            ("09_weighin", WeighIn, 3, new Vector2(960, 900)),
            ("10_fight", Fight, 60, new Vector2(960, 900)),
            ("11_result", Result, 4, null),
            ("12_fight_aim", FightAgain, 90, new Vector2(960, 900)),
            ("13_card", Card, 4, null),
            ("14_calendar", ctx => ctx.Screens.SetRoot(new CalendarScreen(ctx)), 4, null),
            // Taps CALENDAR on a tournament card, which has to land on the month view.
            ("15_card_calendar", Card, 8, new Vector2(620, 950))
        };

        _ = assets;
    }

    public bool Finished => _index >= _scenarios.Count;

    public string CurrentName => _scenarios[Math.Min(_index, _scenarios.Count - 1)].Name;

    public bool NeedsRender { get; private set; }

    private static void CreateCareer(GameContext ctx)
    {
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor", WeightClassId.Welterweight, 4242);
        ctx.State = state;
        ctx.Career.BookNextFight(state, new RandomSource(11));
        ctx.Screens.SetRoot(new HubScreen(ctx));
    }

    private static void WeighIn(GameContext ctx)
    {
        ctx.State ??= BuildCareer();
        ctx.Screens.SetRoot(new WeighInScreen(ctx));
    }

    private static CareerState BuildCareer()
    {
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor", WeightClassId.Welterweight, 4242);
        new CareerService().BookNextFight(state, new RandomSource(11));
        return state;
    }

    /// <summary>
    /// A tournament night with the player on top of the bill. Built by hand so
    /// the card screen can be captured from an amateur career, which does not
    /// run monthly cards of its own.
    /// </summary>
    private static void Card(GameContext ctx)
    {
        var state = ctx.State ??= BuildCareer();
        var pool = state.Fighters.Where(f => f.Id != state.PlayerId).Take(4).ToList();
        if (pool.Count < 4)
        {
            return;
        }

        var card = new TournamentCard
        {
            LeagueId = state.PlayerLeagueId,
            Division = state.PlayerDivision,
            Day = state.Day + 7,
            Venue = "The Slaplantic, Leeds"
        };

        card.Bouts.Add(new CardBout
        {
            FighterAId = pool[0].Id, FighterBId = pool[1].Id,
            FighterAName = pool[0].DisplayName, FighterBName = pool[1].DisplayName,
            Slot = CardSlot.Undercard
        });
        card.Bouts.Add(new CardBout
        {
            FighterAId = pool[2].Id, FighterBId = pool[3].Id,
            FighterAName = pool[2].DisplayName, FighterBName = pool[3].DisplayName,
            Slot = CardSlot.CoMain
        });
        card.Bouts.Add(new CardBout
        {
            FighterAId = state.PlayerId, FighterBId = pool[0].Id,
            FighterAName = state.Player.DisplayName, FighterBName = pool[0].DisplayName,
            IsPlayerBout = true, Slot = CardSlot.MainEvent, TitleFight = true
        });

        state.Cards.Add(card);
        ctx.Screens.SetRoot(new TournamentScreen(ctx, card));
    }

    private static void Fight(GameContext ctx)
    {
        ctx.State ??= BuildCareer();
        ctx.Screens.SetRoot(new FightScreen(ctx));
    }

    private static void FightAgain(GameContext ctx)
    {
        ctx.State ??= BuildCareer();
        ctx.Screens.SetRoot(new FightScreen(ctx));
    }

    private static void Result(GameContext ctx)
    {
        var state = ctx.State ??= BuildCareer();
        var service = new CareerService();
        var rng = new RandomSource(99);
        var offer = state.Offer ?? service.BookNextFight(state, rng);
        var opponent = state.FindFighter(offer.OpponentId)!;

        var result = service.SimulateBout(state.Player, opponent,
            SuperSlapper.Core.Leagues.Leagues.Get(state.PlayerLeagueId).Ruleset, rng,
            offer.TitleFight, offer.LeagueId, state.Day, 0.85, 0.4);

        var weighIn = service.WeighIn(state);
        var outcome = service.ApplyResult(state, result, weighIn);
        ctx.Screens.SetRoot(new ResultScreen(ctx, outcome));
    }

    /// <summary>Enters the current scenario and pumps it forward so the frame looks alive.</summary>
    public void Prepare(GameContext ctx)
    {
        if (_entered || Finished)
        {
            return;
        }

        var scenario = _scenarios[_index];

        if (_index == 0)
        {
            ctx.Screens.SetRoot(new TitleScreen(ctx));
        }
        else
        {
            scenario.Enter(ctx);
        }

        var input = new InputState();
        var time = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 20.0));

        for (var i = 0; i < scenario.Ticks; i++)
        {
            // A real touch: press, hold for a few frames, release. The release is
            // the frame the button fires on, so the whole sequence is exercised.
            var down = scenario.Ticks / 2 - 3;
            if (scenario.TapAt is { } tap && i >= down)
            {
                if (i == down)
                {
                    input.SimulatePress(tap);
                }
                else if (i < scenario.Ticks / 2)
                {
                    input.SimulateHold(tap);
                }
                else if (i == scenario.Ticks / 2)
                {
                    input.SimulateRelease(tap);
                }
                else
                {
                    input.SimulateIdle();
                }
            }
            else
            {
                input.SimulateIdle();
            }

            ctx.Screens.Update(time, input);
        }

        _entered = true;
        NeedsRender = true;

        // The screen a scenario lands on is the assertion: a dead button shows
        // up here as the same screen it started on.
        Console.WriteLine($"[selftest] {scenario.Name} -> {ctx.Screens.Current?.GetType().Name}");
    }

    public void Captured()
    {
        NeedsRender = false;
        _entered = false;
        _index++;
    }

    public string PathFor(string name) => Path.Combine(_directory, name + ".png");
}
