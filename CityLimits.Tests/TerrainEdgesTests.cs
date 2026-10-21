using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TerrainEdgesTests
{
    // A map drawn in rows: '~' water, 'T' trees, '=' a bridge, '#' a road, anything else grass.
    private static TileMap MapOf(params string[] rows)
    {
        var width = rows.Max(row => row.Length);
        var map = TileMap.FromTerrain(width, rows.Length, new TileKind[width * rows.Length]);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var state = rows[y][x] switch
                {
                    '~' => new TileState(TileKind.Water),
                    'T' => new TileState(TileKind.Trees),
                    '=' => new TileState(TileKind.Water, TileUse.Road),
                    '#' => new TileState(TileKind.Grass, TileUse.Road),
                    _ => new TileState(TileKind.Grass),
                };
                map.SetState(x, y, state);
            }
        }

        return map;
    }

    [Fact]
    public void WaterKnowsWhichSidesAreWater()
    {
        var map = MapOf(
            "...",
            ".~~",
            ".~~");

        Assert.Equal(TerrainSides.East | TerrainSides.South, TerrainEdges.SidesAt(map, 1, 1));
    }

    [Fact]
    public void TheMapsEdgeMatchesAnyGround()
    {
        var map = MapOf(
            "~~",
            "..");

        Assert.Equal(TerrainSides.North | TerrainSides.East | TerrainSides.West, TerrainEdges.SidesAt(map, 1, 0));
    }

    [Fact]
    public void ABridgeIsWaterAndARoadIsNot()
    {
        var map = MapOf(
            "~=~",
            "~#~",
            "~~~");

        Assert.Equal(TerrainSides.North | TerrainSides.East | TerrainSides.West, TerrainEdges.SidesAt(map, 1, 0));
        Assert.Equal(TerrainSides.None, TerrainEdges.SidesAt(map, 1, 1));
        Assert.Equal(TerrainSides.All, TerrainEdges.SidesAt(map, 0, 0));
    }

    [Fact]
    public void TreesKnowWhichSidesAreTrees()
    {
        var map = MapOf(
            ".T.",
            "TT~",
            "...");

        Assert.Equal(TerrainSides.North | TerrainSides.West, TerrainEdges.SidesAt(map, 1, 1));
    }

    [Fact]
    public void GrassAndTilesOffTheMapHaveNoEdges()
    {
        var map = MapOf("~.~");

        Assert.Equal(TerrainSides.None, TerrainEdges.SidesAt(map, 1, 0));
        Assert.Equal(TerrainSides.None, TerrainEdges.SidesAt(map, -1, 0));
    }

    [Theory]
    [InlineData(TerrainSides.None, false)]
    [InlineData(TerrainSides.North, false)]
    [InlineData(TerrainSides.North | TerrainSides.South, false)]
    [InlineData(TerrainSides.East | TerrainSides.West, false)]
    [InlineData(TerrainSides.North | TerrainSides.East, true)]
    [InlineData(TerrainSides.East | TerrainSides.South, true)]
    [InlineData(TerrainSides.South | TerrainSides.West, true)]
    [InlineData(TerrainSides.West | TerrainSides.North, true)]
    [InlineData(TerrainSides.North | TerrainSides.East | TerrainSides.South, true)]
    [InlineData(TerrainSides.All, true)]
    public void TreesStandInAWoodWhenTreesMeetAtACornerOfThem(TerrainSides trees, bool expected) =>
        Assert.Equal(expected, TerrainEdges.IsWood(trees));
}
