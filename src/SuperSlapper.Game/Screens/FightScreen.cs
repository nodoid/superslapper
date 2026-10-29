using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Rules;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// The bout itself. A three-quarter view of two fighters in the ring, a
/// spring-loaded power bar for each of them at the top of the screen, and the
/// slap-fighting ritual played out turn by turn: the round card, the call of a hand
/// and a count, the measured palm on the cheek, a practice draw per number, then the
/// slap. Faithful to the rules engine: every slap, foul, count and scorecard comes
/// out of  <see cref="FightEngine"/>.
/// </summary>
public sealed class FightScreen : Screen
{
    private enum Beat
    {
        Intro = 0,

        /// <summary>The ring announces the round on a card before the first call.</summary>
        RoundPlacard = 1,

        PlayerDeclare = 2,

        /// <summary>The palm is set on the opponent's cheek and lined up.</summary>
        PlayerMeasure = 3,

        /// <summary>
        /// The count. Every number draws the arm back and puts the palm back on the
        /// cheek; the number the slap was called on is the one that goes through.
        /// </summary>
        PlayerCount = 4,

        PlayerSwing = 5,
        OpponentCharge = 6,
        OpponentSwing = 7,
        PlayerRecover = 8,
        OpponentRecover = 9,
        Count = 10,
        RoundBreak = 11,
        Finished = 12
    }

    private enum Defence
    {
        Take = 0,
        Brace = 1,
        Flinch = 2,
        Step = 3
    }

    // The two of them stand at slapping distance. Close enough that the hand coming
    // across the gap is the story of the shot rather than two portraits of men.
    private const float PlayerBaselineY = 1010f;
    private const float PlayerX = 620f;
    private const float RivalX = 1300f;
    private const float FighterScale = 1.55f;

    /// <summary>How far a fighter steps in when he throws.</summary>
    private const float LungeDistance = 300f;

    /// <summary>From the arm cocked to the hand on the cheek.</summary>
    private const float SwingSeconds = 0.26f;

    private const float ImpactSeconds = 0.24f;

    /// <summary>How long the round card holds before the first call of the round.</summary>
    private const float RoundPlacardSeconds = 2.4f;

    /// <summary>How far one tap of HIGHER or LOWER moves the palm.</summary>
    private const float MeasureNudge = 24f;

    /// <summary>How far off the cheek line the palm can be set at all.</summary>
    private const float MeasureLimit = 96f;

    /// <summary>Within this of the cheek line the palm is still on a legal cheek.</summary>
    private const float CheekBand = 30f;

    /// <summary>
    /// A hand that is never set is taken as placed after this long. The bout must
    /// never be able to stall waiting for a tap that is not coming.
    /// </summary>
    private const float MeasureSettleSeconds = 15f;

    /// <summary>The rival's own measure, before his count starts.</summary>
    private const float RivalMeasureSeconds = 1.5f;

    /// <summary>The drawn palm on the cheek: a hand across a face, not a hand over a head.</summary>
    private const float HandScale = 0.32f;

    /// <summary>Where the palm sits in hand_closeup.png, so the palm can be put on a cheek.</summary>
    private static readonly Vector2 HandPalm = new(196f, 292f);

    // The replay strip: 960x512 frames in a 4-wide grid, see tools/artgen/photoreal.py.
    private const int ReplayFrameWidth = 960;
    private const int ReplayFrameHeight = 512;
    private const int ReplayColumns = 4;
    private const float ReplayFrameSeconds = 0.085f;
    private const float ReplayTailSeconds = 0.30f;

    /// <summary>
    /// One fighter's body in the ring: where his feet are, how far he is leaning
    /// and whether he is still up. Everything eases towards a target, so a wind-up,
    /// a lunge and a fall all read as movement instead of as a new pose.
    /// </summary>
    private sealed class Body
    {
        public float HomeX;
        public float Facing = 1f;

        public float X;
        public float Y = PlayerBaselineY;
        public float TargetX;
        public float TargetY = PlayerBaselineY;

        public float Tilt;
        public float TargetTilt;
        public float Rate = 16f;

        /// <summary>True once he has hit the canvas: he is drawn over the table then.</summary>
        public bool Down;

        /// <summary>Seconds until the falling sprite becomes the lying one.</summary>
        public float DownDelay;

        public void Reset()
        {
            X = TargetX = HomeX;
            Y = TargetY = PlayerBaselineY;
            Tilt = TargetTilt = 0f;
            Rate = 16f;
            Down = false;
            DownDelay = 0f;
        }

        public void Tick(float dt)
        {
            var k = 1f - (float)Math.Exp(-Rate * dt);
            X += (TargetX - X) * k;
            Y += (TargetY - Y) * k;
            Tilt += (TargetTilt - Tilt) * k;

            if (DownDelay > 0f)
            {
                DownDelay -= dt;
                if (DownDelay <= 0f)
                {
                    // The tilt has taken him to the mat; from here he is the lying
                    // frame, in front of the table so he can be seen on the canvas.
                    Down = true;
                    X = TargetX = HomeX - 40f * Facing;
                    Y = TargetY = PlayerBaselineY + 118f;
                    Tilt = TargetTilt = 0f;
                }
            }
        }

        public void Stand()
        {
            Down = false;
            DownDelay = 0f;
            TargetX = HomeX;
            TargetY = PlayerBaselineY;
            TargetTilt = 0f;
            Rate = 16f;
        }

        public void WindUp()
        {
            Down = false;
            DownDelay = 0f;
            TargetX = HomeX - 46f * Facing;
            TargetY = PlayerBaselineY;
            TargetTilt = -0.06f * Facing;
            Rate = 11f;
        }

        public void Lunge()
        {
            Down = false;
            DownDelay = 0f;
            TargetX = HomeX + LungeDistance * Facing;
            TargetY = PlayerBaselineY + 26f;
            TargetTilt = 0.13f * Facing;
            Rate = 15f;
        }

        public void Recoil(float force)
        {
            Down = false;
            DownDelay = 0f;
            TargetX = HomeX - (50f + 80f * force) * Facing;
            TargetY = PlayerBaselineY;
            TargetTilt = -(0.12f + 0.14f * force) * Facing;
            Rate = 8f;
        }

        public void Fall()
        {
            Down = false;
            DownDelay = 0.42f;
            TargetX = HomeX - 90f * Facing;
            TargetY = PlayerBaselineY + 40f;
            TargetTilt = -1.15f * Facing;
            Rate = 6.5f;
        }

        public void Rise()
        {
            var wasDown = Down || DownDelay > 0f;

            Down = false;
            DownDelay = 0f;

            TargetX = HomeX;
            TargetY = PlayerBaselineY;
            TargetTilt = 0f;

            if (!wasDown)
            {
                return;
            }

            // Coming up off the canvas: start him laid back and let him climb to his
            // feet, or he would stand up in one frame.
            X = HomeX - 40f * Facing;
            Y = PlayerBaselineY + 118f;
            Tilt = -0.95f * Facing;
            Rate = 7f;
        }

        /// <summary>On his feet, or on his way back up.</summary>
        public bool Standing => !Down && DownDelay <= 0f;
    }

    private readonly List<(string Text, Vector2 Position, float Life, Color Colour, float Scale)> _popups = new();

    /// <summary>
    /// A red hand mark on the cheek that was hit. Marks settle rather than fade
    /// out, so they last the whole bout and survive the round breaks.
    /// </summary>
    private readonly List<(bool OnPlayer, Vector2 Offset, float Age, bool Fresh)> _slapMarks = new();

    private const float SlapMarkFreshSeconds = 1.8f;
    private const int SlapMarkLimit = 8;

    private SlapZone _replayZone = SlapZone.RightCheek;

    private readonly WeighInResult? _weighIn;

    private FightEngine _engine = null!;
    private Commentator _commentator = new();
    private PowerMeter _playerMeter = new();
    private PowerMeter _rivalMeter = new(true);

    private Beat _beat;
    private float _beatTimer;
    private float _sceneTime;

    private float _lockedCharge;

    // Every slap is called before it is thrown: the hand that takes it and the
    // number of the count it lands on. The player calls their own; the rival's call
    // is shown as he winds up, so both wind-ups are on screen before either lands.
    private bool _handChosen;
    private bool _leftHand;
    private int _declaredCount;
    private int _countCalled;
    private float _countClock;
    private bool _rivalLeftHand;
    private int _rivalCount;

    // The measure: where the palm sits on the other man's cheek, in virtual pixels
    // up (negative) or down (positive) from the cheek line. It decides both the zone
    // the slap is aimed at and how cleanly the hand arrives.
    private float _measureOffset;
    private float _measureClock;
    private float _rivalMeasureFrom;
    private float _rivalMeasureOffset;

    /// <summary>1 while the striker's palm is on the cheek, 0 while the arm is back.</summary>
    private float _handOnCheek;
    private float _rivalHandOnCheek;

    private float _lockedTiming = 1f;
    private float _lockedAim = 0.9f;
    private SlapZone _lockedZone = SlapZone.Jaw;

    private int _playerFrame = FighterSheet.IdleA;
    private int _rivalFrame = FighterSheet.IdleA;
    private float _playerPoseTimer;
    private float _rivalPoseTimer;

    private readonly Body _playerBody = new() { HomeX = PlayerX, Facing = 1f };
    private readonly Body _rivalBody = new() { HomeX = RivalX, Facing = -1f };

    private bool _strikerIsPlayer;

    /// <summary>Seconds of afterimage left on the arm coming through.</summary>
    private float _swingGhost;

    private float _impactTimer;
    private float _impactPower = 1f;
    private bool _impactOnPlayer;

    /// <summary>A white blink on the frame of contact, so a heavy one is felt.</summary>
    private float _flash;

    /// <summary>The replay holds off until the shot has been seen in the ring.</summary>
    private float _replayDelay;
    private string _pendingReplayCommentary = "";
    private bool _pendingReplayKnockdown;

    private KnockdownResult? _countResult;
    private int _countTarget;
    private float _countHold;
    private bool _downIsPlayer;
    private float _refereeIn;

    private float _shake;
    private float _zoom = 1f;
    private float _targetZoom = 1f;
    private float _timeScale = 1f;
    private Vector2 _focus = new(960, 620);

    private Defence _defence = Defence.Take;
    private float _defenceWindow;
    private float _defenceTimer;
    private bool _defenceLocked;

    private float _replayTimer;
    private float _replayDuration;
    private string _replayTexture = "replay_slap";
    private string _replayCommentary = "";
    private int _replayFrames;

    private int _countValue;
    private float _countTimer;
    private bool _applied;
    private bool _koSlowMotion;

    public FightScreen(GameContext context, WeighInResult? weighIn = null) : base(context)
    {
        _weighIn = weighIn;
    }

    private CareerState State => Context.State!;

    /// <summary>The bout has its own track; everything else in the career shares the menu one.</summary>
    public override string? MusicTrack => "music_fight";

    /// <summary>
    /// The beat, the defence picked and both fighters' condition. Pressing a
    /// defence button changes this on the frame it fires, which is what tells the
    /// button sweep that the button did something.
    /// </summary>
    public override string ViewState => _engine is null
        ? "no bout"
        : $"{_beat}|{_defence}|{_defenceLocked}|{_engine.Round}|" +
          $"{_engine.ConditionA.Health}|{_engine.ConditionB.Health}|" +
          $"hand {(_handChosen ? (_leftHand ? "left" : "right") : "none")}|count {_declaredCount}|" +
          $"measure {_measureOffset:0}|zone {ZoneForOffset(_measureOffset, _leftHand)}|" +
          $"striker {(_engine.StrikerIsA ? "you" : "rival")}|slaps {_engine.SlapsRemainingForCurrentStriker}";

    private Fighter Player => _engine.FighterA;

    private Fighter Rival => _engine.FighterB;

    private Vector2 PlayerHead => HeadOf(_playerBody);

    private Vector2 RivalHead => HeadOf(_rivalBody);

    /// <summary>
    /// Where the head actually is: the lean and the fall rotate the body about his
    /// feet, so a mark or a burst placed at the resting head would drift off his face.
    /// The lying frame carries its head at the end of the body instead.
    /// </summary>
    private static Vector2 HeadOf(Body body)
    {
        var local = body.Down
            ? new Vector2(-217f * body.Facing, -186f)
            : new Vector2(-65f * body.Facing, -601f);

        var cos = MathF.Cos(body.Tilt);
        var sin = MathF.Sin(body.Tilt);

        return new Vector2(body.X + local.X * cos - local.Y * sin,
            body.Y + local.X * sin + local.Y * cos);
    }

    /// <summary>
    /// The cheek the hand is measured against: the side of the head turned towards
    /// the man slapping, below the eye line. It leans with him, like the head does.
    /// </summary>
    private static Vector2 CheekOf(Body body)
    {
        var head = HeadOf(body);
        var local = body.Down
            ? new Vector2(-150f * body.Facing, -175f)
            : new Vector2(50f * body.Facing, 52f);

        var cos = MathF.Cos(body.Tilt);
        var sin = MathF.Sin(body.Tilt);

        return new Vector2(head.X + local.X * cos - local.Y * sin,
            head.Y + local.X * sin + local.Y * cos);
    }

    public override void OnEnter()
    {
        base.OnEnter();
        ClearControls();
        _beat = Beat.Intro;

        if (Context.State is null)
        {
            return;
        }

        _engine = Context.Career.CreateFightEngine(State);
        _playerBody.Reset();
        _rivalBody.Reset();
        _playerMeter.Label = Player.DisplayName;
        _rivalMeter.Label = Rival.DisplayName;
        _playerMeter.Snap(0);
        _rivalMeter.Snap(0);
        _commentator.Clear();

        var league = Leagues.Get(State.PlayerLeagueId);
        _commentator.Say(Loc.T("fight.announce", _engine.Rules.Rounds,
            WeightClasses.Get(State.PlayerDivision).Name), 3.4f);

        Context.Assets.Play("applause_wild", 0.8f);
        Context.Assets.Play("bell", 0.5f);

        _beat = Beat.Intro;
        _beatTimer = 2.6f;
        ClearControls();
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        var raw = (float)time.ElapsedGameTime.TotalSeconds;

        // The pair keep moving while the replay is held back: the replay itself
        // freezes the bout, which is why it waits until the shot has been seen.
        _playerBody.Tick(raw);
        _rivalBody.Tick(raw);
        _swingGhost = Math.Max(0f, _swingGhost - raw);
        _impactTimer = Math.Max(0f, _impactTimer - raw);
        _flash = Math.Max(0f, _flash - raw * 6f);

        if (_replayDelay > 0f)
        {
            _replayDelay -= raw;
            if (_replayDelay <= 0f)
            {
                StartReplay(_pendingReplayCommentary, _pendingReplayKnockdown);
            }
        }

        if (_replayTimer > 0f)
        {
            // Every landed slap gets its replay; the bout holds until it ends.
            _replayTimer -= raw;
            _shake = Math.Max(0f, _shake - raw * 9f);
            _zoom = Mathf.Lerp(_zoom, _targetZoom, raw * 3f);
            _commentator.Update(raw);
            return;
        }

        var dt = raw * _timeScale;
        _sceneTime += dt;

        _playerMeter.Update(raw);
        _rivalMeter.Update(raw);
        _commentator.Update(raw);

        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            var popup = _popups[i];
            popup.Life -= raw;
            _popups[i] = popup;
            if (popup.Life <= 0)
            {
                _popups.RemoveAt(i);
            }
        }

        for (var i = 0; i < _slapMarks.Count; i++)
        {
            var mark = _slapMarks[i];
            mark.Age += raw;
            mark.Fresh = mark.Age < SlapMarkFreshSeconds;
            _slapMarks[i] = mark;
        }

        _shake = Math.Max(0f, _shake - raw * 26f);
        _zoom = Mathf.Lerp(_zoom, _targetZoom, 1f - (float)Math.Pow(0.001, raw));
        _timeScale = Mathf.Lerp(_timeScale, 1f, raw * (1f / Math.Max(0.05f, _koSlowMotion ? 2.2f : 0.25f)));

        _beatTimer -= raw;
        _playerPoseTimer -= raw;
        _rivalPoseTimer -= raw;

        // A fighter on the canvas keeps the pose he landed in; only men on their
        // feet drift back to the idle cycle.
        if (_playerPoseTimer <= 0 && _beat != Beat.PlayerSwing && _playerBody.Standing)
        {
            _playerFrame = FighterSheet.Idle(_sceneTime);
        }

        if (_rivalPoseTimer <= 0 && _beat != Beat.OpponentSwing && _rivalBody.Standing)
        {
            _rivalFrame = FighterSheet.Idle(_sceneTime + 0.37f);
        }

        foreach (var button in Buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }

        switch (_beat)
        {
            case Beat.Intro:
                if (_beatTimer <= 0)
                {
                    StartRoundPlacard();
                }

                break;

            case Beat.RoundPlacard:
                if (_beatTimer <= 0)
                {
                    BeginTurn();
                }

                break;

            case Beat.PlayerMeasure:
                TickPlayerMeasure(dt);
                break;

            case Beat.PlayerCount:
                TickPlayerCount(dt);
                break;

            case Beat.PlayerSwing:
                TickSwing(player: true);
                if (_beatTimer <= 0)
                {
                    ResolvePlayerSlap();
                }

                break;

            case Beat.PlayerRecover:
                if (_beatTimer <= 0)
                {
                    ContinueAfterSlap();
                }

                break;

            case Beat.OpponentRecover:
                if (_beatTimer <= 0)
                {
                    ContinueAfterSlap();
                }

                break;

            case Beat.OpponentCharge:
                TickOpponentCharge(dt, input);
                break;

            case Beat.OpponentSwing:
                TickSwing(player: false);
                if (_beatTimer <= 0)
                {
                    ResolveOpponentSlap();
                }

                break;

            case Beat.Count:
                TickCount(raw);
                break;

            case Beat.RoundBreak:
                TickRoundBreak(dt);
                break;

            case Beat.Finished:
                if (_beatTimer <= 0 && !_applied)
                {
                    LeaveTheRing();
                }

                break;
        }
    }

    // ------------------------------------------------------------------ turns
    private void BeginTurn()
    {
        if (_engine.Phase == FightPhase.Finished)
        {
            EnterFinished();
            return;
        }

        if (_engine.Phase == FightPhase.AwaitingCount)
        {
            StartCount();
            return;
        }

        if (_engine.Phase == FightPhase.RoundBreak)
        {
            StartRoundBreak();
            return;
        }

        _playerMeter.Target = 0;
        _rivalMeter.Target = 0;

        if (_engine.StrikerIsA)
        {
            StartPlayerDeclare();
        }
        else
        {
            StartOpponentCharge();
        }
    }

    /// <summary>
    /// Every slap is called before it is thrown. The player calls the hand, then the
    /// number of the count the slap lands on; only then does the count run.
    /// </summary>
    private void StartPlayerDeclare()
    {
        _beat = Beat.PlayerDeclare;
        _handChosen = false;
        _declaredCount = 0;
        _playerFrame = FighterSheet.IdleA;
        _playerPoseTimer = 0.3f;
        _playerBody.Stand();
        _rivalBody.Rise();
        _playerMeter.Snap(0);
        _commentator.Say(Loc.T("fight.callInstruction"), 2.8f);
        BuildDeclareControls();
    }

    /// <summary>
    /// The two calls, one at a time: the chips for the count only come up once a hand
    /// has been named, so a slap can never be thrown without both being called.
    /// </summary>
    private void BuildDeclareControls()
    {
        ClearControls();

        if (!_handChosen)
        {
            Add(new Button(new Rectangle(460, 700, 480, 96), Loc.T("fight.leftHand"),
                () => PickHand(left: true), ButtonStyle.Ghost));
            Add(new Button(new Rectangle(980, 700, 480, 96), Loc.T("fight.rightHand"),
                () => PickHand(left: false), ButtonStyle.Ghost));
            return;
        }

        for (var count = 1; count <= 3; count++)
        {
            var chosen = count;
            Add(new Button(new Rectangle(560 + (count - 1) * 320, 830, 300, 96),
                CountWord(count), () => PickCount(chosen), ButtonStyle.Ghost));
        }
    }

    private void PickHand(bool left)
    {
        _handChosen = true;
        _leftHand = left;
        Context.Assets.Play("ui_click", 0.5f);
        _commentator.Say(Loc.T("fight.handCalled", HandName(_leftHand)), 2.2f);
        BuildDeclareControls();
    }

    private void PickCount(int count)
    {
        if (!_handChosen)
        {
            return;
        }

        _declaredCount = count;
        Context.Assets.Play("ui_confirm", 0.6f);
        StartPlayerMeasure();
    }

    private static string HandName(bool left) => Loc.T(left ? "fight.hand.left" : "fight.hand.right");

    private static string HandNameLower(bool left) =>
        Loc.T(left ? "fight.hand.left.lower" : "fight.hand.right.lower");

    /// <summary>The count chip's word, in the catalogue's own capitalisation.</summary>
    private static string CountWord(int count) => Loc.T($"fight.count.{Math.Clamp(count, 1, 3)}");

    /// <summary>The same word inside a sentence.</summary>
    private static string CountWordLower(int count) =>
        Loc.T($"fight.count.{Math.Clamp(count, 1, 3)}.lower");

    /// <summary>The referee's numbers, spelled out, up to the count-out.</summary>
    private static string SpellNumber(int count) => count is >= 1 and <= 10
        ? Loc.T($"fight.number.{count}")
        : count.ToString();

    private const float CountBeatSeconds = 0.75f;

    /// <summary>
    /// One number of the count: the palm is set on the cheek, drawn back, and put back
    /// on the cheek. The last of the three is the one that is measured, so the beat is
    /// shared out in those proportions.
    /// </summary>
    private const float PlaceEnd = 0.22f;
    private const float DrawBackEnd = 0.55f;

    /// <summary>
    /// The measure. Before a hand is thrown the palm is set on the other man's cheek:
    /// that is the distance, and lining it up is the player's job. Too high is the
    /// temple and a foul, too low is the jaw, and the nearer the cheek line it sits
    /// the cleaner the slap arrives.
    /// </summary>
    private void StartPlayerMeasure()
    {
        _beat = Beat.PlayerMeasure;
        _measureClock = 0f;
        _handOnCheek = 1f;
        _countCalled = 0;
        _countClock = 0f;

        // He does not get to place it dead on: the palm comes up off the mark and has
        // to be walked onto the cheek, or there would be nothing to adjust.
        var off = _engine.Rng.Next(0, 2) == 0 ? -1f : 1f;
        _measureOffset = off * (CheekBand + 8f + _engine.Rng.Next(0, 26));

        _playerFrame = FighterSheet.StrikeC;
        _playerPoseTimer = 60f;
        _playerBody.Stand();
        _rivalBody.Stand();
        _targetZoom = 1.18f;
        _focus = new Vector2(960f, 560f);
        _commentator.Say(Loc.T("fight.measure.prompt", HandNameLower(_leftHand)), 2.6f);

        BuildMeasureControls();
    }

    private void BuildMeasureControls()
    {
        ClearControls();
        Add(new Button(new Rectangle(560, 852, 340, 100), Loc.T("fight.measure.higher"),
            () => NudgeMeasure(-MeasureNudge), ButtonStyle.Ghost));
        Add(new Button(new Rectangle(940, 852, 440, 100), Loc.T("fight.measure.set"),
            SetTheHand, ButtonStyle.Normal));
        Add(new Button(new Rectangle(1420, 852, 340, 100), Loc.T("fight.measure.lower"),
            () => NudgeMeasure(MeasureNudge), ButtonStyle.Ghost));
    }

    private void NudgeMeasure(float delta)
    {
        _measureOffset = Math.Clamp(_measureOffset + delta, -MeasureLimit, MeasureLimit);
        Context.Assets.Play("ui_click", 0.4f);
    }

    private void TickPlayerMeasure(float dt)
    {
        _measureClock += dt;
        _handOnCheek = 1f;

        if (_measureClock >= MeasureSettleSeconds)
        {
            SetTheHand();
        }
    }

    /// <summary>
    /// The hand is placed. What was measured is what is thrown: the zone comes off
    /// the palm's height, and the aim off how near the cheek it sits.
    /// </summary>
    private void SetTheHand()
    {
        if (_beat != Beat.PlayerMeasure)
        {
            return;
        }

        _lockedZone = ZoneForOffset(_measureOffset, _leftHand);
        _lockedAim = AimForOffset(_measureOffset);
        _lockedTiming = 0.95f;
        _lockedCharge = 0.45f + 0.275f * (_declaredCount - 1);

        Context.Assets.Play("ui_confirm", 0.6f);
        StartPlayerCount();
    }

    /// <summary>
    /// Which part of the head a palm at that height is aimed at. The cheek line is the
    /// only place a legal slap starts from.
    /// </summary>
    private SlapZone ZoneForOffset(float offset, bool leftHand)
    {
        if (offset <= -CheekBand)
        {
            return SlapZone.Temple;
        }

        if (offset >= CheekBand)
        {
            return SlapZone.Jaw;
        }

        return leftHand ? SlapZone.LeftCheek : SlapZone.RightCheek;
    }

    private static float AimForOffset(float offset) =>
        Math.Clamp(1f - MathF.Abs(offset) / 130f, 0.2f, 1f);

    private static string MeasureZoneName(bool leftHand, float offset) =>
        ZoneName(offset <= -CheekBand ? SlapZone.Temple
            : offset >= CheekBand ? SlapZone.Jaw
            : leftHand ? SlapZone.LeftCheek : SlapZone.RightCheek);

    private static string ZoneName(SlapZone zone) => Loc.T(zone switch
    {
        SlapZone.LeftCheek => "fight.zone.leftCheek",
        SlapZone.RightCheek => "fight.zone.rightCheek",
        SlapZone.Jaw => "fight.zone.jaw",
        SlapZone.Temple => "fight.zone.temple",
        _ => "fight.zone.off"
    });

    /// <summary>
    /// The count, and the ritual that goes with it: on every number the arm draws back
    /// and puts the palm back on the cheek. The number the slap was called on is the
    /// one that goes through instead of coming back.
    /// </summary>
    private void StartPlayerCount()
    {
        _beat = Beat.PlayerCount;
        _countCalled = 0;
        _countClock = 0f;
        _handOnCheek = 1f;
        _playerMeter.Snap(0);
        _playerFrame = FighterSheet.StrikeC;
        _playerPoseTimer = 0.4f;
        _playerBody.Stand();
        _rivalBody.Stand();
        _targetZoom = 1.24f;
        _focus = new Vector2(960f, 560f);
        _commentator.Say(
            Loc.T("fight.count.called", HandName(_leftHand), CountWordLower(_declaredCount),
                Loc.T(_declaredCount > 1 ? "fight.count.practice" : "fight.count.live")), 2.6f);

        ClearControls();
    }

    private void TickPlayerCount(float dt)
    {
        _countClock += dt;

        var called = Math.Min(_declaredCount, 1 + (int)(_countClock / CountBeatSeconds));
        if (called != _countCalled)
        {
            _countCalled = called;
            _playerMeter.Impulse(4f);
            Context.Assets.Play("ui_click", 0.35f);

            if (_countCalled >= _declaredCount)
            {
                Context.Assets.Play("bell", 0.25f);
            }
        }

        var phase = _countClock % CountBeatSeconds / CountBeatSeconds;
        var live = _countCalled >= _declaredCount;

        // The called number: the arm goes back, and this time it does not come back to
        // the cheek - it comes through.
        if (live && phase >= DrawBackEnd)
        {
            StartPlayerSwing();
            return;
        }

        // Practice: the palm is placed on the cheek, then the arm draws back, then the
        // palm is placed back on the cheek for the next number.
        var cocked = phase > PlaceEnd && phase < DrawBackEnd;
        _handOnCheek = cocked ? 0f : 1f;
        _playerFrame = cocked ? FighterSheet.WindupB : FighterSheet.StrikeC;
        _playerPoseTimer = 0.5f;

        if (cocked)
        {
            _playerBody.WindUp();
        }
        else
        {
            _playerBody.Stand();
        }

        _playerMeter.Target = live ? 1f : Gfx.EaseInOut(phase);
    }

    private void StartPlayerSwing()
    {
        _beat = Beat.PlayerSwing;
        _beatTimer = SwingSeconds;
        _playerFrame = FighterSheet.StrikeA;
        _playerPoseTimer = SwingSeconds + 0.2f;
        _strikerIsPlayer = true;
        _handOnCheek = 0f;
        _playerBody.Lunge();
        _swingGhost = 0.18f;
        _targetZoom = 1.14f;
        _focus = new Vector2(900f, 520f);
        Context.Assets.Play("whiff", 0.25f, 0.3f);
    }

    /// <summary>
    /// The arm coming through: the strike frames play across the lunge instead of
    /// one still frame, so the hand can be seen travelling to the cheek.
    /// </summary>
    private void TickSwing(bool player)
    {
        var progress = 1f - Math.Clamp(_beatTimer / SwingSeconds, 0f, 1f);
        var frame = progress < 0.30f ? FighterSheet.StrikeA
            : progress < 0.65f ? FighterSheet.StrikeB
            : FighterSheet.StrikeC;

        if (player)
        {
            _playerFrame = frame;
            _playerPoseTimer = 0.2f;
        }
        else
        {
            _rivalFrame = frame;
            _rivalPoseTimer = 0.2f;
        }
    }

    private void ResolvePlayerSlap()
    {
        var attempt = new SlapAttempt
        {
            PowerCharge = _lockedCharge,
            Aim = _lockedAim,
            Timing = _lockedTiming,
            TargetZone = _lockedZone,
            OpenHand = true,
            DefenderBraced = true,
            SteppedForward = false
        };

        var result = _engine.DeliverSlap(attempt, braced: true);
        _playerFrame = FighterSheet.Follow;
        _playerPoseTimer = 0.2f;
        ReportSlap(result, byPlayer: true, attempt.TargetZone);

        // Off the follow-through he walks himself back to his mark, slowly enough
        // that the shot is still being read when the replay comes up.
        _playerBody.Stand();
        _playerBody.Rate = 3.2f;
        _beat = Beat.PlayerRecover;
        _beatTimer = result.Knockdown ? 1.35f : 0.8f;
    }

    private void StartOpponentCharge()
    {
        _beat = Beat.OpponentCharge;

        // The rival calls his slap the same way the player does: a hand and the count
        // it lands on. The window he gives you to answer is that count.
        _rivalLeftHand = _engine.Rng.Chance(0.5);
        _rivalCount = _engine.Rng.Next(1, 4);
        _rivalFrame = FighterSheet.WindupA;
        _rivalPoseTimer = 0.5f;
        _rivalBody.WindUp();
        _playerBody.Stand();

        _defence = Defence.Take;
        _defenceLocked = false;

        // His window is his measure, then one draw per number, and the called number
        // goes through at the same point of the beat the player's does.
        _defenceWindow = RivalMeasureSeconds +
                         CountBeatSeconds * (_rivalCount - 1 + DrawBackEnd);
        _defenceTimer = _defenceWindow;

        // Where his palm comes up and where he walks it to: he has to measure too.
        _rivalMeasureFrom = _engine.Rng.Next(0, 2) == 0 ? -58f : 52f;
        _rivalMeasureOffset = _rivalMeasureFrom;
        _rivalHandOnCheek = 1f;

        ClearControls();
        // What each answer costs rides on the chip itself. As one long line under the
        // row it ran off both edges of a phone and printed over the chips.
        Add(new Button(new Rectangle(560, 760, 250, 96), Loc.T("fight.defence.take"),
            () => PickDefence(Defence.Take), ButtonStyle.Ghost)
        {
            SubText = Loc.T("fight.defence.takeNote")
        });
        Add(new Button(new Rectangle(830, 760, 250, 96), Loc.T("fight.defence.brace"),
            () => PickDefence(Defence.Brace), ButtonStyle.Normal)
        {
            SubText = Loc.T("fight.defence.braceNote")
        });
        Add(new Button(new Rectangle(1100, 760, 250, 96), Loc.T("fight.defence.flinch"),
            () => PickDefence(Defence.Flinch), ButtonStyle.Danger)
        {
            SubText = Loc.T("fight.defence.flinchNote")
        });
        Add(new Button(new Rectangle(1370, 760, 250, 96), Loc.T("fight.defence.stepOff"),
            () => PickDefence(Defence.Step), ButtonStyle.Danger)
        {
            SubText = Loc.T("fight.defence.stepOffNote")
        });

        var foulsLeft = _engine.ConditionA.FoulsRemaining(_engine.Rules);
        _commentator.Say(foulsLeft > 0
            ? Loc.T("fight.rivalCalls", Rival.LastName, HandNameLower(_rivalLeftHand),
                CountWordLower(_rivalCount))
            : Loc.T("fight.noFoulsLeft"), 2.8f);
    }

    private void PickDefence(Defence defence)
    {
        if (_defenceLocked)
        {
            return;
        }

        _defence = defence;
        _defenceLocked = true;
        _defenceTimer = 0f;
        Context.Assets.Play("ui_click", 0.6f);
    }

    private void TickOpponentCharge(float dt, InputState input)
    {
        _defenceTimer -= dt;

        var elapsed = Math.Max(0f, _defenceWindow - _defenceTimer);

        // First the measure: his palm comes up off the mark and he walks it onto your
        // cheek, exactly as he made you do on your own turn.
        if (elapsed < RivalMeasureSeconds)
        {
            var t = Gfx.EaseInOut(Math.Clamp(elapsed / (RivalMeasureSeconds * 0.8f), 0f, 1f));
            _rivalMeasureOffset = Mathf.Lerp(_rivalMeasureFrom, 2f, t);
            _rivalHandOnCheek = 1f;
            _rivalFrame = FighterSheet.StrikeC;
            _rivalPoseTimer = 0.4f;
            _rivalBody.WindUp();
            _rivalMeter.Target = 0.2f * t;
            return;
        }

        var countElapsed = elapsed - RivalMeasureSeconds;
        var called = Math.Clamp(1 + (int)(countElapsed / CountBeatSeconds), 1, _rivalCount);
        var phase = countElapsed % CountBeatSeconds / CountBeatSeconds;
        var live = called >= _rivalCount;

        // The draw and the palm coming back, number by number.
        var cocked = phase > PlaceEnd && phase < DrawBackEnd;
        _rivalHandOnCheek = cocked ? 0f : 1f;
        _rivalFrame = cocked ? FighterSheet.WindupB : FighterSheet.StrikeC;
        _rivalPoseTimer = 0.5f;

        if (cocked)
        {
            _rivalBody.WindUp();
        }
        else
        {
            _rivalBody.Stand();
        }

        _rivalMeter.Target = Mathf.Lerp(0.25f, 0.95f,
            Math.Clamp(countElapsed / (CountBeatSeconds * _rivalCount), 0f, 1f));

        if (_defenceTimer <= 0f)
        {
            ClearControls();
            StartOpponentSwing();
        }
    }

    private void StartOpponentSwing()
    {
        _beat = Beat.OpponentSwing;
        _beatTimer = SwingSeconds;
        _rivalFrame = FighterSheet.StrikeA;
        _rivalPoseTimer = SwingSeconds + 0.2f;
        _strikerIsPlayer = false;
        _rivalHandOnCheek = 0f;
        _rivalBody.Lunge();
        _swingGhost = 0.18f;
        _targetZoom = 1.14f;
        _focus = new Vector2(1020f, 520f);
        Context.Assets.Play("whiff", 0.35f, 0.2f);
        _playerFrame = _defence switch
        {
            Defence.Brace => FighterSheet.Brace,
            Defence.Flinch => FighterSheet.Hit,
            Defence.Step => FighterSheet.Hit,
            _ => FighterSheet.Brace
        };
        _playerPoseTimer = 0.6f;
    }

    private void ResolveOpponentSlap()
    {
        var rival = Rival;
        var skill = Math.Clamp(rival.Overall / 100.0, 0.2, 0.95);
        var attempt = FightEngine.GenerateAiAttempt(rival, skill, _engine.Rng);
        attempt.DefenderBraced = _defence == Defence.Brace;
        attempt.DefenderFlinched = _defence == Defence.Flinch;
        attempt.DefenderSteppedOut = _defence == Defence.Step;

        var result = _engine.DeliverSlap(attempt);
        _rivalFrame = FighterSheet.Follow;
        _rivalPoseTimer = 0.2f;
        ReportSlap(result, byPlayer: false, attempt.TargetZone);

        _rivalBody.Stand();
        _rivalBody.Rate = 3.2f;
        _beat = Beat.OpponentRecover;
        _beatTimer = result.Knockdown ? 1.35f : 0.8f;
        ClearControls();
    }

    private void ReportSlap(SlapResult result, bool byPlayer, SlapZone zone)
    {
        _replayZone = zone;

        var defender = byPlayer ? _rivalBody : _playerBody;
        var defenderX = defender.X;

        // The burst follows the cheek the hand landed on, not the ring.
        _impactOnPlayer = !byPlayer;

        if (result.Verdict == SlapVerdict.Miss)
        {
            Context.Assets.Play("whiff", 0.6f);
            AddPopup(Loc.T("fight.popup.miss"), new Vector2(defenderX, 320), Theme.Muted);
            _commentator.Say(result.Commentary, 2.6f);
            return;
        }

        if (result.Verdict == SlapVerdict.Foul)
        {
            Context.Assets.Play("crowd_boo", 0.75f);
            _shake = 6f;
            AddPopup(Loc.T("fight.popup.foul"), new Vector2(defenderX, 300), Theme.Red, 1.3f);
            _commentator.Say(result.Commentary, 3.2f);
            StrikeEffect(result, defender);
            ScheduleReplay(result.Commentary, knockdown: false, 0.45f);
        }
        else
        {
            var heavy = result.Damage >= 22;
            Context.Assets.Play(heavy ? "slap_heavy" : "slap_light", heavy ? 0.9f : 0.7f,
                0f, byPlayer ? 0.25f : -0.25f);
            AddPopup(result.Damage.ToString(), new Vector2(defenderX, 330),
                heavy ? Theme.Gold : Theme.White, heavy ? 1.4f : 1.1f);
            _shake = 4f + (float)result.Impact * 10f;
            _commentator.Say(result.Commentary, 2.8f);
            StrikeEffect(result, defender);
            ScheduleReplay(result.Commentary, knockdown: false, 0.45f);
        }

        if (result.Fouls.Count > 0)
        {
            foreach (var foul in result.Fouls)
            {
                AddPopup($"{foul.Kind.Display()}", new Vector2(defenderX, 260), Theme.RedLight, 0.9f);
            }
        }

        AddSlapMark(onPlayer: !byPlayer, zone);

        if (result.Knockdown)
        {
            Context.Assets.Play("crowd_cheer", 0.85f);
            Context.Assets.Play("ko", 0.9f);
            AddPopup(Loc.T("fight.popup.knockdown"), new Vector2(defenderX, 220), Theme.Gold, 1.6f);
            ScheduleReplay(result.Commentary, knockdown: true, 1.05f);
            _shake = 22f;
            _targetZoom = 1.34f;
            _focus = new Vector2(defenderX, 640);
            _timeScale = 0.45f;
            _koSlowMotion = true;

            // He goes over on the spot: the body tips past the point of no return,
            // then the lying frame takes over once the tilt has put him on the mat.
            if (byPlayer)
            {
                _rivalFrame = FighterSheet.Ko;
                _rivalPoseTimer = 6f;
                _rivalBody.Fall();
            }
            else
            {
                _playerFrame = FighterSheet.Ko;
                _playerPoseTimer = 6f;
                _playerBody.Fall();
            }

            return;
        }

        if (byPlayer)
        {
            _rivalFrame = result.Damage > 26 ? FighterSheet.Stagger : FighterSheet.Hit;
            _rivalPoseTimer = 0.55f;
        }
        else
        {
            _playerFrame = result.Damage > 26 ? FighterSheet.Stagger : FighterSheet.Hit;
            _playerPoseTimer = 0.55f;
        }
    }

    /// <summary>
    /// What a landed hand does to the man taking it: he is knocked off his mark, the
    /// cheek takes the burst, and the frame blinks white on the heavy ones.
    /// </summary>
    private void StrikeEffect(SlapResult result, Body defender)
    {
        var force = Math.Clamp((float)result.Impact, 0.2f, 1f);

        _impactPower = force;
        _impactTimer = ImpactSeconds;
        _flash = result.Damage >= 22 ? 0.55f : 0.3f;
        _swingGhost = Math.Max(_swingGhost, 0.1f);

        if (!defender.Down)
        {
            defender.Recoil(force);
        }
    }

    /// <summary>
    /// Leaves red finger marks on the cheek the hand landed on. The face turns
    /// towards the striker, so the mark goes on that side of the head.
    /// </summary>
    private void AddSlapMark(bool onPlayer, SlapZone zone)
    {
        var side = onPlayer ? 1f : -1f;
        var (x, y) = zone switch
        {
            SlapZone.RightCheek => (26f, 18f),
            SlapZone.LeftCheek => (10f, 12f),
            SlapZone.Temple => (-4f, -22f),
            SlapZone.Ear => (2f, -8f),
            _ => (18f, 34f)
        };

        _slapMarks.Add((onPlayer, new Vector2(side * x, y), 0f, true));
        while (_slapMarks.Count > SlapMarkLimit)
        {
            _slapMarks.RemoveAt(0);
        }
    }

    private void AddPopup(string text, Vector2 position, Color colour, float scale = 1f)
    {
        _popups.Add((text, position, 1.5f, colour, scale));
    }

    /// <summary>
    /// Starts the high-resolution slap replay. Every landed shot gets one; a
    /// knockdown plays the longer strip where the head keeps going down.
    /// </summary>
    private void StartReplay(string commentary, bool knockdown)
    {
        _replayTexture = knockdown ? "replay_ko" : "replay_slap";
        _replayFrames = knockdown ? 10 : 8;
        _replayCommentary = commentary;
        _replayDuration = _replayFrames * ReplayFrameSeconds + ReplayTailSeconds;
        _replayTimer = _replayDuration;
    }

    /// <summary>
    /// Holds the replay back until the shot has played out in the ring. The replay
    /// freezes the bout while it runs, so on a knockdown it waits for the fall.
    /// </summary>
    private void ScheduleReplay(string commentary, bool knockdown, float delay)
    {
        _pendingReplayCommentary = commentary;
        _pendingReplayKnockdown = knockdown;
        _replayDelay = delay;
    }

    private void ContinueAfterSlap()
    {
        _targetZoom = 1f;
        _focus = new Vector2(960f, 620f);

        if (_engine.Phase == FightPhase.Finished)
        {
            EnterFinished();
            return;
        }

        if (_engine.Phase == FightPhase.AwaitingCount)
        {
            StartCount();
            return;
        }

        if (_engine.Phase == FightPhase.RoundBreak)
        {
            StartRoundBreak();
            return;
        }

        BeginTurn();
    }

    /// <summary>
    /// The referee's count. The rules are read up front - he knows whether the man
    /// gets up and at which number - so the count on screen stops where the count
    /// really stopped instead of always running to ten.
    /// </summary>
    private void StartCount()
    {
        _beat = Beat.Count;
        _countTimer = 0f;
        _countHold = 0f;
        _refereeIn = 0f;
        _timeScale = 1f;
        _koSlowMotion = false;
        Context.Assets.Play("bell_end", 0.5f);

        _downIsPlayer = !_engine.StrikerIsA;
        _countResult = _engine.ResolveCount();
        _countTarget = Math.Clamp(_countResult.Count, 1, _engine.Rules.CountOutSeconds);
        _countValue = 1;

        _commentator.Say(_downIsPlayer
            ? Loc.T("fight.down.you")
            : Loc.T("fight.down.rival", Rival.LastName), 3f);
    }

    private void TickCount(float dt)
    {
        _refereeIn = Math.Min(1f, _refereeIn + dt * 2.4f);

        if (_countHold > 0f)
        {
            _countHold -= dt;
            if (_countHold <= 0f)
            {
                EndCount();
            }

            return;
        }

        _countTimer += dt;
        if (_countTimer < 0.62f)
        {
            return;
        }

        _countTimer = 0f;

        if (_countValue >= _countTarget)
        {
            _countHold = _countResult?.BeatTheCount == true ? 1.1f : 1.6f;
            return;
        }

        _countValue++;
        Context.Assets.Play("ui_click", 0.4f, -0.1f);
    }

    private void EndCount()
    {
        var down = _downIsPlayer ? _playerBody : _rivalBody;

        if (_countResult is { BeatTheCount: true })
        {
            down.Rise();

            if (_downIsPlayer)
            {
                _playerFrame = FighterSheet.Tired;
                _playerPoseTimer = 1.2f;
            }
            else
            {
                _rivalFrame = FighterSheet.Tired;
                _rivalPoseTimer = 1.2f;
            }

            Context.Assets.Play("crowd_cheer", 0.6f);
        }

        ContinueAfterSlap();
    }

    /// <summary>
    /// The ring announces each round on a card. It is the same card every time, which
    /// is the point: nobody in the room is left guessing which round this is.
    /// </summary>
    private void StartRoundPlacard()
    {
        _beat = Beat.RoundPlacard;
        _beatTimer = RoundPlacardSeconds;
        _targetZoom = 1f;
        _focus = new Vector2(960f, 620f);
        _handOnCheek = 0f;
        _rivalHandOnCheek = 0f;
        _playerBody.Stand();
        _rivalBody.Stand();
        _playerFrame = FighterSheet.IdleA;
        _rivalFrame = FighterSheet.IdleA;
        _playerPoseTimer = 0.6f;
        _rivalPoseTimer = 0.6f;
        _playerMeter.Snap(0);
        _rivalMeter.Snap(0);
        ClearControls();

        Context.Assets.Play("bell", 0.55f);
        _commentator.Say(Loc.T("fight.roundOf", SpellNumber(_engine.Round), _engine.Rules.Rounds),
            2.4f);
    }

    private void StartRoundBreak()
    {
        _beat = Beat.RoundBreak;
        _beatTimer = 1.6f;
        _targetZoom = 1f;
        _focus = new Vector2(960f, 620f);
        _playerBody.Stand();
        _rivalBody.Stand();
        _handOnCheek = 0f;
        _rivalHandOnCheek = 0f;

        _commentator.Say(Loc.T("fight.endOfRound", _engine.Round, ScoreLine()), 3.4f);
        Context.Assets.Play("bell", 0.6f);
    }

    private string ScoreLine()
    {
        var a = _engine.ConditionA;
        var b = _engine.ConditionB;
        return Loc.T("fight.scoreLine", Player.LastName, a.LegalSlaps, a.DamageDealt,
            Rival.LastName, b.LegalSlaps, b.DamageDealt);
    }

    private void TickRoundBreak(float dt)
    {
        _beatTimer -= dt;
        if (_beatTimer <= 0f && _engine.Phase == FightPhase.RoundBreak)
        {
            _engine.StartNextRound();
            StartRoundPlacard();
        }
    }

    private void EnterFinished()
    {
        if (_beat == Beat.Finished)
        {
            return;
        }

        _beat = Beat.Finished;
        _beatTimer = 2.4f;
        _targetZoom = 1.1f;
        _focus = new Vector2(960f, 620f);

        var playerWon = _engine.Result?.WinnerId == Player.Id;
        _playerFrame = playerWon ? FighterSheet.Victory : FighterSheet.Ko;
        _rivalFrame = playerWon ? FighterSheet.Ko : FighterSheet.Victory;
        _playerPoseTimer = 5f;
        _rivalPoseTimer = 5f;

        // The winner is on his feet with his arm up; the loser goes over, unless he
        // is already down on the canvas from the count that ended it.
        var winner = playerWon ? _playerBody : _rivalBody;
        var loser = playerWon ? _rivalBody : _playerBody;

        winner.Rise();
        if (loser.Standing)
        {
            loser.Fall();
        }

        if (_engine.Result is { } result)
        {
            Context.Assets.Play(result.IsDraw ? "crowd_boo" : "applause_wild", 0.85f);
            _commentator.Say(result.Headline + ".", 4f);
        }

        ClearControls();
    }

    private void LeaveTheRing()
    {
        if (_applied)
        {
            return;
        }

        _applied = true;

        var result = _engine.Result;
        if (result is null)
        {
            Context.Screens.SetRoot(new HubScreen(Context));
            return;
        }

        var outcome = Context.Career.ApplyResult(State, result, _weighIn);
        Context.Persist();
        Context.Screens.SetRoot(new ResultScreen(Context, outcome));
    }

    // ------------------------------------------------------------------- draw
    public override void Draw(SpriteBatch batch, GameTime time)
    {
        if (!HasCareer || _engine is null)
        {
            DrawNoCareer(batch);
            return;
        }
        var league = Leagues.Get(State.PlayerLeagueId);
        var venue = league.Prestige >= 4 ? "arena_world" : "arena_world";
        var arena = Context.Assets.Tex(venue);

        var camera = Matrix.CreateTranslation(-_focus.X, -_focus.Y, 0f) *
                     Matrix.CreateScale(_zoom, _zoom, 1f) *
                     Matrix.CreateTranslation(960f, 620f, 0f);

        if (_shake > 0.1f)
        {
            var rng = new Random((int)(_sceneTime * 1000));
            camera *= Matrix.CreateTranslation(
                (float)(rng.NextDouble() * 2 - 1) * _shake,
                (float)(rng.NextDouble() * 2 - 1) * _shake, 0f);
        }

        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, camera * Context.VirtualTransform);

        batch.Draw(arena, new Rectangle(-100, -100, GameSettings.VirtualWidth + 200,
            GameSettings.VirtualHeight + 200), Color.White);

        DrawFighters(batch, canvasLayer: false);
        DrawReferee(batch);
        DrawMeasureHand(batch);

        // The slap table stands in front of both fighters; there are no ropes.
        batch.Draw(Context.Assets.Tex("stage_front"),
            new Rectangle(0, 780, GameSettings.VirtualWidth, 340), Color.White);

        // Anyone on the canvas lies in front of the table, or he would be behind it.
        DrawFighters(batch, canvasLayer: true);
        DrawImpact(batch);

        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Context.VirtualTransform);

        DrawHud(batch);
        DrawFlash(batch);
        DrawPopups(batch);

        _commentator.Draw(batch, Context.Assets, new Vector2(620, 1075), 760f, tailLeft: true);

        DrawBeatOverlay(batch);
        DrawReplay(batch);

        foreach (var button in Buttons.ToArray())
        {
            button.Draw(batch, Context.Assets);
        }
    }

    /// <summary>
    /// The slap replay: the high-resolution landing strip played back a frame at
    /// a time, with the commentator's line for that slap underneath.
    /// </summary>
    private void DrawReplay(SpriteBatch batch)
    {
        if (_replayTimer <= 0f || _replayFrames <= 0)
        {
            return;
        }

        var elapsed = _replayDuration - _replayTimer;
        var frame = Math.Clamp((int)(elapsed / ReplayFrameSeconds), 0, _replayFrames - 1);
        var texture = Context.Assets.Tex(_replayTexture);
        var source = new Rectangle((frame % ReplayColumns) * ReplayFrameWidth,
            (frame / ReplayColumns) * ReplayFrameHeight, ReplayFrameWidth, ReplayFrameHeight);

        var alpha = Math.Clamp(elapsed / 0.07f, 0f, 1f) *
                    Math.Clamp(_replayTimer / 0.16f, 0f, 1f);

        // Fit the frame to the screen height and let it bleed off the sides.
        var width = (int)(GameSettings.VirtualHeight * (ReplayFrameWidth / (float)ReplayFrameHeight));
        var rect = new Rectangle((GameSettings.VirtualWidth - width) / 2, 0, width,
            GameSettings.VirtualHeight);

        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
            Color.Black * alpha);
        batch.Draw(texture, rect, source, Color.White * alpha);
        DrawReplayMark(batch, rect, alpha);

        batch.Fill(new Rectangle(0, 884, GameSettings.VirtualWidth, 196), Color.Black * (alpha * 0.62f));
        batch.Text(Context.Assets.Font("medium"), Loc.T("fight.replay"),
            new Vector2(70, 40), Theme.Gold * alpha, 1f, TextAlign.Left);
        batch.Text(Context.Assets.Font("tiny"),
            Loc.T(_replayTexture == "replay_ko" ? "fight.replay.knockdown" : "fight.replay.slap"),
            new Vector2(72, 92), Theme.Muted * alpha, 1f, TextAlign.Left);

        var lines = Context.Assets.Font("body").Wrap(_replayCommentary, 1720f);
        var y = 912f;
        foreach (var line in lines)
        {
            batch.Text(Context.Assets.Font("body"), line,
                new Vector2(960, y), Theme.White * alpha, 1f, TextAlign.Centre);
            y += Context.Assets.Font("body").LineHeight + 6f;
        }
    }

    /// <summary>
    /// The same red mark, laid over the close-up so the replay shows the cheek
    /// already marked rather than a clean face after every shot.
    /// </summary>
    private void DrawReplayMark(SpriteBatch batch, Rectangle rect, float alpha)
    {
        var (dx, dy) = _replayZone switch
        {
            SlapZone.Jaw => (0.01f, 0.07f),
            SlapZone.Temple => (-0.03f, -0.13f),
            SlapZone.Ear => (-0.01f, -0.07f),
            SlapZone.LeftCheek => (-0.03f, 0f),
            _ => (0f, 0f)
        };

        var cx = rect.Left + rect.Width * (0.72f + dx);
        var cy = rect.Top + rect.Height * (0.575f + dy);
        var scale = rect.Height / 1080f;
        var fade = alpha * 0.9f;

        var colour = new Color(198, 40, 32) * (0.85f * fade);
        var soft = new Color(198, 40, 32) * (0.20f * fade);
        for (var i = 0; i < 4; i++)
        {
            var from = new Vector2(cx - 30f * scale + i * 18f * scale, cy - 38f * scale + i * 5f * scale);
            var to = new Vector2(cx - 14f * scale + i * 18f * scale, cy + 34f * scale + i * 5f * scale);
            batch.Line(from, to, soft, 20f * scale);
            batch.Line(from, to, colour, 9f * scale);
        }
    }

    /// <summary>
    /// The two of them drawn where they actually are: the striker stepping in with
    /// the arm going through, the man taking it knocked off his mark, and either of
    /// them down on the canvas.
    /// </summary>
    private void DrawFighters(SpriteBatch batch, bool canvasLayer)
    {
        if (_playerBody.Down == canvasLayer)
        {
            DrawSwingTrail(batch, _playerBody, "fighter_player", _playerFrame, false);
            FighterSheet.Draw(batch, Context.Assets.Tex("fighter_player"), _playerFrame,
                new Vector2(_playerBody.X, _playerBody.Y), FighterScale, false, Color.White,
                _playerBody.Tilt);
        }

        if (_rivalBody.Down == canvasLayer)
        {
            DrawSwingTrail(batch, _rivalBody, "fighter_rival", _rivalFrame, true);
            FighterSheet.Draw(batch, Context.Assets.Tex("fighter_rival"), _rivalFrame,
                new Vector2(_rivalBody.X, _rivalBody.Y), FighterScale, true, Color.White,
                _rivalBody.Tilt);
        }

        if (canvasLayer)
        {
            return;
        }

        foreach (var mark in _slapMarks)
        {
            DrawSlapMark(batch, mark);
        }
    }

    /// <summary>
    /// Three faded copies of the arm left behind by the step-in: the smear that
    /// turns a fast pose into a swing.
    /// </summary>
    private void DrawSwingTrail(SpriteBatch batch, Body body, string texture, int frame, bool flipped)
    {
        if (_swingGhost <= 0f || body.Down || body != (_strikerIsPlayer ? _playerBody : _rivalBody))
        {
            return;
        }

        var tex = Context.Assets.Tex(texture);
        for (var i = 3; i >= 1; i--)
        {
            var position = new Vector2(body.X - i * 80f * body.Facing, body.Y);
            FighterSheet.Draw(batch, tex, frame, position, FighterScale, flipped,
                Color.White * (0.09f * (4 - i)), body.Tilt);
        }
    }

    /// <summary>
    /// The measured palm: set on the cheek, drawn over the man taking it so there is
    /// no doubt where the hand is. It sits there through the measure and rides the
    /// cheek on every practice draw, and it is hidden while the arm is back.
    /// </summary>
    private void DrawMeasureHand(SpriteBatch batch)
    {
        var playerStrikes = _beat is Beat.PlayerMeasure or Beat.PlayerCount;
        var rivalStrikes = _beat == Beat.OpponentCharge;

        if (!playerStrikes && !rivalStrikes)
        {
            return;
        }

        var defender = playerStrikes ? _rivalBody : _playerBody;
        var offset = playerStrikes ? _measureOffset : _rivalMeasureOffset;
        var alpha = playerStrikes
            ? (_beat == Beat.PlayerMeasure ? 1f : _handOnCheek)
            : _rivalHandOnCheek;

        if (defender.Down)
        {
            return;
        }

        var cheek = CheekOf(defender);

        // The mark the palm has to sit on, so moving it to the cheek is obvious.
        DrawReticle(batch, cheek, 48f, (playerStrikes ? Theme.Green : Theme.Amber) * 0.6f);

        if (alpha <= 0.01f)
        {
            return;
        }

        var hand = Context.Assets.Tex("hand_closeup");
        var rotation = -MathF.PI * 0.10f;
        var effects = SpriteEffects.None;

        // The palm comes from the striker's side, fingers across the face.
        if (rivalStrikes)
        {
            rotation = -rotation;
            effects = SpriteEffects.FlipHorizontally;
        }

        batch.Sprite(hand, cheek + new Vector2(0f, offset), Color.White * alpha, HandScale,
            effects, rotation, 0f, HandPalm);
    }

    /// <summary>A dashed ring on the cheek: the spot the palm is being measured onto.</summary>
    private static void DrawReticle(SpriteBatch batch, Vector2 centre, float radius, Color colour)
    {
        const int segments = 28;
        for (var i = 0; i < segments; i += 2)
        {
            var a0 = i * MathF.PI * 2f / segments;
            var a1 = (i + 1) * MathF.PI * 2f / segments;
            batch.Line(centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius,
                centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius, colour, 5f);
        }

        batch.Line(centre + new Vector2(-radius - 16f, 0f),
            centre + new Vector2(-radius + 14f, 0f), colour, 4f);
        batch.Line(centre + new Vector2(radius - 14f, 0f),
            centre + new Vector2(radius + 16f, 0f), colour, 4f);
    }

    /// <summary>
    /// The referee coming in over the fallen man, arm sweeping with each number.
    /// </summary>
    private void DrawReferee(SpriteBatch batch)
    {
        if (_beat != Beat.Count)
        {
            return;
        }

        var down = _downIsPlayer ? _playerBody : _rivalBody;
        var home = down.X + 165f * down.Facing;
        var x = home + (1f - _refereeIn) * 420f * down.Facing;

        // Into position, then sweeping with the count.
        var frame = _refereeIn < 1f ? 1 : _countValue % 2 == 0 ? 0 : 2;

        Gfx.Frame(batch, Context.Assets.Tex("referee"), 340, 500, frame,
            new Vector2(x, 1040f), Color.White, 1.25f);

    }

    private void DrawFlash(SpriteBatch batch)
    {
        if (_flash <= 0f)
        {
            return;
        }

        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
            Color.White * (_flash * 0.22f));
    }

    /// <summary>Four finger streaks and a warm glow, laid on the struck cheek.</summary>
    private void DrawSlapMark(SpriteBatch batch, (bool OnPlayer, Vector2 Offset, float Age, bool Fresh) mark)
    {
        var alpha = mark.Fresh ? 0.88f : 0.62f;

        var head = mark.OnPlayer ? PlayerHead : RivalHead;
        var centre = head + mark.Offset;

        batch.Fill(centre.X - 34f, centre.Y - 40f, 68f, 80f, new Color(196, 46, 38) * (0.16f * alpha));

        var colour = new Color(208, 42, 34) * (0.82f * alpha);
        for (var i = 0; i < 4; i++)
        {
            var from = centre + new Vector2(-15f + i * 10f, -18f + i * 3f);
            var to = centre + new Vector2(-8f + i * 10f, 15f + i * 3f);
            batch.Line(from, to, colour, 6f);
        }
    }

    /// <summary>
    /// The hand landing: a burst on the cheek that took it, speed lines flung out
    /// of it, both gone in a quarter of a second.
    /// </summary>
    private void DrawImpact(SpriteBatch batch)
    {
        if (_impactTimer <= 0f)
        {
            return;
        }

        var t = 1f - _impactTimer / ImpactSeconds;
        var frame = Math.Clamp((int)(t * 5f), 0, 4);
        var scale = (0.6f + 0.6f * t) * (0.8f + 0.45f * _impactPower);
        var alpha = 1f - t * 0.7f;

        // It rides the cheek it landed on: the head comes away from the hand fast,
        // and a burst left hanging in the air between them reads as a miss.
        var body = _impactOnPlayer ? _playerBody : _rivalBody;
        var at = HeadOf(body) + new Vector2(40f * body.Facing, 0f);

        // Centred on the cheek: Gfx.Frame anchors at the feet, which suits a fighter
        // but would float a burst half a frame above the face.
        batch.Draw(Context.Assets.Tex("impact"), at, new Rectangle(frame * 300, 0, 300, 300),
            Color.White * alpha, 0f, new Vector2(150f, 150f), scale, SpriteEffects.None, 0f);

        var reach = 130f * _impactPower * (0.35f + t);
        for (var i = 0; i < 9; i++)
        {
            var angle = i * MathF.PI * 2f / 9f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            batch.Line(at + dir * 34f, at + dir * reach, Theme.White * (0.45f * alpha), 6f);
        }
    }

    private void DrawHud(SpriteBatch batch)
    {
        var assets = Context.Assets;

        // Dual spring-loaded power bars, names underneath.
        var leftBar = new Rectangle(50, 34, 660, 62);
        var rightBar = new Rectangle(1210, 34, 660, 62);

        _playerMeter.Draw(batch, assets, leftBar);
        _rivalMeter.Draw(batch, assets, rightBar);

        var nameFont = assets.Font("body");
        batch.Text(nameFont, Player.DisplayName.ToUpperInvariant(),
            new Vector2(leftBar.Left + 6, leftBar.Bottom + 10), Theme.BlueLight);
        batch.Text(nameFont, Rival.DisplayName.ToUpperInvariant(),
            new Vector2(rightBar.Right - 6, rightBar.Bottom + 10), Theme.RedLight, 1f, TextAlign.Right);

        DrawFighterBars(batch, _engine.ConditionA, new Rectangle(leftBar.Left, leftBar.Bottom + 50, 520, 16),
            Theme.Blue, false);
        DrawFighterBars(batch, _engine.ConditionB, new Rectangle(rightBar.Right - 520, rightBar.Bottom + 50, 520, 16),
            Theme.Red, true);

        // Centre: round and fouls.
        var centre = 960;
        batch.Text(assets.Font("medium"),
            Loc.T("fight.hud.round", Math.Min(_engine.Round, _engine.Rules.Rounds)),
            new Vector2(centre, 26), Theme.Gold, 1f, TextAlign.Centre);

        var pips = Math.Min(_engine.Rules.Rounds, 5);
        var pipStart = centre - (pips * 46) / 2;
        for (var i = 0; i < pips; i++)
        {
            var texture = assets.Tex(i < _engine.Round ? "pip_on" : "pip_off");
            batch.Draw(texture, new Vector2(pipStart + i * 46 + 20, 100), null, Color.White, 0f,
                new Vector2(texture.Width / 2f, texture.Height / 2f), 0.55f, SpriteEffects.None, 0f);
        }

        DrawFoulPips(batch, _engine.ConditionA, centre - 190, 148, Theme.Blue);
        DrawFoulPips(batch, _engine.ConditionB, centre + 110, 148, Theme.Red);

        batch.Text(assets.Font("tiny"), Loc.T("fight.hud.fouls", _engine.ConditionA.Fouls,
                _engine.Rules.FoulsBeforeDisqualification),
            new Vector2(centre - 190, 132), Theme.Muted);
        batch.Text(assets.Font("tiny"), Loc.T("fight.hud.fouls", _engine.ConditionB.Fouls,
                _engine.Rules.FoulsBeforeDisqualification),
            new Vector2(centre + 110, 132), Theme.Muted, 1f, TextAlign.Right);

        batch.Text(assets.Font("tiny"), _engine.Rules.DisplayName,
            new Vector2(centre, 176), Theme.Muted, 1f, TextAlign.Centre);

        DrawTurnIndicator(batch);
    }

    /// <summary>
    /// Whose slap is next, and how many of his slaps this round still holds. The
    /// order is fixed for the whole bout - the man who opens round one opens every
    /// round - so this line only ever reads as the alternation within the round.
    /// </summary>
    private void DrawTurnIndicator(SpriteBatch batch)
    {
        if (_engine.Phase == FightPhase.Finished)
        {
            return;
        }

        var yours = _engine.StrikerIsA;
        var left = _engine.SlapsRemainingForCurrentStriker;

        var label = yours
            ? Loc.T("fight.turn.yours")
            : Loc.T("fight.turn.rival", Rival.LastName.ToUpperInvariant());

        // One slap a round needs no count; the rules that ask for two or three do.
        if (_engine.Rules.SlapsPerRound > 1)
        {
            label += left == 1
                ? Loc.T("fight.turn.lastSlap")
                : Loc.T("fight.turn.slapsLeft", left);
        }

        batch.Text(Context.Assets.Font("small"), label, new Vector2(960, 204),
            yours ? Theme.BlueLight : Theme.RedLight, 1f, TextAlign.Centre);
    }

    private void DrawFoulPips(SpriteBatch batch, FighterCondition condition, int x, int y, Color colour)
    {
        var limit = _engine.Rules.FoulsBeforeDisqualification;
        for (var i = 0; i < limit; i++)
        {
            var filled = i < condition.Fouls;
            batch.Fill(x + i * 26, y, 20, 20, filled ? Theme.Red : new Color(40, 50, 74));
            batch.Outline(new Rectangle(x + i * 26, y, 20, 20), new Color(0, 0, 0, 160), 2);
        }

        batch.Text(Context.Assets.Font("tiny"),
            condition.Fouls >= limit ? Loc.T("fight.hud.dq") : "",
            new Vector2(x + limit * 26 + 4, y), Theme.RedLight);
        _ = colour;
    }

    private void DrawFighterBars(SpriteBatch batch, FighterCondition condition, Rectangle rect, Color colour,
        bool mirror)
    {
        var health = rect.Width * Math.Clamp(condition.Health / 100f, 0f, 1f);
        var stamina = rect.Width * Math.Clamp(condition.Stamina / 100f, 0f, 1f);

        batch.Fill(rect, new Color(10, 14, 24));
        batch.Draw(Context.Assets.Tex("bar_health"),
            new Rectangle(mirror ? rect.Right - (int)health : rect.X, rect.Y, (int)health, rect.Height),
            Color.White);

        var staminaRect = new Rectangle(rect.X, rect.Bottom + 6, rect.Width, 8);
        batch.Fill(staminaRect, new Color(10, 14, 24));
        batch.Draw(Context.Assets.Tex("bar_stamina"),
            new Rectangle(mirror ? staminaRect.Right - (int)stamina : staminaRect.X, staminaRect.Y,
                (int)stamina, staminaRect.Height), Color.White);

        batch.Outline(rect, new Color(0, 0, 0, 140), 2);
        _ = colour;
    }

    private void DrawPopups(SpriteBatch batch)
    {
        foreach (var popup in _popups)
        {
            var alpha = Math.Clamp(popup.Life * 1.4f, 0f, 1f);
            var rise = (1.5f - popup.Life) * 40f;
            batch.Text(Context.Assets.Font("large"), popup.Text,
                new Vector2(popup.Position.X, popup.Position.Y - rise), popup.Colour * alpha,
                popup.Scale, TextAlign.Centre);
        }
    }

    /// <summary>
    /// The call itself: a heading and one line on their own backing, so the ritual
    /// reads over the ring instead of through the fighters.
    /// </summary>
    private void DrawCallBanner(SpriteBatch batch, string heading, string detail, Color? colour = null)
    {
        batch.Fill(new Rectangle(400, 580, 1120, 108), new Color(8, 12, 22) * 0.72f);
        batch.Text(Context.Assets.Font("medium"), heading, new Vector2(960, 590),
            colour ?? Theme.White, 1f, TextAlign.Centre);
        batch.Text(Context.Assets.Font("small"), detail, new Vector2(960, 642),
            Theme.Muted, 1f, TextAlign.Centre);
    }

    /// <summary>
    /// The round card: which round, of how many, in what division and under what
    /// rules, with the two names under it. Held up at the start of every round.
    /// </summary>
    private void DrawRoundPlacard(SpriteBatch batch)
    {
        var assets = Context.Assets;
        var t = 1f - Math.Clamp(_beatTimer / RoundPlacardSeconds, 0f, 1f);
        var fade = Gfx.EaseOutCubic(Math.Clamp(t / 0.16f, 0f, 1f));

        batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
            new Color(4, 7, 14) * (0.66f * fade));

        var card = new Rectangle(560, 288 + (int)((1f - fade) * 30f), 800, 464);
        batch.Fill(new Rectangle(card.X + 16, card.Y + 20, card.Width, card.Height), Color.Black * 0.4f);
        batch.NineSlice(assets.Tex("panel"), card, 40, Color.White * fade);
        batch.Outline(card, Theme.Gold * (0.65f * fade), 4);

        var x = card.Center.X;
        batch.Text(assets.Font("small"), Loc.T("fight.placard.announces"),
            new Vector2(x, card.Top + 32), Theme.Muted * fade, 1f, TextAlign.Centre);
        batch.Text(assets.Font("small"), Loc.T("fight.placard.round"),
            new Vector2(x, card.Top + 76), Theme.White * fade, 1.2f, TextAlign.Centre);
        batch.Text(assets.Font("huge"), SpellNumber(_engine.Round).ToUpperInvariant(),
            new Vector2(x, card.Top + 112), Theme.Gold * fade, 1.4f, TextAlign.Centre);

        batch.Fill(card.Left + 220, card.Top + 302, card.Width - 440, 3, Theme.Gold * (0.55f * fade));
        batch.Text(assets.Font("medium"),
            Loc.T("fight.placard.of", SpellNumber(_engine.Rules.Rounds).ToUpperInvariant()),
            new Vector2(x, card.Top + 320), Theme.White * fade, 1f, TextAlign.Centre);
        batch.Text(assets.Font("small"),
            Loc.T("fight.placard.line", WeightClasses.Get(State.PlayerDivision).Name,
                _engine.Rules.DisplayName),
            new Vector2(x, card.Top + 380), Theme.Muted * fade, 1f, TextAlign.Centre);
        batch.Text(assets.Font("small"),
            Loc.T("fight.placard.names", Player.LastName.ToUpperInvariant(),
                Rival.LastName.ToUpperInvariant()),
            new Vector2(x, card.Top + 418), Theme.BlueLight * fade, 1f, TextAlign.Centre);
    }

    private void DrawBeatOverlay(SpriteBatch batch)
    {
        var assets = Context.Assets;

        if (_beat == Beat.RoundPlacard)
        {
            DrawRoundPlacard(batch);
        }
        else if (_beat == Beat.PlayerDeclare)
        {
            DrawCallBanner(batch,
                Loc.T(_handChosen ? "fight.banner.callCount" : "fight.banner.callHand"),
                _handChosen
                    ? Loc.T("fight.banner.countHint", HandName(_leftHand).ToUpperInvariant())
                    : Loc.T("fight.banner.handHint"));
        }
        else if (_beat == Beat.PlayerMeasure)
        {
            var zone = ZoneForOffset(_measureOffset, _leftHand);
            var legal = SlapZones.IsLegal(zone);
            var colour = legal ? Theme.Green : Theme.Red;

            // The read-out sits under the ring rather than over the head: the hand and
            // the cheek are the thing being looked at.
            DrawCallBanner(batch,
                Loc.T("fight.banner.measure", MeasureZoneName(_leftHand, _measureOffset)) +
                (legal ? "" : Loc.T("fight.banner.foulSuffix")),
                Loc.T(_measureOffset < -CheekBand
                    ? "fight.banner.tooHigh"
                    : _measureOffset > CheekBand
                        ? "fight.banner.tooLow"
                        : "fight.banner.onCheek"),
                colour);
        }
        else if (_beat == Beat.PlayerCount)
        {
            var live = _countCalled >= _declaredCount;

            // The count, as big as the screen allows: the number is the count.
            batch.Fill(new Rectangle(790, 250, 340, 210), new Color(8, 12, 22) * 0.55f);
            batch.Text(assets.Font("huge"), CountWord(_countCalled),
                new Vector2(960, 300), live ? Theme.Gold : Theme.White, 1.5f, TextAlign.Centre);

            DrawCallBanner(batch,
                Loc.T("fight.banner.count", HandName(_leftHand).ToUpperInvariant(),
                    CountWord(_declaredCount)),
                Loc.T(live ? "fight.banner.through" : "fight.banner.drawBack"),
                live ? Theme.Gold : Theme.White);
        }
        else if (_beat == Beat.OpponentCharge)
        {
            var remaining = Math.Clamp(_defenceTimer / _defenceWindow, 0f, 1f);
            var measuring = _defenceWindow - _defenceTimer < RivalMeasureSeconds;

            // His call, in writing, before the hand comes: the same ritual the player
            // has to go through on his own turn.
            batch.Text(assets.Font("small"),
                Loc.T("fight.banner.rivalCalls", Rival.DisplayName.ToUpperInvariant(),
                    HandName(_rivalLeftHand).ToUpperInvariant(), CountWord(_rivalCount)),
                new Vector2(960, 596), Theme.Muted, 1f, TextAlign.Centre);

            batch.Fill(new Rectangle(560, 700, 1060, 16), new Color(20, 26, 40));
            batch.Fill(new Rectangle(560, 700, (int)(1060 * remaining), 16), Theme.Amber);

            batch.Text(assets.Font("medium"),
                measuring
                    ? Loc.T("fight.banner.heMeasures")
                    : Loc.T("fight.banner.drawsBack", Rival.LastName.ToUpperInvariant()),
                new Vector2(960, 640), Theme.RedLight, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.Count)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
                new Color(0, 0, 0, 96));
            batch.Fill(new Rectangle(740, 220, 440, 340), new Color(8, 12, 22) * 0.6f);

            batch.Text(assets.Font("tiny"), Loc.T("fight.banner.refereeCount"),
                new Vector2(960, 236), Theme.Muted, 1f, TextAlign.Centre);

            // The number lands on the beat rather than floating up.
            var pop = Math.Clamp(_countTimer / 0.12f, 0f, 1f);
            var colour = _countHold > 0f ? Theme.Red : Theme.Gold;
            batch.Text(assets.Font("huge"), _countValue.ToString(),
                new Vector2(960, 290), colour, 2.6f + (1f - pop) * 0.8f, TextAlign.Centre);

            var down = _downIsPlayer ? Player : Rival;
            batch.Text(assets.Font("medium"),
                Loc.T("fight.banner.isDown", down.LastName.ToUpperInvariant()),
                new Vector2(960, 700), Theme.White, 1f, TextAlign.Centre);

            if (_countHold > 0f)
            {
                var beat = _countResult?.BeatTheCount == true;
                batch.Text(assets.Font("huge"),
                    Loc.T(beat ? "fight.banner.up" : "fight.banner.out"),
                    new Vector2(960, 760), beat ? Theme.Green : Theme.Red, 1.6f, TextAlign.Centre);
                batch.Text(assets.Font("body"),
                    beat
                        ? Loc.T("fight.banner.beatsCount", SpellNumber(_countValue))
                        : Loc.T("fight.banner.wavesOff"),
                    new Vector2(960, 940), beat ? Theme.Green : Theme.RedLight, 1f, TextAlign.Centre);
            }
        }
        else if (_beat == Beat.RoundBreak)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
                new Color(0, 0, 0, 110));
            batch.Text(assets.Font("huge"), Loc.T("fight.banner.endOfRound", _engine.Round),
                new Vector2(960, 420), Theme.Gold, 1f, TextAlign.Centre);
            batch.Text(assets.Font("body"), ScoreLine(),
                new Vector2(960, 560), Theme.White, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.Finished && _engine.Result is { } result)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
                new Color(0, 0, 0, 120));
            batch.Text(assets.Font("huge"),
                result.IsDraw ? Loc.T("outcome.draw") : result.Kind.ShortDisplay(),
                new Vector2(960, 380), Theme.Gold, 1.4f, TextAlign.Centre);
            batch.Text(assets.Font("medium"), result.Headline,
                new Vector2(960, 520), Theme.White, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.Intro)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, 300),
                new Color(0, 0, 0, 120));
            batch.Text(assets.Font("medium"), Loc.T("fight.tape.title"),
                new Vector2(960, 60), Theme.Gold, 1f, TextAlign.Centre);

            batch.Text(assets.Font("body"),
                Loc.T("fight.tape.line", Player.LastName, Player.Record.DisplayWithKo,
                    Context.FormatWeight(Player.WeightPounds)),
                new Vector2(500, 130), Theme.BlueLight, 1f, TextAlign.Centre);
            batch.Text(assets.Font("body"),
                Loc.T("fight.tape.line", Rival.ShortName, Rival.Record.DisplayWithKo,
                    Context.FormatWeight(Rival.WeightPounds)),
                new Vector2(1420, 130), Theme.RedLight, 1f, TextAlign.Centre);

            batch.Text(assets.Font("small"), _engine.RuleSummary,
                new Vector2(960, 200), Theme.Muted, 1f, TextAlign.Centre);
        }
    }
}
