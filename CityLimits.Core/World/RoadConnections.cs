namespace CityLimits.Core.World;

/// <summary>The sides of a road tile that join another road.</summary>
[Flags]
public enum RoadConnections
{
    /// <summary>No side joins a road.</summary>
    None = 0,

    /// <summary>The road above joins this one.</summary>
    North = 1,

    /// <summary>The road to the right joins this one.</summary>
    East = 2,

    /// <summary>The road below joins this one.</summary>
    South = 4,

    /// <summary>The road to the left joins this one.</summary>
    West = 8,
}
