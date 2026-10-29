using SuperSlapper.Core.Localisation;

namespace SuperSlapper.Core.Rules;

/// <summary>
/// Impact zones on the receiving fighter's head. Slap fighting is deliberately
/// narrow: open hand, cheek or jaw only. Everything else is a foul.
/// </summary>
public enum SlapZone
{
    LeftCheek = 0,
    RightCheek = 1,
    Jaw = 2,
    Chin = 3,
    Temple = 4,
    Ear = 5,
    Eye = 6,
    Nose = 7,
    Throat = 8,
    BackOfHead = 9,
    Neck = 10
}

public static class SlapZones
{
    /// <summary>The three scoring targets an official allows.</summary>
    public static IReadOnlyList<SlapZone> Legal { get; } = new[]
    {
        SlapZone.LeftCheek, SlapZone.RightCheek, SlapZone.Jaw
    };

    public static IReadOnlyList<SlapZone> Illegal { get; } = new[]
    {
        SlapZone.Temple, SlapZone.Ear, SlapZone.Eye, SlapZone.Nose,
        SlapZone.Throat, SlapZone.BackOfHead, SlapZone.Neck
    };

    public static bool IsLegal(SlapZone zone) => Legal.Contains(zone);

    public static bool IsIllegal(SlapZone zone) => Illegal.Contains(zone);

    public static string DisplayName(SlapZone zone) => Loc.T($"zone.{Key(zone)}");

    /// <summary>The zone as it reads inside a sentence, e.g. "the left cheek".</summary>
    public static string DisplayNameLower(SlapZone zone) => Loc.T($"zone.{Key(zone)}.lower");

    /// <summary>Foul book description, as the referee would call it.</summary>
    public static string FoulDescription(SlapZone zone) => Loc.T($"zone.{Key(zone)}.foul");

    private static string Key(SlapZone zone) => zone switch
    {
        SlapZone.LeftCheek => "leftCheek",
        SlapZone.RightCheek => "rightCheek",
        SlapZone.Jaw => "jaw",
        SlapZone.Chin => "chin",
        SlapZone.Temple => "temple",
        SlapZone.Ear => "ear",
        SlapZone.Eye => "eye",
        SlapZone.Nose => "nose",
        SlapZone.Throat => "throat",
        SlapZone.BackOfHead => "backOfHead",
        SlapZone.Neck => "neck",
        _ => "other"
    };
}
