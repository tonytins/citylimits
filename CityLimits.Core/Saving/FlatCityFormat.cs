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
    public static readonly FormatVersion Format = SaveUpgrades.Current;

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

        // A city from a newer save writes back the parts it kept of it, and that save's version when
        // it kept them all.
        var (kept, version) = city.Kept?.ForSaving(Extension) ?? ([], SaveUpgrades.Current);
        save.Format = version;
        save.Schema = FormatSchemas.UrlFor("city-save", version);
        save.Parts = [.. SaveParts.Known, .. kept];
        save.Unknown = kept.Count == 0 ? null : kept.ToDictionary(part => part.Name, part => city.Kept!.Sections[part.Name]);
        return JsonSerializer.Serialize(save, SaveInfo);
    }

    /// <summary>Reads a city from the format's text, or says what is wrong with it.</summary>
    public static bool TryRead(string text, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem) =>
        TryRead(text, out city, out problem, out _);

    /// <summary>
    /// Reads a city from the format's text, or says what is wrong with it. A save of a newer minor
    /// version opens when every part it has that this version doesn't know is one its parts list says
    /// may be left out; the notes say so, and what will be kept when the city is saved.
    /// </summary>
    public static bool TryRead(string text, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        notes = [];
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
            var kept = KeepUnknown(save);
            city = CitySimulation.Restore(save);
            city.Kept = kept;
            notes = SaveParts.NotesFor(kept);
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
    public static bool TryReadFile(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem) =>
        TryReadFile(path, out city, out problem, out _);

    /// <summary>Reads a city from a file, checking its size before reading it; the notes say what a newer save's parts come to.</summary>
    public static bool TryReadFile(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        notes = [];
        return SaveFiles.TryReadBytes(path, MaxBytes, out var bytes, out problem)
            && TryRead(Encoding.UTF8.GetString(bytes), out city, out problem, out notes);
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
            // A newer save of the same major version is listed too: it may open, and if it can't, opening it says why.
            var save = JsonSerializer.Deserialize(bytes, SaveInfo);
            summary = save is not null && save.Format.Major == Format.Major ? SaveFiles.Summarize(path, save) : null;
            return summary is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // A newer minor version's unknown sections are kept when its parts list lets them be left out, and
    // so are those of a save this version wrote from one, which its parts list names too: a newer save
    // with a part that can't be kept saves as this version, with the keepable parts beside it. In any
    // other save this version can read whole, an unknown section is refused like any unknown field.
    private static KeptParts? KeepUnknown(CitySave save)
    {
        var unknown = save.Unknown ?? [];
        save.Unknown = null;
        var newer = save.Format.Major == Format.Major && save.Format.Minor > Format.Minor;
        if (!newer && save.Format.ProblemReadingWith(Format) is { } problem)
        {
            throw new InvalidSave($"format: {problem}");
        }

        if (!newer && (unknown.Count == 0 || save.Parts is null))
        {
            return unknown.Count == 0 ? null : throw new InvalidSave($"{unknown.Keys.First()}: is not a part a save has.");
        }

        var kept = SaveParts.Keep(save.Format, Extension, save.Parts, unknown.Keys, part => part.Name);
        foreach (var part in kept.Parts)
        {
            kept.Sections[part.Name] = unknown[part.Name].Clone();
        }

        return kept;
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
