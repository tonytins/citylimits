namespace CityLimits.Core.Settings;

/// <summary>
/// Where CityLimits keeps what it writes and what the player adds: the settings in the hidden
/// per-user config folder, and mods and saved cities under <c>Documents/CityLimits</c>, where
/// players expect a game's files. A packaged game, such as an AppImage, runs from a read-only
/// folder, so nothing is written beside the executable. The exception is a portable copy: when
/// <c>settings.json</c> already sits beside the executable and that folder can be written to,
/// everything stays there, as it always has.
/// </summary>
public sealed record GameFolders
{
    /// <summary>The folder CityLimits makes for itself in the config and Documents folders.</summary>
    public const string FolderName = "CityLimits";

    /// <summary>The settings file's name.</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>The mods folder's name.</summary>
    public const string ModsFolderName = "Mods";

    /// <summary>The saved cities' folder name.</summary>
    public const string CitiesFolderName = "Cities";

    /// <summary>The base game's spec files' folder name, beside the settings.</summary>
    public const string CommonFolderName = "common";

    /// <summary>The player's own scenarios' folder name.</summary>
    public const string ScenariosFolderName = "Scenarios";

    /// <summary>Where the settings are written.</summary>
    public required string SettingsFile { get; init; }

    /// <summary>
    /// Where the settings are read from: <see cref="SettingsFile"/>, or, the first time the game
    /// runs with per-user folders, the settings an older version left beside the executable.
    /// </summary>
    public required string SettingsToRead { get; init; }

    /// <summary>Where mods are read from.</summary>
    public required string ModsFolder { get; init; }

    /// <summary>
    /// Where the base game's spec files are written for players to read and change: beside the
    /// settings, since a packaged game's own folder can't be written to.
    /// </summary>
    public required string CommonFolder { get; init; }

    /// <summary>Where cities are saved.</summary>
    public required string CitiesFolder { get; init; }

    /// <summary>Where the player's own scenarios are kept, beside their cities; the game lists them with its own.</summary>
    public required string ScenariosFolder { get; init; }

    /// <summary>Tells whether everything is kept beside the executable.</summary>
    public required bool IsPortable { get; init; }

    /// <summary>
    /// Why the per-user folders couldn't be used, when they couldn't and the game fell back to the
    /// executable's folder; null otherwise.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>
    /// Works out the folders from what the machine says. The executable's folder is portable when
    /// it already holds settings and can be written to. Without a config or a Documents folder, the
    /// game falls back to the executable's folder and says why.
    /// </summary>
    /// <param name="executableFolder">The folder the game runs from.</param>
    /// <param name="settingsBesideExecutable">Whether a settings file sits in that folder.</param>
    /// <param name="executableFolderWritable">Whether the game can write to that folder.</param>
    /// <param name="configFolder">The user's config folder, such as <c>~/.config</c> or <c>%APPDATA%</c>.</param>
    /// <param name="documentsFolder">The user's Documents folder.</param>
    /// <param name="settingsInConfig">Whether a settings file already sits in the per-user config folder.</param>
    /// <exception cref="ArgumentException">The executable's folder is empty.</exception>
    public static GameFolders Resolve(
        string executableFolder,
        bool settingsBesideExecutable,
        bool executableFolderWritable,
        string? configFolder,
        string? documentsFolder,
        bool settingsInConfig)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableFolder);

        var besideExecutable = Path.Combine(executableFolder, SettingsFileName);
        if (settingsBesideExecutable && executableFolderWritable)
        {
            return Beside(executableFolder, problem: null);
        }

        if (!IsUsable(configFolder) || !IsUsable(documentsFolder))
        {
            var missing = !IsUsable(configFolder) ? "config" : "Documents";
            return Beside(executableFolder, $"The {missing} folder couldn't be found, so CityLimits keeps its files beside the game");
        }

        var settings = Path.Combine(configFolder!, FolderName, SettingsFileName);
        var documents = Path.Combine(documentsFolder!, FolderName);
        return new GameFolders
        {
            SettingsFile = settings,
            SettingsToRead = !settingsInConfig && settingsBesideExecutable ? besideExecutable : settings,
            ModsFolder = Path.Combine(documents, ModsFolderName),
            CommonFolder = Path.Combine(configFolder!, FolderName, CommonFolderName),
            CitiesFolder = Path.Combine(documents, CitiesFolderName),
            ScenariosFolder = Path.Combine(documents, ScenariosFolderName),
            IsPortable = false,
        };
    }

    /// <summary>Works out the folders on this machine.</summary>
    public static GameFolders Detect()
    {
        var executableFolder = AppContext.BaseDirectory;

        // Without DoNotVerify, .NET gives nothing for a folder that doesn't exist yet, such as a
        // fresh account's config folder. On Linux it reads the desktop's Documents setting itself.
        var configFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify);

        return Resolve(
            executableFolder,
            File.Exists(Path.Combine(executableFolder, SettingsFileName)),
            CanWriteTo(executableFolder),
            configFolder,
            documentsFolder,
            IsUsable(configFolder) && File.Exists(Path.Combine(configFolder, FolderName, SettingsFileName)));
    }

    private static GameFolders Beside(string executableFolder, string? problem) => new()
    {
        SettingsFile = Path.Combine(executableFolder, SettingsFileName),
        SettingsToRead = Path.Combine(executableFolder, SettingsFileName),
        ModsFolder = Path.Combine(executableFolder, ModsFolderName),
        CommonFolder = Path.Combine(executableFolder, CommonFolderName),
        CitiesFolder = Path.Combine(executableFolder, CitiesFolderName),
        ScenariosFolder = Path.Combine(executableFolder, ScenariosFolderName),
        IsPortable = true,
        Problem = problem,
    };

    private static bool IsUsable(string? folder) => !string.IsNullOrWhiteSpace(folder) && Path.IsPathFullyQualified(folder);

    // The only sure test is to write: permissions, a read-only mount, and a full disk all look
    // different from outside.
    private static bool CanWriteTo(string folder)
    {
        var probe = Path.Combine(folder, $".citylimits-write-test-{Environment.ProcessId}");
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
