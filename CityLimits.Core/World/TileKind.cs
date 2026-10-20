namespace CityLimits.Core.World;

/// <summary>The kind of ground a tile is made of.</summary>
public enum TileKind
{
    /// <summary>Open land that can be built on.</summary>
    Grass,

    /// <summary>Water, which cannot be built on.</summary>
    Water,

    /// <summary>Trees, which have to be cleared before building.</summary>
    Trees,

    /// <summary>Bare rock, which cannot be built on.</summary>
    Rock,
}
