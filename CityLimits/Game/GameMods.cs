namespace CityLimits.Game;

/// <summary>The mods the game runs with: the folders in <c>Mods/</c> beside the executable.</summary>
public static class GameMods
{
    /// <summary>The folder mods are read from.</summary>
    public static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Mods");

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The base game's definitions with the mods in <see cref="Folder"/> applied. Each mod that was
    /// skipped is logged and returned in <paramref name="problems"/>, so the game can show it too.
    /// </summary>
    public static CityDefinitions Load(out IReadOnlyList<string> problems)
    {
        var definitions = ModLoader.Load(Folder, out problems);
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
