using System.Text.Json;
using System.Text.Json.Nodes;
using CityLimits.Core.Formats;

namespace CityLimits.Core.Definitions;

/// <summary>
/// The base game's specs as files, in the <c>common/</c> folder beside the settings, for players to
/// read and change. The game writes them when its version changes, over the ones there, so they
/// always show what this version plays with; what a player changed lasts until then, and a mod is
/// the way to keep a change. The game's own values stay in its code, and the files only change
/// them, so a missing or broken file leaves the base game as it is.
/// </summary>
public static class BaseSpecFiles
{
    /// <summary>The file in <c>common/</c> naming the version that last wrote it.</summary>
    public const string VersionFile = "version.txt";

    /// <summary>
    /// Writes the base game's spec files into the folder, unless this version already did. Returns
    /// whether it wrote them.
    /// </summary>
    /// <exception cref="IOException">The folder or a file couldn't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder can't be written to.</exception>
    public static bool Refresh(string folder, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var stamp = Path.Combine(folder, VersionFile);
        if (File.Exists(stamp) && File.ReadAllText(stamp).Trim() == version)
        {
            return false;
        }

        Directory.CreateDirectory(folder);
        foreach (var file in Write(CityDefinitions.Base))
        {
            File.WriteAllText(Path.Combine(folder, file.Name), file.Json);
        }

        // Last, so a refresh cut short is tried again next time.
        File.WriteAllText(stamp, version + Environment.NewLine);
        return true;
    }

    /// <summary>
    /// The base game's definitions with the spec files in the folder applied. A missing folder
    /// changes nothing; files that can't be read change nothing, and why is in
    /// <paramref name="problem"/>.
    /// </summary>
    public static CityDefinitions Load(string folder, out string? problem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        problem = null;
        IReadOnlyList<SpecFile> files;
        try
        {
            files = SpecFolder.Read(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            problem = $"The base game's files in '{folder}' were skipped: {ex.Message}";
            return CityDefinitions.Base;
        }

        if (!DefinitionLoader.TryParseSpecs(files, out var specs, out var error))
        {
            problem = $"The base game's files in '{folder}' were skipped: {error}";
            return CityDefinitions.Base;
        }

        return CityDefinitions.Base.WithBaseFiles(specs);
    }

    /// <summary>
    /// The definitions as spec files in the current format, one for each kind, every field a mod
    /// may change written out.
    /// </summary>
    internal static IReadOnlyList<SpecFile> Write(CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return
        [
            ListFile("zones", "zones", definitions.Zones.Select(zone => new JsonObject
            {
                ["id"] = zone.Id,
                ["name"] = zone.Name,
                ["price"] = zone.Price,
                ["populations"] = new JsonArray(zone.Populations.Select(population => (JsonNode)population).ToArray()),
                ["houses"] = zone.Houses,
                ["trips"] = new JsonArray(zone.TripsTo.Select(use => (JsonNode)definitions.Zone(use).Id).ToArray()),
            })),
            ListFile("power", "plants", definitions.Plants.Select(plant => new JsonObject
            {
                ["id"] = plant.Id,
                ["name"] = plant.Name,
                ["price"] = plant.Price,
                ["capacity"] = plant.Capacity,
            })),
            ListFile("services", "services", definitions.Services.Select(service => new JsonObject
            {
                ["id"] = service.Id,
                ["name"] = service.Name,
                ["price"] = service.Price,
                ["upkeep"] = service.Upkeep,
                ["strength"] = service.Strength,
                ["reach"] = service.Reach,
            })),
            ListFile("facilities", "facilities", definitions.Facilities.Select(facility => new JsonObject
            {
                ["id"] = facility.Id,
                ["name"] = facility.Name,
                ["price"] = facility.Price,
                ["demandCap"] = facility.DemandCap,
                ["pollution"] = facility.Pollution,
                ["influences"] = new JsonArray(facility.Influences.Select(influence => (JsonNode)new JsonObject
                {
                    ["effect"] = DefinitionLoader.EffectName(influence.Effect),
                    ["strength"] = influence.Strength,
                    ["reach"] = influence.Reach,
                }).ToArray()),
            })),
            ListFile("infrastructure", "infrastructure", DefinitionLoader.InfrastructurePrices(definitions.Infrastructure)
                .Select(pair =>
                {
                    var entry = new JsonObject { ["id"] = pair.Id, ["price"] = pair.Price };
                    foreach (var (id, carries) in DefinitionLoader.PowerCarriers(definitions.Infrastructure).Where(carrier => carrier.Id == pair.Id))
                    {
                        entry["carriesPower"] = carries;
                    }

                    return entry;
                })),
            Document("economy", new JsonObject
            {
                ["startingFunds"] = definitions.Economy.StartingFunds,
                ["taxRate"] = definitions.Economy.TaxRate,
            }),
        ];
    }

    private static SpecFile ListFile(string name, string list, IEnumerable<JsonObject> entries) =>
        Document(name, new JsonObject { [list] = new JsonArray(entries.ToArray<JsonNode>()) });

    private static SpecFile Document(string name, JsonObject fields)
    {
        var document = new JsonObject
        {
            ["$schema"] = FormatSchemas.UrlFor(name, DefinitionLoader.Format),
            ["format"] = DefinitionLoader.Format.ToString(),
        };
        foreach (var (key, value) in fields.ToList())
        {
            fields.Remove(key);
            document[key] = value;
        }

        return new SpecFile($"{name}.json", document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
