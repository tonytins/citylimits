using CityLimits.Core.Simulation;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CityLimits.Core.Saving;

/// <summary>
/// The flat city format, <c>.fcl</c>: a saved city as one JSON file a person or an agent can read and
/// compare. It holds everything the city needs to carry on exactly where it was, so a city saved at
/// any step, even paused, loads back and runs on tick for tick as if it had never stopped. Reading is
/// strict: a file too big, a field missing or unknown, or a value out of range is refused, and the
/// first problem is reported with where it is. <c>docs/saving.md</c> describes the format.
/// </summary>
public static class FlatCityFormat
{
    /// <summary>The extension of a flat city file.</summary>
    public const string Extension = ".fcl";

    /// <summary>The version of the format this game writes and reads.</summary>
    public const int Format = 1;

    /// <summary>The largest file read, so a damaged or wrong one can't run away with memory.</summary>
    public const long MaxBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 16,
        Converters = { new CompactNumbers() },
    };

    /// <summary>Writes a city as the format's text.</summary>
    public static string Write(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        return JsonSerializer.Serialize(city.Capture(), Options);
    }

    /// <summary>Reads a city from the format's text, or says what is wrong with it.</summary>
    public static bool TryRead(string text, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem)
    {
        city = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            problem = "The file is empty.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            problem = $"The file is bigger than {MaxBytes / (1024 * 1024)} MB, so it isn't a city CityLimits saved.";
            return false;
        }

        try
        {
            var save = JsonSerializer.Deserialize<CitySave>(text, Options) ?? throw new InvalidSave("The file holds no city.");
            if (save.Format != Format)
            {
                throw new InvalidSave($"format: this version reads format {Format}, not {save.Format}.");
            }

            city = CitySimulation.Restore(save);
            problem = null;
            return true;
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" (line {line + 1}{(string.IsNullOrEmpty(ex.Path) ? string.Empty : $", {ex.Path}")})" : string.Empty;
            problem = $"The file isn't a city CityLimits can read{where}: {ex.Message}";
            return false;
        }
        catch (InvalidSave ex)
        {
            problem = ex.Message;
            return false;
        }
    }

    /// <summary>Reads a city from a file, checking its size before reading it.</summary>
    public static bool TryReadFile(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem)
    {
        city = null;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                problem = $"There's no city at {path}.";
                return false;
            }

            if (file.Length > MaxBytes)
            {
                problem = $"{file.Name} is bigger than {MaxBytes / (1024 * 1024)} MB, so it isn't a city CityLimits saved.";
                return false;
            }

            return TryRead(File.ReadAllText(path), out city, out problem);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            problem = $"{path} couldn't be read: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Writes a city to a file. It goes to a temporary file first and then takes the file's place, so
    /// a failure partway leaves the city saved before it as it was.
    /// </summary>
    public static bool TryWriteFile(CitySimulation city, string path, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(city);
        return TryWriteFile(Write(city), path, out problem);
    }

    /// <summary>Writes text from <see cref="Write"/> to a file, through a temporary file, as <see cref="TryWriteFile(CitySimulation, string, out string?)"/> does.</summary>
    public static bool TryWriteFile(string text, string path, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);

        var temporary = path + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporary, text);
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

    /// <summary>Reads what a list of saved cities shows about one, without restoring the whole city.</summary>
    public static bool TryReadSummary(string path, [NotNullWhen(true)] out SavedCitySummary? summary)
    {
        summary = null;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaxBytes)
            {
                return false;
            }

            var save = JsonSerializer.Deserialize<CitySave>(File.ReadAllText(path), Options);
            if (save is null || save.Format != Format || save.CityTime < 0)
            {
                return false;
            }

            var census = save.Census;
            long population = census is null ? 0 : (long)census.ResidentPopulation + census.CommercialPopulation + census.IndustrialPopulation;
            summary = new SavedCitySummary(path, save.Name, CityCalendar.DateAt(save.CityTime), population, save.Funds, file.LastWriteTimeUtc);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    // Arrays of numbers on one line each, so the indented file stays readable: the land value map
    // alone is thousands of numbers.
    private sealed class CompactNumbers : JsonConverter<int[]>
    {
        public override int[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Needs an array of whole numbers.");
            }

            var numbers = new List<int>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                numbers.Add(reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number)
                    ? number
                    : throw new JsonException("Needs an array of whole numbers."));
            }

            return [.. numbers];
        }

        public override void Write(Utf8JsonWriter writer, int[] value, JsonSerializerOptions options) =>
            writer.WriteRawValue($"[{string.Join(",", value.Select(number => number.ToString(CultureInfo.InvariantCulture)))}]", skipInputValidation: true);
    }
}

/// <summary>What a list of saved cities shows about one: its name, date, population, and money, and when the file was last written.</summary>
public sealed record SavedCitySummary(string Path, string Name, GameDate Date, long Population, int Funds, DateTime SavedUtc);
