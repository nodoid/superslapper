using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;

namespace SuperSlapper.Core.Career;

/// <summary>Where a bout sits on the card.</summary>
public enum CardSlot
{
    Undercard = 0,
    MainCard = 1,
    CoMain = 2,
    MainEvent = 3
}

/// <summary>One bout on a tournament card.</summary>
public sealed class CardBout
{
    public string FighterAId { get; set; } = "";
    public string FighterBId { get; set; } = "";
    public string FighterAName { get; set; } = "";
    public string FighterBName { get; set; } = "";
    public bool TitleFight { get; set; }
    public bool IsPlayerBout { get; set; }
    public CardSlot Slot { get; set; } = CardSlot.Undercard;

    /// <summary>Set once the bout has happened, whether watched or simulated.</summary>
    public FightResult? Result { get; set; }

    /// <summary>
    /// The recorded bout, present for everything except the player's own fight:
    /// that one is played live, so only its result is stored.
    /// </summary>
    public BoutReplay? Replay { get; set; }

    /// <summary>True once the player has actually sat through the replay.</summary>
    public bool Watched { get; set; }

    public bool IsDecided => Result is not null;

    public string SlotName => Slot switch
    {
        CardSlot.MainEvent => Loc.T("card.mainEvent"),
        CardSlot.CoMain => Loc.T("card.coMain"),
        CardSlot.MainCard => Loc.T("card.mainCard"),
        _ => Loc.T("card.undercard")
    };

    public string ResultText
    {
        get
        {
            if (Result is null)
            {
                return Loc.T("card.tba");
            }

            if (Result.IsDraw)
            {
                return Loc.T("card.drawResult", Result.Kind.Display());
            }

            var winner = Result.WinnerId == FighterAId ? FighterAName : FighterBName;
            return Loc.T("card.boutResult", winner.Split(' ')[^1].ToUpperInvariant(),
                Result.Kind.ShortDisplay(), Result.EndRound);
        }
    }
}

/// <summary>
/// A professional tournament: one league, one division, one night. Undercard
/// first, main event last, which is also the order the player can watch them in.
/// </summary>
public sealed class TournamentCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string LeagueId { get; set; } = "";
    public WeightClassId Division { get; set; }
    public int Day { get; set; } = 0;
    public string Venue { get; set; } = "";
    public List<CardBout> Bouts { get; set; } = new();

    public int PlayerBoutIndex => Bouts.FindIndex(b => b.IsPlayerBout);

    public bool PlayerHasFought => PlayerBoutIndex >= 0 && Bouts[PlayerBoutIndex].IsDecided;

    public bool IsComplete => Bouts.Count > 0 && Bouts.All(b => b.IsDecided);

    public int NextUndecidedIndex => Bouts.FindIndex(b => !b.IsDecided);

    /// <summary>
    /// The next bout the player may watch: the earliest undecided bout that is
    /// not their own. Watching stops at the player's bout, exactly as it would
    /// in the arena.
    /// </summary>
    public int NextWatchedIndex
    {
        get
        {
            for (var i = 0; i < Bouts.Count; i++)
            {
                if (Bouts[i].IsDecided)
                {
                    continue;
                }

                return Bouts[i].IsPlayerBout ? -1 : i;
            }

            return -1;
        }
    }
}
