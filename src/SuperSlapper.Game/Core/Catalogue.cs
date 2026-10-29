using System.IO;
using Microsoft.Xna.Framework;
using SuperSlapper.Core.Localisation;

namespace SuperSlapper.Game.Core;

/// <summary>
/// Loads the language catalogues that ship in <c>Content/i18n</c>. English is always
/// read first because it is the fallback; the chosen language then sits over it, so
/// a half-finished translation still leaves a readable game.
/// </summary>
public static class Catalogue
{
    /// <summary>Reads the catalogues and puts the game into <paramref name="language"/>.</summary>
    public static void Load(string? language)
    {
        var chosen = string.IsNullOrWhiteSpace(language) ? Loc.Fallback : language.Trim();

        Loc.LoadEnglish(Read(Loc.Fallback));
        Loc.Load(chosen, chosen == Loc.Fallback ? null : Read(chosen));
    }

    private static string Read(string language)
    {
        try
        {
            using var stream = TitleContainer.OpenStream($"Content/i18n/{language}.json");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception)
        {
            // A missing catalogue is English, not a crash: the key comes back as
            // itself and the missing-key check reports it.
            return "{}";
        }
    }
}
