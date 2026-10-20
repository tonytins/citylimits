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

    [Fact]
    public void ATileThatTouchesARoadIsServed()
    {
        var map = TileMap.FromTerrain(4, 4, new TileKind[16]);
        map.SetState(2, 1, new TileState(TileKind.Grass, TileUse.Road));

        var network = new RoadNetwork();
        network.Rebuild(map);

        Assert.True(network.IsServed(2, 0));
        Assert.True(network.IsServed(2, 2));
        Assert.True(network.IsServed(1, 1));
        Assert.True(network.IsServed(3, 1));
        Assert.False(network.IsServed(0, 0));
    }

    [Fact]
    public void AZoneThatTouchesNoRoadIsNotServed()
    {
        var map = TileMap.FromTerrain(6, 6, new TileKind[36]);
        map.SetState(0, 0, new TileState(TileKind.Grass, TileUse.Residential));
        map.SetState(5, 5, new TileState(TileKind.Grass, TileUse.Road));

        var network = new RoadNetwork();
        network.Rebuild(map);

        Assert.False(network.IsServed(0, 0));
    }

    [Fact]
    public void AZoneDiagonallyNextToARoadIsNotServed()
    {
        var map = TileMap.FromTerrain(4, 4, new TileKind[16]);
        map.SetState(1, 1, new TileState(TileKind.Grass, TileUse.Road));
        map.SetState(0, 0, new TileState(TileKind.Grass, TileUse.Residential));

        var network = new RoadNetwork();
        network.Rebuild(map);

        Assert.False(network.IsServed(0, 0));
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
}
