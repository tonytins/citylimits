using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using CityLimits.Core.Scenarios;

namespace CityLimits.Core.Formats;

/// <summary>
/// The JSON Schemas of the files people and agents write and read: saved cities (<c>.fcl</c>),
/// scenarios, and mod definitions. They are generated from the same code the game reads the files
/// with, so they can't drift from it, and kept in <c>docs/schemas/</c>, where a test checks they are
/// current. A file can name its schema in a <c>"$schema"</c> field, which every loader accepts and
/// ignores, so an editor can check it and suggest its fields as it is written. A schema describes a
/// file's shape: its fields and their types, with no others. The game's own checks go further, to
/// the ranges of numbers and the rules between fields, and stay the word on what loads.
/// </summary>
public static class FormatSchemas
{
    /// <summary>Where the schemas are published: the repository's <c>docs/schemas/</c> folder.</summary>
    public const string BaseUrl = "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/";

    private const string Draft = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>A schema's file name, such as <c>city-save.v1.0.schema.json</c>.</summary>
    public static string FileNameFor(string name, FormatVersion version) => $"{name}.v{version}.schema.json";

    /// <summary>A schema's published address, for a file's <c>"$schema"</c> field.</summary>
    public static string UrlFor(string name, FormatVersion version) => BaseUrl + FileNameFor(name, version);

    /// <summary>Every schema, by its file name, as indented JSON text.</summary>
    public static IReadOnlyDictionary<string, string> Generate()
    {
        var schemas = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [FileNameFor("city-save", FlatCityFormat.Format)] = Schema(FlatCityFormat.SchemaDocument(), "city-save", FlatCityFormat.Format, "A city saved by CityLimits in the flat city format, .fcl", document: false),
            [FileNameFor("scenario", ScenarioLoader.Format)] = Schema(ScenarioLoader.SchemaDocument(), "scenario", ScenarioLoader.Format, "A CityLimits scenario", document: true),
        };

        foreach (var (name, info) in DefinitionLoader.SchemaDocuments())
        {
            schemas[FileNameFor(name, DefinitionLoader.Format)] = Schema(info, name, DefinitionLoader.Format, Title(name), document: true);
        }

        return schemas;
    }

    private static string Title(string name) => name switch
    {
        "definitions" => "CityLimits definitions: zones, power plants, and services, as the base game writes them",
        "mod" => "A CityLimits mod's mod.json: who the mod is",
        "power" => "A CityLimits spec file, power.json, in a mod or common/: the power plants",
        _ => $"A CityLimits spec file, {name}.json, in a mod or common/: the {name}",
    };

    // A document, a scenario's or a mod's, declares its fields nullable only so the loader can name a
    // missing one; a null is refused like a missing field, so its schema doesn't offer null. A save's
    // nulls are real, such as last year's budget in a city's first year.
    private static string Schema(JsonTypeInfo info, string name, FormatVersion version, string title, bool document)
    {
        var options = new JsonSchemaExporterOptions
        {
            // A row of a save's map, or the file itself, is never null, though .NET can't tell from an array's items.
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, schema) => document ? WithoutNull(Describe(context, schema)) : Describe(context, schema),
        };
        var schema = (JsonObject)info.GetJsonSchemaAsNode(options);

        // The keywords that say what the schema is go first, so a person reading it sees them first.
        var root = new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = UrlFor(name, version),
            ["title"] = title,
        };
        foreach (var (key, value) in schema.ToList())
        {
            schema.Remove(key);
            root[key] = value;
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    private static JsonNode WithoutNull(JsonNode schema)
    {
        if (schema is JsonObject node && node["type"] is JsonArray types)
        {
            var kept = types.Select(type => type!.GetValue<string>()).Where(type => type != "null").ToList();
            node["type"] = kept.Count == 1 ? kept[0] : new JsonArray([.. kept.Select(type => (JsonNode)type)]);
        }

        return schema;
    }

    // The two values the game reads with its own converters, which the exporter can't see into: a
    // format version, and the save's arrays of whole numbers.
    private static JsonNode Describe(JsonSchemaExporterContext context, JsonNode schema)
    {
        var type = Nullable.GetUnderlyingType(context.TypeInfo.Type) ?? context.TypeInfo.Type;
        if (type == typeof(FormatVersion))
        {
            return new JsonObject
            {
                ["description"] = "The file's format version, as \"major.minor\"; a whole number, as older files wrote it, is that major version, .0.",
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["type"] = "string", ["pattern"] = "^[0-9]+\\.[0-9]+$" },
                    new JsonObject { ["type"] = "integer", ["minimum"] = 0 }),
            };
        }

        // A save for 0.4 holds its definitions as a definitions file in format 1.0, which has its own schema.
        if (type == typeof(JsonElement))
        {
            return new JsonObject { ["$ref"] = UrlFor("definitions", DefinitionLoader.Format1) };
        }

        if (type == typeof(int[]))
        {
            return new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } };
        }

        if (context.PropertyInfo?.Name == "$schema")
        {
            return new JsonObject { ["description"] = "The schema the file follows, for editors; the game ignores it.", ["type"] = "string" };
        }

        return schema;
    }
}
