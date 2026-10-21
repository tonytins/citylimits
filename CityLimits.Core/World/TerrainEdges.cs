namespace CityLimits.Core.World;

/// <summary>Where water meets land and woods meet clear ground, read straight from the map.</summary>
public static class TerrainEdges
{
    /// <summary>
    /// Tells which sides of a water or tree tile have the same ground beside them. A bridge is
    /// water, the map's edge matches anything, and a tile of any other ground matches nothing.
    /// </summary>
    public static TerrainSides SidesAt(TileMap map, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (!map.Contains(x, y))
        {
            return TerrainSides.None;
        }

        var kind = map.StateAt(x, y).Kind;
        if (kind is not (TileKind.Water or TileKind.Trees))
        {
            return TerrainSides.None;
        }

        var sides = TerrainSides.None;
        if (IsAlike(map, x, y - 1, kind))
        {
            sides |= TerrainSides.North;
        }

        if (IsAlike(map, x + 1, y, kind))
        {
            sides |= TerrainSides.East;
        }

        if (IsAlike(map, x, y + 1, kind))
        {
            sides |= TerrainSides.South;
        }

        if (IsAlike(map, x - 1, y, kind))
        {
            sides |= TerrainSides.West;
        }

        return sides;
    }

    /// <summary>
    /// Tells whether trees with trees on these sides stand in a wood: they have trees on two
    /// sides that meet at a corner, or more. A tree that doesn't is cleared when a map is made.
    /// </summary>
    public static bool IsWood(TerrainSides trees) =>
        HasBoth(trees, TerrainSides.North | TerrainSides.East)
        || HasBoth(trees, TerrainSides.East | TerrainSides.South)
        || HasBoth(trees, TerrainSides.South | TerrainSides.West)
        || HasBoth(trees, TerrainSides.West | TerrainSides.North);

    private static bool HasBoth(TerrainSides sides, TerrainSides pair) => (sides & pair) == pair;

    private static bool IsAlike(TileMap map, int x, int y, TileKind kind) =>
        !map.Contains(x, y) || map.StateAt(x, y).Kind == kind;
}
