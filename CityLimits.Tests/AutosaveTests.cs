using CityLimits.Core.Saving;
using CityLimits.Core.Settings;

namespace CityLimits.Tests;

public class AutosaveTests
{
    [Fact]
    public void AnAutosaveIsDueAfterFifteenMinutesOfPlayAndCountsAgain()
    {
        var autosave = new Autosave();

        Assert.False(autosave.Advance(Autosave.IntervalSeconds - 1, running: true));
        Assert.False(autosave.Advance(600, running: false));
        Assert.True(autosave.Advance(1, running: true));
        Assert.Equal(0, autosave.PlayedSeconds);
        Assert.False(autosave.Advance(1, running: true));
    }

    [Fact]
    public void ATimeThatIsntOneIsRefused()
    {
        var autosave = new Autosave();

        Assert.Throws<ArgumentOutOfRangeException>(() => autosave.Advance(-1, running: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => autosave.Advance(double.NaN, running: true));
    }

    [Fact]
    public void AutosavesAreNamedForTheirCityAndSlot()
    {
        Assert.Equal("Joffburg (autosave).cl", Autosave.FileNameFor("Joffburg", 1));
        Assert.Equal("Joffburg (autosave 2).cl", Autosave.FileNameFor("Joffburg", 2));
        Assert.Equal("A_B (autosave).cl", Autosave.FileNameFor("A/B", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Autosave.FileNameFor("Joffburg", 3));
        Assert.True(Autosave.IsAutosave("Joffburg (autosave 2).cl"));
        Assert.False(Autosave.IsAutosave("Joffburg.cl"));
        Assert.Equal(Path.Combine("Cities", "Joffburg.cl"), Autosave.CityFileFor(Path.Combine("Cities", "Joffburg (autosave 2).cl")));
    }

    [Fact]
    public void WithTwoSlotsTheNewerReplacesTheOlder()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var first = Path.Combine(folder, "Joffburg (autosave).cl");
            var second = Path.Combine(folder, "Joffburg (autosave 2).cl");

            Assert.Equal(first, Autosave.NextPath(folder, "Joffburg", 2));
            File.WriteAllText(first, "older");
            Assert.Equal(second, Autosave.NextPath(folder, "Joffburg", 2));
            File.WriteAllText(second, "newer");
            File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddMinutes(-15));
            Assert.Equal(first, Autosave.NextPath(folder, "Joffburg", 2));
            Assert.Equal(first, Autosave.NextPath(folder, "Joffburg", 1));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheSettingsKeepOneAutosaveUnlessToldTwo()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var path = Path.Combine(folder, "settings.json");
            File.WriteAllText(path, """{ "WindowWidth": 1280, "WindowHeight": 720, "Speed": 1 }""");
            Assert.Equal(1, SettingsStore.Load(path, out var warning).AutosaveSlots);
            Assert.Null(warning);

            Assert.True(SettingsStore.TrySave(path, GameSettings.Default with { AutosaveSlots = 2 }, out _));
            Assert.Equal(2, SettingsStore.Load(path, out _).AutosaveSlots);

            File.WriteAllText(path, """{ "WindowWidth": 1600, "WindowHeight": 900, "Speed": 2, "AutosaveSlots": 3 }""");
            var settings = SettingsStore.Load(path, out warning);
            Assert.Equal(1, settings.AutosaveSlots);
            Assert.Equal((1600, 900, 2), (settings.WindowWidth, settings.WindowHeight, settings.Speed));
            Assert.Contains("AutosaveSlots 3", warning);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AutosaveCanBeTurnedOffAndFullscreenOnAndBothAreKept()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var path = Path.Combine(folder, "settings.json");
            Assert.True(SettingsStore.TrySave(path, GameSettings.Default with { AutosaveSlots = 0, Fullscreen = true }, out _));

            var settings = SettingsStore.Load(path, out var warning);

            Assert.Null(warning);
            Assert.Equal(0, settings.AutosaveSlots);
            Assert.True(settings.Fullscreen);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ASettingsFileFromBeforeFullscreenIsWindowedAndABadSwitchFallsBackAlone()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var path = Path.Combine(folder, "settings.json");
            File.WriteAllText(path, """{ "WindowWidth": 1600, "WindowHeight": 900, "Speed": 2, "AutosaveSlots": 2 }""");
            Assert.False(SettingsStore.Load(path, out var warning).Fullscreen);
            Assert.Null(warning);

            File.WriteAllText(path, """{ "WindowWidth": 1600, "WindowHeight": 900, "Speed": 2, "AutosaveSlots": 2, "Fullscreen": "yes" }""");
            var settings = SettingsStore.Load(path, out warning);
            Assert.False(settings.Fullscreen);
            Assert.Equal((1600, 900, 2, 2), (settings.WindowWidth, settings.WindowHeight, settings.Speed, settings.AutosaveSlots));
            Assert.Contains("Fullscreen that isn't true or false", warning);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
