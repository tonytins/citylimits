namespace CityLimits.Core.Definitions;

/// <summary>
/// Finds the mods in a folder and applies them to the base game's definitions. A mod is a folder
/// named for its id, holding a <c>mod.json</c> manifest and the definitions it changes, such as a
/// <c>zones.json</c>. Mods apply in id order. A mod that can't be read is skipped whole and
/// reported, so a broken mod never stops the game or half-applies.
/// </summary>
public static class ModLoader
{
    /// <summary>The most mods read from one folder.</summary>
    public const int MaxMods = 64;

    /// <summary>The name of a mod's manifest.</summary>
    public const string ManifestFile = "mod.json";

    /// <summary>The name of a mod's zone definitions.</summary>
    public const string ZonesFile = "zones.json";

    /// <summary>
    /// The base game's definitions with every valid mod in the folder applied. A missing folder
    /// means no mods. Each mod that was skipped, and why, is in <paramref name="problems"/>.
    /// </summary>
    public static CityDefinitions Load(string folder, out IReadOnlyList<string> problems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var found = new List<string>();
        problems = found;
        var definitions = CityDefinitions.Base;

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
            var manifest = ReadBounded(Path.Combine(modFolder, ManifestFile));
            if (manifest is null)
            {
                error = $"it has no {ManifestFile}.";
                return false;
            }

            var zones = ReadBounded(Path.Combine(modFolder, ZonesFile));
            return DefinitionLoader.TryParseMod(name, manifest, zones, out mod, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileTooLarge)
        {
            error = ex.Message;
            return false;
        }
    }

    // A file that isn't there is null; one that is too big is refused before it is read.
    private static string? ReadBounded(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return null;
        }

        if (file.Length > DefinitionLoader.MaxFileBytes)
        {
            throw new FileTooLarge($"{file.Name} is {file.Length} bytes; the most is {DefinitionLoader.MaxFileBytes}.");
        }

        return File.ReadAllText(path);
    }

    private sealed class FileTooLarge(string message) : Exception(message);
}
