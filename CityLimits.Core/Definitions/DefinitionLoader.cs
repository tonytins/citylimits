using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CityLimits.Core.Scenarios;

namespace CityLimits.Core.Definitions;

/// <summary>
/// Reads definitions files, the base game's and a mod's, and checks every part of them, reporting
/// the first problem with where it is, as in <c>zones[1].price: needs a number from 0 to 1000000, not -5.</c>
/// A mod, and the base game's own files in <c>common/</c>, are a set of spec files, each named for
/// what it holds: <c>zones.json</c>, <c>power.json</c> (<c>plants.json</c> in format 1),
/// <c>services.json</c>, <c>facilities.json</c>, <c>economy.json</c>, and <c>infrastructure.json</c>;
/// each changes only the fields it sets.
/// </summary>
public static partial class DefinitionLoader
{
    /// <summary>The largest definitions or manifest file read.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    /// <summary>
    /// The newest definitions format this version reads, and the one it writes. 2.0, from 0.5, adds
    /// facilities, the economy, infrastructure, and services' strength and reach, and calls the power
    /// plants' file <c>power.json</c>. A 1.0 file of the three kinds format 1 had reads too; one
    /// written as a whole number, such as <c>1</c>, is 1.0.
    /// </summary>
    public static readonly FormatVersion Format = new(2, 0);

    /// <summary>Format 1's version, which a 1.10 save's definitions are written in for 0.4.</summary>
    public static readonly FormatVersion Format1 = new(1, 0);

    /// <summary>The spec files a mod or the base game may have, by the name each is known by.</summary>
    public static readonly IReadOnlyList<string> SpecFiles = ["zones.json", "power.json", "plants.json", "services.json", "facilities.json", "economy.json", "infrastructure.json"];

    /// <summary>The most a station's strength may be, as a percentage of Micropolis'.</summary>
    public const int MaxStrength = 1000;

    /// <summary>The most times a station's cover may spread.</summary>
    public const int MaxReach = 8;

    /// <summary>The most a zone may cost.</summary>
    public const int MaxPrice = 1_000_000;

    /// <summary>The most population one building level may hold.</summary>
    public const int MaxPopulation = 100_000;

    /// <summary>The most tiles one power plant may power.</summary>
    public const int MaxCapacity = 1_000_000;

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

    // The service ids a definitions file can name, and the use each one is.
    private static readonly Dictionary<string, TileUse> ServiceIds = new(StringComparer.Ordinal)
    {
        ["fire"] = TileUse.FireStation,
        ["police"] = TileUse.PoliceStation,
    };

    // The plant ids a definitions file can name, and the use each one is.
    private static readonly Dictionary<string, TileUse> PlantIds = new(StringComparer.Ordinal)
    {
        ["coal"] = TileUse.CoalPowerPlant,
        ["nuclear"] = TileUse.NuclearPowerPlant,
    };

    // The facility ids a definitions file can name, and the use each one is.
    private static readonly Dictionary<string, TileUse> FacilityIds = new(StringComparer.Ordinal)
    {
        ["stadium"] = TileUse.Stadium,
        ["seaport"] = TileUse.Seaport,
        ["airport"] = TileUse.Airport,
    };

    // The infrastructure an infrastructure file can price, by its id.
    private static readonly string[] InfrastructureIds = ["road", "bridge", "rail", "tunnel", "powerLine", "underwaterPowerLine", "park", "bulldozer", "clearing"];

    /// <summary>The infrastructure's prices by their ids, in the order the ids are listed.</summary>
    internal static IEnumerable<(string Id, int Price)> InfrastructurePrices(InfrastructureSpec prices) =>
        InfrastructureIds.Zip([prices.Road, prices.Bridge, prices.Rail, prices.Tunnel, prices.PowerLine, prices.UnderwaterPowerLine, prices.Park, prices.Bulldozer, prices.Clearing]);

    // The documents' code is generated at build time, so reading them needs no reflection and
    // survives trimming.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = DefinitionJson.Default,
    };

    /// <summary>Reads the base game's definitions, which must define every zone, plant, and service, and every field.</summary>
    public static bool TryParseBase(string json, [NotNullWhen(true)] out CityDefinitions? definitions, out string error)
    {
        definitions = null;
        if (!TryRead<DefinitionsDocument>(json, out var document, out error))
        {
            return false;
        }

        try
        {
            CheckFormat(document.Format, "format", oldest: Format1, newest: Format1);
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
                var trips = Trips(entry.Trips ?? throw new InvalidDefinitions($"{at}.trips: is required."), $"{at}.trips");
                zones[use] = new ZoneSpec(entry.Id!, use, name, price, populations, valueClasses, houses, trips);
            }

            var missing = ZoneIds.Where(pair => !zones.ContainsKey(pair.Value)).Select(pair => pair.Key).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidDefinitions($"zones: the base game must define {string.Join(", ", missing)}.");
            }

            var plants = new Dictionary<TileUse, PlantSpec>();
            var plantEntries = document.Plants ?? throw new InvalidDefinitions("plants: is required.");
            for (var i = 0; i < plantEntries.Count; i++)
            {
                var at = $"plants[{i}]";
                var entry = plantEntries[i] ?? throw new InvalidDefinitions($"{at}: needs a plant, not null.");
                var use = Id(PlantIds, entry.Id, at, plants.Keys);
                var name = Name(entry.Name ?? throw new InvalidDefinitions($"{at}.name: is required."), $"{at}.name");
                var price = Price(entry.Price ?? throw new InvalidDefinitions($"{at}.price: is required."), $"{at}.price");
                var capacity = InRange(entry.Capacity ?? throw new InvalidDefinitions($"{at}.capacity: is required."), 1, MaxCapacity, $"{at}.capacity");
                plants[use] = new PlantSpec(entry.Id!, use, name, price, capacity);
            }

            var missingPlants = PlantIds.Where(pair => !plants.ContainsKey(pair.Value)).Select(pair => pair.Key).ToList();
            if (missingPlants.Count > 0)
            {
                throw new InvalidDefinitions($"plants: the base game must define {string.Join(", ", missingPlants)}.");
            }

            var services = new Dictionary<TileUse, ServiceSpec>();
            var serviceEntries = document.Services ?? throw new InvalidDefinitions("services: is required.");
            for (var i = 0; i < serviceEntries.Count; i++)
            {
                var at = $"services[{i}]";
                var entry = serviceEntries[i] ?? throw new InvalidDefinitions($"{at}: needs a service, not null.");
                var use = Id(ServiceIds, entry.Id, at, services.Keys);
                var name = Name(entry.Name ?? throw new InvalidDefinitions($"{at}.name: is required."), $"{at}.name");
                var price = Price(entry.Price ?? throw new InvalidDefinitions($"{at}.price: is required."), $"{at}.price");
                var upkeep = Price(entry.Upkeep ?? throw new InvalidDefinitions($"{at}.upkeep: is required."), $"{at}.upkeep");
                services[use] = new ServiceSpec(entry.Id!, use, name, price, upkeep);
            }

            var missingServices = ServiceIds.Where(pair => !services.ContainsKey(pair.Value)).Select(pair => pair.Key).ToList();
            if (missingServices.Count > 0)
            {
                throw new InvalidDefinitions($"services: the base game must define {string.Join(", ", missingServices)}.");
            }

            definitions = new CityDefinitions(zones.Values, plants.Values, services.Values, []);
            return true;
        }
        catch (InvalidDefinitions ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Reads a mod of format 1's three files: its manifest, which names it, and its definitions,
    /// which change only the fields they set. The manifest's id must match the folder the mod is in.
    /// </summary>
    /// <param name="folderName">The name of the mod's folder, which is its id.</param>
    /// <param name="manifestJson">The text of the mod's <c>mod.json</c>.</param>
    /// <param name="zonesJson">The text of the mod's <c>zones.json</c>, or null when it has none.</param>
    /// <param name="mod">The mod, when it is valid.</param>
    /// <param name="error">What is wrong, and in which file, when it isn't.</param>
    /// <param name="plantsJson">The text of the mod's <c>plants.json</c>, or null when it has none.</param>
    /// <param name="servicesJson">The text of the mod's <c>services.json</c>, or null when it has none.</param>
    public static bool TryParseMod(string folderName, string manifestJson, string? zonesJson, [NotNullWhen(true)] out ModDefinitions? mod, out string error, string? plantsJson = null, string? servicesJson = null)
    {
        var files = new List<SpecFile>();
        if (zonesJson is not null)
        {
            files.Add(new SpecFile("zones.json", zonesJson));
        }

        if (plantsJson is not null)
        {
            files.Add(new SpecFile("plants.json", plantsJson));
        }

        if (servicesJson is not null)
        {
            files.Add(new SpecFile("services.json", servicesJson));
        }

        return TryParseMod(folderName, manifestJson, files, out mod, out error);
    }

    /// <summary>
    /// Reads a mod: its manifest, which names it, and its spec files, in the order given, each
    /// changing only the fields it sets; a later file's change to the same field wins. The manifest's
    /// id must match the folder the mod is in.
    /// </summary>
    public static bool TryParseMod(string folderName, string manifestJson, IReadOnlyList<SpecFile> files, [NotNullWhen(true)] out ModDefinitions? mod, out string error)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        ArgumentNullException.ThrowIfNull(files);
        mod = null;

        if (!TryRead<ManifestDocument>(manifestJson, out var manifest, out error))
        {
            error = $"mod.json: {error}";
            return false;
        }

        try
        {
            CheckFormat(manifest.Format, "mod.json: format", oldest: Format1, newest: Format);
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

            mod = ReadSpecs(files) with { Id = id, Name = name, Version = version, Description = description };
            return true;
        }
        catch (InvalidDefinitions ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Reads a set of spec files that aren't a mod, such as the base game's own in <c>common/</c>, in
    /// the order given, each changing only the fields it sets.
    /// </summary>
    public static bool TryParseSpecs(IReadOnlyList<SpecFile> files, [NotNullWhen(true)] out ModDefinitions? specs, out string error)
    {
        ArgumentNullException.ThrowIfNull(files);
        specs = null;
        try
        {
            specs = ReadSpecs(files);
            error = string.Empty;
            return true;
        }
        catch (InvalidDefinitions ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // Each file by the kind its name says it is; a later file's change to the same field wins.
    private static ModDefinitions ReadSpecs(IReadOnlyList<SpecFile> files)
    {
        var zones = new Dictionary<TileUse, ZonePatch>();
        var plants = new Dictionary<TileUse, PlantPatch>();
        var services = new Dictionary<TileUse, ServicePatch>();
        var facilities = new Dictionary<TileUse, FacilityPatch>();
        InfrastructurePatch? infrastructure = null;
        EconomyPatch? economy = null;
        foreach (var file in files)
        {
            var kind = System.IO.Path.GetFileName(file.Name).ToLowerInvariant();
            switch (kind)
            {
                case "zones.json":
                    ReadZones(file, zones);
                    break;
                case "power.json":
                case "plants.json":
                    ReadPlants(file, plants, plantsFile: kind == "plants.json");
                    break;
                case "services.json":
                    ReadServices(file, services);
                    break;
                case "facilities.json":
                    ReadFacilities(file, facilities);
                    break;
                case "infrastructure.json":
                    infrastructure = ReadInfrastructure(file, infrastructure);
                    break;
                case "economy.json":
                    economy = ReadEconomy(file, economy);
                    break;
                default:
                    throw new InvalidDefinitions($"{file.Name}: isn't a spec file; spec files are {string.Join(", ", SpecFiles)}.");
            }
        }

        return new ModDefinitions(string.Empty, string.Empty, null, null, zones, plants, services, facilities, infrastructure, economy);
    }

    private static T Document<T>(SpecFile file)
        where T : class
    {
        if (!TryRead<T>(file.Json, out var document, out var error))
        {
            throw new InvalidDefinitions($"{file.Name}: {error}");
        }

        return document;
    }

    private static void ReadZones(SpecFile file, Dictionary<TileUse, ZonePatch> zones)
    {
        var document = Document<ZonesDocument>(file);
        CheckFormat(document.Format, $"{file.Name}: format", oldest: Format1, newest: Format);
        var seen = new HashSet<TileUse>();
        var entries = document.Zones ?? [];
        for (var i = 0; i < entries.Count; i++)
        {
            var at = $"{file.Name}: zones[{i}]";
            var entry = entries[i] ?? throw new InvalidDefinitions($"{at}: needs a zone, not null.");
            var use = ZoneUse(entry.Id, at, seen);
            seen.Add(use);
            if (entry.ValueClasses is not null)
            {
                throw new InvalidDefinitions($"{at}.valueClasses: a mod can't change it, because each class needs its own drawings.");
            }

            var levels = CityDefinitions.Base.Zone(use).Levels;
            var patch = new ZonePatch(
                entry.Name is null ? null : Name(entry.Name, $"{at}.name"),
                entry.Price is null ? null : Price(entry.Price.Value, $"{at}.price"),
                entry.Populations is null ? null : Populations(entry.Populations, levels, $"{at}.populations"),
                entry.Houses is null ? null : Houses(entry.Houses.Value, use, $"{at}.houses"),
                entry.Trips is null ? null : Trips(entry.Trips, $"{at}.trips"));
            zones[use] = zones.TryGetValue(use, out var before) ? before.Then(patch) : patch;
        }
    }

    private static void ReadPlants(SpecFile file, Dictionary<TileUse, PlantPatch> plants, bool plantsFile)
    {
        var document = Document<PlantsDocument>(file);

        // Format 1 called the file plants.json; from 2.0 it is power.json.
        if (document.Format is { } format && (format.Major >= Format.Major) == plantsFile)
        {
            throw new InvalidDefinitions(plantsFile
                ? $"{file.Name}: format {format} names this file power.json; plants.json is format 1.0."
                : $"{file.Name}: format {format} names this file plants.json; power.json is from 2.0.");
        }

        CheckFormat(document.Format, $"{file.Name}: format", oldest: plantsFile ? Format1 : Format, newest: plantsFile ? Format1 : Format);
        var seen = new HashSet<TileUse>();
        var entries = document.Plants ?? [];
        for (var i = 0; i < entries.Count; i++)
        {
            var at = $"{file.Name}: plants[{i}]";
            var entry = entries[i] ?? throw new InvalidDefinitions($"{at}: needs a plant, not null.");
            var use = Id(PlantIds, entry.Id, at, seen);
            seen.Add(use);
            var patch = new PlantPatch(
                entry.Name is null ? null : Name(entry.Name, $"{at}.name"),
                entry.Price is null ? null : Price(entry.Price.Value, $"{at}.price"),
                entry.Capacity is null ? null : InRange(entry.Capacity.Value, 1, MaxCapacity, $"{at}.capacity"));
            plants[use] = plants.TryGetValue(use, out var before) ? before.Then(patch) : patch;
        }
    }

    private static void ReadServices(SpecFile file, Dictionary<TileUse, ServicePatch> services)
    {
        var document = Document<ServicesDocument>(file);
        CheckFormat(document.Format, $"{file.Name}: format", oldest: Format1, newest: Format);
        var seen = new HashSet<TileUse>();
        var entries = document.Services ?? [];
        for (var i = 0; i < entries.Count; i++)
        {
            var at = $"{file.Name}: services[{i}]";
            var entry = entries[i] ?? throw new InvalidDefinitions($"{at}: needs a service, not null.");
            var use = Id(ServiceIds, entry.Id, at, seen);
            seen.Add(use);
            if ((entry.Strength is not null || entry.Reach is not null) && document.Format!.Value.Major < Format.Major)
            {
                throw new InvalidDefinitions($"{at}: strength and reach are from format 2.0; this file is {document.Format}.");
            }

            var patch = new ServicePatch(
                entry.Name is null ? null : Name(entry.Name, $"{at}.name"),
                entry.Price is null ? null : Price(entry.Price.Value, $"{at}.price"),
                entry.Upkeep is null ? null : Price(entry.Upkeep.Value, $"{at}.upkeep"),
                entry.Strength is null ? null : InRange(entry.Strength.Value, 0, MaxStrength, $"{at}.strength"),
                entry.Reach is null ? null : InRange(entry.Reach.Value, 0, MaxReach, $"{at}.reach"));
            services[use] = services.TryGetValue(use, out var before) ? before.Then(patch) : patch;
        }
    }

    private static void ReadFacilities(SpecFile file, Dictionary<TileUse, FacilityPatch> facilities)
    {
        var document = Document<FacilitiesDocument>(file);
        CheckFormat(document.Format, $"{file.Name}: format", oldest: Format, newest: Format);
        var seen = new HashSet<TileUse>();
        var entries = document.Facilities ?? [];
        for (var i = 0; i < entries.Count; i++)
        {
            var at = $"{file.Name}: facilities[{i}]";
            var entry = entries[i] ?? throw new InvalidDefinitions($"{at}: needs a facility, not null.");
            var use = Id(FacilityIds, entry.Id, at, seen);
            seen.Add(use);
            var patch = new FacilityPatch(
                entry.Name is null ? null : Name(entry.Name, $"{at}.name"),
                entry.Price is null ? null : Price(entry.Price.Value, $"{at}.price"),
                entry.DemandCap is null ? null : InRange(entry.DemandCap.Value, 0, MaxPopulation, $"{at}.demandCap"),
                entry.Pollution is null ? null : InRange(entry.Pollution.Value, 0, Simulation.CityMaps.MostPollution, $"{at}.pollution"));
            facilities[use] = facilities.TryGetValue(use, out var before) ? before.Then(patch) : patch;
        }
    }

    private static InfrastructurePatch ReadInfrastructure(SpecFile file, InfrastructurePatch? before)
    {
        var document = Document<InfrastructureDocument>(file);
        CheckFormat(document.Format, $"{file.Name}: format", oldest: Format, newest: Format);
        var prices = new Dictionary<string, int>(StringComparer.Ordinal);
        var entries = document.Infrastructure ?? [];
        for (var i = 0; i < entries.Count; i++)
        {
            var at = $"{file.Name}: infrastructure[{i}]";
            var entry = entries[i] ?? throw new InvalidDefinitions($"{at}: needs an entry, not null.");
            var id = entry.Id ?? throw new InvalidDefinitions($"{at}.id: is required.");
            if (!InfrastructureIds.Contains(id, StringComparer.Ordinal))
            {
                throw new InvalidDefinitions($"{at}.id: '{id}' is not one of {string.Join(", ", InfrastructureIds)}.");
            }

            if (!prices.TryAdd(id, Price(entry.Price ?? throw new InvalidDefinitions($"{at}.price: is required."), $"{at}.price")))
            {
                throw new InvalidDefinitions($"{at}.id: '{id}' is defined twice.");
            }
        }

        int? Of(string id) => prices.TryGetValue(id, out var price) ? price : null;
        var patch = new InfrastructurePatch(Of("road"), Of("bridge"), Of("rail"), Of("tunnel"), Of("powerLine"), Of("underwaterPowerLine"), Of("park"), Of("bulldozer"), Of("clearing"));
        return before?.Then(patch) ?? patch;
    }

    private static EconomyPatch ReadEconomy(SpecFile file, EconomyPatch? before)
    {
        var document = Document<EconomyDocument>(file);
        CheckFormat(document.Format, $"{file.Name}: format", oldest: Format, newest: Format);
        var patch = new EconomyPatch(
            document.StartingFunds is null ? null : InRange(document.StartingFunds.Value, 0, MaxPrice, $"{file.Name}: startingFunds"),
            document.TaxRate is null ? null : InRange(document.TaxRate.Value, 0, Economy.CityBudget.MaxTaxRate, $"{file.Name}: taxRate"));
        return before?.Then(patch) ?? patch;
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
            document = JsonSerializer.Deserialize(json, (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T)));
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

    // A file's version must be one of the majors from oldest to newest that this version reads.
    private static void CheckFormat(FormatVersion? format, string at, FormatVersion oldest, FormatVersion newest)
    {
        var value = format ?? throw new InvalidDefinitions($"{at}: is required.");
        var reader = value.Major == oldest.Major ? oldest : newest;
        if (value.Major < oldest.Major || value.ProblemReadingWith(reader) is { } problem)
        {
            var reads = oldest.Major == newest.Major ? $"up to {newest}" : $"{oldest} and up to {newest}";
            throw new InvalidDefinitions($"{at}: this is format {value}; this version of CityLimits reads {reads}.");
        }
    }

    /// <summary>The documents a definitions file can be, by the name its schema goes by, for the schemas.</summary>
    internal static IEnumerable<(string Name, JsonTypeInfo Info)> SchemaDocuments() =>
    [
        ("mod", Options.GetTypeInfo(typeof(ManifestDocument))),
        ("zones", Options.GetTypeInfo(typeof(ZonesDocument))),
        ("power", Options.GetTypeInfo(typeof(PlantsDocument))),
        ("services", Options.GetTypeInfo(typeof(ServicesDocument))),
        ("facilities", Options.GetTypeInfo(typeof(FacilitiesDocument))),
        ("infrastructure", Options.GetTypeInfo(typeof(InfrastructureDocument))),
        ("economy", Options.GetTypeInfo(typeof(EconomyDocument))),
    ];

    private static TileUse ZoneUse(string? id, string at, IEnumerable<TileUse> seen) => Id(ZoneIds, id, at, seen);

    private static TileUse Id(Dictionary<string, TileUse> ids, string? id, string at, IEnumerable<TileUse> seen)
    {
        if (id is null)
        {
            throw new InvalidDefinitions($"{at}.id: is required.");
        }

        if (!ids.TryGetValue(id, out var use))
        {
            throw new InvalidDefinitions($"{at}.id: '{id}' is not one of {string.Join(", ", ids.Keys)}.");
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

    // A trip has to be looking for something: one to three different kinds of zone.
    private static IReadOnlyList<TileUse> Trips(List<string?> trips, string at)
    {
        if (trips.Count == 0)
        {
            throw new InvalidDefinitions($"{at}: needs at least one kind of zone to go to.");
        }

        var uses = new List<TileUse>();
        for (var i = 0; i < trips.Count; i++)
        {
            var id = trips[i];
            if (id is null || !ZoneIds.TryGetValue(id, out var use))
            {
                throw new InvalidDefinitions($"{at}[{i}]: '{id}' is not one of {string.Join(", ", ZoneIds.Keys)}.");
            }

            if (uses.Contains(use))
            {
                throw new InvalidDefinitions($"{at}[{i}]: '{id}' is listed twice.");
            }

            uses.Add(use);
        }

        return uses;
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

    [JsonSerializable(typeof(DefinitionsDocument))]
    [JsonSerializable(typeof(ManifestDocument))]
    [JsonSerializable(typeof(ZonesDocument))]
    [JsonSerializable(typeof(PlantsDocument))]
    [JsonSerializable(typeof(ServicesDocument))]
    [JsonSerializable(typeof(FacilitiesDocument))]
    [JsonSerializable(typeof(InfrastructureDocument))]
    [JsonSerializable(typeof(EconomyDocument))]
    private sealed partial class DefinitionJson : JsonSerializerContext;

    // Reading stops at the first problem. The exception never leaves this class.
    private sealed class InvalidDefinitions(string message) : Exception(message);

    // The base game's definitions hold both lists; a mod keeps each in its own file.
    private sealed class DefinitionsDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public List<ZoneDocument?>? Zones { get; set; }

        public List<PlantDocument?>? Plants { get; set; }

        public List<ServiceDocument?>? Services { get; set; }
    }

    private sealed class ServicesDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public List<ServiceDocument?>? Services { get; set; }
    }

    private sealed class ServiceDocument
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int? Price { get; set; }

        public int? Upkeep { get; set; }

        public int? Strength { get; set; }

        public int? Reach { get; set; }
    }

    private sealed class FacilitiesDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public List<FacilityDocument?>? Facilities { get; set; }
    }

    private sealed class FacilityDocument
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int? Price { get; set; }

        public int? DemandCap { get; set; }

        public int? Pollution { get; set; }
    }

    private sealed class InfrastructureDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public List<PriceDocument?>? Infrastructure { get; set; }
    }

    private sealed class PriceDocument
    {
        public string? Id { get; set; }

        public int? Price { get; set; }
    }

    private sealed class EconomyDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public int? StartingFunds { get; set; }

        public int? TaxRate { get; set; }
    }

    private sealed class ZonesDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public List<ZoneDocument?>? Zones { get; set; }
    }

    private sealed class PlantsDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public List<PlantDocument?>? Plants { get; set; }
    }

    private sealed class PlantDocument
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int? Price { get; set; }

        public int? Capacity { get; set; }
    }

    private sealed class ZoneDocument
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int? Price { get; set; }

        public List<int>? Populations { get; set; }

        public int? ValueClasses { get; set; }

        public int? Houses { get; set; }

        public List<string?>? Trips { get; set; }
    }

    private sealed class ManifestDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public FormatVersion? Format { get; set; }

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Version { get; set; }

        public string? Description { get; set; }
    }
}

/// <summary>A spec file, by the name it is known by (a path, for a file in a folder), and its text.</summary>
/// <param name="Name">Its file name, which says what it holds, or its path within a mod or <c>common/</c>.</param>
/// <param name="Json">Its text.</param>
public sealed record SpecFile(string Name, string Json);

/// <summary>What a mod changes in one zone's spec; a field left null keeps the value it had.</summary>
public sealed record ZonePatch(string? Name, int? Price, IReadOnlyList<int>? Populations = null, int? Houses = null, IReadOnlyList<TileUse>? TripsTo = null)
{
    /// <summary>This change, then another on top of it.</summary>
    public ZonePatch Then(ZonePatch later) => new(later.Name ?? Name, later.Price ?? Price, later.Populations ?? Populations, later.Houses ?? Houses, later.TripsTo ?? TripsTo);

    /// <summary>The zone's spec with this mod's changes.</summary>
    public ZoneSpec ApplyTo(ZoneSpec zone) => zone with
    {
        Name = Name ?? zone.Name,
        Price = Price ?? zone.Price,
        Populations = Populations ?? zone.Populations,
        Houses = Houses ?? zone.Houses,
        TripsTo = TripsTo ?? zone.TripsTo,
    };
}

/// <summary>What a mod changes in one power plant's spec; a field left null keeps the value it had.</summary>
public sealed record PlantPatch(string? Name, int? Price, int? Capacity)
{
    /// <summary>This change, then another on top of it.</summary>
    public PlantPatch Then(PlantPatch later) => new(later.Name ?? Name, later.Price ?? Price, later.Capacity ?? Capacity);

    /// <summary>The plant's spec with this mod's changes.</summary>
    public PlantSpec ApplyTo(PlantSpec plant) => plant with
    {
        Name = Name ?? plant.Name,
        Price = Price ?? plant.Price,
        Capacity = Capacity ?? plant.Capacity,
    };
}

/// <summary>What a mod changes in one service's spec; a field left null keeps the value it had.</summary>
public sealed record ServicePatch(string? Name, int? Price, int? Upkeep, int? Strength = null, int? Reach = null)
{
    /// <summary>This change, then another on top of it.</summary>
    public ServicePatch Then(ServicePatch later) => new(later.Name ?? Name, later.Price ?? Price, later.Upkeep ?? Upkeep, later.Strength ?? Strength, later.Reach ?? Reach);

    /// <summary>The service's spec with this mod's changes.</summary>
    public ServiceSpec ApplyTo(ServiceSpec service) => service with
    {
        Name = Name ?? service.Name,
        Price = Price ?? service.Price,
        Upkeep = Upkeep ?? service.Upkeep,
        Strength = Strength ?? service.Strength,
        Reach = Reach ?? service.Reach,
    };
}

/// <summary>What a mod changes in one facility's spec; a field left null keeps the value it had.</summary>
public sealed record FacilityPatch(string? Name, int? Price, int? DemandCap, int? Pollution)
{
    /// <summary>This change, then another on top of it.</summary>
    public FacilityPatch Then(FacilityPatch later) => new(later.Name ?? Name, later.Price ?? Price, later.DemandCap ?? DemandCap, later.Pollution ?? Pollution);

    /// <summary>The facility's spec with this mod's changes.</summary>
    public FacilitySpec ApplyTo(FacilitySpec facility) => facility with
    {
        Name = Name ?? facility.Name,
        Price = Price ?? facility.Price,
        DemandCap = DemandCap ?? facility.DemandCap,
        Pollution = Pollution ?? facility.Pollution,
    };
}

/// <summary>What a mod changes in the infrastructure's prices; a price left null keeps the one it had.</summary>
public sealed record InfrastructurePatch(int? Road, int? Bridge, int? Rail, int? Tunnel, int? PowerLine, int? UnderwaterPowerLine, int? Park, int? Bulldozer, int? Clearing)
{
    /// <summary>This change, then another on top of it.</summary>
    public InfrastructurePatch Then(InfrastructurePatch later) => new(
        later.Road ?? Road, later.Bridge ?? Bridge, later.Rail ?? Rail, later.Tunnel ?? Tunnel, later.PowerLine ?? PowerLine,
        later.UnderwaterPowerLine ?? UnderwaterPowerLine, later.Park ?? Park, later.Bulldozer ?? Bulldozer, later.Clearing ?? Clearing);

    /// <summary>The prices with this mod's changes.</summary>
    public InfrastructureSpec ApplyTo(InfrastructureSpec prices) => new(
        Road ?? prices.Road, Bridge ?? prices.Bridge, Rail ?? prices.Rail, Tunnel ?? prices.Tunnel, PowerLine ?? prices.PowerLine,
        UnderwaterPowerLine ?? prices.UnderwaterPowerLine, Park ?? prices.Park, Bulldozer ?? prices.Bulldozer, Clearing ?? prices.Clearing);
}

/// <summary>What a mod changes in what a new city starts with; a field left null keeps the value it had.</summary>
public sealed record EconomyPatch(int? StartingFunds, int? TaxRate)
{
    /// <summary>This change, then another on top of it.</summary>
    public EconomyPatch Then(EconomyPatch later) => new(later.StartingFunds ?? StartingFunds, later.TaxRate ?? TaxRate);

    /// <summary>The economy with this mod's changes.</summary>
    public EconomySpec ApplyTo(EconomySpec economy) => new(StartingFunds ?? economy.StartingFunds, TaxRate ?? economy.TaxRate);
}

/// <summary>A mod, read and checked: who it is, and what it changes.</summary>
/// <param name="Id">The mod's id, which is its folder's name.</param>
/// <param name="Name">The mod's name, as the player sees it.</param>
/// <param name="Version">The mod's own version, as its author writes it, or null.</param>
/// <param name="Description">What the mod does, or null.</param>
/// <param name="Zones">The changes it makes to each zone it names.</param>
/// <param name="Plants">The changes it makes to each power plant it names.</param>
/// <param name="Services">The changes it makes to each service it names.</param>
/// <param name="Facilities">The changes it makes to each stadium, seaport, or airport it names.</param>
/// <param name="Infrastructure">The changes it makes to the infrastructure's prices, or null for none.</param>
/// <param name="Economy">The changes it makes to what a new city starts with, or null for none.</param>
public sealed record ModDefinitions(
    string Id,
    string Name,
    string? Version,
    string? Description,
    IReadOnlyDictionary<TileUse, ZonePatch> Zones,
    IReadOnlyDictionary<TileUse, PlantPatch> Plants,
    IReadOnlyDictionary<TileUse, ServicePatch> Services,
    IReadOnlyDictionary<TileUse, FacilityPatch>? Facilities = null,
    InfrastructurePatch? Infrastructure = null,
    EconomyPatch? Economy = null)
{
    /// <summary>The changes it makes to each stadium, seaport, or airport it names.</summary>
    public IReadOnlyDictionary<TileUse, FacilityPatch> Facilities { get; init; } = Facilities ?? new Dictionary<TileUse, FacilityPatch>();
}
