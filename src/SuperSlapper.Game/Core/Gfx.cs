using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperSlapper.Game.Core;

/// <summary>The single palette every screen draws from.</summary>
public static class Theme
{
    public static readonly Color Ink = new(11, 16, 28);
    public static readonly Color InkSoft = new(22, 30, 48);
    public static readonly Color Panel = new(17, 26, 46);
    public static readonly Color PanelLight = new(30, 44, 74);
    public static readonly Color Gold = new(245, 197, 66);
    public static readonly Color GoldDark = new(168, 124, 18);
    public static readonly Color GoldLight = new(255, 232, 154);
    public static readonly Color White = new(247, 249, 255);
    public static readonly Color Muted = new(150, 163, 190);
    public static readonly Color Blue = new(47, 111, 224);
    public static readonly Color BlueLight = new(142, 194, 255);
    public static readonly Color Red = new(224, 57, 46);
    public static readonly Color RedLight = new(255, 171, 156);
    public static readonly Color Green = new(67, 209, 122);
    public static readonly Color Amber = new(242, 211, 61);
    public static readonly Color Bubble = new(247, 249, 255);

    public static Color ForKit(FighterKit kit) => kit switch
    {
        FighterKit.Player => Blue,
        FighterKit.Rival => Red,
        FighterKit.Champion => Gold,
        _ => Green
    };
}

public enum TextAlign
{
    Left,
    Centre,
    Right
}

/// <summary>SpriteBatch helpers: solid rectangles, outlines, nine-slices, text.</summary>
public static class Gfx
{
    private static Texture2D? _pixel;

    public static void Initialise(GraphicsDevice device)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public static Texture2D Pixel => _pixel ?? throw new InvalidOperationException("Gfx.Initialise first.");

    public static void Fill(this SpriteBatch batch, Rectangle rect, Color colour) =>
        batch.Draw(Pixel, rect, colour);

    public static void Fill(this SpriteBatch batch, float x, float y, float w, float h, Color colour) =>
        batch.Draw(Pixel, new Rectangle((int)x, (int)y, (int)w, (int)h), colour);

    public static void Outline(this SpriteBatch batch, Rectangle rect, Color colour, int thickness = 2)
    {
        batch.Fill(new Rectangle(rect.Left, rect.Top, rect.Width, thickness), colour);
        batch.Fill(new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), colour);
        batch.Fill(new Rectangle(rect.Left, rect.Top, thickness, rect.Height), colour);
        batch.Fill(new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), colour);
    }

    public static void Line(this SpriteBatch batch, Vector2 a, Vector2 b, Color colour, float thickness = 2f)
    {
        var edge = b - a;
        if (edge.LengthSquared() < 0.0001f)
        {
            return;
        }

        var angle = (float)Math.Atan2(edge.Y, edge.X);
        batch.Draw(Pixel, a, null, colour, angle, Vector2.Zero,
            new Vector2(edge.Length(), thickness), SpriteEffects.None, 0f);
    }

    /// <summary>Draws a texture stretched over a rectangle with a nine-slice border.</summary>
    public static void NineSlice(this SpriteBatch batch, Texture2D texture, Rectangle dest, int margin,
        Color tint)
    {
        var w = texture.Width;
        var h = texture.Height;
        if (dest.Width <= 0 || dest.Height <= 0)
        {
            return;
        }

        var m = Math.Min(margin, Math.Min(w, h) / 2);
        var scaleX = dest.Width / (float)w;
        var scaleY = dest.Height / (float)h;

        if (dest.Width < m * 2 || dest.Height < m * 2)
        {
            batch.Draw(texture, dest, tint);
            return;
        }

        var srcMiddleW = w - m * 2;
        var srcMiddleH = h - m * 2;
        var dstMiddleW = dest.Width - (int)(m * scaleX) * 2;
        var dstMiddleH = dest.Height - (int)(m * scaleY) * 2;
        var dstCornerW = (int)(m * scaleX);
        var dstCornerH = (int)(m * scaleY);

        void Part(Rectangle src, Rectangle dst) => batch.Draw(texture, dst, src, tint);

        // Corners.
        Part(new Rectangle(0, 0, m, m), new Rectangle(dest.Left, dest.Top, dstCornerW, dstCornerH));
        Part(new Rectangle(w - m, 0, m, m), new Rectangle(dest.Right - dstCornerW, dest.Top, dstCornerW, dstCornerH));
        Part(new Rectangle(0, h - m, m, m), new Rectangle(dest.Left, dest.Bottom - dstCornerH, dstCornerW, dstCornerH));
        Part(new Rectangle(w - m, h - m, m, m),
            new Rectangle(dest.Right - dstCornerW, dest.Bottom - dstCornerH, dstCornerW, dstCornerH));

        // Edges.
        Part(new Rectangle(m, 0, srcMiddleW, m),
            new Rectangle(dest.Left + dstCornerW, dest.Top, dstMiddleW, dstCornerH));
        Part(new Rectangle(m, h - m, srcMiddleW, m),
            new Rectangle(dest.Left + dstCornerW, dest.Bottom - dstCornerH, dstMiddleW, dstCornerH));
        Part(new Rectangle(0, m, m, srcMiddleH),
            new Rectangle(dest.Left, dest.Top + dstCornerH, dstCornerW, dstMiddleH));
        Part(new Rectangle(w - m, m, m, srcMiddleH),
            new Rectangle(dest.Right - dstCornerW, dest.Top + dstCornerH, dstCornerW, dstMiddleH));

        // Centre.
        Part(new Rectangle(m, m, srcMiddleW, srcMiddleH),
            new Rectangle(dest.Left + dstCornerW, dest.Top + dstCornerH, dstMiddleW, dstMiddleH));
    }

    public static void Text(this SpriteBatch batch, BitmapFont font, string text, Vector2 position,
        Color colour, float scale = 1f, TextAlign align = TextAlign.Left, bool shadow = true)
    {
        var x = position.X;
        if (align == TextAlign.Centre)
        {
            x -= font.MeasureLineWidth(text, scale) / 2f;
        }
        else if (align == TextAlign.Right)
        {
            x -= font.MeasureLineWidth(text, scale);
        }

        if (shadow)
        {
            DrawRaw(batch, font, text, new Vector2(x + 2 * scale, position.Y + 2 * scale),
                new Color(0, 0, 0, 150), scale);
        }

        DrawRaw(batch, font, text, new Vector2(x, position.Y), colour, scale);
    }

    private static void DrawRaw(SpriteBatch batch, BitmapFont font, string text, Vector2 position,
        Color colour, float scale)
    {
        var pen = position;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                pen.X = position.X;
                pen.Y += font.LineHeight * scale;
                continue;
            }

            if (!font.TryGetGlyph(c, out var glyph))
            {
                if (!font.TryGetGlyph('?', out glyph))
                {
                    continue;
                }
            }

            batch.Draw(font.Atlas, new Vector2(pen.X + glyph.OffsetX * scale, pen.Y + glyph.OffsetY * scale),
                glyph.Source, colour, 0f, Vector2.Zero,
                new Vector2(scale * glyph.Source.Width / (float)glyph.Source.Width,
                    scale * glyph.Source.Height / (float)glyph.Source.Height),
                SpriteEffects.None, 0f);
            pen.X += glyph.Advance * scale;
        }
    }

    /// <summary>Multi-line wrapped text; returns the height used.</summary>
    public static float WrappedText(this SpriteBatch batch, BitmapFont font, string text, Vector2 position,
        float maxWidth, Color colour, float scale = 1f, TextAlign align = TextAlign.Left)
    {
        var lines = font.Wrap(text, maxWidth, scale);
        var y = position.Y;
        foreach (var line in lines)
        {
            batch.Text(font, line, new Vector2(position.X, y), colour, scale, align);
            y += font.LineHeight * scale;
        }

        return lines.Count * font.LineHeight * scale;
    }

    public static void Sprite(this SpriteBatch batch, Texture2D texture, Vector2 position, Color tint,
        float scale = 1f, SpriteEffects effects = SpriteEffects.None, float rotation = 0f,
        float depth = 0f, Vector2? originOverride = null)
    {
        var origin = originOverride ?? new Vector2(texture.Width / 2f, texture.Height / 2f);
        batch.Draw(texture, position, null, tint, rotation, origin,
            new Vector2(scale, scale), effects, depth);
    }

    public static void Frame(this SpriteBatch batch, Texture2D sheet, int frameWidth, int frameHeight,
        int index, Vector2 position, Color tint, float scale, SpriteEffects effects = SpriteEffects.None,
        float rotation = 0f)
    {
        var columns = Math.Max(1, sheet.Width / frameWidth);
        var src = new Rectangle((index % columns) * frameWidth, (index / columns) * frameHeight,
            frameWidth, frameHeight);
        batch.Draw(sheet, position, src, tint, rotation,
            new Vector2(frameWidth / 2f, frameHeight), new Vector2(scale, scale), effects, 0f);
    }

    /// <summary>Coin flip style easing used all over the UI.</summary>
    public static float EaseOutCubic(float t) => 1f - (float)Math.Pow(1f - Math.Clamp(t, 0f, 1f), 3);

    public static float EaseInOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
    }
}
