using System.Text.Json;

namespace CityLimits.Core.Settings;

/// <summary>Reads and writes the settings file.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// Reads the settings, falling back to the defaults when the file is missing,
    /// unreadable, or damaged. A problem is reported through
    /// <paramref name="warning"/> instead of throwing.
    /// </summary>
    public static GameSettings Load(string path, out string? warning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        warning = null;

        try
        {
            if (!File.Exists(path))
            {
                return GameSettings.Default;
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<GameSettings>(json, SerializerOptions);
            if (settings is null)
            {
                warning = $"The settings file '{path}' is empty, so the defaults are in use.";
                return GameSettings.Default;
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            warning = $"The settings file '{path}' could not be read ({ex.Message}), so the defaults are in use.";
            return GameSettings.Default;
        }
    }

    /// <summary>Writes the settings, reporting a failure instead of throwing.</summary>
    public static bool TrySave(string path, GameSettings settings, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);
        error = null;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(settings, SerializerOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = $"The settings could not be saved to '{path}': {ex.Message}";
            return false;
        }
    }
}
