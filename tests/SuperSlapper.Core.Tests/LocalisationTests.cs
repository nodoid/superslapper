using System.IO;
using System.Runtime.CompilerServices;
using SuperSlapper.Core.Fight;
using SuperSlapper.Core.Fighters;
using SuperSlapper.Core.Leagues;
using SuperSlapper.Core.Localisation;
using SuperSlapper.Core.Rules;
using SuperSlapper.Core.Training;
using Xunit;
using LeagueBook = SuperSlapper.Core.Leagues.Leagues;

namespace SuperSlapper.Core.Tests;

/// <summary>
/// The tests read the real catalogue, so a Core string that has no entry fails
/// here rather than showing up as a key on a player's screen. Loaded once, before
/// any test runs.
/// </summary>
internal static class Catalogue
{
    [ModuleInitializer]
    internal static void Load()
    {
        Loc.LoadEnglish(File.ReadAllText(Path));
        Loc.Load(Loc.Fallback, null);
    }

    /// <summary>The shipped English catalogue, found by walking up from the test binaries.</summary>
    internal static string Path
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = System.IO.Path.Combine(directory.FullName, "Content", "i18n", "en.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Content/i18n/en.json could not be found from the test binaries.");
        }
    }
}

public class LocalisationTests
{
    /// <summary>True when the string came out of a catalogue rather than back as its key.</summary>
    private static void Translated(string value, string prefix, string what)
    {
        Assert.False(value.StartsWith(prefix, StringComparison.Ordinal),
            $"{what} has no catalogue entry: it reads \"{value}\"");
    }

    [Fact]
    public void TheCatalogueLoads()
    {
        Assert.Equal(Loc.Fallback, Loc.Language);
        Assert.True(Loc.Count > 400, $"only {Loc.Count} strings are in the catalogue");
        Assert.Contains("title.newCareer", Loc.Keys);
    }

    [Fact]
    public void EveryDivisionHasANameAndABlurb()
    {
        foreach (var division in WeightClasses.All)
        {
            Translated(division.Name, "division.", "a division name");
            Translated(division.Blurb, "division.", "a division blurb");
            Assert.False(string.IsNullOrWhiteSpace(division.Blurb));
        }
    }

    [Fact]
    public void EveryTrainingSessionIsTranslated()
    {
        foreach (var activity in TrainingCatalog.All)
        {
            Translated(activity.DisplayName, "camp.", "a training session");
            Translated(activity.DisplayDescription, "camp.", "a training description");
        }
    }

    [Fact]
    public void EveryZoneReadsInASentence()
    {
        foreach (var zone in Enum.GetValues<SlapZone>())
        {
            Translated(SlapZones.DisplayName(zone), "zone.", "a zone name");
            Translated(SlapZones.DisplayNameLower(zone), "zone.", "a zone in a sentence");
            Translated(SlapZones.FoulDescription(zone), "zone.", "a zone's foul call");
        }
    }

    [Fact]
    public void EveryInjuryIsTranslated()
    {
        foreach (var type in Enum.GetValues<InjuryType>())
        {
            Translated(new Injury { Type = type }.DisplayName, "injury.", "an injury name");

            foreach (var severity in Enum.GetValues<InjurySeverity>())
            {
                var graded = new Injury { Type = type, Severity = severity };
                Translated(graded.SeverityText, "injury.", "an injury severity");
            }
        }
    }

    [Fact]
    public void EveryLeagueAndRulebookIsTranslated()
    {
        foreach (var league in LeagueBook.All)
        {
            Translated(league.DisplayName, "league.", "a league name");
            Translated(league.DisplayRegion, "league.", "a league region");
            Translated(league.Ruleset.DisplayName, "ruleset.", "a rulebook name");
        }
    }

    [Fact]
    public void EveryOutcomeReadsOutLoud()
    {
        foreach (var kind in Enum.GetValues<FightOutcomeKind>())
        {
            Translated(kind.Display(), "outcome.", "an outcome");
            Translated(kind.ShortDisplay(), "outcome.", "a short outcome");
        }

        foreach (var foul in Enum.GetValues<FoulKind>())
        {
            Translated(foul.Display(), "foul.", "a foul call");
        }
    }
}
