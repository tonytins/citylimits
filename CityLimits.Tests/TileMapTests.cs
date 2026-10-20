using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TileMapTests
{
    [Fact]
    public void TheMapKeepsTheTilesItWasGiven()
    {
        var tiles = new[] { TileKind.Grass, TileKind.Water, TileKind.Trees, TileKind.Rock };
        var map = TileMap.FromTerrain(2, 2, tiles);

        Assert.Equal(2, map.Width);
        Assert.Equal(2, map.Height);
        Assert.Equal(TileKind.Grass, map[0, 0]);
        Assert.Equal(TileKind.Water, map[1, 0]);
        Assert.Equal(TileKind.Trees, map[0, 1]);
        Assert.Equal(TileKind.Rock, map[1, 1]);
    }

    [Fact]
    public void TheMapCopiesItsTiles()
    {
        var tiles = new[] { TileKind.Grass };
        var map = TileMap.FromTerrain(1, 1, tiles);

        tiles[0] = TileKind.Rock;

        Assert.Equal(TileKind.Grass, map[0, 0]);
    }

    [Fact]
    public void TheMapRejectsASizeThatDoesNotFit()
    {
        Assert.Throws<ArgumentException>(() => TileMap.FromTerrain(2, 2, new[] { TileKind.Grass }));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.FromTerrain(0, 2, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.FromTerrain(2, -1, []));
    }

    [Fact]
    public void TilesOutsideTheMapAreRejected()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        Assert.False(map.Contains(-1, 0));
        Assert.False(map.Contains(0, 2));
        Assert.True(map.Contains(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map[2, 0]);
    }

    [Fact]
    public void TilesOutsideTheMapFallBack()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        Assert.Equal(TileKind.Water, map.TileAtOrDefault(9, 9, TileKind.Water));
        Assert.Equal(TileKind.Grass, map.TileAtOrDefault(9, 9));
    }

    [Fact]
    public void ANewTileHasNothingBuiltOnIt()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        var state = map.StateAt(1, 1);

        Assert.Equal(TileUse.None, state.Use);
        Assert.Equal(0, state.Development);
        Assert.False(state.IsRoad);
        Assert.False(state.IsZoned);
    }

    [Fact]
    public void TheStateOfATileKeepsWhatWasPutOnIt()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        map.SetState(0, 1, new TileState(TileKind.Grass, TileUse.Road));

        Assert.Equal(TileUse.Road, map.StateAt(0, 1).Use);
        Assert.Equal(TileUse.None, map.StateAt(1, 1).Use);
    }

    [Fact]
    public void TheStateOutsideTheMapIsRejected()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.StateAt(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetState(2, 0, new TileState(TileKind.Grass)));
        Assert.Equal(TileUse.Road, map.StateAtOrDefault(9, 9, new TileState(TileKind.Grass, TileUse.Road)).Use);
    }
}
