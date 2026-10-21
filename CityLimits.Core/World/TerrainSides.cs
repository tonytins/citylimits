namespace CityLimits.Core.World;

/// <summary>The sides of a tile whose neighbour is the same kind of ground, which decide how its edge is drawn.</summary>
[Flags]
public enum TerrainSides
{
    /// <summary>No side matches.</summary>
    None = 0,

    /// <summary>The tile above is the same ground.</summary>
    North = 1,

    /// <summary>The tile to the right is the same ground.</summary>
    East = 2,

    /// <summary>The tile below is the same ground.</summary>
    South = 4,

    /// <summary>The tile to the left is the same ground.</summary>
    West = 8,

    /// <summary>Every side matches.</summary>
    All = North | East | South | West,
}
