using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperSlapper.Game.Core;
using Button = SuperSlapper.Game.Core.Button;

namespace SuperSlapper.Game.Screens;

/// <summary>Frame layout of the generated fighter sheets.</summary>
public static class FighterSheet
{
    public const int FrameWidth = 480;
    public const int FrameHeight = 540;

    public const int IdleA = 0;
    public const int IdleB = 1;
    public const int WindupA = 2;
    public const int WindupB = 3;
    public const int StrikeA = 4;
    public const int StrikeB = 5;
    public const int StrikeC = 6;
    public const int Follow = 7;
    public const int Hit = 8;
    public const int Stagger = 9;
    public const int Ko = 10;
    public const int Victory = 11;
    public const int Brace = 12;
    public const int Taunt = 13;
    public const int Tired = 14;

    public static string KitTexture(FighterKit kit) => kit switch
    {
        FighterKit.Player => "fighter_player",
        FighterKit.Rival => "fighter_rival",
        FighterKit.Champion => "fighter_champion",
        _ => "fighter_green"
    };

    /// <summary>Two-frame idle cycle.</summary>
    public static int Idle(float seconds) => ((int)(seconds * 2.4f) % 2) == 0 ? IdleA : IdleB;

    /// <param name="tilt">Rotation at the feet: the lean of a wind-up, or a fall.</param>
    public static void Draw(SpriteBatch batch, Texture2D sheet, int frame, Vector2 position,
        float scale, bool flipped, Color tint, float tilt = 0f)
    {
        Gfx.Frame(batch, sheet, FrameWidth, FrameHeight, frame, position, tint, scale,
            flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, tilt);
    }
}
