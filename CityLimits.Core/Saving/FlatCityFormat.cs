using CityLimits.Core.Definitions;
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
    /// The version of the format this game writes, and the newest it reads. Every older version reads
    /// too; one written as a whole number, such as <c>1</c>, is 1.0.
    /// </summary>
    public static readonly FormatVersion Format = SaveUpgrades.Current;

    /// <summary>The version written when the format 0.4 reads is asked for: 1.10, the last of format 1.</summary>
    public static readonly FormatVersion LegacyFormat = SaveUpgrades.Legacy;

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

    /// <summary>
    /// Writes a city as the format's text: version 2.0, or, when <paramref name="legacy"/> asks, 1.10,
    /// which 0.4 reads, with the city's definitions in it and its parts listed by version. A 1.10 file
    /// leaves out what format 1 can't hold, such as a newer save's features kept here.
    /// </summary>
    public static string Write(CitySimulation city, bool legacy = false)
    {
        ArgumentNullException.ThrowIfNull(city);
        var save = Capture(city, legacy, Extension);
        return JsonSerializer.Serialize(save, SaveInfo);
    }

    // The one save model, shaped as 2.0 or as 1.10, for the format with the extension given, whose
    // own kept features go back into it. A 1.10 save is a 2.0 save with format 1's definitions and
    // parts list put back.
    internal static CitySave Capture(CitySimulation city, bool legacy, string format)
    {
        var save = city.Capture();
        if (legacy)
        {
            save.Format = SaveUpgrades.Legacy;
            save.Schema = FormatSchemas.UrlFor("city-save", SaveUpgrades.Legacy);
            save.Definitions = DefinitionWriter.Write(city.Definitions);
            save.Parts = SaveParts.Legacy;

            // What came after 1.10 is left out, as 0.4 never had it.
            save.Ordinances = null;
            save.Connections = null;
            return save;
        }

        // A city from a newer save writes back the reserved features it kept of it.
        var kept = city.Kept?.ForSaving(format) ?? [];
        save.Format = SaveUpgrades.Current;
        save.Schema = FormatSchemas.UrlFor("city-save", SaveUpgrades.Current);
        save.Features = [.. SaveParts.FeatureNames, .. kept.Select(part => part.Name)];
        // A flat save's kept sections go in beside its own; a binary save writes its kept chunks itself.
        save.Unknown = kept.Count == 0 || format != Extension ? null : kept.ToDictionary(part => part.Name, part => city.Kept!.Sections[part.Name]);
        return save;
    }

    /// <summary>Reads a city from the format's text on the base game, or says what is wrong with it.</summary>
    public static bool TryRead(string text, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem) =>
        TryRead(text, CityDefinitions.Base, out city, out problem, out _);

    /// <summary>Reads a city from the format's text on the base game, or says what is wrong with it; the notes say what opening it came to.</summary>
    public static bool TryRead(string text, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes) =>
        TryRead(text, CityDefinitions.Base, out city, out problem, out notes);

    /// <summary>
    /// Reads a city from the format's text, running on the definitions given, or says what is wrong
    /// with it. A save with features this version doesn't know opens when its features list says each
    /// may be left out; the notes say so, what will be kept when the city is saved, and whether the
    /// city was played with other mods.
    /// </summary>
    public static bool TryRead(string text, CityDefinitions definitions, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(definitions);
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
            var opened = new List<string>();
            city = CitySimulation.Restore(save, definitions, opened);
            city.Kept = kept;
            notes = [.. SaveParts.NotesFor(kept), .. opened];
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

    /// <summary>Reads a city from a file on the base game, checking its size before reading it.</summary>
    public static bool TryReadFile(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem) =>
        TryReadFile(path, CityDefinitions.Base, out city, out problem, out _);

    /// <summary>Reads a city from a file on the definitions given, checking its size before reading it; the notes say what opening it came to.</summary>
    public static bool TryReadFile(string path, CityDefinitions definitions, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        notes = [];
        return SaveFiles.TryReadBytes(path, MaxBytes, out var bytes, out problem)
            && TryRead(Encoding.UTF8.GetString(bytes), definitions, out city, out problem, out notes);
    }

    /// <summary>
    /// Writes a city to a file. It goes to a temporary file first and then takes the file's place, so
    /// a failure partway leaves the city saved before it as it was.
    /// </summary>
    public static bool TryWriteFile(CitySimulation city, string path, [NotNullWhen(false)] out string? problem, bool legacy = false)
    {
        ArgumentNullException.ThrowIfNull(city);
        return TryWriteFile(Write(city, legacy), path, out problem);
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
            // Every version this one reads is listed, and a newer save of the same major version too:
            // it may open, and if it can't, opening it says why.
            var save = JsonSerializer.Deserialize(bytes, SaveInfo);
            summary = save is not null && save.Format.Major is >= 1 && save.Format.Major <= Format.Major ? SaveFiles.Summarize(path, save) : null;
            return summary is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // In a 2.0 save, a section this version doesn't know is left out, here and when the city is saved,
    // unless it is a feature this version reserves, which is kept as it was. In a 1.x save, a newer
    // minor version's unknown sections are kept when its parts list lets them be, as 0.4 read them. In
    // any other save, an unknown section is refused like any unknown field.
    private static KeptParts? KeepUnknown(CitySave save)
    {
        var unknown = save.Unknown ?? [];
        save.Unknown = null;
        if (save.Format.Major > Format.Major || save.Format.Major < 1)
        {
            throw new InvalidSave($"format: {save.Format.ProblemReadingWith(Format)}");
        }

        if (save.Format.Major == Format.Major)
        {
            if (unknown.Count == 0)
            {
                return null;
            }

            // A feature is always a section of its own; anything else unknown is refused like any unknown field.
            if (unknown.FirstOrDefault(entry => entry.Value.ValueKind != JsonValueKind.Object) is { Key: { } stray })
            {
                throw new InvalidSave($"{stray}: is not a field a save has, nor a part a newer version added.");
            }

            var features = SaveParts.KeepFeatures(save.Format, Extension, unknown.Keys.Select(name => (name, name)));
            foreach (var part in features.Parts.Where(part => part.Keepable))
            {
                features.Sections[part.Name] = unknown[part.Name].Clone();
            }

            return features;
        }

        var legacy = SaveUpgrades.Legacy;
        var newer = save.Format.Major == legacy.Major && save.Format.Minor > legacy.Minor;
        if (!newer && save.Format.ProblemReadingWith(legacy) is { } problem)
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
