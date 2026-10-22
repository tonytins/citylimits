using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CityLimits.Core.Settings;

/// <summary>Reads and writes the settings file.</summary>
public static class SettingsStore
{
    // The settings' code is generated at build time, so reading them needs no reflection and survives trimming.
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true, TypeInfoResolver = SettingsJson.Default };

    private static JsonTypeInfo<GameSettings> SettingsInfo => (JsonTypeInfo<GameSettings>)SerializerOptions.GetTypeInfo(typeof(GameSettings));

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
            var settings = JsonSerializer.Deserialize(json, SettingsInfo);
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

            File.WriteAllText(path, JsonSerializer.Serialize(settings, SettingsInfo));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = $"The settings could not be saved to '{path}': {ex.Message}";
            return false;
        }
    }
}

[JsonSerializable(typeof(GameSettings))]
internal sealed partial class SettingsJson : JsonSerializerContext;
