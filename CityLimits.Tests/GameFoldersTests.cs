using CityLimits.Core.Settings;

namespace CityLimits.Tests;

public class GameFoldersTests
{
    private static readonly string Game = Path.Combine(Path.GetTempPath(), "game");
    private static readonly string Config = Path.Combine(Path.GetTempPath(), "home", ".config");
    private static readonly string Documents = Path.Combine(Path.GetTempPath(), "home", "Documents");

    [Fact]
    public void SettingsGoInTheConfigFolderAndModsAndCitiesInDocuments()
    {
        var folders = GameFolders.Resolve(Game, settingsBesideExecutable: false, executableFolderWritable: false, Config, Documents, settingsInConfig: false);

        Assert.False(folders.IsPortable);
        Assert.Null(folders.Problem);
        Assert.Equal(Path.Combine(Config, "CityLimits", "settings.json"), folders.SettingsFile);
        Assert.Equal(folders.SettingsFile, folders.SettingsToRead);
        Assert.Equal(Path.Combine(Documents, "CityLimits", "Mods"), folders.ModsFolder);
        Assert.Equal(Path.Combine(Config, "CityLimits", "common"), folders.CommonFolder);
        Assert.Equal(Path.Combine(Documents, "CityLimits", "Cities"), folders.CitiesFolder);
    }

    [Fact]
    public void AWritableFolderWithSettingsBesideTheGameIsPortable()
    {
        var folders = GameFolders.Resolve(Game, settingsBesideExecutable: true, executableFolderWritable: true, Config, Documents, settingsInConfig: false);

        Assert.True(folders.IsPortable);
        Assert.Null(folders.Problem);
        Assert.Equal(Path.Combine(Game, "settings.json"), folders.SettingsFile);
        Assert.Equal(Path.Combine(Game, "Mods"), folders.ModsFolder);
        Assert.Equal(Path.Combine(Game, "common"), folders.CommonFolder);
        Assert.Equal(Path.Combine(Game, "Cities"), folders.CitiesFolder);
    }

    [Fact]
    public void AWritableFolderWithoutSettingsIsNotPortable()
    {
        var folders = GameFolders.Resolve(Game, settingsBesideExecutable: false, executableFolderWritable: true, Config, Documents, settingsInConfig: false);

        Assert.False(folders.IsPortable);
    }

    [Fact]
    public void OldSettingsInAReadOnlyFolderAreReadOnceAndWrittenToTheConfigFolder()
    {
        var first = GameFolders.Resolve(Game, settingsBesideExecutable: true, executableFolderWritable: false, Config, Documents, settingsInConfig: false);
        var later = GameFolders.Resolve(Game, settingsBesideExecutable: true, executableFolderWritable: false, Config, Documents, settingsInConfig: true);

        Assert.False(first.IsPortable);
        Assert.Equal(Path.Combine(Game, "settings.json"), first.SettingsToRead);
        Assert.Equal(Path.Combine(Config, "CityLimits", "settings.json"), first.SettingsFile);
        Assert.Equal(later.SettingsFile, later.SettingsToRead);
    }

    [Theory]
    [InlineData(null, "Documents")]
    [InlineData("", "Documents")]
    [InlineData("relative/config", "Documents")]
    [InlineData("config", null)]
    public void WithoutAUserFolderTheGameKeepsItsFilesBesideItselfAndSaysWhy(string? config, string? documents)
    {
        var configFolder = config == "config" ? Config : config;
        var documentsFolder = documents == "Documents" ? Documents : documents;

        var folders = GameFolders.Resolve(Game, settingsBesideExecutable: false, executableFolderWritable: false, configFolder, documentsFolder, settingsInConfig: false);

        Assert.True(folders.IsPortable);
        Assert.Equal(Path.Combine(Game, "settings.json"), folders.SettingsFile);
        Assert.NotNull(folders.Problem);
        Assert.Contains(configFolder == Config ? "Documents" : "config", folders.Problem);
    }

    [Fact]
    public void ADevelopmentBuildKeepsOnlyItsSettingsBesideItselfAndBringsThePlayersAlongOnce()
    {
        var player = GameFolders.Resolve(Game, settingsBesideExecutable: false, executableFolderWritable: true, Config, Documents, settingsInConfig: true);

        var first = player.WithDevelopmentSettings(Game, settingsThere: false);
        var later = player.WithDevelopmentSettings(Game, settingsThere: true);

        Assert.Equal(Path.Combine(Game, "settings.json"), first.SettingsFile);
        Assert.Equal(player.SettingsFile, first.SettingsToRead);
        Assert.Equal(first.SettingsFile, later.SettingsToRead);
        Assert.False(later.IsPortable);
        Assert.Equal((player.ModsFolder, player.CitiesFolder, player.ScenariosFolder, player.CommonFolder), (later.ModsFolder, later.CitiesFolder, later.ScenariosFolder, later.CommonFolder));
    }

    [Fact]
    public void TheGamesFolderIsRequired()
    {
        Assert.ThrowsAny<ArgumentException>(() => GameFolders.Resolve(" ", false, false, Config, Documents, false));
    }
}
