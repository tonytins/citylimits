namespace CityLimits.Core.World;

/// <summary>
/// Which sides of a tile rail runs toward, as in Micropolis: every side of track or a tunnel,
/// and only along its rail where it crosses a road or a power line.
/// </summary>
public static class Rails
{
    /// <summary>Tells whether rail leaves a tile through one of its sides.</summary>
    public static bool RunsToward(TileState state, RoadConnections side) => state.Use switch
    {
        TileUse.Rail => true,
        TileUse.RailAcrossRoad or TileUse.RailAcrossPowerLine => side is RoadConnections.East or RoadConnections.West,
        TileUse.RailDownRoad or TileUse.RailDownPowerLine => side is RoadConnections.North or RoadConnections.South,
        _ => false,
    };

    /// <summary>
    /// Tells which sides of a rail tile join other rail, read straight from the map. A tile with
    /// no rail has none, and the map's edge joins nothing.
    /// </summary>
    public static RoadConnections ConnectionsAt(TileMap map, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(map);

        var state = map.StateAtOrDefault(x, y);
        if (!state.IsRail)
        {
            return RoadConnections.None;
        }

        var connections = RoadConnections.None;
        foreach (var (side, dx, dy) in Sides.All)
        {
            if (RunsToward(state, side) && RunsToward(map.StateAtOrDefault(x + dx, y + dy), Sides.Opposite(side)))
            {
                connections |= side;
            }
        }

        return connections;
    }
}
