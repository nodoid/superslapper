using Microsoft.Xna.Framework;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Units;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;
using LeagueBook = SuperSlapper.Core.Leagues.Leagues;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// Renders every screen to a PNG so the build can be verified without a human
/// sitting in front of it, and checks that each scripted tap lands on the screen
/// it was aimed at. Used by the Windows and iPhone smoke tests; a scenario that
/// does not arrive where it should fails the run.
/// </summary>
public sealed class SelfTestRunner
{
    /// <summary>Ceiling on frames for a scenario that runs until a condition holds.</summary>
    private const int UntilFrameGuard = 20000;

    /// <summary>How many seeds a result scenario may try before giving up.</summary>
    private const int OutcomeSearchLimit = 400;

    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 20.0);

    private sealed class Scenario
    {
        public required string Name { get; init; }

        /// <summary>Puts the screen under test on the stack.</summary>
        public required Action<GameContext> Enter { get; init; }

        /// <summary>The screen the run has to be showing once the ticks are up.</summary>
        public required string Expected { get; init; }

        /// <summary>Frames to pump for a fixed-length scenario.</summary>
        public int Ticks { get; init; } = 3;

        /// <summary>Real touches, one press-hold-release each, at these frames.</summary>
        public IReadOnlyList<(int Frame, Vector2 Position)> Taps { get; init; } =
            Array.Empty<(int, Vector2)>();

        /// <summary>Pump - tapping here every frame - until this says stop.</summary>
        public Func<GameContext, bool>? Until { get; init; }

        /// <summary>Tap position for <see cref="Until"/> mode.</summary>
        public Vector2 TapAt { get; init; } = new(960, 900);

        /// <summary>
        /// Points tapped in turn, one a frame, for a screen that wants more than one
        /// call before it moves on. The bout is the one that does: a hand, then a count.
        /// </summary>
        public IReadOnlyList<Vector2> TapPoints { get; init; } = Array.Empty<Vector2>();

        /// <summary>Extra assertion: a failure message, or null when it holds.</summary>
        public Func<GameContext, string?>? Verify { get; init; }

        /// <summary>Puts back anything global the scenario changed, once it is checked.</summary>
        public Action<GameContext>? After { get; init; }
    }

    private readonly string _directory;
    private readonly List<Scenario> _scenarios;
    private readonly List<string> _failures = new();
    private readonly List<string> _sweepProblems = new();
    private TournamentCard? _stagedCard;

    /// <summary>Who threw first in round one, kept so round two can be held to it.</summary>
    private static string _openingStriker = "unknown";
    private int _index;
    private bool _entered;
    private int _swept;
    private int _acted;
    private int _inert;
    private int _dead;
    private int _buttonsSwept;
    private int _buttonsActed;
    private int _regionsSwept;
    private int _regionsActed;

    public SelfTestRunner(string directory, Assets assets)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        // Captured by the scenarios below so each can assert the branch it was
        // built to reach.
        CareerOutcome? win = null;
        CareerOutcome? loss = null;
        CareerOutcome? draw = null;

        _scenarios = new List<Scenario>
        {
            new()
            {
                Name = "01_title", Enter = ctx => ctx.Screens.SetRoot(new TitleScreen(ctx)),
                Expected = nameof(TitleScreen)
            },
            new()
            {
                Name = "02_create", Enter = ctx => ctx.Screens.SetRoot(new CreateCareerScreen(ctx)),
                Expected = nameof(CreateCareerScreen)
            },
            // Taps the real START CAREER button, so a dead button shows up here.
            new()
            {
                Name = "03_new_career", Enter = ctx => ctx.Screens.SetRoot(new CreateCareerScreen(ctx)),
                Expected = nameof(HubScreen), Ticks = 8, Taps = Tap(4, 1640, 980)
            },
            new()
            {
                Name = "04_hub", Enter = CreateCareer, Expected = nameof(HubScreen),
                Verify = ctx => ctx.Music.Track == "music_menu"
                    ? null
                    : $"the hub should play the menu bed, not {ctx.Music.Track ?? "silence"}"
            },
            new()
            {
                Name = "05_training", Enter = ctx => ctx.Screens.SetRoot(new TrainingScreen(ctx)),
                Expected = nameof(TrainingScreen)
            },
            // A real session, so the camp log has to gain a day.
            new()
            {
                Name = "06_training_session",
                Enter = ctx => ctx.Screens.SetRoot(new TrainingScreen(ctx)),
                Expected = nameof(TrainingScreen), Ticks = 8, Taps = Tap(4, 280, 260),
                Verify = ctx => ctx.State!.TrainingLog.Count > 0
                    ? null
                    : "picking a drill logged no session"
            },
            new()
            {
                Name = "07_rankings",
                Enter = ctx => ctx.Screens.SetRoot(new RecordsScreen(ctx, RecordsScreen.Tab.Rankings)),
                Expected = nameof(RecordsScreen)
            },
            new()
            {
                Name = "08_belts",
                Enter = ctx => ctx.Screens.SetRoot(new RecordsScreen(ctx, RecordsScreen.Tab.Belts)),
                Expected = nameof(RecordsScreen)
            },
            new()
            {
                Name = "09_rules",
                Enter = ctx => ctx.Screens.SetRoot(new RecordsScreen(ctx, RecordsScreen.Tab.Rules)),
                Expected = nameof(RecordsScreen)
            },
            // The book opens from the title screen, before a career exists: the
            // tabbed screen has to fall back to its reference world.
            new()
            {
                Name = "10_rules_from_title",
                Enter = ctx =>
                {
                    ctx.State = null;
                    ctx.Screens.SetRoot(new TitleScreen(ctx));
                },
                // The title screen only arms its buttons once the logo has settled.
                Expected = nameof(RecordsScreen), Ticks = 16, Taps = Tap(10, 300, 850),
                Verify = ctx => ctx.State is null ? null : "the reference world became the career"
            },
            // Every screen has to draw its no-career stand-in instead of throwing.
            new()
            {
                Name = "11_no_career_hub", Enter = NoCareer(ctx => new HubScreen(ctx)),
                Expected = nameof(HubScreen)
            },
            new()
            {
                Name = "12_no_career_training", Enter = NoCareer(ctx => new TrainingScreen(ctx)),
                Expected = nameof(TrainingScreen)
            },
            new()
            {
                Name = "13_no_career_weighin", Enter = NoCareer(ctx => new WeighInScreen(ctx)),
                Expected = nameof(WeighInScreen)
            },
            new()
            {
                Name = "14_no_career_fight", Enter = NoCareer(ctx => new FightScreen(ctx)),
                Expected = nameof(FightScreen)
            },
            new()
            {
                Name = "15_no_career_calendar", Enter = NoCareer(ctx => new CalendarScreen(ctx)),
                Expected = nameof(CalendarScreen)
            },
            new()
            {
                Name = "16_no_career_card",
                Enter = NoCareer(ctx => new TournamentScreen(ctx, new TournamentCard())),
                Expected = nameof(TournamentScreen)
            },
            // Morning of the bout: step on the scale, then walk into the ring.
            new()
            {
                Name = "17_weighin", Enter = WeighIn, Expected = nameof(WeighInScreen),
                Ticks = 7, Taps = Tap(3, 960, 940)
            },
            new()
            {
                Name = "18_weighin_ring", Enter = WeighIn, Expected = nameof(FightScreen),
                Ticks = 13, Taps = Taps((3, 960, 940), (8, 960, 940))
            },
            new()
            {
                Name = "19_fight", Enter = Fight, Expected = nameof(FightScreen),
                Ticks = 130, Taps = Tap(30, 960, 900),
                Verify = ctx => ctx.Music.Track == "music_fight"
                    ? null
                    : $"the bout should play the fight track, not {ctx.Music.Track ?? "silence"}"
            },
            new()
            {
                Name = "20_fight_aim", Enter = Fight, Expected = nameof(FightScreen),
                Ticks = 90, Taps = Tap(45, 960, 900)
            },
            // The call that has to come before a slap: the hand chips first, then the
            // count chips once a hand has been named.
            new()
            {
                Name = "20a_fight_call_hand",
                Enter = ctx =>
                {
                    Fight(ctx);
                    AdvanceUntil(ctx, "PlayerDeclare", 4000);
                },
                Expected = nameof(FightScreen), Ticks = 2
            },
            new()
            {
                Name = "20b_fight_call_count",
                Enter = ctx =>
                {
                    Fight(ctx);
                    AdvanceUntil(ctx, "PlayerDeclare", 4000);
                    Press(ctx, new Rectangle(460, 700, 480, 96));
                },
                Expected = nameof(FightScreen), Ticks = 2
            },
            // The ring's round card, which opens every round.
            new()
            {
                Name = "20e_fight_round",
                Enter = ctx =>
                {
                    Fight(ctx);
                    AdvanceUntil(ctx, "RoundPlacard", 4000);
                },
                Expected = nameof(FightScreen), Ticks = 20,
                Verify = ctx => ctx.Screens.Current?.ViewState.Contains("RoundPlacard") == true
                    ? null
                    : $"the round was never announced ({ctx.Screens.Current?.ViewState})"
            },
            // The rival measures too: his palm goes on your cheek before his count.
            new()
            {
                Name = "20g_fight_rival_measure",
                Enter = ctx =>
                {
                    Fight(ctx);
                    AdvanceUntil(ctx, "OpponentCharge", 4000);
                },
                Expected = nameof(FightScreen), Ticks = 10
            },
            // The measure: the palm goes on the other man's cheek and has to be walked
            // onto the mark before anything is thrown.
            new()
            {
                Name = "20c_fight_measure",
                Enter = ctx =>
                {
                    Fight(ctx);
                    CallCount(ctx);
                },
                Expected = nameof(FightScreen), Ticks = 10,
                Verify = ctx => ctx.Screens.Current?.ViewState.Contains("PlayerMeasure") == true
                    ? null
                    : $"the measure did not open ({ctx.Screens.Current?.ViewState})"
            },
            // The ritual count: every number draws the arm back and puts the palm back
            // on the cheek, and the called number is the one that goes through.
            new()
            {
                Name = "20d_fight_practice",
                Enter = ctx =>
                {
                    Fight(ctx);
                    CallCount(ctx, count: 3);
                    Press(ctx, new Rectangle(940, 852, 440, 100));
                    Settle(ctx, 8);
                },
                Expected = nameof(FightScreen), Ticks = 2,
                Verify = ctx => ctx.Screens.Current?.ViewState.Contains("PlayerCount") == true
                    ? null
                    : $"the count did not follow the measure ({ctx.Screens.Current?.ViewState})"
            },
            // The called number, going through.
            new()
            {
                Name = "20f_fight_slap",
                Enter = ctx =>
                {
                    Fight(ctx);
                    CallCount(ctx);
                    Press(ctx, new Rectangle(940, 852, 440, 100));
                    Settle(ctx, 14);
                },
                Expected = nameof(FightScreen), Ticks = 2
            },
            // The bell between rounds: the scores go up while the round that was just
            // fought is still the one named, not the round about to start.
            new()
            {
                Name = "20h_fight_round_break", Enter = Fight, Expected = nameof(FightScreen),
                Until = ctx => ctx.Screens.Current?.ViewState.StartsWith("RoundBreak|", StringComparison.Ordinal) == true,
                TapPoints = new[]
                {
                    new Vector2(700, 748), new Vector2(1030, 878),
                    new Vector2(1160, 902), new Vector2(955, 808)
                },
                Verify = ctx => RoundBreakProblem(ctx)
            },
            // A round later: the bell has to hand over to round two and round two has
            // to open on the same man who opened round one, however round one went.
            new()
            {
                Name = "20i_fight_round_two",
                Enter = ctx =>
                {
                    Fight(ctx);
                    _openingStriker = StrikerOf(ctx);
                },
                Expected = nameof(FightScreen),
                Until = ctx => RoundOf(ctx) >= 2,
                TapPoints = new[]
                {
                    new Vector2(700, 748), new Vector2(1030, 878),
                    new Vector2(1160, 902), new Vector2(955, 808)
                },
                Verify = ctx => RoundTwoProblem(ctx)
            },
            // Plays the bout out to its verdict. Every beat of the fight - charge,
            // aim, defence, count, replay - has to end with a result on screen.
            new()
            {
                Name = "21_fight_verdict", Enter = Fight, Expected = nameof(ResultScreen),
                Until = ctx => ctx.Screens.Current is not FightScreen,
                TapPoints = new[]
                {
                    new Vector2(700, 748), new Vector2(1030, 878),
                    new Vector2(1160, 902), new Vector2(955, 808)
                }
            },
            // The three ways a bout gets written up. The win is pumped past the
            // belt reveal, which is the last thing the screen draws.
            new()
            {
                Name = "22_result_win", Enter = ctx => win = PlayOutFor(ctx, r => r.WonBy(ctx.State!.PlayerId), 0.7, 0.45),
                Expected = nameof(ResultScreen), Ticks = 48,
                Verify = ctx => win is { PlayerWon: true }
                    ? ctx.Music.Track == "music_victory" ? null : "a win should play the victory track"
                    : "the win branch did not render"
            },
            new()
            {
                Name = "23_result_loss",
                Enter = ctx => loss = PlayOutFor(ctx,
                    r => !r.IsDraw && r.WinnerId != ctx.State!.PlayerId, 0.15, 0.95),
                Expected = nameof(ResultScreen), Ticks = 8,
                Verify = ctx => loss is { PlayerWon: false }
                    ? ctx.Music.Track == "music_menu" ? null : "a loss should fall back to the menu bed"
                    : "the loss branch did not render"
            },
            new()
            {
                Name = "24_result_draw", Enter = ctx => draw = PlayOutFor(ctx, r => r.IsDraw, 0.5, 0.5),
                Expected = nameof(ResultScreen), Ticks = 8,
                Verify = _ => draw is { IsDraw: true } ? null : "the draw branch did not render"
            },
            // One night's card with the player on top of the bill.
            new() { Name = "25_card", Enter = Card(0), Expected = nameof(TournamentScreen) },
            // Running the rest of the card has to decide every bout that is not the player's.
            new()
            {
                Name = "26_card_results", Enter = Card(0), Expected = nameof(TournamentScreen),
                Ticks = 10, Taps = Tap(4, 1380, 950),
                Verify = _ => CardReplayProblem(_stagedCard)
            },
            // The player's own bout is on the card, so the card offers the weigh-in.
            new()
            {
                Name = "27_card_weigh_in", Enter = Card(1), Expected = nameof(WeighInScreen),
                Ticks = 10, Taps = Tap(4, 1730, 950)
            },
            // Taps CALENDAR on a tournament card, which has to land on the month view.
            new()
            {
                Name = "28_card_calendar", Enter = Card(0), Expected = nameof(CalendarScreen),
                Ticks = 10, Taps = Tap(4, 620, 950)
            },
            // Calendar: months, day cells and the way back to the card.
            new()
            {
                Name = "29_calendar_month", Enter = Calendar, Expected = nameof(CalendarScreen),
                Ticks = 10, Taps = Taps((3, 1142, 78), (8, 1142, 78))
            },
            new()
            {
                Name = "30_calendar_day", Enter = Calendar, Expected = nameof(CalendarScreen),
                Ticks = 10, Taps = Tap(4, 400, 400)
            },
            new()
            {
                Name = "31_calendar_open_card", Enter = CalendarWithCard, Expected = nameof(TournamentScreen),
                Ticks = 10, Taps = Tap(4, 1380, 950)
            },
            // A career already on disk, so the title screen offers CONTINUE. The
            // automated runs point the repository at their own scratch folder, so
            // this can never be the player's save.
            new()
            {
                Name = "32_title_continue",
                Enter = ctx =>
                {
                    var saved = WorldGenerator.Create("Saved", "Doyle", "The Bookmark",
                        WeightClassId.Welterweight, 777);
                    ctx.Career.BookNextFight(saved, new RandomSource(5));
                    ctx.Repository.Save(saved);
                    ctx.Screens.SetRoot(new TitleScreen(ctx));
                },
                Expected = nameof(HubScreen), Ticks = 16, Taps = Tap(10, 300, 750),
                Verify = ctx => ctx.State?.Player.LastName == "Doyle"
                    ? null
                    : "CONTINUE did not open the saved career"
            },
            // The music control steps NORMAL to LOUD to OFF, and the music follows
            // while the effects level is left alone.
            new()
            {
                Name = "33_music_off",
                Enter = ctx => ctx.Screens.SetRoot(new TitleScreen(ctx)),
                Expected = nameof(TitleScreen), Ticks = 24, Taps = Taps((10, 1710, 82), (16, 1710, 82)),
                Verify = MusicOffProblem
            },
            new()
            {
                Name = "34_music_restore",
                Enter = ctx => ctx.Screens.SetRoot(new TitleScreen(ctx)),
                Expected = nameof(TitleScreen), Ticks = 24, Taps = Taps((10, 1710, 82), (16, 1710, 82)),
                Verify = ctx => ctx.Settings.Music.Label == "NORMAL" && ctx.Music.Volume > 0f
                    ? null
                    : $"the music control did not restore the music (level {ctx.Settings.Music.Label})"
            },
            // The effects have their own chip, on the title screen and on the hub.
            new()
            {
                Name = "35_effects_off",
                Enter = ctx => ctx.Screens.SetRoot(new TitleScreen(ctx)),
                Expected = nameof(TitleScreen), Ticks = 24, Taps = Taps((10, 1710, 182), (16, 1710, 182)),
                Verify = EffectsOffProblem
            },
            // The hub carries the same pair, so either can be turned down mid-career.
            new()
            {
                Name = "36_hub_sound", Enter = CreateCareer, Expected = nameof(HubScreen),
                Ticks = 20, Taps = Taps((4, 1070, 57), (12, 820, 57)),
                Verify = HubSoundProblem
            },
            // The one scenario that writes a save. It goes through the game's own
            // persist path, from a tap on a screen, and reads it back field by field.
            new()
            {
                Name = "37_save_round_trip",
                Enter = ctx =>
                {
                    ctx.State = BuildCareer();
                    ctx.PersistEnabled = true;
                    ctx.Repository.Delete();
                    ctx.Screens.SetRoot(new TrainingScreen(ctx));
                },
                Expected = nameof(TrainingScreen), Ticks = 8, Taps = Tap(4, 280, 260),
                Verify = RoundTripProblem,
                After = ctx => ctx.PersistEnabled = false
            },
            // The settings sheet, opened from the hub: it takes a level from each
            // row and a set of units, and writes them where the next launch looks.
            new()
            {
                Name = "38_settings",
                Enter = ctx =>
                {
                    ctx.State ??= BuildCareer();
                    ctx.PersistEnabled = true;
                    ctx.Screens.SetRoot(new HubScreen(ctx));
                },
                Expected = nameof(SettingsScreen), Ticks = 36,
                Taps = Taps((4, 1680, 57), (12, 1040, 277), (20, 1640, 427), (28, 1040, 577)),
                Verify = SettingsProblem,
                After = ctx =>
                {
                    // Leave the scratch settings as the harness found them.
                    ctx.SetUnits(WeightUnit.Metric);
                    ctx.PersistEnabled = false;
                }
            },
            // A tab that is not the one showing has to take the book, and the chips
            // have to be rebuilt so the frame moves to the page that is now open.
            new()
            {
                Name = "39_records_rules_tab",
                Enter = ctx =>
                {
                    ctx.State ??= BuildCareer();
                    ctx.Screens.SetRoot(new RecordsScreen(ctx, RecordsScreen.Tab.Rankings));
                },
                Expected = nameof(RecordsScreen), Ticks = 14, Taps = Tap(6, 870, 192),
                Verify = ctx => ctx.Screens.Current is RecordsScreen { ViewState: var view }
                    && view.StartsWith("Rules", StringComparison.Ordinal)
                    ? null
                    : $"the RULES tab left the book on {ctx.Screens.Current?.ViewState ?? "nothing"}"
            },
            // The book closes from its own BACK button, back onto the screen under it.
            new()
            {
                Name = "40_records_back",
                Enter = ctx =>
                {
                    ctx.State ??= BuildCareer();
                    ctx.Screens.SetRoot(new HubScreen(ctx));
                    ctx.Screens.Push(new RecordsScreen(ctx, RecordsScreen.Tab.Belts));
                },
                Expected = nameof(HubScreen), Ticks = 14, Taps = Tap(6, 1740, 950)
            },
            // On the morning of the bout the camp's way out is the scales.
            new()
            {
                Name = "41_training_fight_day",
                Enter = ctx =>
                {
                    var state = ctx.State ??= BuildCareer();
                    if (state.Offer is { } offer)
                    {
                        state.Day = offer.FightDay;
                    }

                    ctx.Screens.SetRoot(new HubScreen(ctx));
                    ctx.Screens.Push(new TrainingScreen(ctx));
                },
                Expected = nameof(WeighInScreen), Ticks = 14, Taps = Tap(6, 660, 950)
            },
            // Every control on every screen, pressed and checked. A button that
            // changes nothing - not the screen, the career or the settings - fails
            // here, which is the point of the run.
            new()
            {
                Name = "42_buttons",
                Enter = ctx =>
                {
                    SweepButtons(ctx);

                    // Leave a real screen up for the capture and for the check below.
                    ctx.State = FreshCareer();
                    ctx.Screens.SetRoot(new HubScreen(ctx));
                },
                Expected = nameof(HubScreen), Ticks = 4,
                Verify = ctx => _sweepProblems.Count == 0
                    ? null
                    : string.Join("; ", _sweepProblems.Take(6)) +
                      (_sweepProblems.Count > 6 ? $" (+{_sweepProblems.Count - 6} more)" : string.Empty),
                After = ctx =>
                {
                    // The sweep presses CONTINUE and the unit chips, so put the save
                    // and the two volume levels back the way it found them.
                    ctx.Repository.Delete();
                    ctx.SetLevel(AudioChannel.Music, 2);
                    ctx.SetLevel(AudioChannel.Effects, 2);
                    ctx.SetUnits(WeightUnit.Metric);
                }
            },
            // Every word the run put on screen has to have come out of the
            // catalogue. A key that resolved to itself is a missing string.
            new()
            {
                Name = "43_strings",
                Enter = ctx =>
                {
                    ctx.State = FreshCareer();
                    ctx.Screens.SetRoot(new HubScreen(ctx));
                },
                Expected = nameof(HubScreen), Ticks = 2,
                Verify = _ => Loc.Missing.Count == 0
                    ? null
                    : $"missing strings: {string.Join(", ", Loc.Missing.Take(8))}"
            }
        };

        _ = assets;
    }

    public bool Finished => _index >= _scenarios.Count;

    public string CurrentName => _scenarios[Math.Min(_index, _scenarios.Count - 1)].Name;

    public bool NeedsRender { get; private set; }

    /// <summary>Scenarios that did not land where they were aimed. Zero is a pass.</summary>
    public int FailureCount => _failures.Count;

    /// <summary>
    /// Running the rest of the card has to leave a usable record behind: every
    /// other bout decided and replayable, and the player's own bout left as a bare
    /// result, because that one is fought rather than watched. The replay is what a
    /// spectator screen would redraw the bout from, so it has to match the result.
    /// </summary>
    private static string? CardReplayProblem(TournamentCard? card)
    {
        if (card is null)
        {
            return "no card was staged";
        }

        foreach (var bout in card.Bouts)
        {
            var billing = $"{bout.FighterAName} vs {bout.FighterBName}";

            if (bout.IsPlayerBout)
            {
                if (bout.Replay is not null)
                {
                    return "the player's own bout should not have a recorded replay";
                }

                continue;
            }

            if (!bout.IsDecided || bout.Result is null)
            {
                return $"running the card left {billing} unbooked";
            }

            if (!bout.Watched)
            {
                return $"{billing} was not marked as watched";
            }

            var replay = bout.Replay;
            if (replay is null)
            {
                return $"{billing} was left with no replay to watch";
            }

            if (!ReferenceEquals(replay.Result, bout.Result))
            {
                return $"{billing}: the replay and the card disagree about the result";
            }

            if (replay.FighterAId != bout.FighterAId || replay.FighterBId != bout.FighterBId)
            {
                return $"{billing}: the replay is of different fighters";
            }

            if (replay.Events.Count == 0 || replay.Events[0].Kind != BoutEventKind.RoundStart)
            {
                return $"{billing}: the replay does not start at the opening bell";
            }

            if (!replay.Events.Any(e => e.Kind == BoutEventKind.Slap))
            {
                return $"{billing}: the replay records no slaps";
            }

            var knockdowns = replay.Events.Count(e => e.Kind == BoutEventKind.Knockdown);
            var claimed = bout.Result.FighterAKnockdowns + bout.Result.FighterBKnockdowns;
            if (knockdowns != claimed)
            {
                return $"{billing}: the replay records {knockdowns} knockdowns, the result says {claimed}";
            }

            if (replay.Events.Any(e => e.Round < 1 || e.Round > replay.Result.EndRound))
            {
                return $"{billing}: the replay has a beat outside the rounds that were fought";
            }

            if (replay.Events.Any(e => e.HealthA is < 0 or > 100 || e.HealthB is < 0 or > 100 ||
                                       e.StaminaA is < 0 or > 100 || e.StaminaB is < 0 or > 100))
            {
                return $"{billing}: the replay has a condition reading out of range";
            }
        }

        return null;
    }

    /// <summary>The effects level has to be somewhere it is expected to be, or say why not.</summary>
    private static string? EffectsUntouched(GameContext ctx, string expected) =>
        ctx.Settings.Effects.Label == expected
            ? null
            : $"the music control moved the effects level to {ctx.Settings.Effects.Label}";

    /// <summary>
    /// The music chip on the title screen: two taps take it from NORMAL to LOUD to
    /// OFF, and the effects level is left where it was.
    /// </summary>
    private static string? MusicOffProblem(GameContext ctx)
    {
        if (ctx.Settings.Music.Label != "OFF" || ctx.Music.Volume != 0f)
        {
            return $"the music chip did not silence the music (level {ctx.Settings.Music.Label}, " +
                   $"volume {ctx.Music.Volume})";
        }

        return EffectsUntouched(ctx, "NORMAL");
    }

    /// <summary>
    /// The effects chip: two taps take it from NORMAL to LOUD to OFF, and the music
    /// keeps playing.
    /// </summary>
    private static string? EffectsOffProblem(GameContext ctx)
    {
        if (ctx.Settings.Effects.Label != "OFF" || ctx.Assets.EffectsVolume != 0f)
        {
            return $"the effects chip did not silence them (level {ctx.Settings.Effects.Label}, " +
                   $"volume {ctx.Assets.EffectsVolume})";
        }

        return ctx.Music.Volume > 0f ? null : "silencing the effects also silenced the music";
    }

    /// <summary>
    /// The hub's pair: its effects chip steps OFF up to LOW and its music chip NORMAL
    /// up to LOUD, and both levels reach the things that play sound.
    /// </summary>
    private static string? HubSoundProblem(GameContext ctx)
    {
        if (ctx.Settings.Effects.Label != "LOW" || ctx.Assets.EffectsVolume != 0.15f)
        {
            return $"the hub effects chip should have stepped to LOW, it is " +
                   $"{ctx.Settings.Effects.Label} ({ctx.Assets.EffectsVolume})";
        }

        if (ctx.Settings.Music.Label != "LOUD" || ctx.Music.Volume != 0.6f)
        {
            return $"the hub music chip should have stepped to LOUD, it is " +
                   $"{ctx.Settings.Music.Label} ({ctx.Music.Volume})";
        }

        return null;
    }

    /// <summary>
    /// The settings sheet: every choice has to land on the game, and be written
    /// where the next launch would read it back.
    /// </summary>
    private static string? SettingsProblem(GameContext ctx)
    {
        if (ctx.Settings.Music.Label != "LOW" || ctx.Music.Volume != 0.15f)
        {
            return $"the sheet did not set the music to LOW (it is {ctx.Settings.Music.Label})";
        }

        if (ctx.Settings.Effects.Label != "LOUD" || ctx.Assets.EffectsVolume != 0.6f)
        {
            return $"the sheet did not set the effects to LOUD (it is {ctx.Settings.Effects.Label})";
        }

        if (ctx.Units != WeightUnit.Imperial || ctx.Settings.UnitsLabel != "POUNDS")
        {
            return $"the sheet did not switch the units to pounds (they are {ctx.Settings.UnitsLabel})";
        }

        var written = PlayerSettings.Load(ctx.SavePath);
        if (written.Music.Label != "LOW" || written.Effects.Label != "LOUD" ||
            written.Units != WeightUnit.Imperial)
        {
            return $"the choices were not written down (music {written.Music.Label}, " +
                   $"effects {written.Effects.Label}, units {written.Units})";
        }

        return null;
    }

    /// <summary>
    /// Compares the career on disk with the one on screen. This is the whole save
    /// path as the game walks it: a tap, the screen's own persist call, the
    /// repository and the file.
    /// </summary>
    /// <summary>
    /// The end-of-round screen has to name the round the bout just fought. The card
    /// that follows names the next one; naming it a bell early is the bug this
    /// guards against.
    /// </summary>
    private static string? RoundBreakProblem(GameContext ctx)
    {
        var state = ctx.Screens.Current?.ViewState ?? "";

        if (!state.StartsWith("RoundBreak|", StringComparison.Ordinal))
        {
            return $"the first bell never came ({state})";
        }

        var parts = state.Split('|');
        return parts.Length > 3 && parts[3] == "1"
            ? null
            : $"the first break should still name round one ({state})";
    }

    /// <summary>The bout's round number, as the fight screen reports it.</summary>
    private static int RoundOf(GameContext ctx)
    {
        var parts = (ctx.Screens.Current?.ViewState ?? "").Split('|');
        return parts.Length > 3 && int.TryParse(parts[3], out var round) ? round : 0;
    }

    /// <summary>The man the engine would have slap next: "you" or "rival".</summary>
    private static string StrikerOf(GameContext ctx) =>
        (ctx.Screens.Current?.ViewState ?? "").Split('|')
            .FirstOrDefault(part => part.StartsWith("striker ", StringComparison.Ordinal))
            ?.Substring("striker ".Length) ?? "unknown";

    /// <summary>
    /// Round two has to be round two on the clock, and it has to open on the same
    /// man round one did: the order is set once at the toss, not flipped each bell.
    /// </summary>
    private static string? RoundTwoProblem(GameContext ctx)
    {
        var state = ctx.Screens.Current?.ViewState ?? "";

        if (RoundOf(ctx) < 2)
        {
            return $"the bout never reached round two ({state})";
        }

        var striker = StrikerOf(ctx);
        return striker == _openingStriker
            ? null
            : $"round two opens on the {striker}, but round one opened on the {_openingStriker} ({state})";
    }

    private static string? RoundTripProblem(GameContext ctx)
    {
        var state = ctx.State!;

        if (state.TrainingLog.Count == 0)
        {
            return "the session never happened, so there was nothing to save";
        }

        if (!ctx.Repository.Exists())
        {
            return "training a day wrote no save";
        }

        var loaded = ctx.Repository.Load();
        if (loaded is null)
        {
            return "the save could not be read back";
        }

        if (loaded.PlayerId != state.PlayerId || loaded.Player.LastName != state.Player.LastName)
        {
            return "the loaded career is a different fighter";
        }

        if (loaded.Day != state.Day || loaded.Money != state.Money)
        {
            return $"the loaded career is on day {loaded.Day} with ${loaded.Money}, " +
                   $"the screen shows day {state.Day} with ${state.Money}";
        }

        if (loaded.Player.Energy != state.Player.Energy || loaded.Player.Damage != state.Player.Damage)
        {
            return "the loaded condition does not match the fighter on screen";
        }

        if (loaded.Player.Record.Display != state.Player.Record.Display)
        {
            return "the loaded record does not match";
        }

        if (loaded.TrainingLog.Count != state.TrainingLog.Count)
        {
            return $"the loaded camp log has {loaded.TrainingLog.Count} sessions, " +
                   $"the career has {state.TrainingLog.Count}";
        }

        if (loaded.Offer?.FightDay != state.Offer?.FightDay ||
            loaded.Offer?.OpponentId != state.Offer?.OpponentId)
        {
            return "the loaded bout does not match";
        }

        return null;
    }

    private static (int, Vector2)[] Tap(int frame, float x, float y) =>
        new[] { (frame, new Vector2(x, y)) };

    private static (int, Vector2)[] Taps(params (int Frame, float X, float Y)[] taps) =>
        taps.Select(t => (t.Frame, new Vector2(t.X, t.Y))).ToArray();

    /// <summary>A career on the opening day, with the next bout already booked.</summary>
    private static void CreateCareer(GameContext ctx)
    {
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor",
            WeightClassId.Welterweight, 4242);
        ctx.State = state;
        ctx.Career.BookNextFight(state, new RandomSource(11));
        ctx.Screens.SetRoot(new HubScreen(ctx));
    }

    private static CareerState BuildCareer()
    {
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor",
            WeightClassId.Welterweight, 4242);
        new CareerService().BookNextFight(state, new RandomSource(11));
        return state;
    }

    private static void WeighIn(GameContext ctx)
    {
        ctx.State ??= BuildCareer();
        ctx.Screens.SetRoot(new WeighInScreen(ctx));
    }

    private static void Fight(GameContext ctx)
    {
        ctx.State ??= BuildCareer();
        ctx.Screens.SetRoot(new FightScreen(ctx));
    }

    private static void Calendar(GameContext ctx)
    {
        ctx.State ??= BuildCareer();
        ctx.Screens.SetRoot(new CalendarScreen(ctx));
    }

    /// <summary>The calendar with the player's card already staged in the month on screen.</summary>
    private void CalendarWithCard(GameContext ctx)
    {
        var state = ctx.State ??= BuildCareer();
        _stagedCard = StageCard(state, state.Day);
        ctx.Screens.SetRoot(new CalendarScreen(ctx));
    }

    /// <summary>
    /// A screen opened with no career at all. Each one is supposed to draw its
    /// stand-in panel rather than throw, which is what these scenarios check.
    /// </summary>
    private static Action<GameContext> NoCareer(Func<GameContext, Screen> screen) =>
        ctx =>
        {
            ctx.State = null;
            ctx.Screens.SetRoot(screen(ctx));
        };

    /// <summary>
    /// A tournament night for the player's league and division. Built by hand
    /// because a fresh amateur career does not run monthly cards of its own.
    /// </summary>
    /// <param name="onOfferDay">
    /// 0 to stage it a week out, 1 to put it on the day of the booked bout, which
    /// is what makes the card offer the weigh-in.
    /// </param>
    private Action<GameContext> Card(int onOfferDay) => ctx =>
    {
        var state = ctx.State ??= BuildCareer();
        var day = onOfferDay == 1 ? state.Offer?.FightDay ?? state.Day : state.Day;
        _stagedCard = StageCard(state, day);
        ctx.Screens.SetRoot(new TournamentScreen(ctx, _stagedCard));
    };

    /// <summary>Puts a night's card on the player's own league and division.</summary>
    private static TournamentCard StageCard(CareerState state, int day)
    {
        var pool = state.Fighters
            .Where(f => f.Id != state.PlayerId && f.LeagueId == state.PlayerLeagueId &&
                        f.Division == state.PlayerDivision)
            .Take(4)
            .ToList();

        var card = new TournamentCard
        {
            LeagueId = state.PlayerLeagueId,
            Division = state.PlayerDivision,
            Day = day,
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
        return card;
    }

    /// <summary>
    /// Renders a result screen for a bout that ends the way the scenario needs.
    /// The seeds are searched in a fixed order, so a given build always shows the
    /// same branch.
    /// </summary>
    private static CareerOutcome PlayOutFor(GameContext ctx, Func<FightResult, bool> wanted,
        double skillA, double skillB)
    {
        var state = ctx.State ??= BuildCareer();
        var service = new CareerService();
        var offer = state.Offer ?? service.BookNextFight(state, new RandomSource(11));
        var opponent = state.FindFighter(offer.OpponentId)!;
        var rules = LeagueBook.Get(offer.LeagueId).Ruleset;

        for (var seed = 1; seed <= OutcomeSearchLimit; seed++)
        {
            var result = service.SimulateBout(state.Player, opponent, rules, new RandomSource(seed),
                offer.TitleFight, offer.LeagueId, state.Day, skillA, skillB);

            if (!wanted(result))
            {
                continue;
            }

            var outcome = service.ApplyResult(state, result, service.WeighIn(state));
            ctx.Screens.SetRoot(new ResultScreen(ctx, outcome));
            return outcome;
        }

        throw new InvalidOperationException(
            $"No bout in the first {OutcomeSearchLimit} seeds ended the way this screen needs.");
    }

    /// <summary>Enters the current scenario and pumps it forward so the frame looks alive.</summary>
    public void Prepare(GameContext ctx)
    {
        if (_entered || Finished)
        {
            return;
        }

        var scenario = _scenarios[_index];
        scenario.Enter(ctx);

        if (scenario.Until is { } until)
        {
            RunUntil(ctx, scenario, until);
        }
        else
        {
            RunTicks(ctx, scenario);
        }

        _entered = true;
        NeedsRender = true;
        Check(ctx, scenario);
        scenario.After?.Invoke(ctx);
    }

    private static void RunTicks(GameContext ctx, Scenario scenario)
    {
        var input = new InputState();
        var time = new GameTime(TimeSpan.Zero, Frame);

        for (var frame = 0; frame < scenario.Ticks; frame++)
        {
            input.SimulateIdle();

            for (var t = 0; t < scenario.Taps.Count; t++)
            {
                var tap = scenario.Taps[t];
                if (frame < tap.Frame || frame > tap.Frame + 2)
                {
                    continue;
                }

                // A real touch: press, hold for a frame, release. The release is
                // the frame the button fires on, so the whole sequence runs.
                if (frame == tap.Frame)
                {
                    input.SimulatePress(tap.Position);
                }
                else if (frame == tap.Frame + 1)
                {
                    input.SimulateHold(tap.Position);
                }
                else
                {
                    input.SimulateRelease(tap.Position);
                }

                break;
            }

            ctx.Screens.Update(time, input);
        }
    }

    /// <summary>
    /// Taps every frame until the scenario's condition holds. The bout is the one
    /// thing that cannot be stepped a fixed number of frames, so it drives itself.
    /// </summary>
    private static void RunUntil(GameContext ctx, Scenario scenario, Func<GameContext, bool> until)
    {
        var input = new InputState();
        var time = new GameTime(TimeSpan.Zero, Frame);

        for (var frame = 0; frame < UntilFrameGuard && !until(ctx); frame++)
        {
            var point = scenario.TapPoints.Count > 0
                ? scenario.TapPoints[frame % scenario.TapPoints.Count]
                : scenario.TapAt;

            input.SimulateTap(point);
            ctx.Screens.Update(time, input);
        }
    }

    /// <summary>
    /// The assertion: the screen the scenario set out to reach has to be the one
    /// on top of the stack, and its own extra check has to hold.
    /// </summary>
    private void Check(GameContext ctx, Scenario scenario)
    {
        var actual = ctx.Screens.Current?.GetType().Name ?? "nothing";
        var problem = actual == scenario.Expected
            ? MissingTrack(ctx) ?? scenario.Verify?.Invoke(ctx)
            : null;

        if (actual != scenario.Expected)
        {
            Fail($"{scenario.Name}: landed on {actual}, expected {scenario.Expected}");
            Console.WriteLine($"[selftest] FAIL {scenario.Name} -> {actual} (expected {scenario.Expected})");
            return;
        }

        if (problem is not null)
        {
            Fail($"{scenario.Name}: {problem}");
            Console.WriteLine($"[selftest] FAIL {scenario.Name} -> {actual} ({problem})");
            return;
        }

        Console.WriteLine($"[selftest] PASS {scenario.Name} -> {actual}");
    }

    /// <summary>
    /// Every scenario has to be asking for music that actually exists: a track name
    /// that matches no loaded sound is silence, and silence is easy to miss.
    /// </summary>
    private static string? MissingTrack(GameContext ctx) =>
        ctx.Music.Track is { } track && ctx.Assets.Sound(track) is null
            ? $"asked for \"{track}\", which is not a loaded sound"
            : null;

    /// <summary>
    /// A screen staged for the button sweep. The stage is entered again before
    /// every press, always from a brand new career, so one press can never hide
    /// another's effect and every press starts from the same place.
    /// </summary>
    private sealed class SweepStage
    {
        public required string Name { get; init; }
        public required Action<GameContext> Enter { get; init; }

        /// <summary>
        /// The fewest controls this screen must offer. A screen that quietly stopped
        /// declaring its buttons would otherwise pass by having nothing to press.
        /// </summary>
        public int AtLeast { get; init; } = 1;

        /// <summary>
        /// The fewest controls that must actually do something. The calendar needs
        /// this: most of its day cells have nothing on them, but the button row, the
        /// card and the player's own day all have to respond.
        /// </summary>
        public int AtLeastActing { get; init; } = 1;

        /// <summary>A control that is meant to do nothing, with the reason why.</summary>
        public Func<string, string?>? InertByName { get; init; }
    }

    /// <summary>
    /// Presses every control on every screen and insists it does something. A control
    /// on a screen with a career behind it either changes the screen, the career or
    /// the settings - or it is declared inert here with a reason, and then it must
    /// change nothing. A button that does neither is a dead button, and this is how
    /// one gets caught before a player finds it.
    /// </summary>
    private void SweepButtons(GameContext ctx)
    {
        _sweepProblems.Clear();
        _swept = 0;
        _acted = 0;
        _inert = 0;
        _dead = 0;
        _buttonsSwept = 0;
        _buttonsActed = 0;
        _regionsSwept = 0;
        _regionsActed = 0;

        foreach (var stage in SweepStages())
        {
            var controls = Inventory(ctx, stage);
            if (controls.Count < stage.AtLeast)
            {
                Problem($"{stage.Name}: only {controls.Count} controls are declared, expected at least " +
                        $"{stage.AtLeast}");
            }

            Console.WriteLine($"[buttons] {stage.Name}: {controls.Count} control(s) - " +
                              $"{string.Join(", ", controls.Select(c => c.Name))}");

            var acting = 0;

            for (var i = 0; i < controls.Count; i++)
            {
                // A fresh career and a fresh stack for every single press.
                stage.Enter(ctx);
                Settle(ctx, 12);

                var live = Describe(ctx).ToList();
                if (i >= live.Count)
                {
                    break;
                }

                var control = live[i];
                var expected = stage.InertByName?.Invoke(control.Name);
                var before = Fingerprint(ctx);
                _swept++;

                if (control.IsButton)
                {
                    _buttonsSwept++;
                }
                else
                {
                    _regionsSwept++;
                }

                string? problem = null;
                try
                {
                    Press(ctx, control.Bounds);
                }
                catch (Exception ex)
                {
                    problem = $"threw {ex.GetType().Name}: {ex.Message}";
                }

                var changed = before != Fingerprint(ctx);

                if (problem is not null)
                {
                    _dead++;
                }
                else if (expected is not null)
                {
                    _inert++;
                    if (changed)
                    {
                        problem = $"was expected to do nothing ({expected}) but changed the screen";
                    }
                }
                else if (!control.Enabled)
                {
                    _inert++;
                    if (changed)
                    {
                        problem = "is disabled but still fired";
                    }
                }
                else if (changed)
                {
                    _acted++;
                    acting++;
                    if (control.IsButton)
                    {
                        _buttonsActed++;
                    }
                    else
                    {
                        _regionsActed++;
                    }
                }
                else
                {
                    _dead++;
                    problem = "did nothing at all";
                }

                var line = $"[buttons] {(problem is null ? "PASS" : "FAIL")} {stage.Name}/{control.Name}";
                if (problem is null)
                {
                    Console.WriteLine(line + (expected is not null || !control.Enabled
                        ? " -> inert by design"
                        : " -> acted"));
                }
                else
                {
                    Console.WriteLine($"{line} ({problem})");
                    Problem($"{stage.Name}/{control.Name}: {problem}");
                }
            }

            if (acting < stage.AtLeastActing)
            {
                Problem($"{stage.Name}: only {acting} control(s) did anything, expected at least " +
                        $"{stage.AtLeastActing}");
            }
        }

        Console.WriteLine($"[buttons] swept {_swept} control(s): {_acted} acted, {_inert} inert by design, " +
                          $"{_dead} dead");
        Console.WriteLine($"[buttons] {_buttonsSwept} button(s) on screen: {_buttonsActed} acted; " +
                          $"{_regionsSwept} declared region(s): {_regionsActed} acted (the rest of those are " +
                          $"calendar days with nothing on them)");

        if (_sweepProblems.Count > 0)
        {
            Console.WriteLine($"[buttons] FAILED - {_sweepProblems.Count} control(s) did not do their job");
        }
    }

    /// <summary>
    /// Every screen a player can reach with a career behind it, staged with a brand
    /// new career and a screen underneath so a BACK button has somewhere to go.
    /// </summary>
    private List<SweepStage> SweepStages()
    {
        var stages = new List<SweepStage>();

        // The title screen with nothing saved: CONTINUE is meant to sit dead, and
        // the rest of the column and both sound chips have to work.
        stages.Add(new SweepStage
        {
            Name = "TitleScreen (no save)", AtLeast = 6, AtLeastActing = 5,
            Enter = ctx =>
            {
                ctx.State = null;
                ctx.Repository.Delete();
                ctx.Screens.SetRoot(new TitleScreen(ctx));
            }
        });

        // The same screen with a career on disk, so CONTINUE is live.
        stages.Add(new SweepStage
        {
            Name = "TitleScreen (with save)", AtLeast = 6, AtLeastActing = 6,
            Enter = ctx =>
            {
                ctx.State = null;
                ctx.Repository.Save(FreshCareer());
                ctx.Screens.SetRoot(new TitleScreen(ctx));
            }
        });

        // Career setup, pushed from the title so BACK has somewhere to go.
        stages.Add(new SweepStage
        {
            Name = "CreateCareerScreen", AtLeast = 8, AtLeastActing = 8,
            Enter = ctx =>
            {
                ctx.State = null;
                ctx.Screens.SetRoot(new TitleScreen(ctx));
                ctx.Screens.Push(new CreateCareerScreen(ctx));
            }
        });

        // The hub: every footer button opens another screen.
        stages.Add(new SweepStage
        {
            Name = "HubScreen", AtLeast = 9, AtLeastActing = 9,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new TitleScreen(ctx));
                ctx.Screens.Push(new HubScreen(ctx));
            }
        });

        // Camp: every drill trains a day, which changes the career.
        stages.Add(new SweepStage
        {
            Name = "TrainingScreen", AtLeast = 13, AtLeastActing = 13,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new TrainingScreen(ctx));
            }
        });

        // The record book, once per tab: the tab that is already open does nothing
        // when its own chip is pressed, which is what a radio chip should do.
        foreach (var tab in new[] { RecordsScreen.Tab.Rankings, RecordsScreen.Tab.Belts,
                     RecordsScreen.Tab.Rules })
        {
            var open = tab;
            stages.Add(new SweepStage
            {
                Name = $"RecordsScreen ({open})", AtLeast = 6, AtLeastActing = 5,
                InertByName = name => name == open.ToString().ToUpperInvariant()
                    ? "it is the tab already open"
                    : null,
                Enter = ctx =>
                {
                    ctx.State = FreshCareer();
                    ctx.Screens.SetRoot(new HubScreen(ctx));
                    ctx.Screens.Push(new RecordsScreen(ctx, open));
                }
            });
        }

        // The settings sheet: the chip for the choice already in force is a radio
        // button sitting on its own value, so it is inert by design.
        stages.Add(new SweepStage
        {
            Name = "SettingsScreen", AtLeast = 11, AtLeastActing = 8,
            InertByName = name => name is "NORMAL" or "KILOGRAMS"
                ? "it is the choice already in force"
                : null,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.SetLevel(AudioChannel.Music, 2);
                ctx.SetLevel(AudioChannel.Effects, 2);
                ctx.SetUnits(WeightUnit.Metric);
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new SettingsScreen(ctx));
            }
        });

        // The calendar in the career's own month, and again one month back so TODAY
        // has somewhere to go. A day cell answers only when the day has something on
        // it, which is why the screen is asked rather than assumed about.
        stages.Add(CalendarStage("CalendarScreen (this month)", shiftedBack: false,
            todayIsInert: true, atLeastActing: 8));
        stages.Add(CalendarStage("CalendarScreen (last month)", shiftedBack: true,
            todayIsInert: false, atLeastActing: 7));

        // Fight night's card, with the player's own bout due, so it offers WEIGH IN.
        stages.Add(new SweepStage
        {
            Name = "TournamentScreen", AtLeast = 4, AtLeastActing = 4,
            Enter = ctx =>
            {
                var state = FreshCareer();
                ctx.State = state;
                _stagedCard = StageCard(state, state.Offer?.FightDay ?? state.Day);
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new TournamentScreen(ctx, _stagedCard));
            }
        });

        // The morning of the bout: the scale writes a weigh-in into the career.
        stages.Add(new SweepStage
        {
            Name = "WeighInScreen", AtLeast = 2, AtLeastActing = 2,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new WeighInScreen(ctx));
            }
        });

        // The player's own turn, which is one tap anywhere on the screen.
        stages.Add(new SweepStage
        {
            Name = "FightScreen (call the hand)", AtLeast = 2, AtLeastActing = 2,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new FightScreen(ctx));

                // The beat names come from the bout screen's own ViewState.
                AdvanceUntil(ctx, "PlayerDeclare", 1200);
            }
        });

        // The count, which only comes up once a hand has been called.
        stages.Add(new SweepStage
        {
            Name = "FightScreen (call the count)", AtLeast = 3, AtLeastActing = 3,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new FightScreen(ctx));
                AdvanceUntil(ctx, "PlayerDeclare", 1200);
                Press(ctx, new Rectangle(460, 700, 480, 96));
                Settle(ctx, 2);
            }
        });

        // The measure, where the palm is walked onto the cheek and then set.
        stages.Add(new SweepStage
        {
            Name = "FightScreen (measure)", AtLeast = 3, AtLeastActing = 3,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new FightScreen(ctx));
                CallCount(ctx);
            }
        });

        // The rival's turn, where the four defence buttons come up.
        stages.Add(new SweepStage
        {
            Name = "FightScreen (defence)", AtLeast = 4, AtLeastActing = 4,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new HubScreen(ctx));
                ctx.Screens.Push(new FightScreen(ctx));
                AdvanceUntil(ctx, "OpponentCharge", 1200);
            }
        });

        // The award ceremony, which is one button back to the hub.
        stages.Add(new SweepStage
        {
            Name = "ResultScreen", AtLeast = 1, AtLeastActing = 1,
            Enter = ctx =>
            {
                ctx.State = FreshCareer();
                ctx.Screens.SetRoot(new HubScreen(ctx));
                PlayOutFor(ctx, r => r.WonBy(ctx.State!.PlayerId), 0.7, 0.45);
            }
        });

        return stages;
    }

    /// <summary>
    /// A calendar stage. The screen itself says which days have something on them, so
    /// the sweep neither demands a note from an empty day nor lets an empty-day
    /// assumption cover for a cell that has stopped answering.
    /// </summary>
    private SweepStage CalendarStage(string name, bool shiftedBack, bool todayIsInert, int atLeastActing)
    {
        CalendarScreen? calendar = null;

        return new SweepStage
        {
            Name = name, AtLeast = 35, AtLeastActing = atLeastActing,
            InertByName = control =>
            {
                if (todayIsInert && control == "TODAY")
                {
                    return "the month on screen is already the current one";
                }

                if (control.StartsWith("day ", StringComparison.Ordinal) &&
                    int.TryParse(control.AsSpan(4), out var day) &&
                    calendar is not null && !calendar.DayIsLive(day))
                {
                    return "nothing is on that day, so the cell has nothing to open or name";
                }

                return null;
            },
            Enter = ctx =>
            {
                var state = FreshCareer();
                ctx.State = state;
                _stagedCard = StageCard(state, state.Offer?.FightDay ?? state.Day);
                ctx.Screens.SetRoot(new HubScreen(ctx));
                calendar = new CalendarScreen(ctx);
                ctx.Screens.Push(calendar);

                if (shiftedBack)
                {
                    ShiftMonthBack(ctx);
                }
            }
        };
    }

    /// <summary>A brand new career with the next bout booked, so every stage starts
    /// from exactly the same place however the last press went.</summary>
    private static CareerState FreshCareer()
    {
        var state = WorldGenerator.Create("Paul", "Johnson", "The Professor",
            WeightClassId.Welterweight, 4242);
        new CareerService().BookNextFight(state, new RandomSource(11));
        return state;
    }

    /// <summary>Presses the calendar's back arrow once, on the screen that is up.</summary>
    private static void ShiftMonthBack(GameContext ctx) => Press(ctx, new Rectangle(1000, 42, 84, 72));

    /// <summary>
    /// Taps the screen on until it reports the state a stage wants to press the
    /// buttons of. The bout is the one screen whose controls only exist at certain
    /// beats, so this is how the sweep gets to them.
    /// </summary>
    private static void AdvanceUntil(GameContext ctx, string marker, int guard)
    {
        var input = new InputState();
        var time = new GameTime(TimeSpan.Zero, Frame);

        for (var frame = 0; frame < guard; frame++)
        {
            if (ctx.Screens.Current?.ViewState.Contains(marker, StringComparison.Ordinal) == true)
            {
                return;
            }

            // The bout asks for two calls before a slap, so the sweep taps both points:
            // the player's hand chip, then a count chip. On any other screen the first
            // point is simply a tap in empty space.
            input.SimulateTap(frame % 2 == 0 ? new Vector2(700, 748) : new Vector2(1030, 878));
            ctx.Screens.Update(time, input);
        }
    }

    /// <summary>
    /// Makes the player's call - a hand, then the number the slap lands on - which is
    /// what opens the measure.
    /// </summary>
    private static void CallCount(GameContext ctx, int count = 1)
    {
        AdvanceUntil(ctx, "PlayerDeclare", 4000);
        Press(ctx, new Rectangle(460, 700, 480, 96));
        Settle(ctx, 2);
        Press(ctx, new Rectangle(560 + (count - 1) * 320, 830, 300, 96));
        Settle(ctx, 2);
    }

    /// <summary>Every control a stage offers, without pressing any of them.</summary>
    private static List<Control> Inventory(GameContext ctx, SweepStage stage)
    {
        stage.Enter(ctx);
        Settle(ctx, 12);
        return Describe(ctx).ToList();
    }

    /// <summary>One pressable thing on a screen: a button widget, or a region the
    /// screen hit-tests itself.</summary>
    private readonly record struct Control(string Name, Rectangle Bounds, bool Enabled, bool IsButton);

    /// <summary>The buttons and the declared regions on the screen that is up now.</summary>
    private static IEnumerable<Control> Describe(GameContext ctx)
    {
        var screen = ctx.Screens.Current;
        if (screen is null)
        {
            yield break;
        }

        foreach (var button in screen.Buttons)
        {
            yield return new Control(button.Text, button.Bounds, button.Enabled, true);
        }

        foreach (var target in screen.Targets)
        {
            yield return new Control(target.Name, target.Bounds, true, false);
        }
    }

    /// <summary>
    /// What is on the screens right now: which screen is up, how deep the stack is,
    /// what the screen is showing, the player's preferences and the whole career.
    /// A press that leaves all of this identical changed nothing.
    /// </summary>
    private static string Fingerprint(GameContext ctx)
    {
        var screen = ctx.Screens.Current;
        var career = ctx.State is null
            ? "no career"
            : CareerRepository.Fingerprint(ctx.State);

        return $"{screen?.GetType().Name}#{ctx.Screens.Depth}#{screen?.ViewState}" +
               $"|{ctx.Settings.Music.Label}/{ctx.Settings.Effects.Label}/{ctx.Settings.Units}|{career}";
    }

    /// <summary>Runs the screens on with no input at all, so a view settles.</summary>
    private static void Settle(GameContext ctx, int frames)
    {
        var input = new InputState();
        var time = new GameTime(TimeSpan.Zero, Frame);

        for (var frame = 0; frame < frames; frame++)
        {
            input.SimulateIdle();
            ctx.Screens.Update(time, input);
        }
    }

    /// <summary>
    /// One real touch in the middle of a control: press, hold, release. The release
    /// is the frame a button fires on, so anything the press caused has happened by
    /// the time this returns.
    /// </summary>
    private static void Press(GameContext ctx, Rectangle bounds)
    {
        var centre = new Vector2(bounds.Center.X, bounds.Center.Y);
        var input = new InputState();
        var time = new GameTime(TimeSpan.Zero, Frame);

        input.SimulatePress(centre);
        ctx.Screens.Update(time, input);

        input.SimulateHold(centre);
        ctx.Screens.Update(time, input);

        input.SimulateRelease(centre);
        ctx.Screens.Update(time, input);

        // Two more frames with nothing held, so a screen that acts on the frame
        // after the tap has had its chance.
        for (var frame = 0; frame < 2; frame++)
        {
            input.SimulateIdle();
            ctx.Screens.Update(time, input);
        }
    }

    private void Problem(string message) => _sweepProblems.Add(message);

    private void Fail(string message) => _failures.Add(message);

    /// <summary>Records every catalogue key the run could not resolve.</summary>
    public void FailMissingStrings(IReadOnlyCollection<string> missing)
    {
        foreach (var key in missing)
        {
            Fail($"missing string \"{key}\"");
        }
    }

    public void Captured()
    {
        NeedsRender = false;
        _entered = false;
        _index++;
    }

    public string PathFor(string name) => Path.Combine(_directory, name + ".png");
}
