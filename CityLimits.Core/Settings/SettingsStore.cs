using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CityLimits.Core.Settings;

/// <summary>Reads and writes the settings file.</summary>
public static class SettingsStore
{
    // The settings' code is generated at build time, so reading them needs no reflection and survives trimming.
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true, TypeInfoResolver = SettingsJson.Default };

    private static JsonTypeInfo<GameSettings> SettingsInfo => (JsonTypeInfo<GameSettings>)SerializerOptions.GetTypeInfo(typeof(GameSettings));

    /// <summary>The largest settings file read, so a damaged one can't run away with memory.</summary>
    public const long MaxFileBytes = 64 * 1024;

    /// <summary>
    /// Reads the settings, falling back to the defaults when the file is missing, unreadable, or not
    /// JSON at all. Each setting is read on its own: one that is missing keeps its default quietly,
    /// and one that is the wrong type or out of range keeps its default with a warning, so a single
    /// bad value, such as one a newer version wrote, doesn't cost the rest. Settings this version
    /// doesn't know are ignored. A problem is reported through <paramref name="warning"/> instead of
    /// throwing.
    /// </summary>
    public static GameSettings Load(string path, out string? warning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        warning = null;

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return GameSettings.Default;
            }

            if (file.Length > MaxFileBytes)
            {
                warning = $"The settings file '{path}' is bigger than {MaxFileBytes / 1024} KB, so the defaults are in use.";
                return GameSettings.Default;
            }

            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject json)
            {
                warning = $"The settings file '{path}' holds no settings, so the defaults are in use.";
                return GameSettings.Default;
            }

            var refused = new List<string>();
            var defaults = GameSettings.Default;
            var settings = new GameSettings(
                Read(json, nameof(GameSettings.WindowWidth), GameSettings.MinWindowWidth, GameSettings.MaxWindowWidth, defaults.WindowWidth, refused),
                Read(json, nameof(GameSettings.WindowHeight), GameSettings.MinWindowHeight, GameSettings.MaxWindowHeight, defaults.WindowHeight, refused),
                Read(json, nameof(GameSettings.Speed), GameSettings.MinSpeed, GameSettings.MaxSpeed, defaults.Speed, refused),
                Read(json, nameof(GameSettings.AutosaveSlots), GameSettings.MinAutosaveSlots, GameSettings.MaxAutosaveSlots, defaults.AutosaveSlots, refused),
                ReadSwitch(json, nameof(GameSettings.Fullscreen), defaults.Fullscreen, refused),
                ReadSwitch(json, nameof(GameSettings.Disasters), defaults.Disasters, refused),
                ReadSwitch(json, nameof(GameSettings.Fires), defaults.Fires, refused),
                ReadSwitch(json, nameof(GameSettings.Floods), defaults.Floods, refused),
                ReadSwitch(json, nameof(GameSettings.Earthquakes), defaults.Earthquakes, refused),
                ReadSwitch(json, nameof(GameSettings.SaveOlderFormats), defaults.SaveOlderFormats, refused),
                ReadSwitch(json, nameof(GameSettings.Meltdowns), defaults.Meltdowns, refused),
                ReadSwitch(json, nameof(GameSettings.AirCrashes), defaults.AirCrashes, refused),
                ReadSwitch(json, nameof(GameSettings.Shipwrecks), defaults.Shipwrecks, refused));
            if (refused.Count > 0)
            {
                warning = $"The settings file '{path}' had {string.Join(", ", refused)}, so {(refused.Count == 1 ? "that one is" : "those are")} back to the default; the rest are kept.";
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = $"The settings file '{path}' could not be read ({ex.Message}), so the defaults are in use.";
            return GameSettings.Default;
        }
    }

    // A setting's whole number, or its default: quietly when it is missing, with a note of why when it
    // is the wrong type or out of range.
    private static int Read(JsonObject json, string name, int min, int max, int fallback, List<string> refused)
    {
        if (!json.TryGetPropertyValue(name, out var node) || node is null)
        {
            return fallback;
        }

        if (node is JsonValue value && value.TryGetValue<int>(out var number))
        {
            if (number >= min && number <= max)
            {
                return number;
            }

            refused.Add($"{name} {number}, outside {min} to {max}");
            return fallback;
        }

        refused.Add($"{name} that isn't a whole number");
        return fallback;
    }

    // A setting that is on or off, or its default, in the same way.
    private static bool ReadSwitch(JsonObject json, string name, bool fallback, List<string> refused)
    {
        if (!json.TryGetPropertyValue(name, out var node) || node is null)
        {
            return fallback;
        }

        if (node is JsonValue value && value.TryGetValue<bool>(out var on))
        {
            return on;
        }

        refused.Add($"{name} that isn't true or false");
        return fallback;
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
