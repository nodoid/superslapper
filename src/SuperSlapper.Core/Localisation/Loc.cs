using System.Globalization;
using System.Text.Json;

namespace SuperSlapper.Core.Localisation;

/// <summary>
/// Every string a player can read, looked up by key. The catalogue is a flat
/// JSON object shipped beside the art in <c>Content/i18n</c>, so a new language is
/// a new file and nothing else. English is both the shipped language and the
/// fallback: a key missing from the chosen catalogue falls back to English, and a
/// key missing from both is returned as the key itself and recorded, which is what
/// the tests and the self-test fail on.
/// </summary>
public static class Loc
{
    /// <summary>The language the game is written in, and the one it falls back to.</summary>
    public const string Fallback = "en";

    private static readonly Dictionary<string, string> Strings = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> FallbackStrings = new(StringComparer.Ordinal);
    private static readonly HashSet<string> MissingKeys = new(StringComparer.Ordinal);

    /// <summary>The language in use: the chosen one when it loaded, English otherwise.</summary>
    public static string Language { get; private set; } = Fallback;

    /// <summary>How many strings the catalogue holds. The self-test reports it.</summary>
    public static int Count => Strings.Count;

    /// <summary>Keys asked for that no catalogue could answer, in the order they were asked.</summary>
    public static IReadOnlyCollection<string> Missing => MissingKeys;

    /// <summary>Every key the catalogue holds, for the tests that check it is complete.</summary>
    public static IReadOnlyCollection<string> Keys => Strings.Keys;

    /// <summary>True when the catalogue has this key.</summary>
    public static bool Has(string key) => Strings.ContainsKey(key);

    /// <summary>
    /// Loads English, which also becomes the fallback for every other language.
    /// Called once at startup, before any screen is built.
    /// </summary>
    public static void LoadEnglish(string json)
    {
        FallbackStrings.Clear();
        Merge(FallbackStrings, json);
    }

    /// <summary>
    /// Loads a language over the English fallback. An empty or unreadable catalogue
    /// leaves the game in English rather than blank: a broken translation must never
    /// take the words away.
    /// </summary>
    public static void Load(string language, string? json)
    {
        Strings.Clear();
        MissingKeys.Clear();

        var code = string.IsNullOrWhiteSpace(language) ? Fallback : language.Trim().ToLowerInvariant();

        if (code != Fallback && !string.IsNullOrWhiteSpace(json))
        {
            Merge(Strings, json!);
        }

        if (code == Fallback || Strings.Count == 0)
        {
            foreach (var pair in FallbackStrings)
            {
                Strings[pair.Key] = pair.Value;
            }

            Language = Fallback;
            return;
        }

        // Anything the translation did not cover reads in English.
        foreach (var pair in FallbackStrings)
        {
            Strings.TryAdd(pair.Key, pair.Value);
        }

        Language = code;
    }

    /// <summary>Every language that has a catalogue in the content folder.</summary>
    public static IReadOnlyList<string> Available { get; } = new[] { Fallback };

    /// <summary>The string for a key, with no arguments.</summary>
    public static string T(string key) => Strings.TryGetValue(key, out var text)
        ? text
        : FallbackStrings.TryGetValue(key, out var english)
            ? english
            : Unresolved(key);

    /// <summary>
    /// The string for a key, filled in with arguments. Placeholders are the usual
    /// <c>{0}</c>, <c>{1}</c> style so a translation can move them around the sentence.
    /// </summary>
    public static string T(string key, params object?[] args)
    {
        var format = T(key);
        return args.Length == 0 ? format : string.Format(CultureInfo.InvariantCulture, format, args);
    }

    private static string Unresolved(string key)
    {
        MissingKeys.Add(key);
        return key;
    }

    private static void Merge(Dictionary<string, string> into, string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("A language catalogue must be a JSON object of key/value strings.");
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            into[property.Name] = property.Value.GetString() ?? "";
        }
    }
}
