namespace CityLimits.Core.World;

/// <summary>What a kind of ground is called, and what the player can do with it.</summary>
public static class TileInfo
{
    /// <summary>The name of a kind of ground, as the player sees it.</summary>
    public static string Name(TileKind kind) => kind switch
    {
        TileKind.Water => "Water",
        TileKind.Trees => "Trees",
        TileKind.Rock => "Rock",
        TileKind.Rubble => "Rubble",
        _ => "Grass",
    };

    /// <summary>What the player can do with a kind of ground.</summary>
    public static string BuildAdvice(TileKind kind) => kind switch
    {
        TileKind.Water => "only a bridge can cross it",
        TileKind.Rock => "cannot be built on",
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
        _ => "Empty",
    };

    /// <summary>The name of a use, shortened for a narrow panel.</summary>
    public static string ShortName(TileUse use) => use switch
    {
        TileUse.Road => "road",
        TileUse.Residential => "residential",
        TileUse.Commercial => "commercial",
        TileUse.Industrial => "industrial",
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
        _ => BuildAdvice(state.Kind),
    };

    /// <summary>The line the readout shows for a tile.</summary>
    public static string Describe(TileState state)
    {
        if (state.Use == TileUse.None)
        {
            return Describe(state.Kind);
        }

        return $"{Name(state.Use)} - {BuildAdvice(state)}";
    }
}
