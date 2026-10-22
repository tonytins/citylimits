using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>
/// The saved cities in a folder: each city is saved under its own name, and the list a player picks
/// from is the folder's flat city files, newest first. There is no file dialog; the folder is
/// <c>Documents/CityLimits/Cities</c>, or <c>Cities</c> beside a portable game.
/// </summary>
public static class CityFiles
{
    /// <summary>The most cities a list shows, so a crowded folder can't stall the menu.</summary>
    public const int MaxListed = 200;

    /// <summary>
    /// The file a city of the given name is saved to: its name, with any character a file name
    /// can't hold on Windows or Linux made an underscore, and the format's extension.
    /// </summary>
    /// <exception cref="ArgumentException">The name isn't one a city can have.</exception>
    public static string FileNameFor(string cityName)
    {
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

        return safe + FlatCityFormat.Extension;
    }

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
                .EnumerateFiles("*" + FlatCityFormat.Extension)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(MaxListed);
            var cities = new List<SavedCitySummary>();
            foreach (var file in files)
            {
                if (FlatCityFormat.TryReadSummary(file.FullName, out var summary))
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
