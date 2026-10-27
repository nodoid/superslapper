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
        FightOutcomeKind.Knockout => "KO",
        FightOutcomeKind.TechnicalKnockout => "TKO",
        FightOutcomeKind.Decision => "Unanimous decision",
        FightOutcomeKind.SplitDecision => "Split decision",
        FightOutcomeKind.MajorityDecision => "Majority decision",
        FightOutcomeKind.Draw => "Draw",
        FightOutcomeKind.Disqualification => "Disqualification",
        _ => "No result"
    };

    public static string ShortDisplay(this FightOutcomeKind kind) => kind switch
    {
        FightOutcomeKind.Knockout => "KO",
        FightOutcomeKind.TechnicalKnockout => "TKO",
        FightOutcomeKind.Decision => "UD",
        FightOutcomeKind.SplitDecision => "SD",
        FightOutcomeKind.MajorityDecision => "MD",
        FightOutcomeKind.Draw => "D",
        FightOutcomeKind.Disqualification => "DQ",
        _ => "-"
    };
}
