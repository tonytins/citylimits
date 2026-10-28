namespace CityLimits.Game;

/// <summary>
/// The definitions the game runs with: the base game's, changed by the spec files in
/// <c>common/</c> beside the settings, then by the folders in the mods folder,
/// <c>Documents/CityLimits/Mods</c> unless the game is portable (<see cref="GameFolders"/>).
/// </summary>
public static class GameMods
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // common/ is refreshed once a run, the first time the definitions are wanted.
    private static bool _refreshed;

    /// <summary>
    /// The base game's definitions with <c>common/</c> and the mods applied. Each problem with
    /// either is logged and returned in <paramref name="problems"/>, so the game can show it too.
    /// </summary>
    public static CityDefinitions Load(GameFolders folders, out IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var found = new List<string>();
        if (!_refreshed)
        {
            _refreshed = true;
            if (Refresh(folders.CommonFolder) is { } refreshProblem)
            {
                found.Add(refreshProblem);
            }
        }

        var start = BaseSpecFiles.Load(folders.CommonFolder, out var baseProblem);
        if (baseProblem is not null)
        {
            found.Add(baseProblem);
        }

        var definitions = ModLoader.Load(folders.ModsFolder, out var modProblems, start);
        found.AddRange(modProblems);
        foreach (var problem in found)
        {
            Logger.Warn("{Problem}", problem);
        }

        if (definitions.Mods.Count > 0)
        {
            Logger.Info("Mods applied, in order: {Mods}", string.Join(", ", definitions.Mods));
        }

        problems = found;
        return definitions;
    }

    // Writes this build's base game into common/ when another build wrote what is there. The build
    // number is part of the stamp, so a new build of the same version refreshes it too.
    private static string? Refresh(string folder)
    {
        try
        {
            if (BaseSpecFiles.Refresh(folder, VersionInfo.DisplayVersion))
            {
                Logger.Info("The base game's spec files were written to {Folder}", folder);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"The base game's spec files couldn't be written to '{folder}': {ex.Message}";
        }
    }
}
