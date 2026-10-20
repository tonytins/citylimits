namespace CityLimits.Core.World;

/// <summary>The kind of ground a tile is made of.</summary>
public enum TileKind
{
    /// <summary>Open land that can be built on.</summary>
    Grass,

    /// <summary>Water, which only a bridge can cross.</summary>
    Water,

    /// <summary>Trees, which the tools clear for a little more money.</summary>
    Trees,

    /// <summary>Bare rock, which cannot be built on. Micropolis has no rock; it goes with the new terrain.</summary>
    Rock,

    /// <summary>What a bulldozed zone leaves behind, which the tools clear like trees.</summary>
    Rubble,
}
