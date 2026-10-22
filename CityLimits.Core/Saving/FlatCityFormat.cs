using CityLimits.Core.Simulation;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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

    /// <summary>
    /// The version of the format this game writes, and the newest it reads. A file of an older minor
    /// version reads too; one written as a whole number, such as <c>1</c>, is 1.0.
    /// </summary>
    public static readonly FormatVersion Format = new(1, 0);

    /// <summary>The schema a flat city file names, for editors to check it against.</summary>
    public static string SchemaUrl => FormatSchemas.UrlFor("city-save", Format);

    /// <summary>The largest file read, so a damaged or wrong one can't run away with memory.</summary>
    public const long MaxBytes = 16 * 1024 * 1024;

    // The save model's code is generated at build time, so reading and writing it needs no
    // reflection and survives trimming.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 16,
        Converters = { new CompactNumbers() },
        TypeInfoResolver = SaveJson.Default,
    };

    private static JsonTypeInfo<CitySave> SaveInfo => (JsonTypeInfo<CitySave>)Options.GetTypeInfo(typeof(CitySave));

    /// <summary>Writes a city as the format's text.</summary>
    public static string Write(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        var save = city.Capture();
        save.Schema = SchemaUrl;
        return JsonSerializer.Serialize(save, SaveInfo);
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
            var save = JsonSerializer.Deserialize(text, SaveInfo) ?? throw new InvalidSave("The file holds no city.");
            if (save.Format.ProblemReadingWith(Format) is { } tooNew)
            {
                throw new InvalidSave($"format: {tooNew}");
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
        return SaveFiles.TryReadBytes(path, MaxBytes, out var bytes, out problem)
            && TryRead(Encoding.UTF8.GetString(bytes), out city, out problem);
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
        return SaveFiles.TryWriteBytes(Encoding.UTF8.GetBytes(text), path, out problem);
    }

    /// <summary>Reads what a list of saved cities shows about one, without restoring the whole city.</summary>
    public static bool TryReadSummary(string path, [NotNullWhen(true)] out SavedCitySummary? summary)
    {
        summary = null;
        if (!SaveFiles.TryReadBytes(path, MaxBytes, out var bytes, out _))
        {
            return false;
        }

        try
        {
            var save = JsonSerializer.Deserialize(bytes, SaveInfo);
            summary = save is not null && save.Format.ProblemReadingWith(Format) is null ? SaveFiles.Summarize(path, save) : null;
            return summary is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The save model, for its schema.</summary>
    internal static JsonTypeInfo SchemaDocument() => Options.GetTypeInfo(typeof(CitySave));

    // Arrays of numbers on one line each, so the indented file stays readable: the land value map
    // alone is thousands of numbers.
    internal sealed class CompactNumbers : JsonConverter<int[]>
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

[JsonSerializable(typeof(CitySave))]
internal sealed partial class SaveJson : JsonSerializerContext;
