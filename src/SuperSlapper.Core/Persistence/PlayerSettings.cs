using System.Text.Json;
using SuperSlapper.Core.Units;

namespace SuperSlapper.Core.Persistence;

/// <summary>The two things in the game that make a noise the player can turn down.</summary>
public enum AudioChannel
{
    Music = 0,
    Effects = 1
}

/// <summary>
/// One volume control: a level from OFF up to LOUD, so muting and quietening are
/// the same gesture.
/// </summary>
public sealed class AudioChannelLevel
{
    public int Level { get; set; } = PlayerSettings.DefaultLevel;

    /// <summary>The level in range, so a hand-edited file can never pick a bad one.</summary>
    public int Index => Math.Clamp(Level, 0, PlayerSettings.Levels.Length - 1);

    public string Label => PlayerSettings.Levels[Index].Label;

    public float Volume => PlayerSettings.Levels[Index].Volume;

    public void Cycle() => Level = (Index + 1) % PlayerSettings.Levels.Length;
}

/// <summary>
/// The player's preferences: a level each for the music and the sound effects, and
/// the units the weights are read in. They are kept beside the save rather than
/// inside the career, so starting a new one does not reset them.
///
/// The file is read and written by hand rather than through the reflection
/// serializer, which the phone heads flag as trim-unsafe - the same reason the
/// font atlases are parsed with <see cref="JsonDocument"/>.
/// </summary>
public sealed class PlayerSettings
{
    public const string FileName = "settings.json";

    private const string MusicKey = "musicLevel";
    private const string EffectsKey = "effectsLevel";
    private const string UnitsKey = "units";
    private const string LanguageKey = "language";

    /// <summary>The levels the controls step through, quietest first.</summary>
    public static readonly (string Label, float Volume)[] Levels =
    {
        ("OFF", 0f),
        ("LOW", 0.15f),
        ("NORMAL", 0.35f),
        ("LOUD", 0.6f)
    };

    public const int DefaultLevel = 2;

    public AudioChannelLevel Music { get; } = new();

    public AudioChannelLevel Effects { get; } = new();

    public WeightUnit Units { get; set; } = WeightUnit.Metric;

    /// <summary>
    /// The language catalogue to read. Kept beside the save like everything else, so
    /// choosing a language sticks across careers; English is what ships.
    /// </summary>
    public string Language { get; set; } = Localisation.Loc.Fallback;

    public AudioChannelLevel For(AudioChannel channel) =>
        channel == AudioChannel.Music ? Music : Effects;

    public string UnitsLabel => Units == WeightUnit.Metric ? "KILOGRAMS" : "POUNDS";

    public static PlayerSettings Load(string directory)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return new PlayerSettings();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var settings = new PlayerSettings();
                settings.Music.Level = ReadLevel(root, MusicKey) ?? DefaultLevel;
                settings.Effects.Level = ReadLevel(root, EffectsKey) ?? DefaultLevel;
                settings.Units = ReadUnits(root) ?? WeightUnit.Metric;
                settings.Language = ReadLanguage(root) ?? Localisation.Loc.Fallback;
                return settings;
            }
        }
        catch (Exception ex)
        {
            // A settings file that cannot be read is not worth a failed launch.
            Console.WriteLine($"Could not read {FileName}: {ex.Message}");
        }

        return new PlayerSettings();
    }

    public void Save(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            using var stream = File.Create(Path.Combine(directory, FileName));
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteNumber(MusicKey, Music.Level);
            writer.WriteNumber(EffectsKey, Effects.Level);
            writer.WriteString(UnitsKey, Units.ToString());
            writer.WriteString(LanguageKey, Language);
            writer.WriteEndObject();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not write {FileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// A missing key keeps the default, so a file written before the effects got
    /// their own level, or before the units were remembered at all, still opens.
    /// </summary>
    private static int? ReadLevel(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.TryGetInt32(out var level) ? level : null;

    private static string? ReadLanguage(JsonElement root) =>
        root.TryGetProperty(LanguageKey, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static WeightUnit? ReadUnits(JsonElement root) =>
        root.TryGetProperty(UnitsKey, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        Enum.TryParse<WeightUnit>(value.GetString(), ignoreCase: true, out var units)
            ? units
            : null;
}
