using CityLimits.Core.Settings;

namespace CityLimits.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"citylimits-tests-{Guid.NewGuid():N}");

    [Fact]
    public void SettingsSurviveARoundTrip()
    {
        var path = Path.Combine(_directory, "settings.json");
        var settings = new GameSettings(1600, 900, 2);

        Assert.True(SettingsStore.TrySave(path, settings, out var saveError));
        Assert.Null(saveError);

        var loaded = SettingsStore.Load(path, out var warning);

        Assert.Null(warning);
        Assert.Equal(settings, loaded);
    }

    [Fact]
    public void AMissingFileFallsBackToTheDefaults()
    {
        var loaded = SettingsStore.Load(Path.Combine(_directory, "absent.json"), out var warning);

        Assert.Null(warning);
        Assert.Equal(GameSettings.Default, loaded);
    }

    [Fact]
    public void ADamagedFileIsReportedAndTheDefaultsAreUsed()
    {
        var path = Path.Combine(_directory, "settings.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ this is not json");

        var loaded = SettingsStore.Load(path, out var warning);

        Assert.NotNull(warning);
        Assert.Equal(GameSettings.Default, loaded);
    }

    [Fact]
    public void AFileWithImpossibleValuesIsReportedAndTheDefaultsAreUsed()
    {
        var path = Path.Combine(_directory, "settings.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, """{ "WindowWidth": 10, "WindowHeight": 10, "Speed": 9 }""");

        var loaded = SettingsStore.Load(path, out var warning);

        Assert.NotNull(warning);
        Assert.Equal(GameSettings.Default, loaded);
    }

    [Fact]
    public void SettingsOutsideTheAllowedRangeAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameSettings(10, 720, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameSettings(1280, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameSettings(1280, 720, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameSettings(1280, 720, -1));
    }

    [Fact]
    public void AGameAtSpeedZeroIsPaused()
    {
        Assert.True(new GameSettings(1280, 720, 0).IsPaused);
        Assert.False(new GameSettings(1280, 720, 1).IsPaused);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
