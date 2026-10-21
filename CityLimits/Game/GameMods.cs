namespace CityLimits.Game;

/// <summary>
/// The mods the game runs with: the folders in the mods folder, <c>Documents/CityLimits/Mods</c>
/// unless the game is portable (<see cref="GameFolders"/>).
/// </summary>
public static class GameMods
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The base game's definitions with the mods in the folder applied. Each mod that was skipped is
    /// logged and returned in <paramref name="problems"/>, so the game can show it too.
    /// </summary>
    public static CityDefinitions Load(string folder, out IReadOnlyList<string> problems)
    {
        var definitions = ModLoader.Load(folder, out problems);
        foreach (var problem in problems)
        {
            Logger.Warn("{Problem}", problem);
        }

        if (definitions.Mods.Count > 0)
        {
            Logger.Info("Mods applied, in order: {Mods}", string.Join(", ", definitions.Mods));
        }

        return definitions;
    }
}
