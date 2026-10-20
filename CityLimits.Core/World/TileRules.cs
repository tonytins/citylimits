namespace CityLimits.Core.World;

/// <summary>What can be done with a kind of ground.</summary>
public static class TileRules
{
    /// <summary>Tells whether a tile can be built on once it has been cleared.</summary>
    public static bool CanBuildOn(TileKind kind) => kind is TileKind.Grass or TileKind.Trees;

    /// <summary>Tells whether a tile has to be cleared before it can be built on.</summary>
    public static bool NeedsClearing(TileKind kind) => kind is TileKind.Trees;

    /// <summary>Tells whether a tile is ground that is already clear.</summary>
    public static bool IsClearGround(TileKind kind) => kind is TileKind.Grass;
}
