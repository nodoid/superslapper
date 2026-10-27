using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Core.Career;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Rules;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>
/// The bout itself. A three-quarter view of two fighters in the ring, a
/// spring-loaded power bar for each of them at the top of the screen, a
/// sweeping aim reticle and a defend prompt. Faithful to the rules engine:
/// every slap, foul, count and scorecard comes out of  <see cref="FightEngine"/>.
/// </summary>
public sealed class FightScreen : Screen
{
    private enum Beat
    {
        Intro = 0,
        PlayerCharge = 1,
        PlayerAim = 2,
        PlayerSwing = 3,
        OpponentCharge = 4,
        OpponentSwing = 5,
        PlayerRecover = 6,
        OpponentRecover = 7,
        Count = 8,
        RoundBreak = 9,
        Finished = 10
    }

    private enum Defence
    {
        Take = 0,
        Brace = 1,
        Flinch = 2,
        Step = 3
    }

    private const float PlayerBaselineY = 1010f;
    private const float PlayerX = 500f;
    private const float RivalX = 1420f;
    private const float FighterScale = 1.55f;

    // The replay strip: 960x512 frames in a 4-wide grid, see tools/artgen/photoreal.py.
    private const int ReplayFrameWidth = 960;
    private const int ReplayFrameHeight = 512;
    private const int ReplayColumns = 4;
    private const float ReplayFrameSeconds = 0.085f;
    private const float ReplayTailSeconds = 0.30f;

    private readonly List<Button> _buttons = new();
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

    private float _charge;
    private float _chargeDirection = 1f;
    private float _lockedCharge;

    private float _aimPosition;
    private float _aimDirection = 1f;
    private float _lockedAim;
    private SlapZone _lockedZone = SlapZone.Jaw;

    private int _playerFrame = FighterSheet.IdleA;
    private int _rivalFrame = FighterSheet.IdleA;
    private float _playerPoseTimer;
    private float _rivalPoseTimer;

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
    private bool _countResolved;
    private bool _applied;
    private bool _koSlowMotion;

    public FightScreen(GameContext context, WeighInResult? weighIn = null) : base(context)
    {
        _weighIn = weighIn;
    }

    private CareerState State => Context.State!;

    private Fighter Player => _engine.FighterA;

    private Fighter Rival => _engine.FighterB;

    private Vector2 PlayerHead => new(PlayerX - 65, 399);

    private Vector2 RivalHead => new(RivalX + 65, 399);

    public override void OnEnter()
    {
        base.OnEnter();
        _buttons.Clear();
        _beat = Beat.Intro;

        if (Context.State is null)
        {
            return;
        }

        _engine = Context.Career.CreateFightEngine(State);
        _playerMeter.Label = Player.DisplayName;
        _rivalMeter.Label = Rival.DisplayName;
        _playerMeter.Snap(0);
        _rivalMeter.Snap(0);
        _commentator.Clear();

        var league = Leagues.Get(State.PlayerLeagueId);
        _commentator.Say($"Ladies and gentlemen, {_engine.Rules.Rounds} rounds for the " +
                         $"{WeightClasses.Get(State.PlayerDivision).Name} division.", 3.4f);

        Context.Assets.Play("applause_wild", 0.8f);
        Context.Assets.Play("bell", 0.5f);

        _beat = Beat.Intro;
        _beatTimer = 2.6f;
        _buttons.Clear();
    }

    public override void Update(GameTime time, InputState input)
    {
        if (MissingCareer(input))
        {
            return;
        }

        var raw = (float)time.ElapsedGameTime.TotalSeconds;

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

        if (_playerPoseTimer <= 0 && _beat != Beat.PlayerSwing)
        {
            _playerFrame = FighterSheet.Idle(_sceneTime);
        }

        if (_rivalPoseTimer <= 0 && _beat != Beat.OpponentSwing)
        {
            _rivalFrame = FighterSheet.Idle(_sceneTime + 0.37f);
        }

        foreach (var button in _buttons.ToArray())
        {
            button.Update(time, input, () => Context.Assets.Play("ui_click", 0.4f));
        }

        switch (_beat)
        {
            case Beat.Intro:
                if (_beatTimer <= 0)
                {
                    BeginTurn();
                }

                break;

            case Beat.PlayerCharge:
                TickPlayerCharge(dt, input);
                break;

            case Beat.PlayerAim:
                TickPlayerAim(dt, input);
                break;

            case Beat.PlayerSwing:
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
            StartPlayerCharge();
        }
        else
        {
            StartOpponentCharge();
        }
    }

    private void StartPlayerCharge()
    {
        _beat = Beat.PlayerCharge;
        _charge = 0f;
        _chargeDirection = 1f;
        _lockedCharge = 0f;
        _playerMeter.Snap(0);
        _playerFrame = FighterSheet.WindupA;
        _playerPoseTimer = 0.4f;
        _commentator.Say("Your slap. Stop the bar in the gold for maximum power.", 2.4f);
    }

    private void TickPlayerCharge(float dt, InputState input)
    {
        // Spring-loaded: the bar runs up, bounces off the top and falls back.
        _charge += dt * 1.25f * _chargeDirection;
        if (_charge >= 1f)
        {
            _charge = 1f;
            _chargeDirection = -1f;
        }
        else if (_charge <= 0f)
        {
            _charge = 0f;
            _chargeDirection = 1f;
        }

        _playerMeter.Target = _charge;

        if (input.LeftPressed || input.Tapped)
        {
            _lockedCharge = _charge;
            _playerMeter.Impulse(5f);
            Context.Assets.Play("ui_click", 0.4f);
            StartPlayerAim();
        }
    }

    private void StartPlayerAim()
    {
        _beat = Beat.PlayerAim;
        _aimPosition = 0f;
        _aimDirection = 1f;
        _playerFrame = FighterSheet.WindupB;
        _playerPoseTimer = 0.6f;
        _commentator.Say("Aim for the cheek or the jaw. A closed fist or the temple is a foul.", 2.6f);
    }

    private void TickPlayerAim(float dt, InputState input)
    {
        _aimPosition += dt * 1.45f * _aimDirection;
        if (_aimPosition >= 1f)
        {
            _aimPosition = 1f;
            _aimDirection = -1f;
        }
        else if (_aimPosition <= 0f)
        {
            _aimPosition = 0f;
            _aimDirection = 1f;
        }

        if (input.LeftPressed || input.Tapped)
        {
            _lockedAim = AimQuality(_aimPosition);
            _lockedZone = ZoneAt(_aimPosition);
            Context.Assets.Play("ui_click", 0.5f);
            StartPlayerSwing();
        }
    }

    private static SlapZone ZoneAt(float position) => position switch
    {
        < 0.14f => SlapZone.Temple,
        < 0.36f => SlapZone.RightCheek,
        < 0.64f => SlapZone.Jaw,
        < 0.87f => SlapZone.LeftCheek,
        _ => SlapZone.Ear
    };

    /// <summary>1.0 dead centre on the jaw, 0.5 on the cheeks, 0.15 at the foul zones.</summary>
    private static float AimQuality(float position)
    {
        var distance = Math.Abs(position - 0.5f) / 0.5f;
        return Math.Clamp(1f - distance * 0.92f, 0.05f, 1f);
    }

    private void StartPlayerSwing()
    {
        _beat = Beat.PlayerSwing;
        _beatTimer = 0.30f;
        _playerFrame = FighterSheet.StrikeA;
        _playerPoseTimer = 0.3f;
        Context.Assets.Play("whiff", 0.25f, 0.3f);
    }

    private void ResolvePlayerSlap()
    {
        var attempt = new SlapAttempt
        {
            PowerCharge = _lockedCharge,
            Aim = _lockedAim,
            Timing = 1f,
            TargetZone = _lockedZone,
            OpenHand = true,
            DefenderBraced = true,
            SteppedForward = false
        };

        var result = _engine.DeliverSlap(attempt, braced: true);
        _playerFrame = FighterSheet.StrikeC;
        _playerPoseTimer = 0.25f;
        ReportSlap(result, byPlayer: true, attempt.TargetZone);
        _beat = Beat.PlayerRecover;
        _beatTimer = 0.95f;
    }

    private void StartOpponentCharge()
    {
        _beat = Beat.OpponentCharge;
        _rivalFrame = FighterSheet.WindupB;
        _rivalPoseTimer = 1.8f;

        _defence = Defence.Take;
        _defenceLocked = false;
        _defenceWindow = 1.9f;
        _defenceTimer = _defenceWindow;

        _buttons.Clear();
        _buttons.Add(new Button(new Rectangle(560, 760, 250, 96), "TAKE IT",
            () => PickDefence(Defence.Take), ButtonStyle.Ghost));
        _buttons.Add(new Button(new Rectangle(830, 760, 250, 96), "BRACE",
            () => PickDefence(Defence.Brace), ButtonStyle.Normal));
        _buttons.Add(new Button(new Rectangle(1100, 760, 250, 96), "FLINCH",
            () => PickDefence(Defence.Flinch), ButtonStyle.Danger));
        _buttons.Add(new Button(new Rectangle(1370, 760, 250, 96), "STEP OFF",
            () => PickDefence(Defence.Step), ButtonStyle.Danger));

        var foulsLeft = _engine.ConditionA.FoulsRemaining(_engine.Rules);
        _commentator.Say(foulsLeft > 0
            ? $"Here comes {Rival.LastName}. Brace, or give away a foul to duck it."
            : "No fouls left - another one ends your night.", 2.8f);
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

        var progress = 1f - Math.Clamp(_defenceTimer / _defenceWindow, 0f, 1f);
        _rivalMeter.Target = Mathf.Lerp(0.25f, 0.95f, Gfx.EaseInOut(progress));

        if (_defenceTimer <= 0f)
        {
            _buttons.Clear();
            StartOpponentSwing();
        }
    }

    private void StartOpponentSwing()
    {
        _beat = Beat.OpponentSwing;
        _beatTimer = 0.26f;
        _rivalFrame = FighterSheet.StrikeA;
        _rivalPoseTimer = 0.26f;
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
        _rivalFrame = FighterSheet.StrikeC;
        _rivalPoseTimer = 0.2f;
        ReportSlap(result, byPlayer: false, attempt.TargetZone);
        _beat = Beat.OpponentRecover;
        _beatTimer = 0.95f;
        _buttons.Clear();
    }

    private void ReportSlap(SlapResult result, bool byPlayer, SlapZone zone)
    {
        _replayZone = zone;

        var defenderX = byPlayer ? RivalX : PlayerX;

        if (result.Verdict == SlapVerdict.Miss)
        {
            Context.Assets.Play("whiff", 0.6f);
            AddPopup("MISS", new Vector2(defenderX, 320), Theme.Muted);
            _commentator.Say(result.Commentary, 2.6f);
            return;
        }

        if (result.Verdict == SlapVerdict.Foul)
        {
            Context.Assets.Play("crowd_boo", 0.75f);
            _shake = 6f;
            AddPopup("FOUL!", new Vector2(defenderX, 300), Theme.Red, 1.3f);
            _commentator.Say(result.Commentary, 3.2f);
            StartReplay(result.Commentary, knockdown: false);
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
            StartReplay(result.Commentary, knockdown: false);
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
            AddPopup("KNOCKDOWN!", new Vector2(defenderX, 220), Theme.Gold, 1.6f);
            StartReplay(result.Commentary, knockdown: true);
            _shake = 22f;
            _targetZoom = 1.34f;
            _focus = new Vector2(defenderX, 420);
            _timeScale = 0.45f;
            _koSlowMotion = true;

            if (byPlayer)
            {
                _rivalFrame = FighterSheet.Ko;
                _rivalPoseTimer = 3f;
            }
            else
            {
                _playerFrame = FighterSheet.Ko;
                _playerPoseTimer = 3f;
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

    private void ContinueAfterSlap()
    {
        _targetZoom = 1f;

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

    private void StartCount()
    {
        _beat = Beat.Count;
        _countValue = 1;
        _countTimer = 0f;
        _countResolved = false;
        _timeScale = 1f;
        _koSlowMotion = false;
        Context.Assets.Play("bell_end", 0.5f);

        var downIsPlayer = !_engine.StrikerIsA;
        _commentator.Say(downIsPlayer
            ? "You are down! Beat the count."
            : $"{Rival.LastName} is down. The referee starts the count.", 3f);
    }

    private void TickCount(float dt)
    {
        _countTimer += dt;
        if (_countTimer >= 0.75f)
        {
            _countTimer = 0f;
            _countValue++;

            if (_countValue > _engine.Rules.CountOutSeconds)
            {
                if (!_countResolved)
                {
                    _countResolved = true;
                    _engine.ResolveCount();
                }

                ContinueAfterSlap();
                return;
            }
        }
    }

    private void StartRoundBreak()
    {
        _beat = Beat.RoundBreak;
        _beatTimer = 1.6f;
        _targetZoom = 1f;

        _commentator.Say($"End of round {_engine.Round}. {ScoreLine()}", 3.4f);
        Context.Assets.Play("bell", 0.6f);
    }

    private string ScoreLine()
    {
        var a = _engine.ConditionA;
        var b = _engine.ConditionB;
        return $"{Player.LastName} has landed {a.LegalSlaps} clean slaps for {a.DamageDealt} damage; " +
               $"{Rival.LastName} {b.LegalSlaps} for {b.DamageDealt}.";
    }

    private void TickRoundBreak(float dt)
    {
        _beatTimer -= dt;
        if (_beatTimer <= 0f && _engine.Phase == FightPhase.RoundBreak)
        {
            _engine.StartNextRound();
            BeginTurn();
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
        _playerFrame = _engine.Result?.WinnerId == Player.Id ? FighterSheet.Victory : FighterSheet.Ko;
        _rivalFrame = _engine.Result?.WinnerId == Rival.Id ? FighterSheet.Victory : FighterSheet.Ko;
        _playerPoseTimer = 5f;
        _rivalPoseTimer = 5f;

        if (_engine.Result is { } result)
        {
            Context.Assets.Play(result.IsDraw ? "crowd_boo" : "applause_wild", 0.85f);
            _commentator.Say(result.Headline + ".", 4f);
        }

        _buttons.Clear();
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

        DrawFighters(batch);

        // The slap table stands in front of both fighters; there are no ropes.
        batch.Draw(Context.Assets.Tex("stage_front"),
            new Rectangle(0, 780, GameSettings.VirtualWidth, 340), Color.White);

        DrawImpact(batch);

        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Context.VirtualTransform);

        DrawHud(batch);
        DrawPopups(batch);

        _commentator.Draw(batch, Context.Assets, new Vector2(620, 1075), 760f, tailLeft: true);

        DrawBeatOverlay(batch);
        DrawReplay(batch);

        foreach (var button in _buttons.ToArray())
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
        batch.Text(Context.Assets.Font("medium"), "REPLAY",
            new Vector2(70, 40), Theme.Gold * alpha, 1f, TextAlign.Left);
        batch.Text(Context.Assets.Font("tiny"), _replayTexture == "replay_ko" ? "KNOCKDOWN" : "SLAP LANDING",
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

    private void DrawFighters(SpriteBatch batch)
    {
        var playerTint = _playerFrame == FighterSheet.Ko ? Color.White : Color.White;
        FighterSheet.Draw(batch, Context.Assets.Tex("fighter_player"), _playerFrame,
            new Vector2(PlayerX, PlayerBaselineY), FighterScale, false, playerTint);
        FighterSheet.Draw(batch, Context.Assets.Tex("fighter_rival"), _rivalFrame,
            new Vector2(RivalX, PlayerBaselineY), FighterScale, true, Color.White);

        foreach (var mark in _slapMarks)
        {
            DrawSlapMark(batch, mark);
        }
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

    private void DrawImpact(SpriteBatch batch)
    {
        if (_shake < 3f)
        {
            return;
        }

        var sheet = Context.Assets.Tex("impact");
        var frame = Math.Clamp((int)((22f - _shake) / 5f), 0, 4);
        var target = _beat == Beat.OpponentSwing ? PlayerHead : RivalHead;
        var scale = 0.55f + _shake * 0.02f;
        Gfx.Frame(batch, sheet, 300, 300, frame, target, Color.White, scale);
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
        batch.Text(assets.Font("medium"), $"ROUND {Math.Min(_engine.Round, _engine.Rules.Rounds)}",
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

        batch.Text(assets.Font("tiny"), $"FOULS {_engine.ConditionA.Fouls}/{_engine.Rules.FoulsBeforeDisqualification}",
            new Vector2(centre - 190, 132), Theme.Muted);
        batch.Text(assets.Font("tiny"), $"FOULS {_engine.ConditionB.Fouls}/{_engine.Rules.FoulsBeforeDisqualification}",
            new Vector2(centre + 110, 132), Theme.Muted, 1f, TextAlign.Right);

        batch.Text(assets.Font("tiny"), _engine.Rules.Name,
            new Vector2(centre, 176), Theme.Muted, 1f, TextAlign.Centre);
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

        batch.Text(Context.Assets.Font("tiny"), condition.Fouls >= limit ? "DQ" : "",
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

    private void DrawBeatOverlay(SpriteBatch batch)
    {
        var assets = Context.Assets;

        if (_beat == Beat.PlayerCharge)
        {
            batch.Text(assets.Font("medium"), "TAP TO STOP THE POWER BAR",
                new Vector2(960, 900), Theme.White, 1f, TextAlign.Centre);
            batch.Text(assets.Font("small"), "Gold zone = maximum pop. The bar is spring-loaded, so time it.",
                new Vector2(960, 950), Theme.Muted, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.PlayerAim)
        {
            batch.Text(assets.Font("medium"), "TAP TO PLACE THE SLAP",
                new Vector2(960, 900), Theme.White, 1f, TextAlign.Centre);

            var zone = ZoneAt(_aimPosition);
            var legal = SlapZones.IsLegal(zone);
            batch.Text(assets.Font("body"), SlapZones.DisplayName(zone).ToUpperInvariant(),
                new Vector2(RivalHead.X, 250), legal ? Theme.Green : Theme.Red, 1.2f, TextAlign.Centre);

            // Reticle.
            var x = Mathf.Lerp(RivalHead.X - 80, RivalHead.X + 80, _aimPosition);
            batch.Draw(assets.Tex("icon_target"), new Vector2(x, RivalHead.Y), null, Color.White, 0f,
                new Vector2(64, 64), 0.5f, SpriteEffects.None, 0f);
        }
        else if (_beat == Beat.OpponentCharge)
        {
            var remaining = Math.Clamp(_defenceTimer / _defenceWindow, 0f, 1f);
            batch.Fill(new Rectangle(560, 700, 1060, 16), new Color(20, 26, 40));
            batch.Fill(new Rectangle(560, 700, (int)(1060 * remaining), 16), Theme.Amber);

            batch.Text(assets.Font("medium"), $"{Rival.LastName.ToUpperInvariant()} IS WINDING UP",
                new Vector2(960, 640), Theme.RedLight, 1f, TextAlign.Centre);
            batch.Text(assets.Font("small"),
                "TAKE IT · BRACE (-25% damage, costs stamina) · FLINCH (-40%, 1 foul) · STEP OFF (no damage, 1 foul)",
                new Vector2(960, 700 + 26), Theme.Muted, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.Count)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
                new Color(0, 0, 0, 90));
            batch.Text(assets.Font("huge"), _countValue.ToString(),
                new Vector2(960, 480), Theme.Gold, 3.2f, TextAlign.Centre);
            batch.Text(assets.Font("medium"), "COUNT",
                new Vector2(960, 300), Theme.White, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.RoundBreak)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
                new Color(0, 0, 0, 110));
            batch.Text(assets.Font("huge"), $"END OF ROUND {_engine.Round}",
                new Vector2(960, 420), Theme.Gold, 1f, TextAlign.Centre);
            batch.Text(assets.Font("body"), ScoreLine(),
                new Vector2(960, 560), Theme.White, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.Finished && _engine.Result is { } result)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, GameSettings.VirtualHeight),
                new Color(0, 0, 0, 120));
            batch.Text(assets.Font("huge"), result.IsDraw ? "DRAW" : result.Kind.ShortDisplay(),
                new Vector2(960, 380), Theme.Gold, 1.4f, TextAlign.Centre);
            batch.Text(assets.Font("medium"), result.Headline,
                new Vector2(960, 520), Theme.White, 1f, TextAlign.Centre);
        }
        else if (_beat == Beat.Intro)
        {
            batch.Fill(new Rectangle(0, 0, GameSettings.VirtualWidth, 300),
                new Color(0, 0, 0, 120));
            batch.Text(assets.Font("medium"), "TALE OF THE TAPE",
                new Vector2(960, 60), Theme.Gold, 1f, TextAlign.Centre);

            batch.Text(assets.Font("body"),
                $"{Player.LastName}  {Player.Record.DisplayWithKo}  {Context.FormatWeight(Player.WeightPounds)}",
                new Vector2(500, 130), Theme.BlueLight, 1f, TextAlign.Centre);
            batch.Text(assets.Font("body"),
                $"{Rival.ShortName}  {Rival.Record.DisplayWithKo}  {Context.FormatWeight(Rival.WeightPounds)}",
                new Vector2(1420, 130), Theme.RedLight, 1f, TextAlign.Centre);

            batch.Text(assets.Font("small"), _engine.RuleSummary,
                new Vector2(960, 200), Theme.Muted, 1f, TextAlign.Centre);
        }
    }
}
