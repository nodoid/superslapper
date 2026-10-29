using SuperSlapper.Core.Persistence;
using SuperSlapper.Core.Units;

namespace SuperSlapper.Core.Tests;

public class PlayerSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "superslapper-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(_dir, PlayerSettings.FileName);

    [Fact]
    public void LoadingWithNothingSavedGivesTheDefaults()
    {
        var settings = PlayerSettings.Load(_dir);

        Assert.Equal("NORMAL", settings.Music.Label);
        Assert.Equal(0.35f, settings.Music.Volume);
        Assert.Equal("NORMAL", settings.Effects.Label);
        Assert.Equal(0.35f, settings.Effects.Volume);
        Assert.Equal(WeightUnit.Metric, settings.Units);
        Assert.Equal("KILOGRAMS", settings.UnitsLabel);
    }

    [Theory]
    [InlineData(0, "OFF", 0f)]
    [InlineData(1, "LOW", 0.15f)]
    [InlineData(2, "NORMAL", 0.35f)]
    [InlineData(3, "LOUD", 0.6f)]
    public void SavingThenLoadingKeepsEachLevel(int level, string label, float volume)
    {
        var settings = new PlayerSettings();
        settings.Music.Level = level;
        settings.Effects.Level = level;
        settings.Save(_dir);

        var loaded = PlayerSettings.Load(_dir);

        foreach (var channel in new[] { loaded.Music, loaded.Effects })
        {
            Assert.Equal(level, channel.Level);
            Assert.Equal(label, channel.Label);
            Assert.Equal(volume, channel.Volume);
        }
    }

    [Theory]
    [InlineData(WeightUnit.Metric)]
    [InlineData(WeightUnit.Imperial)]
    public void SavingThenLoadingKeepsTheUnits(WeightUnit units)
    {
        var settings = new PlayerSettings { Units = units };
        settings.Save(_dir);

        var loaded = PlayerSettings.Load(_dir);

        Assert.Equal(units, loaded.Units);
        Assert.Equal(units == WeightUnit.Metric ? "KILOGRAMS" : "POUNDS", loaded.UnitsLabel);
    }

    [Fact]
    public void TheFileIsReadableJsonUnderTheDocumentedKeys()
    {
        var settings = new PlayerSettings { Units = WeightUnit.Imperial };
        settings.Music.Level = 3;
        settings.Effects.Level = 0;
        settings.Save(_dir);

        var text = File.ReadAllText(SettingsPath);

        Assert.Contains("\"musicLevel\": 3", text);
        Assert.Contains("\"effectsLevel\": 0", text);
        Assert.Contains("\"units\": \"Imperial\"", text);
    }

    [Fact]
    public void TheTwoChannelsAreIndependent()
    {
        var settings = new PlayerSettings();
        settings.Music.Cycle();
        settings.Save(_dir);

        var loaded = PlayerSettings.Load(_dir);

        Assert.Equal("LOUD", loaded.Music.Label);
        Assert.Equal("NORMAL", loaded.Effects.Label);
    }

    [Fact]
    public void AFileWrittenBeforeTheEffectsHadTheirOwnLevelStillOpens()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ \"musicLevel\": 0 }");

        var loaded = PlayerSettings.Load(_dir);

        Assert.Equal("OFF", loaded.Music.Label);
        Assert.Equal(PlayerSettings.DefaultLevel, loaded.Effects.Level);
        Assert.Equal(WeightUnit.Metric, loaded.Units);
    }

    [Fact]
    public void AnUnreadableUnitsValueFallsBackToMetric()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ \"units\": \"stones\" }");

        var loaded = PlayerSettings.Load(_dir);

        Assert.Equal(WeightUnit.Metric, loaded.Units);
    }

    [Fact]
    public void TheCycleWalksOffLowNormalLoudAndWraps()
    {
        var music = new AudioChannelLevel { Level = 0 };
        var seen = new List<string>();

        for (var step = 0; step < PlayerSettings.Levels.Length + 1; step++)
        {
            seen.Add(music.Label);
            music.Cycle();
        }

        Assert.Equal(new[] { "OFF", "LOW", "NORMAL", "LOUD", "OFF" }, seen);
    }

    [Fact]
    public void SilencingIsTheSameControlAsQuietening()
    {
        var effects = new AudioChannelLevel { Level = 0 };

        Assert.Equal(0f, effects.Volume);
        Assert.Equal("OFF", effects.Label);
    }

    [Theory]
    [InlineData(99, "LOUD")]
    [InlineData(-4, "OFF")]
    public void AHandEditedLevelIsClampedToTheRange(int level, string label)
    {
        var channel = new AudioChannelLevel { Level = level };

        // The level is kept as written; it is the label and the volume that clamp.
        Assert.Equal(level, channel.Level);
        Assert.Equal(label, channel.Label);
    }

    [Fact]
    public void ACorruptFileFallsBackToTheDefaultsInsteadOfThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ this is not json");

        var settings = PlayerSettings.Load(_dir);

        Assert.Equal(PlayerSettings.DefaultLevel, settings.Music.Level);
        Assert.Equal(PlayerSettings.DefaultLevel, settings.Effects.Level);
        Assert.Equal(WeightUnit.Metric, settings.Units);
    }

    [Fact]
    public void AFileWithNoneOfOurKeysInItFallsBackToTheDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ \"somethingElse\": 5 }");

        var settings = PlayerSettings.Load(_dir);

        Assert.Equal(PlayerSettings.DefaultLevel, settings.Music.Level);
        Assert.Equal(PlayerSettings.DefaultLevel, settings.Effects.Level);
        Assert.Equal(WeightUnit.Metric, settings.Units);
    }

    [Fact]
    public void ALaterSaveOverwritesTheEarlierOne()
    {
        var first = new PlayerSettings { Units = WeightUnit.Imperial };
        first.Music.Level = 3;
        first.Save(_dir);

        var second = new PlayerSettings();
        second.Music.Level = 0;
        second.Effects.Level = 3;
        second.Save(_dir);

        var loaded = PlayerSettings.Load(_dir);

        Assert.Equal("OFF", loaded.Music.Label);
        Assert.Equal("LOUD", loaded.Effects.Label);
        Assert.Equal(WeightUnit.Metric, loaded.Units);
    }
}
