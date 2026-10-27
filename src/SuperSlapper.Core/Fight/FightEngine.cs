using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Fight;

public enum FightPhase
{
    NotStarted = 0,

    /// <summary>Waiting for the active striker to deliver a slap.</summary>
    AwaitingSlap = 1,

    /// <summary>A knockdown has happened; waiting for the count to be beaten.</summary>
    AwaitingCount = 2,

    RoundBreak = 3,
    Finished = 4
}

/// <summary>Live per-fighter condition inside a bout.</summary>
public sealed class FighterCondition
{
    public string FighterId { get; set; } = "";
    public int Health { get; set; } = 100;
    public int Stamina { get; set; } = 100;
    public int KnockdownsThisRound { get; set; }
    public int TotalKnockdowns { get; set; }
    public int DamageDealt { get; set; }

    /// <summary>Total fouls called against this fighter.</summary>
    public int Fouls { get; set; }

    public int PointDeductions { get; set; }
    public bool Disqualified { get; set; }
    public int SlapsThrown { get; set; }
    public int LegalSlaps { get; set; }
    public List<FoulKind> FoulLog { get; set; } = new();

    public bool IsDown => KnockdownsThisRound > 0;

    /// <summary>Fouls left before this fighter is thrown out.</summary>
    public int FoulsRemaining(Ruleset rules) => Math.Max(0, rules.FoulsBeforeDisqualification - Fouls);
}

/// <summary>
/// The slap fight state machine. It owns the rulebook: round structure,
/// order of strikers, foul policy, the count, knockdowns and the judges.
/// The game screens drive it one slap at a time; tests drive it directly.
/// </summary>
public sealed class FightEngine
{
    private readonly List<(int A, int B)> _roundDamage = new();
    private readonly List<(int A, int B)> _perRoundPointDeductions = new();

    public FightEngine(Fighter fighterA, Fighter fighterB, Ruleset rules, IRandomSource rng,
        bool titleFight = false, string leagueId = "", int day = 0)
    {
        FighterA = fighterA;
        FighterB = fighterB;
        Rules = rules;
        Rng = rng;
        TitleFight = titleFight;
        LeagueId = leagueId;
        Day = day;

        var errors = rules.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Illegal ruleset: " + string.Join("; ", errors));
        }

        ConditionA = new FighterCondition { FighterId = fighterA.Id };
        ConditionB = new FighterCondition { FighterId = fighterB.Id };

        _strikerIsA = rng.NextDouble() < 0.5;
        _firstStrikerWasA = _strikerIsA;
        Round = 1;
        Phase = FightPhase.AwaitingSlap;
    }

    public Fighter FighterA { get; }
    public Fighter FighterB { get; }
    public Ruleset Rules { get; }
    public IRandomSource Rng { get; }
    public bool TitleFight { get; }
    public string LeagueId { get; }
    public int Day { get; }

    public FighterCondition ConditionA { get; }
    public FighterCondition ConditionB { get; }

    public int Round { get; private set; }
    public FightPhase Phase { get; private set; }
    public FightResult? Result { get; private set; }

    public int SlapsThisRoundA { get; private set; }
    public int SlapsThisRoundB { get; private set; }

    private bool _strikerIsA;
    private bool _firstStrikerWasA;

    public bool StrikerIsA => _strikerIsA;

    public Fighter Striker => _strikerIsA ? FighterA : FighterB;

    public Fighter Defender => _strikerIsA ? FighterB : FighterA;

    public FighterCondition StrikerCondition => _strikerIsA ? ConditionA : ConditionB;

    public FighterCondition DefenderCondition => _strikerIsA ? ConditionB : ConditionA;

    public FighterCondition ConditionOf(string fighterId) =>
        fighterId == FighterA.Id ? ConditionA : ConditionB;

    public int SlapsRemainingForCurrentStriker =>
        Math.Max(0, Rules.SlapsPerRound - (_strikerIsA ? SlapsThisRoundA : SlapsThisRoundB));

    public string RuleSummary =>
        $"{Rules.Rounds} rounds x {Rules.SlapsPerRound} slap{(Rules.SlapsPerRound == 1 ? "" : "s")} · " +
        $"{Rules.FoulsBeforeDisqualification} fouls = DQ · {Rules.CountOutSeconds}-count";

    /// <summary>
    /// Resolves the active striker's slap. Everything the referee could call -
    /// an illegal zone, a closed fist, a flinch, a step off the mark - is
    /// decided here and written into the result so the commentary can report it.
    /// </summary>
    public SlapResult DeliverSlap(SlapAttempt attempt, bool? braced = null)
    {
        if (Phase != FightPhase.AwaitingSlap)
        {
            throw new InvalidOperationException($"Cannot slap during phase {Phase}.");
        }

        var strikerIsA = _strikerIsA;
        var striker = Striker;
        var defender = Defender;
        var strikerCondition = StrikerCondition;
        var defenderCondition = DefenderCondition;

        var strikerAttrs = striker.EffectiveAttributes();
        var defenderAttrs = defender.EffectiveAttributes();

        var result = new SlapResult
        {
            StrikerId = striker.Id,
            DefenderId = defender.Id,
            Zone = attempt.TargetZone,
            Round = Round
        };

        var flinched = attempt.DefenderFlinched && Rules.FlinchingIsFoul;
        var steppedOut = attempt.DefenderSteppedOut && Rules.SteppingIsFoul;
        var steppedIn = attempt.SteppedForward && Rules.SteppingIsFoul;

        var defenderBrace = braced ?? attempt.DefenderBraced;
        var energy = 0.70 + 0.30 * strikerCondition.Stamina / 100.0;
        var conditionsFactor = 0.70 + 0.30 * strikerCondition.Health / 100.0;
        var rhythm = 0.55 + 0.45 * Math.Clamp(attempt.Timing, 0, 1);

        var rawAim = 0.45 * Math.Clamp(attempt.Aim, 0, 1) + 0.55 * strikerAttrs.Aim / 100.0;
        var aimScore = Math.Clamp(rawAim - defenderAttrs.Reflex / 100.0 * 0.22, 0.02, 1.0);

        // A defender who steps off the mark takes themselves out of range: the
        // slap cannot land and they are penalised for it.
        if (steppedOut)
        {
            result.Verdict = SlapVerdict.Miss;
            result.Fouls.Add(RegisterFoul(!strikerIsA, FoulKind.Stepping,
                $"{defender.LastName} stepped off the mark", result));
            result.Commentary = $"{defender.LastName} steps away - foul, and the slap never lands.";
            strikerCondition.Stamina = Math.Max(0, strikerCondition.Stamina - 5);
            FinishTurn(strikerIsA, result, 0);
            return result;
        }

        var roll = Rng.NextDouble();
        var aimWithFlinch = flinched ? Math.Min(1.0, aimScore + 0.25) : aimScore;

        if (roll > 0.35 + 0.65 * aimWithFlinch)
        {
            result.Verdict = SlapVerdict.Miss;
            result.Commentary = $"{striker.LastName} swings and misses. {defender.LastName} smiles.";
            strikerCondition.Stamina = Math.Max(0, strikerCondition.Stamina - (int)(6 + 8 * attempt.PowerCharge));
            if (steppedIn)
            {
                result.Fouls.Add(RegisterFoul(strikerIsA, FoulKind.Stepping,
                    $"{striker.LastName} stepped in as they slapped", result));
            }

            FinishTurn(strikerIsA, result, 0);
            return result;
        }

        SlapZone zone;
        if (roll > aimWithFlinch)
        {
            zone = DriftZone(attempt.TargetZone, aimWithFlinch);
        }
        else
        {
            zone = SlapZones.IsLegal(attempt.TargetZone)
                ? attempt.TargetZone
                : DriftZone(attempt.TargetZone, aimWithFlinch);
        }

        result.Zone = zone;

        var illegalZone = Rules.IsIllegalZone(zone) || !SlapZones.IsLegal(zone);
        var illegalTechnique = Rules.OpenHandRequired && !attempt.OpenHand;

        if (illegalZone || illegalTechnique)
        {
            result.Verdict = SlapVerdict.Foul;
            var kind = illegalTechnique ? FoulKind.ClosedFist : FoulKind.IllegalZone;
            var what = illegalTechnique
                ? "a closed fist"
                : SlapZones.FoulDescription(zone).ToLowerInvariant();

            var foulDamage = (int)Math.Round(4 + 8 * attempt.PowerCharge);
            defenderCondition.Health = Math.Max(0, defenderCondition.Health - foulDamage);
            result.Damage = foulDamage;
            result.Impact = 0.25;

            result.Fouls.Add(RegisterFoul(strikerIsA, kind, what, result));

            if (flinched)
            {
                result.Fouls.Add(RegisterFoul(!strikerIsA, FoulKind.Flinching,
                    $"{defender.LastName} flinched", result));
            }

            FinishTurn(strikerIsA, result, foulDamage);
            return result;
        }

        // Clean slap.
        var charge = 0.60 + 0.40 * Math.Clamp(attempt.PowerCharge, 0, 1);
        var rehydration = 0.80m + Math.Min(0.28m,
            Math.Max(0m, striker.WeightPounds - defender.WeightPounds) * 0.002m);
        var power = strikerAttrs.Power * charge * energy * conditionsFactor * rhythm * (double)rehydration;

        if (flinched)
        {
            power *= 1.2;
        }

        var zoneFactor = zone switch
        {
            SlapZone.Jaw => 1.12,
            SlapZone.LeftCheek or SlapZone.RightCheek => 1.0,
            SlapZone.Chin => 0.95,
            _ => 0.9
        };

        var chinFactor = 1.0 - defenderAttrs.Chin / 250.0;
        var braceFactor = defenderBrace ? 0.72 : 1.0;

        var damage = power * 0.42 * (0.85 + 0.30 * aimWithFlinch) * zoneFactor * chinFactor * braceFactor;
        damage *= 1.0 + defender.Damage / 400.0;

        var applied = Math.Max(1, (int)Math.Round(damage, MidpointRounding.AwayFromZero));
        defenderCondition.Health = Math.Max(0, defenderCondition.Health - applied);
        defender.Damage = Math.Min(100, defender.Damage + Math.Max(1, applied / 3));

        strikerCondition.Stamina = Math.Max(0, strikerCondition.Stamina - (int)(6 + 10 * attempt.PowerCharge));
        defenderCondition.Stamina = Math.Max(0,
            defenderCondition.Stamina - (defenderBrace ? 12 : 4) - applied / 8);

        result.Verdict = SlapVerdict.Legal;
        result.Damage = applied;
        result.Impact = Math.Clamp(applied / 40.0, 0.12, 1.0);

        var knockdownThreshold = (double)Rules.KnockdownDamageThreshold * (1 + defenderAttrs.Chin / 200.0)
                                  * (defenderBrace ? 1.25 : 1.0);

        var knockdownChance = Math.Pow(Math.Clamp(applied / knockdownThreshold, 0, 3), 3) * 0.55;
        var knockedDown = applied >= knockdownThreshold * 1.35 || Rng.NextDouble() < knockdownChance;

        if (defenderCondition.Health <= 0)
        {
            knockedDown = true;
        }

        if (knockedDown)
        {
            result.Knockdown = true;
            defenderCondition.KnockdownsThisRound++;
            defenderCondition.TotalKnockdowns++;
        }

        result.Commentary = BuildCommentary(striker, defender, zone, applied, knockedDown);

        if (defenderCondition.Health <= 0)
        {
            result.Knockout = true;
        }

        if (flinched)
        {
            result.Fouls.Add(RegisterFoul(!strikerIsA, FoulKind.Flinching,
                $"{defender.LastName} flinched away from the slap", result));
        }

        if (steppedIn)
        {
            result.Fouls.Add(RegisterFoul(strikerIsA, FoulKind.Stepping,
                $"{striker.LastName} stepped in with the slap", result));
        }

        FinishTurn(strikerIsA, result, applied);
        return result;
    }

    private string BuildCommentary(Fighter striker, Fighter defender, SlapZone zone, int damage, bool knockdown)
    {
        if (knockdown)
        {
            return damage >= 40
                ? $"Brutal open hand from {striker.LastName}! {defender.LastName} is down!"
                : $"{striker.LastName} lands flush on the {SlapZones.DisplayName(zone).ToLowerInvariant()} and {defender.LastName} drops!";
        }

        return damage switch
        {
            >= 32 => $"{striker.LastName} detonates a slap. {defender.LastName} wobbles!",
            >= 20 => $"Clean slap lands on the {SlapZones.DisplayName(zone).ToLowerInvariant()}.",
            >= 10 => $"{striker.LastName} scores with a short open hand.",
            _ => $"Glancing slap from {striker.LastName}."
        };
    }

    private SlapZone DriftZone(SlapZone intended, double aimScore)
    {
        var danger = 1.0 - aimScore;
        if (danger < 0.25)
        {
            return intended;
        }

        var options = intended switch
        {
            SlapZone.Jaw => new[] { SlapZone.Chin, SlapZone.Throat, SlapZone.Neck },
            SlapZone.LeftCheek => new[] { SlapZone.Eye, SlapZone.Temple, SlapZone.Nose },
            SlapZone.RightCheek => new[] { SlapZone.Ear, SlapZone.Temple, SlapZone.Nose },
            _ => new[] { SlapZone.Temple, SlapZone.Ear, SlapZone.Eye }
        };

        return options[Rng.Next(0, options.Length)];
    }

    /// <summary>
    /// Records a foul: every foul costs a point on the judges' cards, and the
    /// fighter is disqualified once they reach the rulebook's foul limit.
    /// </summary>
    private FoulCall RegisterFoul(bool isA, FoulKind kind, string description, SlapResult result)
    {
        var condition = isA ? ConditionA : ConditionB;
        var fighter = isA ? FighterA : FighterB;

        condition.Fouls++;
        condition.FoulLog.Add(kind);
        condition.PointDeductions++;
        AddPointDeduction(isA, 1);

        var call = new FoulCall
        {
            FighterId = fighter.Id,
            FighterName = fighter.DisplayName,
            Kind = kind,
            Description = description,
            PointDeduction = true
        };

        if (condition.Fouls >= Rules.FoulsBeforeDisqualification)
        {
            condition.Disqualified = true;
            call.Disqualified = true;
            result.Disqualified = true;
            result.Commentary = $"{fighter.LastName} has committed {condition.Fouls} fouls. Disqualified!";
        }
        else
        {
            result.Commentary = $"Foul against {fighter.LastName}: {description}. Point deducted.";
        }

        return call;
    }

    private void AddPointDeduction(bool isA, int points)
    {
        while (_perRoundPointDeductions.Count <= Round - 1)
        {
            _perRoundPointDeductions.Add((0, 0));
        }

        var current = _perRoundPointDeductions[Round - 1];
        _perRoundPointDeductions[Round - 1] = isA
            ? (current.A + points, current.B)
            : (current.A, current.B + points);
    }

    private void FinishTurn(bool strikerIsA, SlapResult result, int damage)
    {
        var strikerCondition = strikerIsA ? ConditionA : ConditionB;
        var defenderCondition = strikerIsA ? ConditionB : ConditionA;

        strikerCondition.SlapsThrown++;
        if (result.Verdict == SlapVerdict.Legal)
        {
            strikerCondition.LegalSlaps++;
        }

        strikerCondition.DamageDealt += damage;

        if (strikerIsA)
        {
            SlapsThisRoundA++;
        }
        else
        {
            SlapsThisRoundB++;
        }

        if (ConditionA.Disqualified)
        {
            FinishByDisqualification(strikerIsA: true);
            return;
        }

        if (ConditionB.Disqualified)
        {
            FinishByDisqualification(strikerIsA: false);
            return;
        }

        if (result.Knockdown || defenderCondition.Health <= 0)
        {
            result.Knockdown = true;
            result.Knockout = defenderCondition.Health <= 0;
            Phase = FightPhase.AwaitingCount;
            return;
        }

        AdvanceAfterSlap();
    }

    /// <summary>
    /// Called after a knockdown. The knocked-down fighter must beat the count.
    /// </summary>
    public KnockdownResult ResolveCount()
    {
        if (Phase != FightPhase.AwaitingCount)
        {
            throw new InvalidOperationException("No count is in progress.");
        }

        var downIsA = !_strikerIsA;
        var down = downIsA ? FighterA : FighterB;
        var downCondition = downIsA ? ConditionA : ConditionB;
        var attrs = down.EffectiveAttributes();

        var chance = 0.46
                     + 0.30 * downCondition.Health / 100.0
                     + 0.18 * attrs.Chin / 100.0
                     - Math.Min(0.35, downCondition.TotalKnockdowns * 0.10)
                     - down.Damage / 400.0;

        var beat = Rng.NextDouble() < Math.Clamp(chance, 0.05, 0.97);
        var count = beat ? Rng.Next(4, 9) : Rules.CountOutSeconds;

        var outcome = new KnockdownResult
        {
            BeatTheCount = beat,
            Count = count,
            DefenderId = down.Id,
            Knockout = !beat
        };

        if (!beat)
        {
            outcome.Commentary = $"{down.LastName} cannot beat the count. It is all over!";
            FinishByKnockout(downIsA, count);
            return outcome;
        }

        downCondition.Health = Math.Min(100, downCondition.Health + 18);

        if (downCondition.KnockdownsThisRound >= Rules.MaxKnockdownsPerRound)
        {
            outcome.Knockout = true;
            outcome.Commentary =
                $"That is {downCondition.KnockdownsThisRound} knockdowns. The referee waves it off - technical knockout.";
            FinishByTechnicalKnockout(downIsA);
            return outcome;
        }

        outcome.Commentary = $"{down.LastName} is up at {count}. The referee is satisfied.";
        Phase = FightPhase.AwaitingSlap;
        AdvanceAfterSlap();
        return outcome;
    }

    private void AdvanceAfterSlap()
    {
        Phase = FightPhase.AwaitingSlap;

        var roundComplete = SlapsThisRoundA >= Rules.SlapsPerRound && SlapsThisRoundB >= Rules.SlapsPerRound;
        if (roundComplete)
        {
            EndRound();
            return;
        }

        if (_strikerIsA && SlapsThisRoundA >= Rules.SlapsPerRound)
        {
            _strikerIsA = false;
            return;
        }

        if (!_strikerIsA && SlapsThisRoundB >= Rules.SlapsPerRound)
        {
            _strikerIsA = true;
            return;
        }

        _strikerIsA = !_strikerIsA;
    }

    private void EndRound()
    {
        _roundDamage.Add((ConditionA.DamageDealt, ConditionB.DamageDealt));

        if (Round >= Rules.Rounds)
        {
            FinishByDecision();
            return;
        }

        Round++;
        SlapsThisRoundA = 0;
        SlapsThisRoundB = 0;
        ConditionA.KnockdownsThisRound = 0;
        ConditionB.KnockdownsThisRound = 0;

        ConditionA.Health = Math.Min(100, ConditionA.Health + 14);
        ConditionB.Health = Math.Min(100, ConditionB.Health + 14);
        ConditionA.Stamina = Math.Min(100, ConditionA.Stamina + 20);
        ConditionB.Stamina = Math.Min(100, ConditionB.Stamina + 20);

        _strikerIsA = !_firstStrikerWasA;
        _firstStrikerWasA = _strikerIsA;
        Phase = FightPhase.RoundBreak;
    }

    /// <summary>Called by the UI once the between-rounds break has been acknowledged.</summary>
    public void StartNextRound()
    {
        if (Phase == FightPhase.RoundBreak)
        {
            Phase = FightPhase.AwaitingSlap;
        }
    }

    private void FinishByDisqualification(bool strikerIsA)
    {
        var loser = strikerIsA ? FighterA : FighterB;
        var winner = strikerIsA ? FighterB : FighterA;
        Complete(winner, loser, FightOutcomeKind.Disqualification, 0);
    }

    private void FinishByKnockout(bool downIsA, int count)
    {
        var loser = downIsA ? FighterA : FighterB;
        var winner = downIsA ? FighterB : FighterA;
        Complete(winner, loser, FightOutcomeKind.Knockout, count);
    }

    private void FinishByTechnicalKnockout(bool downIsA)
    {
        var loser = downIsA ? FighterA : FighterB;
        var winner = downIsA ? FighterB : FighterA;
        Complete(winner, loser, FightOutcomeKind.TechnicalKnockout, 0);
    }

    private void FinishByDecision()
    {
        var cards = BuildScorecards();
        var aWins = cards.Count(c => c.First > c.Second);
        var bWins = cards.Count(c => c.Second > c.First);
        var draws = cards.Count(c => c.First == c.Second);

        FightOutcomeKind kind;
        Fighter? winner;
        Fighter? loser;

        if (aWins > bWins)
        {
            winner = FighterA;
            loser = FighterB;
            kind = aWins == cards.Count ? FightOutcomeKind.Decision
                : draws > 0 ? FightOutcomeKind.MajorityDecision
                : FightOutcomeKind.SplitDecision;
        }
        else if (bWins > aWins)
        {
            winner = FighterB;
            loser = FighterA;
            kind = bWins == cards.Count ? FightOutcomeKind.Decision
                : draws > 0 ? FightOutcomeKind.MajorityDecision
                : FightOutcomeKind.SplitDecision;
        }
        else
        {
            Complete(null, null, FightOutcomeKind.Draw, 0, cards);
            return;
        }

        Complete(winner, loser, kind, 0, cards);
    }

    private List<JudgeScorecard> BuildScorecards()
    {
        var cards = new List<JudgeScorecard>();
        var judgeNames = JudgeNames.Take(Rules.JudgesCount).ToList();

        for (var j = 0; j < Rules.JudgesCount; j++)
        {
            var card = new JudgeScorecard { Judge = judgeNames[j % judgeNames.Count] };
            for (var r = 0; r < _roundDamage.Count; r++)
            {
                var (damageA, damageB) = _roundDamage[r];
                var (deductionA, deductionB) = r < _perRoundPointDeductions.Count
                    ? _perRoundPointDeductions[r]
                    : (0, 0);

                var bias = 0.92 + Rng.NextDouble() * 0.16;
                var scoreA = damageA * bias;
                var scoreB = damageB * (1.0 / bias);

                int first;
                int second;
                if (Math.Abs(scoreA - scoreB) < 1.5)
                {
                    first = 10;
                    second = 10;
                }
                else if (scoreA > scoreB)
                {
                    first = 10;
                    second = 9;
                }
                else
                {
                    first = 9;
                    second = 10;
                }

                first = Math.Max(7, first - deductionA);
                second = Math.Max(7, second - deductionB);
                card.Rounds.Add((first, second));
            }

            cards.Add(card);
        }

        return cards;
    }

    private void Complete(Fighter? winner, Fighter? loser, FightOutcomeKind kind, int timeSeconds,
        List<JudgeScorecard>? cards = null)
    {
        Phase = FightPhase.Finished;
        Result = new FightResult
        {
            FighterAId = FighterA.Id,
            FighterBId = FighterB.Id,
            WinnerId = winner?.Id ?? "",
            LoserId = loser?.Id ?? "",
            Kind = kind,
            EndRound = Round,
            EndTimeSeconds = timeSeconds,
            WasTitleFight = TitleFight,
            Scorecards = cards ?? BuildScorecardsIfFinished(),
            FighterAKnockdowns = ConditionA.TotalKnockdowns,
            FighterBKnockdowns = ConditionB.TotalKnockdowns,
            FighterAFouls = ConditionA.Fouls,
            FighterBFouls = ConditionB.Fouls,
            TotalDamageA = ConditionA.DamageDealt,
            TotalDamageB = ConditionB.DamageDealt,
            Day = Day,
            LeagueId = LeagueId,
            Division = FighterA.Division,
            FighterAFoulLog = ConditionA.FoulLog.ToList(),
            FighterBFoulLog = ConditionB.FoulLog.ToList(),
            Headline = winner is null
                ? $"{FighterA.DisplayName} and {FighterB.DisplayName} fight to a draw"
                : $"{winner.DisplayName} beats {loser!.DisplayName} by {kind.Display()}"
        };
    }

    private List<JudgeScorecard> BuildScorecardsIfFinished()
    {
        return _roundDamage.Count == 0 ? new List<JudgeScorecard>() : BuildScorecards();
    }

    private static readonly string[] JudgeNames =
    {
        "Sal DiMaggio", "Kazuo Mori", "Derek Boateng", "Anne-Marie Rousseau",
        "Bill Hutchins", "Rosa Camacho"
    };

    /// <summary>
    /// Generates a CPU slap. Skill 0-1 controls both accuracy and how close to
    /// maximum charge the AI dares to go. Better fighters foul less often.
    /// </summary>
    public static SlapAttempt GenerateAiAttempt(Fighter striker, double skill, IRandomSource rng,
        SlapZone zone = SlapZone.RightCheek)
    {
        var aimBase = striker.Attributes.Aim / 100.0;
        var accuracy = Math.Clamp(aimBase * 0.7 + skill * 0.3, 0, 1);
        var jitter = (rng.NextDouble() - 0.5) * 0.3;
        var charge = Math.Clamp(0.45 + skill * 0.5 + (rng.NextDouble() - 0.5) * 0.3, 0.15, 1.0);
        var timing = Math.Clamp(0.55 + skill * 0.4 + (rng.NextDouble() - 0.5) * 0.25, 0.1, 1.0);

        return new SlapAttempt
        {
            Aim = Math.Clamp(accuracy + jitter, 0.05, 1.0),
            PowerCharge = charge,
            Timing = timing,
            TargetZone = zone,
            OpenHand = rng.NextDouble() > 0.03,
            DefenderBraced = rng.NextDouble() < 0.35,
            DefenderFlinched = rng.NextDouble() < 0.05 * (1 - skill),
            SteppedForward = rng.NextDouble() < 0.03 * (1 - skill)
        };
    }
}
