namespace CityLimits.Core.World;

/// <summary>What can be done with a kind of ground, and with a tile that has been used.</summary>
public static class TileRules
{
    /// <summary>Tells whether a tile can be built on once it has been cleared.</summary>
    public static bool CanBuildOn(TileKind kind) => kind is TileKind.Grass or TileKind.Trees;

    /// <summary>Tells whether a tile has to be cleared before it can be built on.</summary>
    public static bool NeedsClearing(TileKind kind) => kind is TileKind.Trees;

    /// <summary>Tells whether a tile is ground that is already clear.</summary>
    public static bool IsClearGround(TileKind kind) => kind is TileKind.Grass;

    /// <summary>Tells whether a use is one of the three zones.</summary>
    public static bool IsZone(TileUse use) => use is TileUse.Residential or TileUse.Commercial or TileUse.Industrial;

    /// <summary>Tells whether a road can be laid on the given ground.</summary>
    public static bool CanLayRoad(TileKind kind) => CanBuildOn(kind);

    /// <summary>Tells whether a zone can be painted on the given ground.</summary>
    public static bool CanZone(TileKind kind) => CanBuildOn(kind);

    /// <summary>Tells whether a tile holds something the bulldozer can clear.</summary>
    public static bool HasSomethingToClear(TileState state) =>
        state.Use != TileUse.None || state.Kind == TileKind.Trees;
}
