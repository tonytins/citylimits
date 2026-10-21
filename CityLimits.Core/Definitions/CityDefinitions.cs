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

    internal CityDefinitions(IEnumerable<ZoneSpec> zones, IReadOnlyList<string> mods)
    {
        _zones = zones.ToDictionary(zone => zone.Use);
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

    /// <summary>What a tool costs before the ground under it is cleared, as the palette shows it.</summary>
    public int CostOf(BuildAction action) => action switch
    {
        BuildAction.Road => TileCosts.Road,
        BuildAction.ZoneResidential => Zone(TileUse.Residential).Price,
        BuildAction.ZoneCommercial => Zone(TileUse.Commercial).Price,
        BuildAction.ZoneIndustrial => Zone(TileUse.Industrial).Price,
        BuildAction.Bulldoze => TileCosts.Bulldoze,
        BuildAction.CoalPowerPlant => TileCosts.CoalPowerPlant,
        BuildAction.NuclearPowerPlant => TileCosts.NuclearPowerPlant,
        _ => 0,
    };

    // A mod's zone entry changes only the fields it sets.
    internal CityDefinitions With(ModDefinitions mod)
    {
        var zones = _zones.Values.Select(zone => mod.Zones.TryGetValue(zone.Use, out var patch) ? patch.ApplyTo(zone) : zone);
        return new CityDefinitions(zones, [.. Mods, mod.Id]);
    }
}
