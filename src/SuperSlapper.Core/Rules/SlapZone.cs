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

    public static string DisplayName(SlapZone zone) => zone switch
    {
        SlapZone.LeftCheek => "Left cheek",
        SlapZone.RightCheek => "Right cheek",
        SlapZone.Jaw => "Jaw",
        SlapZone.Chin => "Chin",
        SlapZone.Temple => "Temple",
        SlapZone.Ear => "Ear",
        SlapZone.Eye => "Eye",
        SlapZone.Nose => "Nose",
        SlapZone.Throat => "Throat",
        SlapZone.BackOfHead => "Back of the head",
        SlapZone.Neck => "Neck",
        _ => zone.ToString()
    };

    /// <summary>Foul book description, as the referee would call it.</summary>
    public static string FoulDescription(SlapZone zone) => zone switch
    {
        SlapZone.Temple => "Slap to the temple",
        SlapZone.Ear => "Slap to the ear",
        SlapZone.Eye => "Slap to the eye",
        SlapZone.Nose => "Slap across the nose",
        SlapZone.Throat => "Slap to the throat",
        SlapZone.BackOfHead => "Slap to the back of the head",
        SlapZone.Neck => "Slap to the neck",
        _ => "Illegal slap"
    };
}
