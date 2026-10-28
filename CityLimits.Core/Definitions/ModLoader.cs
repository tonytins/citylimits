namespace CityLimits.Core.Definitions;

/// <summary>
/// Finds the mods in a folder and applies them to the base game's definitions. A mod is a folder
/// named for its id, holding a <c>mod.json</c> manifest and the spec files it changes, such as
/// <c>zones.json</c> and <c>services.json</c>, anywhere in the folders under it
/// (<see cref="SpecFolder"/>). Mods apply in id order. A mod that can't be read is skipped whole
/// and reported, so a broken mod never stops the game or half-applies.
/// </summary>
public static class ModLoader
{
    /// <summary>The most mods read from one folder.</summary>
    public const int MaxMods = 64;

    /// <summary>The name of a mod's manifest.</summary>
    public const string ManifestFile = "mod.json";

    /// <summary>
    /// The definitions given, the base game's unless a <c>common/</c> folder changed them, with
    /// every valid mod in the folder applied. A missing folder means no mods. Each mod that was
    /// skipped, and why, is in <paramref name="problems"/>.
    /// </summary>
    public static CityDefinitions Load(string folder, out IReadOnlyList<string> problems, CityDefinitions? start = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var found = new List<string>();
        problems = found;
        var definitions = start ?? CityDefinitions.Base;

        List<string> modFolders;
        try
        {
            if (!Directory.Exists(folder))
            {
                return definitions;
            }

            modFolders = Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            found.Add($"The mods folder '{folder}' could not be read: {ex.Message}");
            return definitions;
        }

        if (modFolders.Count > MaxMods)
        {
            found.Add($"The mods folder holds {modFolders.Count} mods; only the first {MaxMods} by name are read.");
            modFolders = modFolders.Take(MaxMods).ToList();
        }

        foreach (var modFolder in modFolders)
        {
            var name = Path.GetFileName(modFolder);
            if (TryRead(modFolder, name, out var mod, out var error))
            {
                definitions = definitions.With(mod);
            }
            else
            {
                found.Add($"The mod '{name}' was skipped: {error}");
            }
        }

        return definitions;
    }

    private static bool TryRead(string modFolder, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ModDefinitions? mod, out string error)
    {
        mod = null;
        try
        {
            var manifest = Path.Combine(modFolder, ManifestFile);
            if (!File.Exists(manifest))
            {
                error = $"it has no {ManifestFile}.";
                return false;
            }

            return DefinitionLoader.TryParseMod(name, SpecFolder.ReadBounded(manifest), SpecFolder.Read(modFolder), out mod, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error = ex.Message;
            return false;
        }
    }
}
