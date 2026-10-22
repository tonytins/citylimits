using System.Diagnostics.CodeAnalysis;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>
/// The saved cities in a folder: each city is saved under its own name, in the binary format unless
/// it came from a flat file, and the list a player picks from is the folder's city files of either
/// format, and Micropolis cities to import, newest first. There is no file dialog; the folder is <c>Documents/CityLimits/Cities</c>,
/// or <c>Cities</c> beside a portable game.
/// </summary>
public static class CityFiles
{
    /// <summary>The most cities a list shows, so a crowded folder can't stall the menu.</summary>
    public const int MaxListed = 200;

    /// <summary>
    /// The file a city of the given name is saved to: its name, with any character a file name
    /// can't hold on Windows or Linux made an underscore, and the format's extension, the binary
    /// format's unless another is given.
    /// </summary>
    /// <exception cref="ArgumentException">The name isn't one a city can have, or the extension isn't a city format's.</exception>
    public static string FileNameFor(string cityName, string extension = BinaryCityFormat.Extension)
    {
        if (!IsCityExtension(extension))
        {
            throw new ArgumentException($"'{extension}' isn't a city format's extension.", nameof(extension));
        }

        if (!CitySimulation.IsValidName(cityName))
        {
            throw new ArgumentException($"'{cityName}' isn't a name a city can have.", nameof(cityName));
        }

        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).ToHashSet();
        var safe = new string([.. cityName.Trim().Select(c => invalid.Contains(c) ? '_' : c)]);

        // Windows refuses a name ending in a dot or a space, and a few device names outright.
        safe = safe.TrimEnd('.', ' ');
        if (safe.Length == 0 || IsReservedOnWindows(safe))
        {
            safe = $"_{safe}";
        }

        return safe + extension;
    }

    /// <summary>Tells whether a file is a city by its extension: binary, flat, or a Micropolis city to import.</summary>
    public static bool IsCityFile(string path) => IsListedExtension(Path.GetExtension(path));

    /// <summary>Tells whether a file is a Micropolis city, which is imported rather than opened.</summary>
    public static bool IsImport(string path) => Path.GetExtension(path).Equals(MicropolisCityImport.Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a city from a file of either format, or imports a Micropolis city, by its extension.
    /// The notes say what was read but not used, or what an import replaced.
    /// </summary>
    public static bool TryRead(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        notes = [];
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case BinaryCityFormat.Extension:
                return BinaryCityFormat.TryReadFile(path, out city, out problem, out notes);
            case FlatCityFormat.Extension:
                return FlatCityFormat.TryReadFile(path, out city, out problem);
            case MicropolisCityImport.Extension:
                return MicropolisCityImport.TryImportFile(path, out city, out problem, out notes);
            default:
                city = null;
                problem = $"{Path.GetFileName(path)} isn't a city file.";
                return false;
        }
    }

    /// <summary>Writes a city to a file in the format its extension names; a Micropolis city is never written.</summary>
    /// <exception cref="ArgumentException">The extension isn't a format CityLimits writes.</exception>
    public static bool TryWrite(CitySimulation city, string path, [NotNullWhen(false)] out string? problem) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            BinaryCityFormat.Extension => BinaryCityFormat.TryWriteFile(city, path, out problem),
            FlatCityFormat.Extension => FlatCityFormat.TryWriteFile(city, path, out problem),
            _ => throw new ArgumentException($"{Path.GetFileName(path)} isn't a city file.", nameof(path)),
        };

    private static bool IsCityExtension(string extension) =>
        extension.Equals(BinaryCityFormat.Extension, StringComparison.OrdinalIgnoreCase)
        || extension.Equals(FlatCityFormat.Extension, StringComparison.OrdinalIgnoreCase);

    private static bool IsListedExtension(string extension) =>
        IsCityExtension(extension) || extension.Equals(MicropolisCityImport.Extension, StringComparison.OrdinalIgnoreCase);

    private static bool TryReadSummary(FileInfo file, [NotNullWhen(true)] out SavedCitySummary? summary) =>
        file.Extension.ToLowerInvariant() switch
        {
            BinaryCityFormat.Extension => BinaryCityFormat.TryReadSummary(file.FullName, out summary),
            MicropolisCityImport.Extension => MicropolisCityImport.TryReadSummary(file.FullName, out summary),
            _ => FlatCityFormat.TryReadSummary(file.FullName, out summary),
        };

    /// <summary>The cities saved in a folder, newest first; files that can't be read are left out. An empty or missing folder has none.</summary>
    public static IReadOnlyList<SavedCitySummary> List(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return [];
            }

            var files = new DirectoryInfo(folder)
                .EnumerateFiles()
                .Where(file => IsListedExtension(file.Extension))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(MaxListed);
            var cities = new List<SavedCitySummary>();
            foreach (var file in files)
            {
                if (TryReadSummary(file, out var summary))
                {
                    cities.Add(summary);
                }
            }

            return cities;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static bool IsReservedOnWindows(string name)
    {
        var stem = name.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL"
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && char.IsAsciiDigit(stem[3]));
    }
}
