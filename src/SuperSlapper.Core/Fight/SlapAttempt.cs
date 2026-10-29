using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Fight;

/// <summary>
/// One player (or AI) input: how hard, how accurately and where. Aim is the
/// single most important value: it decides whether the open hand lands on the
/// cheek or drifts into a temple, which is the difference between a knockout
/// and a disqualification.
/// </summary>
public sealed class SlapAttempt
{
    /// <summary>0-1 charge. Harder slaps do more damage and cost more stamina.</summary>
    public double PowerCharge { get; set; } = 0.6;

    /// <summary>0-1 placement accuracy as delivered by the swing gesture.</summary>
    public double Aim { get; set; } = 0.6;

    /// <summary>0-1 rhythm. Late or early slaps lose power.</summary>
    public double Timing { get; set; } = 0.8;

    public SlapZone TargetZone { get; set; } = SlapZone.RightCheek;

    /// <summary>Open hand (legal). A closed fist is a foul when the ruleset requires an open hand.</summary>
    public bool OpenHand { get; set; } = true;

    /// <summary>Defender chose to brace, trading stamina for reduced damage.</summary>
    public bool DefenderBraced { get; set; }

    /// <summary>Defender moved their head away: illegal, and it hands the striker a cleaner look.</summary>
    public bool DefenderFlinched { get; set; }

    /// <summary>Defender stepped off the mark: illegal, and the slap cannot land.</summary>
    public bool DefenderSteppedOut { get; set; }

    /// <summary>Striker stepped in with the slap: illegal.</summary>
    public bool SteppedForward { get; set; }

    public static SlapAttempt Basic(SlapZone zone = SlapZone.RightCheek) => new() { TargetZone = zone };
}

/// <summary>The infractions a referee can call.</summary>
public enum FoulKind
{
    IllegalZone = 0,
    ClosedFist = 1,
    Flinching = 2,
    Stepping = 3,
    Delay = 4
}

public static class FoulKinds
{
    public static string Display(this FoulKind kind) => kind switch
    {
        FoulKind.IllegalZone => Loc.T("foul.illegalTarget"),
        FoulKind.ClosedFist => Loc.T("foul.closedFist"),
        FoulKind.Flinching => Loc.T("foul.flinching"),
        FoulKind.Stepping => Loc.T("foul.stepping"),
        FoulKind.Delay => Loc.T("foul.delay"),
        _ => kind.ToString()
    };
}

/// <summary>One foul call, with the penalty that came with it.</summary>
public sealed class FoulCall
{
    public string FighterId { get; set; } = "";
    public string FighterName { get; set; } = "";
    public FoulKind Kind { get; set; }
    public string Description { get; set; } = "";
    public bool PointDeduction { get; set; }
    public bool Disqualified { get; set; }

    public string DisplayText => Loc.T("foul.display", Kind.Display(), Description);
}

public enum SlapVerdict
{
    /// <summary>Clean open-hand slap on a sanctioned zone.</summary>
    Legal = 0,

    /// <summary>Hand never arrived.</summary>
    Miss = 1,

    /// <summary>Landed but on an illegal zone (or with a fist) - referee calls a foul.</summary>
    Foul = 2,

    /// <summary>Referee waved the attempt off (time limit).</summary>
    Forfeit = 3
}

public sealed class SlapResult
{
    public SlapVerdict Verdict { get; set; } = SlapVerdict.Legal;
    public string StrikerId { get; set; } = "";
    public string DefenderId { get; set; } = "";
    public SlapZone Zone { get; set; } = SlapZone.RightCheek;
    public int Damage { get; set; }
    public bool Knockdown { get; set; }
    public bool Knockout { get; set; }
    public bool PointDeduction { get; set; }
    public bool Disqualified { get; set; }
    public int Round { get; set; }
    public string Commentary { get; set; } = "";

    /// <summary>Every foul called on this slap, in the order the referee called them.</summary>
    public List<FoulCall> Fouls { get; set; } = new();

    public bool HasFoul => Fouls.Count > 0;

    /// <summary>0-1 strength of the impact, used for camera shake and hit effects.</summary>
    public double Impact { get; set; }

    public bool IsLegalHit => Verdict == SlapVerdict.Legal && Damage > 0;
}

public sealed class KnockdownResult
{
    public bool BeatTheCount { get; set; }
    public bool Knockout { get; set; }
    public int Count { get; set; }
    public string DefenderId { get; set; } = "";
    public string Commentary { get; set; } = "";
}

public sealed class JudgeScorecard
{
    public string Judge { get; set; } = "";
    public List<(int Striker, int Defender)> Rounds { get; set; } = new();

    public int First
    {
        get { return Rounds.Sum(r => r.Striker); }
    }

    public int Second
    {
        get { return Rounds.Sum(r => r.Defender); }
    }
}

/// <summary>The final word on a bout. This is what career mode consumes.</summary>
public sealed class FightResult
{
    public string FighterAId { get; set; } = "";
    public string FighterBId { get; set; } = "";
    public string WinnerId { get; set; } = "";
    public string LoserId { get; set; } = "";
    public FightOutcomeKind Kind { get; set; } = FightOutcomeKind.Undecided;
    public int EndRound { get; set; } = 1;
    public int EndTimeSeconds { get; set; }
    public bool WasTitleFight { get; set; }
    public List<JudgeScorecard> Scorecards { get; set; } = new();
    public int FighterAKnockdowns { get; set; }
    public int FighterBKnockdowns { get; set; }
    public int FighterAFouls { get; set; }
    public int FighterBFouls { get; set; }
    public List<FoulKind> FighterAFoulLog { get; set; } = new();
    public List<FoulKind> FighterBFoulLog { get; set; } = new();
    public int TotalDamageA { get; set; }
    public int TotalDamageB { get; set; }
    public int Day { get; set; }
    public string LeagueId { get; set; } = "";
    public Rules.WeightClassId Division { get; set; }
    public string Headline { get; set; } = "";

    public bool IsDraw => Kind == FightOutcomeKind.Draw;

    public bool WonBy(string fighterId) => !IsDraw && WinnerId == fighterId;

    public string Summary => IsDraw
        ? Loc.T(EndRound == 1 ? "result.drawAfterOne" : "result.drawAfterMany", EndRound)
        : Loc.T("result.summary", Kind.Display(), EndRound);

    public string ScorecardText => Scorecards.Count == 0
        ? string.Empty
        : string.Join(" | ", Scorecards.Select(c => $"{c.First}-{c.Second}"));
}
