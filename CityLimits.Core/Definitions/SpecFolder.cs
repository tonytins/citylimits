namespace CityLimits.Core.Definitions;

/// <summary>
/// Finds the spec files in a folder and the folders under it, as a mod and the base game's
/// <c>common/</c> keep them: any file named for a spec, such as <c>zones.json</c>, however deep,
/// so a mod can sort its files into folders of its own. They are read in the order of their paths
/// within the folder, letter by letter, and a later file's change to a field wins. Other files are
/// left alone.
/// </summary>
internal static class SpecFolder
{
    /// <summary>The most spec files read from one folder.</summary>
    public const int MaxFiles = 64;

    /// <summary>How many folders deep the search goes.</summary>
    public const int MaxDepth = 8;

    /// <summary>
    /// The spec files in the folder, by their paths within it. A folder that isn't there has none.
    /// </summary>
    /// <exception cref="IOException">The folder or a file couldn't be read, or a file is too big.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or a file can't be read.</exception>
    /// <exception cref="InvalidDataException">The folder holds more than <see cref="MaxFiles"/> spec files.</exception>
    public static IReadOnlyList<SpecFile> Read(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        // The depth bounds the search, even through a link that loops back on itself.
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = MaxDepth, IgnoreInaccessible = false };
        var paths = Directory.EnumerateFiles(folder, "*.json", options)
            .Where(path => DefinitionLoader.SpecFiles.Contains(Path.GetFileName(path).ToLowerInvariant(), StringComparer.Ordinal))
            .Take(MaxFiles + 1)
            .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (paths.Count > MaxFiles)
        {
            throw new InvalidDataException($"it holds more than {MaxFiles} spec files.");
        }

        return paths.Select(path => new SpecFile(path, ReadBounded(Path.Combine(folder, path)))).ToList();
    }

    /// <summary>A file's text, refused before it is read when it is too big.</summary>
    /// <exception cref="IOException">The file couldn't be read, or is too big.</exception>
    public static string ReadBounded(string path)
    {
        var file = new FileInfo(path);
        if (file.Length > DefinitionLoader.MaxFileBytes)
        {
            throw new IOException($"{file.Name} is {file.Length} bytes; the most is {DefinitionLoader.MaxFileBytes}.");
        }

        return File.ReadAllText(path);
    }
}
