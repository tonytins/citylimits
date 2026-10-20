using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TileMapTests
{
    [Fact]
    public void TheMapKeepsTheTilesItWasGiven()
    {
        var tiles = new[] { TileKind.Grass, TileKind.Water, TileKind.Trees, TileKind.Rock };
        var map = new TileMap(2, 2, tiles);

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
        var map = new TileMap(1, 1, tiles);

        tiles[0] = TileKind.Rock;

        Assert.Equal(TileKind.Grass, map[0, 0]);
    }

    [Fact]
    public void TheMapRejectsASizeThatDoesNotFit()
    {
        Assert.Throws<ArgumentException>(() => new TileMap(2, 2, new[] { TileKind.Grass }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileMap(0, 2, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileMap(2, -1, []));
    }

    [Fact]
    public void TilesOutsideTheMapAreRejected()
    {
        var map = new TileMap(2, 2, new TileKind[4]);

        Assert.False(map.Contains(-1, 0));
        Assert.False(map.Contains(0, 2));
        Assert.True(map.Contains(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map[2, 0]);
    }

    [Fact]
    public void TilesOutsideTheMapFallBack()
    {
        var map = new TileMap(2, 2, new TileKind[4]);

        Assert.Equal(TileKind.Water, map.TileAtOrDefault(9, 9, TileKind.Water));
        Assert.Equal(TileKind.Grass, map.TileAtOrDefault(9, 9));
    }
}
