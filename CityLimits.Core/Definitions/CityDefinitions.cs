namespace CityLimits.Core.Definitions;

/// <summary>
/// The definitions a city runs on: the base game's, with any mods applied on top: its zones, power
/// plants, services, facilities, infrastructure, economy, and ordinances. The same definitions, seed, and work
/// give the same city.
/// </summary>
public sealed class CityDefinitions
{
    private static readonly Lazy<CityDefinitions> BaseDefinitions = new(() =>
        DefinitionLoader.TryParseBase(BaseGame.Json, out var definitions, out var error)
            ? definitions
            : throw new InvalidOperationException($"The base game's definitions are not valid: {error}"));

    private readonly Dictionary<TileUse, ZoneSpec> _zones;
    private readonly Dictionary<TileUse, PlantSpec> _plants;
    private readonly Dictionary<TileUse, ServiceSpec> _services;
    private readonly Dictionary<TileUse, FacilitySpec> _facilities;
    private readonly OrdinanceSpec[] _ordinances;

    // Zones, plants, and services from a definitions file; the facilities, infrastructure, and
    // economy the base game has, until a file gives others.
    internal CityDefinitions(IEnumerable<ZoneSpec> zones, IEnumerable<PlantSpec> plants, IEnumerable<ServiceSpec> services, IReadOnlyList<AppliedMod> mods)
        : this(zones, plants, services, BaseGame.Facilities, BaseGame.Infrastructure, BaseGame.Economy, mods)
    {
    }

    internal CityDefinitions(
        IEnumerable<ZoneSpec> zones,
        IEnumerable<PlantSpec> plants,
        IEnumerable<ServiceSpec> services,
        IEnumerable<FacilitySpec> facilities,
        InfrastructureSpec infrastructure,
        EconomySpec economy,
        IReadOnlyList<AppliedMod> mods,
        IEnumerable<OrdinanceSpec>? ordinances = null)
    {
        _ordinances = [.. ordinances ?? BaseGame.Ordinances];
        _zones = zones.ToDictionary(zone => zone.Use);
        _plants = plants.ToDictionary(plant => plant.Use);
        _services = services.ToDictionary(service => service.Use);
        _facilities = facilities.ToDictionary(facility => facility.Use);
        Infrastructure = infrastructure;
        Economy = economy;
        AppliedMods = mods;
        Mods = [.. mods.Select(mod => mod.Id)];
    }

    /// <summary>
    /// The base game's definitions, with no mods. They are written in the same format a mod uses
    /// and read by the same loader; a test keeps them valid, so reading them can't fail in play.
    /// </summary>
    public static CityDefinitions Base => BaseDefinitions.Value;

    /// <summary>What roads, rail, power lines, parks, and the bulldozer cost.</summary>
    public InfrastructureSpec Infrastructure { get; }

    /// <summary>What a new city starts with.</summary>
    public EconomySpec Economy { get; }

    /// <summary>The ids of the mods applied, in the order they were applied; empty for the base game.</summary>
    public IReadOnlyList<string> Mods { get; }

    /// <summary>The mods applied, with their names and versions, in the order they were applied.</summary>
    public IReadOnlyList<AppliedMod> AppliedMods { get; }

    /// <summary>The spec of the zone a use belongs to.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The use isn't a zone.</exception>
    public ZoneSpec Zone(TileUse use) =>
        _zones.TryGetValue(use, out var zone) ? zone : throw new ArgumentOutOfRangeException(nameof(use), $"{use} is not a zone.");

    /// <summary>The spec of the power plant a use belongs to.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The use isn't a power plant.</exception>
    public PlantSpec Plant(TileUse use) =>
        _plants.TryGetValue(use, out var plant) ? plant : throw new ArgumentOutOfRangeException(nameof(use), $"{use} is not a power plant.");

    /// <summary>The spec of the service building a use belongs to.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The use isn't a service building.</exception>
    public ServiceSpec Service(TileUse use) =>
        _services.TryGetValue(use, out var service) ? service : throw new ArgumentOutOfRangeException(nameof(use), $"{use} is not a service building.");

    /// <summary>The spec of the stadium, seaport, or airport a use belongs to.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The use isn't a stadium, a seaport, or an airport.</exception>
    public FacilitySpec Facility(TileUse use) =>
        _facilities.TryGetValue(use, out var facility) ? facility : throw new ArgumentOutOfRangeException(nameof(use), $"{use} is not a stadium, a seaport, or an airport.");

    /// <summary>The ordinances, in the order the player is shown them.</summary>
    public IReadOnlyList<OrdinanceSpec> Ordinances => _ordinances;

    /// <summary>The ordinance with an id, or null when there is none.</summary>
    public OrdinanceSpec? Ordinance(string id) => Array.Find(_ordinances, ordinance => ordinance.Id == id);

    /// <summary>What a tool costs before the ground under it is cleared, as the palette shows it.</summary>
    public int CostOf(BuildAction action) => action switch
    {
        BuildAction.Road => Infrastructure.Road,
        BuildAction.ZoneResidential => Zone(TileUse.Residential).Price,
        BuildAction.ZoneCommercial => Zone(TileUse.Commercial).Price,
        BuildAction.ZoneIndustrial => Zone(TileUse.Industrial).Price,
        BuildAction.Bulldoze => Infrastructure.Bulldozer,
        BuildAction.CoalPowerPlant => Plant(TileUse.CoalPowerPlant).Price,
        BuildAction.NuclearPowerPlant => Plant(TileUse.NuclearPowerPlant).Price,
        BuildAction.PowerLine => Infrastructure.PowerLine,
        BuildAction.Park => Infrastructure.Park,
        BuildAction.Rail => Infrastructure.Rail,
        BuildAction.FireStation => Service(TileUse.FireStation).Price,
        BuildAction.PoliceStation => Service(TileUse.PoliceStation).Price,
        BuildAction.Stadium => Facility(TileUse.Stadium).Price,
        BuildAction.Seaport => Facility(TileUse.Seaport).Price,
        BuildAction.Airport => Facility(TileUse.Airport).Price,
        _ => 0,
    };

    /// <summary>The zone specs, in the order of their uses.</summary>
    internal IEnumerable<ZoneSpec> Zones => _zones.Values.OrderBy(zone => zone.Use);

    /// <summary>The plant specs, in the order of their uses.</summary>
    internal IEnumerable<PlantSpec> Plants => _plants.Values.OrderBy(plant => plant.Use);

    /// <summary>The service specs, in the order of their uses.</summary>
    internal IEnumerable<ServiceSpec> Services => _services.Values.OrderBy(service => service.Use);

    /// <summary>The facility specs, in the order of their uses.</summary>
    internal IEnumerable<FacilitySpec> Facilities => _facilities.Values.OrderBy(facility => facility.Use);

    // A saved city's definitions are read as a whole and then told which mods made them.
    internal CityDefinitions WithAppliedMods(IReadOnlyList<AppliedMod> mods) => new(_zones.Values, _plants.Values, _services.Values, _facilities.Values, Infrastructure, Economy, mods, _ordinances);

    // A mod's entries change only the fields they set, and the mod is listed as applied.
    internal CityDefinitions With(ModDefinitions mod) => Patched(mod, [.. AppliedMods, new AppliedMod(mod.Id, mod.Name, mod.Version)]);

    // The base game's own files change the code's values as a mod would, but aren't a mod.
    internal CityDefinitions WithBaseFiles(ModDefinitions files) => Patched(files, AppliedMods);

    private CityDefinitions Patched(ModDefinitions mod, IReadOnlyList<AppliedMod> mods)
    {
        var zones = _zones.Values.Select(zone => mod.Zones.TryGetValue(zone.Use, out var patch) ? patch.ApplyTo(zone) : zone);
        var plants = _plants.Values.Select(plant => mod.Plants.TryGetValue(plant.Use, out var patch) ? patch.ApplyTo(plant) : plant);
        var services = _services.Values.Select(service => mod.Services.TryGetValue(service.Use, out var patch) ? patch.ApplyTo(service) : service);
        var facilities = _facilities.Values.Select(facility => mod.Facilities.TryGetValue(facility.Use, out var patch) ? patch.ApplyTo(facility) : facility);
        var infrastructure = mod.Infrastructure?.ApplyTo(Infrastructure) ?? Infrastructure;
        var economy = mod.Economy?.ApplyTo(Economy) ?? Economy;
        var ordinances = _ordinances.Select(ordinance => mod.Ordinances.TryGetValue(ordinance.Id, out var patch) ? patch.ApplyTo(ordinance) : ordinance);
        return new CityDefinitions(zones, plants, services, facilities, infrastructure, economy, mods, ordinances);
    }
}

/// <summary>A mod a city's definitions were made with, as its <c>mod.json</c> names it.</summary>
/// <param name="Id">The mod's id, which orders the mods.</param>
/// <param name="Name">The mod's name, for players.</param>
/// <param name="Version">The mod's own version, as its author writes it, or null.</param>
public sealed record AppliedMod(string Id, string Name, string? Version);
