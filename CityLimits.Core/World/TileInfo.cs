namespace CityLimits.Core.World;

/// <summary>What a kind of ground is called, and what the player can do with it.</summary>
public static class TileInfo
{
    /// <summary>The name of a kind of ground, as the player sees it.</summary>
    public static string Name(TileKind kind) => kind switch
    {
        TileKind.Water => "Water",
        TileKind.Trees => "Trees",
        TileKind.Rubble => "Rubble",
        _ => "Grass",
    };

    /// <summary>What the player can do with a kind of ground.</summary>
    public static string BuildAdvice(TileKind kind) => kind switch
    {
        TileKind.Water => "only a bridge or a power line can cross it",
        TileKind.Trees or TileKind.Rubble => "cleared for $1 a tile when built on",
        _ => "ready to build on",
    };

    /// <summary>The line the readout shows for a kind of ground.</summary>
    public static string Describe(TileKind kind) => $"{Name(kind)} - {BuildAdvice(kind)}";

    /// <summary>The name of a use, as the player sees it.</summary>
    public static string Name(TileUse use) => use switch
    {
        TileUse.Road => "Road",
        TileUse.Residential => "Residential zone",
        TileUse.Commercial => "Commercial zone",
        TileUse.Industrial => "Industrial zone",
        TileUse.CoalPowerPlant => "Coal power plant",
        TileUse.NuclearPowerPlant => "Nuclear power plant",
        TileUse.PowerLine => "Power line",
        TileUse.RoadAcrossPowerLine or TileUse.RoadDownPowerLine => "Road and power line",
        TileUse.FireStation => "Fire station",
        TileUse.PoliceStation => "Police station",
        TileUse.Park => "Park",
        _ => "Empty",
    };

    /// <summary>The name of a use, shortened for a narrow panel.</summary>
    public static string ShortName(TileUse use) => use switch
    {
        TileUse.Road => "road",
        TileUse.Residential => "residential",
        TileUse.Commercial => "commercial",
        TileUse.Industrial => "industrial",
        TileUse.CoalPowerPlant => "coal plant",
        TileUse.NuclearPowerPlant => "nuclear plant",
        TileUse.PowerLine => "power line",
        TileUse.RoadAcrossPowerLine or TileUse.RoadDownPowerLine => "road and line",
        TileUse.FireStation => "fire station",
        TileUse.PoliceStation => "police station",
        TileUse.Park => "park",
        _ => "empty",
    };

    /// <summary>
    /// What the player can do with a tile that carries a use. For a zone, pass the state of its
    /// centre (<see cref="TileMap.ZoneStateAt"/>), which holds the zone's development.
    /// </summary>
    public static string BuildAdvice(TileState state) => state.Use switch
    {
        TileUse.Road when state.IsBridge => "a bridge, carries traffic",
        TileUse.Road => "carries traffic",
        TileUse.Residential or TileUse.Commercial or TileUse.Industrial when state.Development == 0 => "not developed yet",
        TileUse.Residential or TileUse.Commercial or TileUse.Industrial => $"developed to level {state.Development}",
        TileUse.CoalPowerPlant or TileUse.NuclearPowerPlant or TileUse.FireStation or TileUse.PoliceStation => "bulldoze its centre to clear it",
        TileUse.Park => "the bulldozer clears it for $1",
        TileUse.PowerLine when state.Kind == TileKind.Water => "laid under water",
        TileUse.PowerLine => "strung on poles",
        TileUse.RoadAcrossPowerLine or TileUse.RoadDownPowerLine => "carries traffic across the line",
        _ => BuildAdvice(state.Kind),
    };

    /// <summary>The line the readout shows for a tile; a zone is named by its spec in the given definitions.</summary>
    public static string Describe(TileState state, CityDefinitions? definitions = null)
    {
        if (state.Use == TileUse.None)
        {
            return Describe(state.Kind);
        }

        var name = definitions switch
        {
            not null when state.IsZoned => definitions.Zone(state.Use).Name,
            not null when TileRules.IsPowerPlant(state.Use) => definitions.Plant(state.Use).Name,
            not null when TileRules.IsService(state.Use) => definitions.Service(state.Use).Name,
            _ => Name(state.Use),
        };
        return $"{name} - {BuildAdvice(state)}";
    }

    /// <summary>
    /// The line the readout shows for a tile on a map. A zone is described as a whole, from its
    /// centre and its houses: its name, how far it has grown, its population, and whether the
    /// scan last found it without power.
    /// </summary>
    public static string Describe(TileMap map, int x, int y, CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(definitions);

        var state = map.StateAt(x, y);
        if (state.Use == TileUse.Park)
        {
            return Parks.IsWoodedAt(x, y) ? "Park - woods that make the land around them worth more" : "Park - a fountain";
        }

        if (!state.IsZoned)
        {
            return Describe(state, definitions);
        }

        var spec = definitions.Zone(state.Use);
        var (centerX, centerY) = map.ZoneCenterOf(x, y);
        var center = map.StateAt(centerX, centerY);
        var population = spec.PopulationOf(map, centerX, centerY);

        var growth = center.Development switch
        {
            > 0 => $"level {center.Development} of {spec.Levels}, population {population}",
            _ when population > 0 => population == 1 ? "1 house" : $"{population} houses",
            _ => "not developed yet",
        };

        return center.Powered ? $"{spec.Name} - {growth}" : $"{spec.Name} - {growth}, no power";
    }
}
