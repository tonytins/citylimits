namespace CityLimits.Core.World;

/// <summary>What can be done with a kind of ground, and with a tile that has been used.</summary>
public static class TileRules
{
    /// <summary>Tells whether a tile can be built on, once a tool has cleared it if it needs to.</summary>
    public static bool CanBuildOn(TileKind kind) => kind is TileKind.Grass or TileKind.Trees or TileKind.Rubble;

    /// <summary>
    /// Tells whether a tool has to clear the ground before it builds there. As in Micropolis,
    /// the tool does it for <see cref="TileCosts.AutoClear"/> a tile.
    /// </summary>
    public static bool NeedsClearing(TileKind kind) => kind is TileKind.Trees or TileKind.Rubble;

    /// <summary>Tells whether a tile is ground that is already clear.</summary>
    public static bool IsClearGround(TileKind kind) => kind is TileKind.Grass;

    /// <summary>Tells whether a use is one of the three zones.</summary>
    public static bool IsZone(TileUse use) => use is TileUse.Residential or TileUse.Commercial or TileUse.Industrial;

    /// <summary>Tells whether a use is a power plant.</summary>
    public static bool IsPowerPlant(TileUse use) => use is TileUse.CoalPowerPlant or TileUse.NuclearPowerPlant;

    /// <summary>Tells whether a road can be laid on the given ground. Water takes a bridge, which has rules of its own.</summary>
    public static bool CanLayRoad(TileKind kind) => CanBuildOn(kind);

    /// <summary>Tells whether a zone's tile can go on the given ground.</summary>
    public static bool CanZone(TileKind kind) => CanBuildOn(kind);

    /// <summary>
    /// Tells whether the bulldozer can clear a tile on its own: a road, a bridge, a power line,
    /// trees, or rubble. A zone or a plant is cleared only through its centre.
    /// </summary>
    public static bool HasSomethingToClear(TileState state) =>
        state.IsRoad || state.HasPowerLine || (state.Use == TileUse.None && NeedsClearing(state.Kind));
}
