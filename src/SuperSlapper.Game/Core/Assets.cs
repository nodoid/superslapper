using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace SuperSlapper.Game.Core;

/// <summary>A single glyph inside a bitmap font atlas.</summary>
public sealed class Glyph
{
    public Rectangle Source { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public float Advance { get; init; }
}

/// <summary>
/// Bitmap font rendered from the generated atlas. No content pipeline and no
/// platform font lookup, so the text looks identical on every target.
/// </summary>
public sealed class BitmapFont
{
    private readonly Dictionary<char, Glyph> _glyphs;

    public BitmapFont(Texture2D atlas, Dictionary<char, Glyph> glyphs, int lineHeight, int size)
    {
        Atlas = atlas;
        _glyphs = glyphs;
        LineHeight = lineHeight;
        Size = size;
    }

    public Texture2D Atlas { get; }
    public int LineHeight { get; }
    public int Size { get; }

    public bool TryGetGlyph(char c, out Glyph glyph) => _glyphs.TryGetValue(c, out glyph!);

    public float MeasureLineWidth(string text, float scale = 1f)
    {
        var width = 0f;
        foreach (var c in text)
        {
            if (_glyphs.TryGetValue(c, out var glyph))
            {
                width += glyph.Advance;
            }
            else if (_glyphs.TryGetValue('?', out var fallback))
            {
                width += fallback.Advance;
            }
        }

        return width * scale;
    }

    public Vector2 Measure(string text, float scale = 1f)
    {
        var lines = text.Split('\n');
        var widest = lines.Max(l => MeasureLineWidth(l, scale));
        return new Vector2(widest, lines.Length * LineHeight * scale);
    }

    /// <summary>Greedy word wrap to a pixel width.</summary>
    public List<string> Wrap(string text, float maxWidth, float scale = 1f)
    {
        var result = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = string.Empty;
            foreach (var word in words)
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (MeasureLineWidth(candidate, scale) > maxWidth && line.Length > 0)
                {
                    result.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            result.Add(line);
        }

        return result;
    }
}

/// <summary>
/// Loads and caches every texture, font and sound from the Content folder.
/// Files are plain PNG/WAV/JSON so there is no MGCB build step on any platform.
/// </summary>
public sealed class Assets : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapFont> _fonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = new();

    public Assets(GraphicsDevice device) => _device = device;

    public IReadOnlyList<string> Errors => _errors;

    public static readonly string[] TextureNames =
    {
        "appicon_512", "arena_hall", "arena_outdoor", "arena_world", "arrow_down", "arrow_up",
        "bar_damage", "bar_health", "bar_power", "bar_stamina", "bar_trough", "belt", "bubble",
        "bubble_tail_left", "bubble_tail_right", "button_danger", "button_ghost", "button_hover",
        "button_normal", "button_pressed", "fighter_champion", "fighter_green", "fighter_player",
        "fighter_rival", "hand_closeup", "hero_slap", "icon_hand", "icon_medkit", "icon_reflex", "icon_run",
        "icon_target", "icon_video", "icon_water", "icon_weights", "impact", "logo", "medal",
        "menu_bg", "needle", "panel", "pip_off", "pip_on", "poster", "referee", "replay_ko",
        "replay_slap", "ring_logo",
        "scale", "stage_front", "stars", "trophy"
    };

    public static readonly string[] FontNames = { "tiny", "small", "body", "medium", "large", "huge" };

    public static readonly string[] SoundNames =
    {
        "applause", "applause_wild", "bell", "bell_end", "crowd_boo", "crowd_cheer",
        "fanfare", "ko", "music_fight", "music_menu", "music_victory", "slap_heavy",
        "slap_light", "ui_click", "ui_confirm", "whiff"
    };

    public void LoadAll()
    {
        foreach (var name in TextureNames)
        {
            try
            {
                _textures[name] = LoadTexture($"Content/art/{name}.png");
            }
            catch (Exception ex)
            {
                _errors.Add($"{name}: {ex.Message}");
            }
        }

        foreach (var name in FontNames)
        {
            try
            {
                _fonts[name] = LoadFont(name);
            }
            catch (Exception ex)
            {
                _errors.Add($"font {name}: {ex.Message}");
            }
        }

        foreach (var name in SoundNames)
        {
            try
            {
                using var stream = TitleContainer.OpenStream($"Content/audio/{name}.wav");
                _sounds[name] = SoundEffect.FromStream(stream);
            }
            catch (Exception ex)
            {
                _errors.Add($"sound {name}: {ex.Message}");
            }
        }
    }

    private Texture2D LoadTexture(string path)
    {
        using var stream = TitleContainer.OpenStream(path);
        return Texture2D.FromStream(_device, stream);
    }

    private BitmapFont LoadFont(string name)
    {
        using var stream = TitleContainer.OpenStream($"Content/font/font_{name}.png");
        var atlas = Texture2D.FromStream(_device, stream);

        string json;
        using (var jsonStream = TitleContainer.OpenStream($"Content/font/font_{name}.json"))
        using (var reader = new StreamReader(jsonStream))
        {
            json = reader.ReadToEnd();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var glyphs = new Dictionary<char, Glyph>();

        foreach (var property in root.GetProperty("glyphs").EnumerateObject())
        {
            if (property.Name.Length == 0)
            {
                continue;
            }

            var g = property.Value;
            glyphs[property.Name[0]] = new Glyph
            {
                Source = new Rectangle(
                    g.GetProperty("x").GetInt32(),
                    g.GetProperty("y").GetInt32(),
                    Math.Max(1, g.GetProperty("w").GetInt32()),
                    Math.Max(1, g.GetProperty("h").GetInt32())),
                OffsetX = g.GetProperty("ox").GetInt32(),
                OffsetY = g.GetProperty("oy").GetInt32(),
                Advance = g.GetProperty("adv").GetSingle()
            };
        }

        return new BitmapFont(atlas, glyphs, root.GetProperty("lineHeight").GetInt32(),
            root.GetProperty("size").GetInt32());
    }

    /// <summary>
    /// A missing texture falls back to another loaded one and finally to a single
    /// white pixel, so a partial asset set can never take the game down.
    /// </summary>
    public Texture2D Tex(string name)
    {
        if (_textures.TryGetValue(name, out var texture))
        {
            return texture;
        }

        if (_textures.TryGetValue("appicon_512", out var fallback))
        {
            return fallback;
        }

        return _textures.Values.FirstOrDefault() ?? Gfx.Pixel;
    }

    public bool HasTexture(string name) => _textures.TryGetValue(name, out var texture) && texture is not null;

    /// <summary>An unmapped font name falls back to any loaded font, then to an empty one.</summary>
    public BitmapFont Font(string name)
    {
        if (_fonts.TryGetValue(name, out var font))
        {
            return font;
        }

        return _fonts.TryGetValue("body", out var fallback)
            ? fallback
            : _fonts.Values.FirstOrDefault() ?? EmptyFont();
    }

    private BitmapFont? _emptyFont;

    /// <summary>Last resort so text calls still have an atlas to sample when nothing loaded.</summary>
    private BitmapFont EmptyFont() => _emptyFont ??=
        new BitmapFont(Gfx.Pixel, new Dictionary<char, Glyph>(), 24, 24);

    public SoundEffect? Sound(string name) =>
        _sounds.TryGetValue(name, out var sound) ? sound : null;

    /// <summary>
    /// Scales every effect played through <see cref="Play"/>. Music is played by
    /// the music player instead of through here, so it has a level of its own.
    /// </summary>
    public float EffectsVolume { get; set; } = 1f;

    public void Play(string name, float volume = 0.8f, float pitch = 0f, float pan = 0f)
    {
        var sound = Sound(name);
        if (sound is null || EffectsVolume <= 0f)
        {
            return;
        }

        try
        {
            sound.Play(Math.Clamp(volume * EffectsVolume, 0f, 1f), Math.Clamp(pitch, -1f, 1f),
                Math.Clamp(pan, -1f, 1f));
        }
        catch
        {
            // Audio device unavailable (headless test runs); never take the game down for it.
        }
    }

    public int LoadedTextureCount => _textures.Count;
    public int LoadedFontCount => _fonts.Count;
    public int LoadedSoundCount => _sounds.Count;

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        foreach (var font in _fonts.Values)
        {
            font.Atlas.Dispose();
        }

        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }

        _textures.Clear();
        _fonts.Clear();
        _sounds.Clear();
    }
}
