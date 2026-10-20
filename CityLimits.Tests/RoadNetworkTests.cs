using CityLimits.Core.World;

namespace CityLimits.Tests;

public class RoadNetworkTests
{
    [Fact]
    public void AnEmptyMapHasNoRoads()
    {
        var network = new RoadNetwork();

        network.Rebuild(TileMap.FromTerrain(4, 4, new TileKind[16]));

        Assert.Equal(0, network.RoadCount);
        Assert.False(network.HasRoad(1, 1));
    }

    [Fact]
    public void TheNetworkFindsTheRoadsOnTheMap()
    {
        var map = TileMap.FromTerrain(4, 4, new TileKind[16]);
        map.SetState(2, 1, new TileState(TileKind.Grass, TileUse.Road));

        var network = new RoadNetwork();
        network.Rebuild(map);

        Assert.Equal(1, network.RoadCount);
        Assert.True(network.HasRoad(2, 1));
        Assert.False(network.HasRoad(1, 1));
    }

    [Theory]
    [InlineData(4, 2)] // above the zone
    [InlineData(6, 5)] // to its right
    [InlineData(3, 6)] // below it
    [InlineData(2, 4)] // to its left
    public void AZoneWithARoadAlongItsEdgeIsServed(int roadX, int roadY)
    {
        var network = NetworkWithRoadAt(roadX, roadY);

        Assert.True(network.IsZoneServed(4, 4));
    }

    [Theory]
    [InlineData(2, 2)] // beyond the top-left corner
    [InlineData(6, 6)] // beyond the bottom-right corner
    [InlineData(4, 1)] // two tiles away
    [InlineData(4, 4)] // under the zone itself
    public void AZoneWithNoRoadAlongItsEdgeIsNotServed(int roadX, int roadY)
    {
        var network = NetworkWithRoadAt(roadX, roadY);

        Assert.False(network.IsZoneServed(4, 4));
    }

    [Fact]
    public void AZoneAtTheMapEdgeIsServedFromInside()
    {
        var network = NetworkWithRoadAt(3, 2);

        Assert.True(network.IsZoneServed(1, 1));
        Assert.False(network.IsZoneServed(0, 0));
    }

    [Fact]
    public void TheNetworkIsOnlyRebuiltWhenItIsOutOfDate()
    {
        var map = TileMap.FromTerrain(4, 4, new TileKind[16]);
        var network = new RoadNetwork();

        network.EnsureCurrent(map);
        Assert.False(network.IsDirty);
        Assert.Equal(0, network.RoadCount);

        map.SetState(0, 0, new TileState(TileKind.Grass, TileUse.Road));
        network.EnsureCurrent(map);
        Assert.Equal(0, network.RoadCount);

        network.MarkDirty();
        network.EnsureCurrent(map);
        Assert.Equal(1, network.RoadCount);
    }

    private static RoadNetwork NetworkWithRoadAt(int x, int y)
    {
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        map.SetState(x, y, new TileState(TileKind.Grass, TileUse.Road));
        var network = new RoadNetwork();
        network.Rebuild(map);
        return network;
    }
}
