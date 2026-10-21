namespace CityLimits.Core.Definitions;

/// <summary>
/// The definitions a city runs on: the base game's, with any mods applied on top. A city keeps
/// the definitions it was made with, so the same definitions, seed, and work give the same city.
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

    internal CityDefinitions(IEnumerable<ZoneSpec> zones, IEnumerable<PlantSpec> plants, IEnumerable<ServiceSpec> services, IReadOnlyList<string> mods)
    {
        _zones = zones.ToDictionary(zone => zone.Use);
        _plants = plants.ToDictionary(plant => plant.Use);
        _services = services.ToDictionary(service => service.Use);
        Mods = mods;
    }

    /// <summary>
    /// The base game's definitions, with no mods. They are written in the same format a mod uses
    /// and read by the same loader; a test keeps them valid, so reading them can't fail in play.
    /// </summary>
    public static CityDefinitions Base => BaseDefinitions.Value;

    /// <summary>The ids of the mods applied, in the order they were applied; empty for the base game.</summary>
    public IReadOnlyList<string> Mods { get; }

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

    /// <summary>What a tool costs before the ground under it is cleared, as the palette shows it.</summary>
    public int CostOf(BuildAction action) => action switch
    {
        BuildAction.Road => TileCosts.Road,
        BuildAction.ZoneResidential => Zone(TileUse.Residential).Price,
        BuildAction.ZoneCommercial => Zone(TileUse.Commercial).Price,
        BuildAction.ZoneIndustrial => Zone(TileUse.Industrial).Price,
        BuildAction.Bulldoze => TileCosts.Bulldoze,
        BuildAction.CoalPowerPlant => Plant(TileUse.CoalPowerPlant).Price,
        BuildAction.NuclearPowerPlant => Plant(TileUse.NuclearPowerPlant).Price,
        BuildAction.PowerLine => TileCosts.PowerLine,
        BuildAction.Park => TileCosts.Park,
        BuildAction.FireStation => Service(TileUse.FireStation).Price,
        BuildAction.PoliceStation => Service(TileUse.PoliceStation).Price,
        _ => 0,
    };

    // A mod's zone, plant, or service entry changes only the fields it sets.
    internal CityDefinitions With(ModDefinitions mod)
    {
        var zones = _zones.Values.Select(zone => mod.Zones.TryGetValue(zone.Use, out var patch) ? patch.ApplyTo(zone) : zone);
        var plants = _plants.Values.Select(plant => mod.Plants.TryGetValue(plant.Use, out var patch) ? patch.ApplyTo(plant) : plant);
        var services = _services.Values.Select(service => mod.Services.TryGetValue(service.Use, out var patch) ? patch.ApplyTo(service) : service);
        return new CityDefinitions(zones, plants, services, [.. Mods, mod.Id]);
    }
}
