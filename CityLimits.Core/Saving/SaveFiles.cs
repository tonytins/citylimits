using System.Diagnostics.CodeAnalysis;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>What every save format does with files: reading with a size limit, and writing through a temporary file.</summary>
internal static class SaveFiles
{
    /// <summary>Reads a file's bytes, refusing a missing file or one over the limit before reading it.</summary>
    public static bool TryReadBytes(string path, long maxBytes, [NotNullWhen(true)] out byte[]? bytes, [NotNullWhen(false)] out string? problem)
    {
        bytes = null;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                problem = $"There's no city at {path}.";
                return false;
            }

            if (file.Length > maxBytes)
            {
                problem = $"{file.Name} is bigger than {maxBytes / (1024 * 1024)} MB, so it isn't a city CityLimits saved.";
                return false;
            }

            bytes = File.ReadAllBytes(path);
            problem = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            problem = $"{path} couldn't be read: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Writes bytes to a file. They go to a temporary file first, which then takes the file's place,
    /// so a failure partway leaves the file before it as it was.
    /// </summary>
    public static bool TryWriteBytes(byte[] bytes, string path, [NotNullWhen(false)] out string? problem)
    {
        var temporary = path + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
            problem = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            problem = $"{Path.GetFileName(path)} couldn't be saved: {ex.Message}";
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The temporary file is left behind; the save before it is untouched either way.
            }

            return false;
        }
    }

    /// <summary>What a list shows about a saved city, from its save model; null for one that can't be shown.</summary>
    public static SavedCitySummary? Summarize(string path, CitySave save)
    {
        if (string.IsNullOrWhiteSpace(save.Name) || save.CityTime < 0)
        {
            return null;
        }

        var census = save.Census;
        var population = census is null ? 0 : (long)census.ResidentPopulation + census.CommercialPopulation + census.IndustrialPopulation;
        return new SavedCitySummary(path, save.Name, CityCalendar.DateAt(save.CityTime), population, save.Funds, File.GetLastWriteTimeUtc(path));
    }
}
