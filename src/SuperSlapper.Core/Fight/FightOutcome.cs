using SuperSlapper.Core.Localisation;

namespace SuperSlapper.Core.Fight;

public enum FightOutcomeKind
{
    Undecided = 0,
    Knockout = 1,
    TechnicalKnockout = 2,
    Decision = 3,
    SplitDecision = 4,
    MajorityDecision = 5,
    Draw = 6,
    Disqualification = 7
}

public static class FightOutcomeKinds
{
    public static bool IsWin(this FightOutcomeKind kind) => kind is
        FightOutcomeKind.Knockout or FightOutcomeKind.TechnicalKnockout or FightOutcomeKind.Decision
        or FightOutcomeKind.SplitDecision or FightOutcomeKind.MajorityDecision or FightOutcomeKind.Disqualification;

    public static bool IsDraw(this FightOutcomeKind kind) => kind == FightOutcomeKind.Draw;

    public static string Display(this FightOutcomeKind kind) => kind switch
    {
        FightOutcomeKind.Knockout => Loc.T("outcome.ko"),
        FightOutcomeKind.TechnicalKnockout => Loc.T("outcome.tko"),
        FightOutcomeKind.Decision => Loc.T("outcome.decision"),
        FightOutcomeKind.SplitDecision => Loc.T("outcome.splitDecision"),
        FightOutcomeKind.MajorityDecision => Loc.T("outcome.majorityDecision"),
        FightOutcomeKind.Draw => Loc.T("outcome.draw"),
        FightOutcomeKind.Disqualification => Loc.T("outcome.disqualification"),
        _ => Loc.T("outcome.none")
    };

    public static string ShortDisplay(this FightOutcomeKind kind) => kind switch
    {
        FightOutcomeKind.Knockout => Loc.T("outcome.ko.short"),
        FightOutcomeKind.TechnicalKnockout => Loc.T("outcome.tko.short"),
        FightOutcomeKind.Decision => Loc.T("outcome.decision.short"),
        FightOutcomeKind.SplitDecision => Loc.T("outcome.splitDecision.short"),
        FightOutcomeKind.MajorityDecision => Loc.T("outcome.majorityDecision.short"),
        FightOutcomeKind.Draw => Loc.T("outcome.draw.short"),
        FightOutcomeKind.Disqualification => Loc.T("outcome.disqualification.short"),
        _ => Loc.T("outcome.none.short")
    };
}
