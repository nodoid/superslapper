using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Random;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Career;

public enum BoutEventKind
{
    RoundStart,
    Slap,
    Knockdown,
    RoundEnd
}

/// <summary>One beat of a recorded bout: enough to redraw and call the action.</summary>
public sealed class BoutEvent
{
    public BoutEventKind Kind { get; set; }
    public int Round { get; set; }

    /// <summary>True when fighter A was the striker.</summary>
    public bool ByA { get; set; }

    public SlapZone Zone { get; set; }
    public int Damage { get; set; }
    public bool Knockdown { get; set; }
    public bool Knockout { get; set; }
    public string Commentary { get; set; } = "";
    public double Impact { get; set; }

    /// <summary>Condition after the beat, so the bars can be driven from the log.</summary>
    public int HealthA { get; set; } = 100;
    public int HealthB { get; set; } = 100;
    public int StaminaA { get; set; } = 100;
    public int StaminaB { get; set; } = 100;

    /// <summary>Only used by knockdown beats.</summary>
    public int Count { get; set; }
    public bool BeatTheCount { get; set; }

    /// <summary>How long the beat should hold if a speed multiplier is applied.</summary>
    public float Seconds => Kind switch
    {
        BoutEventKind.RoundStart => 1.5f,
        BoutEventKind.RoundEnd => 1.4f,
        BoutEventKind.Knockdown => Knockout ? 3.2f : 2.4f,
        _ => Knockdown ? 2.0f : 1.05f
    };
}

/// <summary>
/// A bout played out by the engine, kept as a script. Every slap, count and
/// round break is stored with the condition after it, so a spectator screen can
/// replay the bout exactly as it was decided without re-running the simulation.
/// </summary>
public sealed class BoutReplay
{
    public string FighterAId { get; set; } = "";
    public string FighterBId { get; set; } = "";
    public string FighterAName { get; set; } = "";
    public string FighterBName { get; set; } = "";
    public string LeagueId { get; set; } = "";
    public WeightClassId Division { get; set; }
    public bool TitleFight { get; set; }
    public int Day { get; set; }
    public List<BoutEvent> Events { get; set; } = new();
    public FightResult Result { get; set; } = new();

    public bool WonByA => !Result.IsDraw && Result.WinnerId == FighterAId;

    /// <summary>Runs the bout with both fighters on AI and records every beat.</summary>
    public static BoutReplay Record(Fighter a, Fighter b, Ruleset rules, IRandomSource rng,
        bool titleFight, string leagueId, int day, double skillA, double skillB)
    {
        var engine = new FightEngine(a.Clone(), b.Clone(), rules, rng, titleFight, leagueId, day);
        var replay = new BoutReplay
        {
            FighterAId = a.Id,
            FighterBId = b.Id,
            FighterAName = a.DisplayName,
            FighterBName = b.DisplayName,
            LeagueId = leagueId,
            Division = a.Division,
            TitleFight = titleFight,
            Day = day
        };

        replay.Add(engine, BoutEventKind.RoundStart);

        var guard = 0;
        while (engine.Phase != FightPhase.Finished && guard++ < 200)
        {
            switch (engine.Phase)
            {
                case FightPhase.AwaitingSlap:
                {
                    var skill = engine.StrikerIsA ? skillA : skillB;
                    var attempt = FightEngine.GenerateAiAttempt(engine.Striker, skill, rng);
                    attempt.DefenderBraced = rng.NextDouble() < 0.35;
                    var slap = engine.DeliverSlap(attempt);

                    var ev = replay.Add(engine, BoutEventKind.Slap);
                    ev.ByA = slap.StrikerId == replay.FighterAId;
                    ev.Zone = slap.Zone;
                    ev.Damage = slap.Damage;
                    ev.Knockdown = slap.Knockdown;
                    ev.Knockout = slap.Knockout;
                    ev.Commentary = slap.Commentary;
                    ev.Impact = slap.Impact;
                    break;
                }

                case FightPhase.AwaitingCount:
                {
                    var count = engine.ResolveCount();
                    var ev = replay.Add(engine, BoutEventKind.Knockdown);
                    ev.ByA = count.DefenderId == replay.FighterBId;
                    ev.Count = count.Count;
                    ev.BeatTheCount = count.BeatTheCount;
                    ev.Knockout = count.Knockout;
                    ev.Commentary = count.Commentary;
                    break;
                }

                case FightPhase.RoundBreak:
                    replay.Add(engine, BoutEventKind.RoundEnd);
                    engine.StartNextRound();
                    replay.Add(engine, BoutEventKind.RoundStart);
                    break;
            }
        }

        if (engine.Phase != FightPhase.Finished || engine.Result is null)
        {
            throw new InvalidOperationException("Bout failed to reach a result.");
        }

        replay.Result = engine.Result;
        return replay;
    }

    private BoutEvent Add(FightEngine engine, BoutEventKind kind)
    {
        var ev = new BoutEvent
        {
            Kind = kind,
            Round = engine.Round,
            HealthA = engine.ConditionA.Health,
            HealthB = engine.ConditionB.Health,
            StaminaA = engine.ConditionA.Stamina,
            StaminaB = engine.ConditionB.Stamina
        };
        Events.Add(ev);
        return ev;
    }
}
