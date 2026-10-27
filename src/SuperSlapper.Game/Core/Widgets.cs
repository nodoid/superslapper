using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperSlapper.Game.Core;

public enum ButtonStyle
{
    Normal,
    Danger,
    Ghost
}

/// <summary>A nine-sliced button with hover and press feedback.</summary>
public sealed class Button
{
    public Button(Rectangle bounds, string text, Action? onClick = null, ButtonStyle style = ButtonStyle.Normal)
    {
        Bounds = bounds;
        Text = text;
        OnClick = onClick;
        Style = style;
    }

    public Rectangle Bounds { get; set; }
    public string Text { get; set; }
    public string? SubText { get; set; }
    public string? IconKey { get; set; }
    public bool Enabled { get; set; } = true;
    public ButtonStyle Style { get; set; }
    public Action? OnClick { get; set; }
    public float Pulse { get; private set; }

    public bool Hovered { get; private set; }
    public float PressedAmount { get; private set; }

    /// <summary>
    /// Slack around the frame that still counts as a hit. A fingertip is about
    /// 40 virtual pixels across and lands a little off the mark, so a button
    /// only as big as its drawing is a button that misses on a phone. Kept at
    /// half the 20px gap between neighbouring buttons so hit boxes never
    /// overlap, or one tap would fire both.
    /// </summary>
    public const int TouchSlop = 10;

    private Rectangle HitBox => new(Bounds.X - TouchSlop, Bounds.Y - TouchSlop,
        Bounds.Width + TouchSlop * 2, Bounds.Height + TouchSlop * 2);

    public void Update(GameTime time, InputState input, Action? clickSound = null)
    {
        Hovered = Enabled && input.HoveringOver(HitBox);
        var hovered = Hovered && input.LeftDown;
        PressedAmount = MathHelper.Lerp(PressedAmount, hovered ? 1f : 0f, 0.35f);
        Pulse += (float)time.ElapsedGameTime.TotalSeconds;

        if (Enabled && input.TappedIn(HitBox))
        {
            clickSound?.Invoke();
            OnClick?.Invoke();
        }
    }

    public void Draw(SpriteBatch batch, Assets assets)
    {
        var state = !Enabled ? "ghost" : Style switch
        {
            ButtonStyle.Danger => "danger",
            ButtonStyle.Ghost => "ghost",
            _ => Hovered ? "hover" : "normal"
        };

        var offset = (int)(PressedAmount * 4);
        var rect = new Rectangle(Bounds.X, Bounds.Y + offset, Bounds.Width, Bounds.Height);
        var tint = Enabled ? Color.White : new Color(140, 150, 170);

        batch.NineSlice(assets.Tex($"button_{state}"), rect, 28, tint);

        var font = assets.Font("medium");

        // A long label shrinks to fit rather than spilling over the frame.
        var hasIcon = IconKey is not null && assets.HasTexture(IconKey);
        var inset = hasIcon ? 84 + 24 : 56;
        var available = Math.Max(40f, rect.Width - inset);
        var labelWidth = font.MeasureLineWidth(Text);
        var labelScale = labelWidth > available ? available / labelWidth : 1f;

        var labelY = SubText is null
            ? rect.Center.Y - font.LineHeight * labelScale / 2f
            : rect.Center.Y - font.LineHeight * labelScale + 2;

        if (hasIcon)
        {
            var icon = assets.Tex(IconKey!);
            var size = 56f;
            var iconPos = new Vector2(rect.Left + 46, rect.Center.Y);
            batch.Draw(icon, iconPos, null, tint, 0f, new Vector2(icon.Width / 2f, icon.Height / 2f),
                new Vector2(size / icon.Width, size / icon.Height), SpriteEffects.None, 0f);
            batch.Text(font, Text, new Vector2(rect.Left + 84, labelY), tint, labelScale, TextAlign.Left);
        }
        else
        {
            batch.Text(font, Text, new Vector2(rect.Center.X, labelY), tint, labelScale, TextAlign.Centre);
        }

        if (SubText is not null)
        {
            batch.Text(assets.Font("small"), SubText,
                new Vector2(rect.Center.X, rect.Center.Y + 14), Theme.Muted, 1f, TextAlign.Centre);
        }
    }
}

/// <summary>
/// The spring-loaded power bar. The needle is driven by a damped spring so it
/// overshoots and settles, and it doubles as the live power display during a
/// bout (one bar per fighter, top left and top right).
/// </summary>
public sealed class PowerMeter
{
    private float _value;
    private float _velocity;
    private float _display;
    private float _displayVelocity;

    public PowerMeter(bool mirror = false) => Mirror = mirror;

    /// <summary>Right-hand bar fills from the right so the two bars face each other.</summary>
    public bool Mirror { get; }

    public string Label { get; set; } = "";
    public Color Accent { get; set; } = Theme.Blue;

    /// <summary>0-1 target the needle springs towards.</summary>
    public float Target
    {
        get => _value;
        set => _value = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>0-1 value actually drawn, including spring overshoot.</summary>
    public float Display => Mathf.Clamp(_display, 0f, 1.08f);

    public void Snap(float value)
    {
        Target = value;
        _display = value;
        _displayVelocity = 0f;
        _velocity = 0f;
    }

    public void Impulse(float strength) => _displayVelocity += strength;

    public void Update(float dt)
    {
        // Critically-ish damped spring for the needle.
        const float stiffness = 78f;
        const float damping = 11.5f;
        var delta = _value - _display;
        _displayVelocity += (delta * stiffness - _displayVelocity * damping) * dt;
        _display += _displayVelocity * dt;

        _velocity *= 0.92f;
    }

    public void Draw(SpriteBatch batch, Assets assets, Rectangle bounds, bool showNeedle = true)
    {
        var trough = assets.Tex("bar_trough");
        batch.NineSlice(trough, bounds, 12, new Color(20, 26, 40, 235));

        var inner = new Rectangle(bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 12);
        var filled = (int)(inner.Width * Display);
        if (filled > 0)
        {
            var fillRect = Mirror
                ? new Rectangle(inner.Right - filled, inner.Y, filled, inner.Height)
                : new Rectangle(inner.X, inner.Y, filled, inner.Height);
            batch.Draw(assets.Tex("bar_power"), fillRect, Color.White);
        }

        // Tick marks for the sweet spot at 75-95%.
        for (var i = 1; i < 10; i++)
        {
            var x = inner.X + inner.Width * i / 10f;
            var tick = i >= 8 ? Theme.Gold : new Color(255, 255, 255, 60);
            batch.Fill(x, inner.Y, 2, i == 8 || i == 9 ? inner.Height : inner.Height / 3f, tick);
        }

        if (showNeedle)
        {
            var nx = Mirror
                ? inner.Right - inner.Width * Display
                : inner.X + inner.Width * Display;
            var needle = assets.Tex("needle");
            batch.Draw(needle, new Vector2(nx, bounds.Center.Y), null, Color.White, 0f,
                new Vector2(needle.Width / 2f, needle.Height / 2f),
                new Vector2(1f, bounds.Height / (float)needle.Height * 1.1f), SpriteEffects.None, 0f);
        }

        batch.Outline(bounds, new Color(0, 0, 0, 140), 2);
    }
}

/// <summary>A thin stat bar with a label and value, used on career and camp screens.</summary>
public static class StatBar
{
    public static void Draw(SpriteBatch batch, Assets assets, Rectangle bounds, string label, int value,
        Color colour, bool showValue = true)
    {
        var font = assets.Font("small");
        batch.Text(font, label, new Vector2(bounds.Left, bounds.Top - 26), Theme.Muted);

        if (showValue)
        {
            batch.Text(font, value.ToString(), new Vector2(bounds.Right, bounds.Top - 26), Theme.White,
                1f, TextAlign.Right);
        }

        batch.Fill(bounds, new Color(12, 18, 30));
        var width = (int)(bounds.Width * Math.Clamp(value / 100f, 0f, 1f));
        batch.Fill(new Rectangle(bounds.X, bounds.Y, width, bounds.Height), colour);
        batch.Outline(bounds, new Color(0, 0, 0, 120), 2);
    }
}

/// <summary>
/// The ringside commentator. Lines queue up and are shown in a speech bubble,
/// longest-first-trimmed so they always fit.
/// </summary>
public sealed class Commentator
{
    private readonly Queue<string> _queue = new();
    private string _current = string.Empty;
    private float _timer;

    public string Name { get; set; } = "Bobby 'The Mouth' Kavanagh";

    public string Current => _current;
    public float Visibility => Math.Clamp(_timer, 0f, 1f);
    public bool Speaking => _current.Length > 0 && _timer > 0f;

    public void Say(string line, float seconds = 3.2f)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (_timer > 0.6f && _current.Length > 0)
        {
            _queue.Enqueue(line);
            while (_queue.Count > 4)
            {
                _queue.Dequeue();
            }

            return;
        }

        _current = line;
        _timer = seconds;
    }

    public void Clear()
    {
        _queue.Clear();
        _current = string.Empty;
        _timer = 0f;
    }

    public void Update(float dt)
    {
        if (_timer > 0f)
        {
            _timer -= dt;
            if (_timer <= 0f && _queue.Count > 0)
            {
                _current = _queue.Dequeue();
                _timer = 3.0f;
            }

            return;
        }

        _current = string.Empty;
    }

    public void Draw(SpriteBatch batch, Assets assets, Vector2 anchor, float maxWidth = 720f,
        bool tailLeft = true)
    {
        if (!Speaking)
        {
            return;
        }

        var font = assets.Font("body");
        var lines = font.Wrap(_current, maxWidth - 64, 1f);
        var height = 70 + lines.Count * font.LineHeight;
        var width = Math.Min(maxWidth, lines.Max(l => font.MeasureLineWidth(l)) + 80);

        var alpha = Math.Clamp(Visibility * 2.2f, 0f, 1f);
        var rect = new Rectangle((int)(anchor.X - width / 2f), (int)(anchor.Y - height), (int)width, height);

        batch.NineSlice(assets.Tex("bubble"), rect, 34, Color.White * alpha);

        var tail = assets.Tex(tailLeft ? "bubble_tail_left" : "bubble_tail_right");
        var tailPos = new Vector2(tailLeft ? rect.Left + 60 : rect.Right - 60, rect.Bottom - 6);
        batch.Draw(tail, tailPos, null, Color.White * alpha, 0f,
            new Vector2(tail.Width / 2f, 0f), 1f, SpriteEffects.None, 0f);

        batch.Text(assets.Font("tiny"), Name.ToUpperInvariant(),
            new Vector2(rect.Left + 32, rect.Top + 18), new Color(70, 86, 120) * alpha);

        var y = rect.Top + 44;
        foreach (var line in lines)
        {
            batch.Text(font, line, new Vector2(rect.Left + 32, y), new Color(20, 26, 40) * alpha, 1f,
                TextAlign.Left, shadow: false);
            y += font.LineHeight;
        }
    }
}

public static class Mathf
{
    public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    public static float SmoothStep(float t) => t * t * (3f - 2f * t);
}
