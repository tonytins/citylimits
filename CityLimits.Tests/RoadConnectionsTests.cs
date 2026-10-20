using CityLimits.Core.World;

namespace CityLimits.Tests;

public class RoadConnectionsTests
{
    // Builds a 5 by 5 map from a picture: '#' is a road, anything else is grass.
    private static TileMap MapOf(params string[] rows)
    {
        var map = TileMap.FromTerrain(5, 5, new TileKind[25]);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] == '#')
                {
                    map.SetState(x, y, new TileState(TileKind.Grass, TileUse.Road));
                }
            }
        }

        return map;
    }

    [Fact]
    public void ARoadOnItsOwnJoinsNothing() =>
        Assert.Equal(RoadConnections.None, RoadNetwork.ConnectionsAt(MapOf("", "", "  #"), 2, 2));

    [Fact]
    public void ATileWithNoRoadHasNoConnections() =>
        Assert.Equal(RoadConnections.None, RoadNetwork.ConnectionsAt(MapOf("", "  #", " # #", "  #"), 2, 2));

    [Fact]
    public void AStraightRoadJoinsBothEnds()
    {
        var map = MapOf("", "", " ###");

        Assert.Equal(RoadConnections.East | RoadConnections.West, RoadNetwork.ConnectionsAt(map, 2, 2));
        Assert.Equal(RoadConnections.East, RoadNetwork.ConnectionsAt(map, 1, 2));
        Assert.Equal(RoadConnections.West, RoadNetwork.ConnectionsAt(map, 3, 2));
    }

    [Fact]
    public void ACornerJoinsItsTwoSides() =>
        Assert.Equal(RoadConnections.North | RoadConnections.East, RoadNetwork.ConnectionsAt(MapOf("", "  #", "  ##"), 2, 2));

    [Fact]
    public void ACrossroadsJoinsAllFourSides() =>
        Assert.Equal(
            RoadConnections.North | RoadConnections.East | RoadConnections.South | RoadConnections.West,
            RoadNetwork.ConnectionsAt(MapOf("", "  #", " ###", "  #"), 2, 2));

    [Fact]
    public void ADiagonalRoadDoesNotJoin() =>
        Assert.Equal(RoadConnections.None, RoadNetwork.ConnectionsAt(MapOf("", " #", "  #"), 2, 2));

    [Fact]
    public void TheMapEdgeJoinsNothing() =>
        Assert.Equal(RoadConnections.South, RoadNetwork.ConnectionsAt(MapOf("#", "#"), 0, 0));

    [Fact]
    public void ATileOffTheMapHasNoConnections() =>
        Assert.Equal(RoadConnections.None, RoadNetwork.ConnectionsAt(MapOf("#"), -1, 0));
}
