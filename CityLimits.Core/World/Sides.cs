namespace CityLimits.Core.World;

/// <summary>The four sides of a tile, as the flags roads and power lines are joined by.</summary>
internal static class Sides
{
    /// <summary>Each side, with the step to the tile beyond it.</summary>
    public static readonly (RoadConnections Side, int Dx, int Dy)[] All =
    [
        (RoadConnections.North, 0, -1),
        (RoadConnections.East, 1, 0),
        (RoadConnections.South, 0, 1),
        (RoadConnections.West, -1, 0),
    ];

    /// <summary>The side facing the other way.</summary>
    public static RoadConnections Opposite(RoadConnections side) => side switch
    {
        RoadConnections.North => RoadConnections.South,
        RoadConnections.East => RoadConnections.West,
        RoadConnections.South => RoadConnections.North,
        _ => RoadConnections.East,
    };
}
