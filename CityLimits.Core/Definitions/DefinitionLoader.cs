using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using CityLimits.Core.Scenarios;

namespace CityLimits.Core.Definitions;

/// <summary>
/// Reads definitions files, the base game's and a mod's, and checks every part of them, reporting
/// the first problem with where it is, as in <c>zones[1].price: needs a number from 0 to 1000000, not -5.</c>
/// </summary>
public static class DefinitionLoader
{
    /// <summary>The largest definitions or manifest file read.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    /// <summary>The only definitions format this version reads.</summary>
    public const int Format = 1;

    /// <summary>The most a zone may cost.</summary>
    public const int MaxPrice = 1_000_000;

    /// <summary>The most population one building level may hold.</summary>
    public const int MaxPopulation = 100_000;

    private const int MaxNameLength = 80;
    private const int MaxVersionLength = 32;
    private const int MaxDescriptionLength = 500;

    // The zone ids a definitions file can name, and the use each one is.
    private static readonly Dictionary<string, TileUse> ZoneIds = new(StringComparer.Ordinal)
    {
        ["residential"] = TileUse.Residential,
        ["commercial"] = TileUse.Commercial,
        ["industrial"] = TileUse.Industrial,
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Reads the base game's definitions, which must define every zone and every field.</summary>
    public static bool TryParseBase(string json, [NotNullWhen(true)] out CityDefinitions? definitions, out string error)
    {
        definitions = null;
        if (!TryRead<DefinitionsDocument>(json, out var document, out error))
        {
            return false;
        }

        try
        {
            CheckFormat(document.Format, "format");
            var zones = new Dictionary<TileUse, ZoneSpec>();
            var entries = document.Zones ?? throw new InvalidDefinitions("zones: is required.");
            for (var i = 0; i < entries.Count; i++)
            {
                var at = $"zones[{i}]";
                var entry = entries[i] ?? throw new InvalidDefinitions($"{at}: needs a zone, not null.");
                var use = ZoneUse(entry.Id, at, zones.Keys);
                var name = Name(entry.Name ?? throw new InvalidDefinitions($"{at}.name: is required."), $"{at}.name");
                var price = Price(entry.Price ?? throw new InvalidDefinitions($"{at}.price: is required."), $"{at}.price");
                var populations = Populations(entry.Populations ?? throw new InvalidDefinitions($"{at}.populations: is required."), null, $"{at}.populations");
                var valueClasses = InRange(entry.ValueClasses ?? throw new InvalidDefinitions($"{at}.valueClasses: is required."), 1, TileState.MaxValueClass + 1, $"{at}.valueClasses");
                var houses = Houses(entry.Houses ?? throw new InvalidDefinitions($"{at}.houses: is required."), use, $"{at}.houses");
                zones[use] = new ZoneSpec(entry.Id!, use, name, price, populations, valueClasses, houses);
            }

            var missing = ZoneIds.Where(pair => !zones.ContainsKey(pair.Value)).Select(pair => pair.Key).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidDefinitions($"zones: the base game must define {string.Join(", ", missing)}.");
            }

            definitions = new CityDefinitions(zones.Values, []);
            return true;
        }
        catch (InvalidDefinitions ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Reads a mod: its manifest, which names it, and its definitions, which change only the fields
    /// they set. The manifest's id must match the folder the mod is in.
    /// </summary>
    /// <param name="folderName">The name of the mod's folder, which is its id.</param>
    /// <param name="manifestJson">The text of the mod's <c>mod.json</c>.</param>
    /// <param name="zonesJson">The text of the mod's <c>zones.json</c>, or null when it has none.</param>
    /// <param name="mod">The mod, when it is valid.</param>
    /// <param name="error">What is wrong, and in which file, when it isn't.</param>
    public static bool TryParseMod(string folderName, string manifestJson, string? zonesJson, [NotNullWhen(true)] out ModDefinitions? mod, out string error)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        mod = null;

        if (!TryRead<ManifestDocument>(manifestJson, out var manifest, out error))
        {
            error = $"mod.json: {error}";
            return false;
        }

        var zones = new Dictionary<TileUse, ZonePatch>();
        try
        {
            CheckFormat(manifest.Format, "mod.json: format");
            var id = manifest.Id ?? throw new InvalidDefinitions("mod.json: id: is required.");
            if (!ScenarioCatalog.IsValidId(id) || id != folderName)
            {
                throw new InvalidDefinitions($"mod.json: id: '{id}' must be the mod's folder name, '{folderName}', in lowercase letters, digits, and single hyphens.");
            }

            var name = Name(manifest.Name ?? throw new InvalidDefinitions("mod.json: name: is required."), "mod.json: name");
            var version = manifest.Version?.Trim();
            if (version is { Length: 0 or > MaxVersionLength })
            {
                throw new InvalidDefinitions($"mod.json: version: needs 1 to {MaxVersionLength} characters, not {version.Length}.");
            }

            var description = manifest.Description?.Trim();
            if (description is { Length: > MaxDescriptionLength })
            {
                throw new InvalidDefinitions($"mod.json: description: needs at most {MaxDescriptionLength} characters, not {description.Length}.");
            }

            if (zonesJson is not null)
            {
                if (!TryRead<DefinitionsDocument>(zonesJson, out var document, out error))
                {
                    error = $"zones.json: {error}";
                    return false;
                }

                CheckFormat(document.Format, "zones.json: format");
                var entries = document.Zones ?? [];
                for (var i = 0; i < entries.Count; i++)
                {
                    var at = $"zones.json: zones[{i}]";
                    var entry = entries[i] ?? throw new InvalidDefinitions($"{at}: needs a zone, not null.");
                    var use = ZoneUse(entry.Id, at, zones.Keys);
                    if (entry.ValueClasses is not null)
                    {
                        throw new InvalidDefinitions($"{at}.valueClasses: a mod can't change it, because each class needs its own drawings.");
                    }

                    var levels = CityDefinitions.Base.Zone(use).Levels;
                    zones[use] = new ZonePatch(
                        entry.Name is null ? null : Name(entry.Name, $"{at}.name"),
                        entry.Price is null ? null : Price(entry.Price.Value, $"{at}.price"),
                        entry.Populations is null ? null : Populations(entry.Populations, levels, $"{at}.populations"),
                        entry.Houses is null ? null : Houses(entry.Houses.Value, use, $"{at}.houses"));
                }
            }

            mod = new ModDefinitions(id, name, version, description, zones);
            return true;
        }
        catch (InvalidDefinitions ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryRead<T>(string json, [NotNullWhen(true)] out T? document, out string error)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(json);
        document = null;
        error = string.Empty;

        if (json.Length > MaxFileBytes)
        {
            error = $"the file is {json.Length} characters long; the most is {MaxFileBytes}.";
            return false;
        }

        try
        {
            document = JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            var where = string.IsNullOrEmpty(ex.Path) || ex.Path == "$" ? "the file" : ex.Path.TrimStart('$', '.');
            error = ex.Message.Contains("could not be mapped", StringComparison.Ordinal)
                ? $"{where}: is not a field definitions have."
                : $"{where}: is not valid JSON, or has the wrong type of value.";
            return false;
        }

        if (document is null)
        {
            error = "the file is empty.";
            return false;
        }

        return true;
    }

    private static void CheckFormat(int? format, string at)
    {
        var value = format ?? throw new InvalidDefinitions($"{at}: is required.");
        if (value != Format)
        {
            throw new InvalidDefinitions($"{at}: this version reads format {Format}, not {value}.");
        }
    }

    private static TileUse ZoneUse(string? id, string at, IEnumerable<TileUse> seen)
    {
        if (id is null)
        {
            throw new InvalidDefinitions($"{at}.id: is required.");
        }

        if (!ZoneIds.TryGetValue(id, out var use))
        {
            throw new InvalidDefinitions($"{at}.id: '{id}' is not one of {string.Join(", ", ZoneIds.Keys)}.");
        }

        if (seen.Contains(use))
        {
            throw new InvalidDefinitions($"{at}.id: '{id}' is defined twice.");
        }

        return use;
    }

    private static string Name(string text, string at)
    {
        var name = text.Trim();
        if (name.Length is 0 or > MaxNameLength)
        {
            throw new InvalidDefinitions($"{at}: needs 1 to {MaxNameLength} characters, not {name.Length}.");
        }

        return name;
    }

    // The number of levels is fixed by the drawings: the base game sets it, and a mod must keep it.
    private static IReadOnlyList<int> Populations(List<int> populations, int? levels, string at)
    {
        if (levels is { } required && populations.Count != required)
        {
            throw new InvalidDefinitions($"{at}: needs {required} levels, one for each building drawing, not {populations.Count}.");
        }

        if (populations.Count is 0 or > TileState.MaxDevelopment)
        {
            throw new InvalidDefinitions($"{at}: needs 1 to {TileState.MaxDevelopment} levels, not {populations.Count}.");
        }

        for (var i = 0; i < populations.Count; i++)
        {
            InRange(populations[i], 0, MaxPopulation, $"{at}[{i}]");
        }

        return populations.ToArray();
    }

    // Single houses stand on a zone's outer tiles, and only residential zones have house drawings.
    private static int Houses(int houses, TileUse use, string at)
    {
        InRange(houses, 0, ZoneLayout.Parts - 1, at);
        if (houses > 0 && use != TileUse.Residential)
        {
            throw new InvalidDefinitions($"{at}: only a residential zone grows single houses.");
        }

        return houses;
    }

    private static int InRange(int value, int minimum, int maximum, string at)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidDefinitions($"{at}: needs a number from {minimum} to {maximum}, not {value}.");
        }

        return value;
    }

    private static int Price(int price, string at)
    {
        if (price < 0 || price > MaxPrice)
        {
            throw new InvalidDefinitions($"{at}: needs a number from 0 to {MaxPrice}, not {price}.");
        }

        return price;
    }

    // Reading stops at the first problem. The exception never leaves this class.
    private sealed class InvalidDefinitions(string message) : Exception(message);

    private sealed class DefinitionsDocument
    {
        public int? Format { get; set; }

        public List<ZoneDocument?>? Zones { get; set; }
    }

    private sealed class ZoneDocument
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int? Price { get; set; }

        public List<int>? Populations { get; set; }

        public int? ValueClasses { get; set; }

        public int? Houses { get; set; }
    }

    private sealed class ManifestDocument
    {
        public int? Format { get; set; }

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Version { get; set; }

        public string? Description { get; set; }
    }
}

/// <summary>What a mod changes in one zone's spec; a field left null keeps the value it had.</summary>
public sealed record ZonePatch(string? Name, int? Price, IReadOnlyList<int>? Populations = null, int? Houses = null)
{
    /// <summary>The zone's spec with this mod's changes.</summary>
    public ZoneSpec ApplyTo(ZoneSpec zone) => zone with
    {
        Name = Name ?? zone.Name,
        Price = Price ?? zone.Price,
        Populations = Populations ?? zone.Populations,
        Houses = Houses ?? zone.Houses,
    };
}

/// <summary>A mod, read and checked: who it is, and what it changes.</summary>
/// <param name="Id">The mod's id, which is its folder's name.</param>
/// <param name="Name">The mod's name, as the player sees it.</param>
/// <param name="Version">The mod's own version, as its author writes it, or null.</param>
/// <param name="Description">What the mod does, or null.</param>
/// <param name="Zones">The changes it makes to each zone it names.</param>
public sealed record ModDefinitions(string Id, string Name, string? Version, string? Description, IReadOnlyDictionary<TileUse, ZonePatch> Zones);
