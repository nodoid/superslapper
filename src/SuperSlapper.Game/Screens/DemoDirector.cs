using Microsoft.Xna.Framework;
using SuperSlapper.Game.Core;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// Plays the game on its own, in real time, through the same screens and the same
/// controls a person uses. A capture of the running app - an iPhone simulator
/// screen recording, say - is taken from this, so the store video is of the real
/// game rather than a separate mock-up.
///
/// Every tap is a synthetic touch handed to the frame's <see cref="InputState"/>:
/// press, hold, then release, which is the frame a button fires on. Nothing is
/// pushed or popped directly, so a control that stopped responding would stall
/// the demo exactly as it would stall a player.
/// </summary>
public sealed class DemoDirector
{
    /// <summary>The hand is called early in the declaration, the count a beat later.</summary>
    private const float HandSeconds = 0.45f;
    private const float DeclaredCountSeconds = 1.1f;

    /// <summary>Time for the palm to be walked onto the cheek before it is set.</summary>
    private const float MeasureSeconds = 1.4f;

    /// <summary>The rival's window is his own count; give the pick time to read.</summary>
    private const float DefenceSeconds = 0.95f;

    /// <summary>One stretch of the demo: what it waits for, and what it taps.</summary>
    private sealed record Beat
    {
        public required string Name { get; init; }

        /// <summary>Ceiling for the beat. The screen arriving is what normally ends it.</summary>
        public required float Seconds { get; init; }

        /// <summary>Real touches, as (seconds into the beat, x, y) in virtual pixels.</summary>
        public IReadOnlyList<(float At, float X, float Y)> Taps { get; init; } =
            Array.Empty<(float, float, float)>();

        /// <summary>True once the beat has reached the screen it was aiming for.</summary>
        public Func<GameContext, bool>? Until { get; init; }

        /// <summary>Plays the bout instead of following a fixed tap list.</summary>
        public bool Bout { get; init; }
    }

    private readonly GameContext _context;
    private readonly List<Beat> _beats;
    private readonly string? _logFile;
    private bool[] _fired = Array.Empty<bool>();
    private int _index;
    private int _activeTap = -1;
    private int _tapStage;
    private float _elapsed;
    private bool _started;

    // The bout is not on a timeline: the screens tell the demo which beat it is on.
    private string _boutBeat = string.Empty;
    private float _boutBeatTime;
    private int _boutTaps;

    public DemoDirector(GameContext context, string? captureDirectory, string? logFile = null)
    {
        _context = context;
        CaptureDirectory = captureDirectory;
        _logFile = logFile;

        if (captureDirectory is not null)
        {
            Directory.CreateDirectory(captureDirectory);
        }

        if (logFile is not null)
        {
            // Cleared first: the recorder outside watches this file to know the run
            // is over, and the last take's "complete" would end the next one early.
            Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
            File.WriteAllText(logFile, string.Empty);
        }

        _beats = new List<Beat>
        {
            // The title arms its buttons once the logo has settled.
            new()
            {
                Name = "title", Seconds = 4.5f,
                Taps = Tap(1.6f, 300, 652),
                Until = ctx => ctx.Screens.Current is CreateCareerScreen
            },
            // START CAREER books the first bout before it opens the hub.
            new()
            {
                Name = "create", Seconds = 6.5f,
                Taps = Tap(2.4f, 1640, 980),
                Until = ctx => ctx.Screens.Current is HubScreen
            },
            new()
            {
                Name = "hub", Seconds = 6f,
                Taps = Tap(2.9f, 218, 950),
                Until = ctx => ctx.Screens.Current is TrainingScreen
            },
            // One session, then straight back out: the camp log has to gain a day.
            new()
            {
                Name = "training", Seconds = 6.5f,
                Taps = Taps((0.9f, 280, 260), (4.4f, 660, 950)),
                Until = ctx => ctx.Screens.Current is HubScreen
            },
            new()
            {
                Name = "hub_calendar", Seconds = 5f,
                Taps = Tap(2.2f, 1402, 950),
                Until = ctx => ctx.Screens.Current is CalendarScreen
            },
            // A month forward, a day picked, then out.
            new()
            {
                Name = "calendar", Seconds = 7f,
                Taps = Taps((0.8f, 1142, 78), (2.1f, 400, 400), (4.6f, 1740, 950)),
                Until = ctx => ctx.Screens.Current is HubScreen
            },
            new()
            {
                Name = "hub_weigh_in", Seconds = 4.5f,
                Taps = Tap(2.0f, 1698, 950),
                Until = ctx => ctx.Screens.Current is WeighInScreen
            },
            // Step on the scale, let the needle settle, then walk to the ring.
            new()
            {
                Name = "weigh_in", Seconds = 6.5f,
                Taps = Taps((1.2f, 980, 940), (3.6f, 980, 940)),
                Until = ctx => ctx.Screens.Current is FightScreen
            },
            // The bout runs as long as the bout runs; the guard is only a backstop.
            new() { Name = "bout", Seconds = 420f, Bout = true, Until = ctx => ctx.Screens.Current is ResultScreen },
            new()
            {
                Name = "result", Seconds = 7f,
                Taps = Tap(4.2f, 960, 950),
                Until = ctx => ctx.Screens.Current is HubScreen
            },
            new() { Name = "hub_final", Seconds = 3.5f }
        };

        _fired = new bool[Current.Taps.Count];
    }

    /// <summary>Where the beat captures are written, when the run is being checked.</summary>
    public string? CaptureDirectory { get; }

    /// <summary>Where the beat log is written, so a recorder can follow the run.</summary>
    public string? LogFile => _logFile;

    /// <summary>Set when the frame that just went by should be saved to disk.</summary>
    public bool WantsCapture { get; private set; }

    /// <summary>The file the pending capture goes to, without the extension.</summary>
    public string? CaptureName { get; private set; }

    /// <summary>True once the script has run off the end of its last beat.</summary>
    public bool Finished => _index >= _beats.Count;

    private Beat Current => _beats[Math.Min(_index, _beats.Count - 1)];

    /// <summary>Fills the frame's input. Called before the screens see it.</summary>
    public void Update(GameTime time, InputState input)
    {
        input.SimulateIdle();

        if (!_started)
        {
            // One line to mark the take: the recorder outside has a wall clock too,
            // and this is what lets it line the edit up with the footage.
            _started = true;
            Say($"[demo] start {Stamp()}");
        }

        if (Finished)
        {
            return;
        }

        var beat = Current;
        var dt = (float)time.ElapsedGameTime.TotalSeconds;
        _elapsed += dt;

        if (_activeTap >= 0)
        {
            Fire(input, _activeTap);
        }
        else if (beat.Bout)
        {
            DriveBout(input, dt);
        }
        else
        {
            for (var i = 0; i < beat.Taps.Count; i++)
            {
                if (_fired[i] || _elapsed < beat.Taps[i].At)
                {
                    continue;
                }

                _activeTap = i;
                _tapStage = 0;
                Fire(input, i);
                break;
            }
        }

        var arrived = beat.Until?.Invoke(_context) == true;
        if (!arrived && _elapsed < beat.Seconds)
        {
            return;
        }

        Advance(beat, arrived);
    }

    /// <summary>One real touch: press one frame, hold the next, release on the third.</summary>
    private void Fire(InputState input, int index)
    {
        var tap = Current.Taps[index];
        var position = new Vector2(tap.X, tap.Y);

        switch (_tapStage)
        {
            case 0:
                input.SimulatePress(position);
                break;
            case 1:
                input.SimulateHold(position);
                break;
            default:
                input.SimulateRelease(position);
                break;
        }

        _tapStage++;

        if (_tapStage <= 2)
        {
            return;
        }

        _tapStage = 0;
        _activeTap = -1;
        _fired[index] = true;
    }

    /// <summary>
    /// Plays the bout the way a competent player would: wait for the power bar to
    /// reach the gold, let the aim marker come round to the jaw, brace for the
    /// rival's slap. The fight screen names its own beat, so this follows the bout
    /// rather than guessing at timings.
    /// </summary>
    private void DriveBout(InputState input, float dt)
    {
        // The beat name is the first field of the screen's view state.
        var beat = _context.Screens.Current?.ViewState.Split('|')[0] ?? string.Empty;
        if (beat != _boutBeat)
        {
            _boutBeat = beat;
            _boutBeatTime = 0f;
            _boutTaps = 0;

            if (!string.IsNullOrEmpty(beat))
            {
                Say($"[demo] bout {beat} {Stamp()}");
            }
        }
        else
        {
            _boutBeatTime += dt;
        }

        // The declaration takes two calls - the hand, then the number - so it is the
        // one beat that gets more than one tap.
        var when = beat switch
        {
            "PlayerDeclare" => _boutTaps == 0 ? HandSeconds : DeclaredCountSeconds,
            "PlayerMeasure" => MeasureSeconds,
            "OpponentCharge" => DefenceSeconds,
            _ => float.MaxValue
        };

        var taps = beat == "PlayerDeclare" ? 2 : 1;
        if (_boutTaps >= taps || _boutBeatTime < when)
        {
            return;
        }

        // One whole touch in a single frame, for the same reason the charge took the
        // press: a touch left hanging over the beat change would land the next beat's
        // first control the instant it starts.
        var point = beat switch
        {
            "PlayerDeclare" => _boutTaps == 0 ? new Vector2(700, 748) : new Vector2(1030, 878),
            "PlayerMeasure" => new Vector2(1160, 902),
            "OpponentCharge" => new Vector2(955, 808),
            _ => new Vector2(960, 540)
        };

        input.SimulateTap(point);
        _boutTaps++;
    }

    private void Advance(Beat beat, bool arrived)
    {
        var screen = _context.Screens.Current?.GetType().Name ?? "nothing";

        Say($"[demo] {_index + 1:D2} {beat.Name}: " +
            $"{(arrived ? "arrived at" : "no arrival, still on")} {screen} " +
            $"after {_elapsed:F1}s {Stamp()}");

        _index++;
        _elapsed = 0f;
        _activeTap = -1;
        _tapStage = 0;
        _boutBeat = string.Empty;
        _boutBeatTime = 0f;
        _boutTaps = 0;

        if (Finished)
        {
            Say($"[demo] complete {Stamp()}");
            return;
        }

        // The next beat starts here, so the captures are of the screen it arrived on.
        _fired = new bool[Current.Taps.Count];
        if (CaptureDirectory is not null)
        {
            WantsCapture = true;
            CaptureName = $"{_index + 1:D2}_{Current.Name}";
        }
    }

    /// <summary>
    /// Tells the director its frame was written. The pending capture is only
    /// cleared here, by the draw that wrote it: a fixed timestep can run several
    /// updates before the next draw, and clearing it there would drop the frame.
    /// </summary>
    public void Captured() => WantsCapture = false;

    /// <summary>
    /// Reports a beat to the console and, when one was named, to the log file the
    /// recorder outside is watching. The log is the one the edit is planned from.
    /// </summary>
    private void Say(string line)
    {
        Console.WriteLine(line);

        if (_logFile is not null)
        {
            File.AppendAllText(_logFile, line + Environment.NewLine);
        }
    }

    /// <summary>A wall-clock stamp a recorder outside the app can line its take up with.</summary>
    private static string Stamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>A single tap, expressed the way the beat table reads it.</summary>
    private static (float, float, float)[] Tap(float at, float x, float y) => new[] { (at, x, y) };

    private static (float, float, float)[] Taps(params (float At, float X, float Y)[] taps) => taps;
}
