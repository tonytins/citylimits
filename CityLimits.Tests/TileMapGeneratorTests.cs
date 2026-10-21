using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TileMapGeneratorTests
{
    [Fact]
    public void TheSameSeedGivesTheSameMap()
    {
        var first = TileMapGenerator.Generate(1234, 32, 24);
        var second = TileMapGenerator.Generate(1234, 32, 24);

        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                Assert.Equal(first[x, y], second[x, y]);
            }
        }
    }

    [Fact]
    public void DifferentSeedsGiveDifferentMaps()
    {
        var first = TileMapGenerator.Generate(1, 32, 32);
        var second = TileMapGenerator.Generate(2, 32, 32);

        var differences = 0;
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (first[x, y] != second[x, y])
                {
                    differences++;
                }
            }
        }

        Assert.True(differences > 0, "Two seeds produced exactly the same map.");
    }

    [Fact]
    public void TheMapHasTheSizeItWasAskedFor()
    {
        var map = TileMapGenerator.Generate(7, 40, 18);

        Assert.Equal(40, map.Width);
        Assert.Equal(18, map.Height);
    }

    [Fact]
    public void TheMapHasLandAndWaterInIt()
    {
        var map = TileMapGenerator.Generate(2000, 64, 64);
        var kinds = new HashSet<TileKind>();

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                kinds.Add(map[x, y]);
            }
        }

        Assert.Contains(TileKind.Water, kinds);
        Assert.Contains(TileKind.Grass, kinds);
    }

    [Fact]
    public void AlmostEveryTreeStandsInAWood()
    {
        // Thinning clears trees without trees on two sides that meet; the map's edge is no tree.
        // It works in place, twice, as in Micropolis, so a tree cleared late in the second pass
        // can leave one it had already passed on its own: a few in a thousand.
        var (trees, lone) = (0, 0);
        for (var seed = 1; seed <= 20; seed++)
        {
            var map = TileMapGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (map[x, y] != TileKind.Trees)
                    {
                        continue;
                    }

                    var sides = (IsTrees(map, x, y - 1) ? TerrainSides.North : 0)
                        | (IsTrees(map, x + 1, y) ? TerrainSides.East : 0)
                        | (IsTrees(map, x, y + 1) ? TerrainSides.South : 0)
                        | (IsTrees(map, x - 1, y) ? TerrainSides.West : 0);
                    trees++;
                    lone += TerrainEdges.IsWood(sides) ? 0 : 1;
                }
            }
        }

        Assert.True(trees > 0);
        Assert.InRange(lone, 0, trees / 200);
    }

    [Fact]
    public void AboutOneMapInTenIsAnIsland()
    {
        var islands = Enumerable.Range(1, 200)
            .Select(seed => TileMapGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight))
            .Count(map => EdgesWithWater(map) == 4 && Border(map).All(kind => kind == TileKind.Water));

        Assert.InRange(islands, 10, 30);
    }

    [Fact]
    public void AMapThatIsntAnIslandHasRiversRunningOffItsEdges()
    {
        // The two wide rivers set off in opposite directions and run until they leave the map.
        for (var seed = 1; seed <= 50; seed++)
        {
            var map = TileMapGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
            if (Border(map).All(kind => kind == TileKind.Water))
            {
                continue;
            }

            Assert.True(EdgesWithWater(map) >= 2, $"seed {seed}: the rivers don't reach two edges");
        }
    }

    [Fact]
    public void TheGeneratorKeepsToTheMapAtEverySizeAScenarioCanAskFor()
    {
        foreach (var (width, height) in new[] { (16, 16), (16, 512), (512, 16), (79, 66), (512, 512) })
        {
            var map = TileMapGenerator.Generate(2000, width, height);

            Assert.Equal((width, height), (map.Width, map.Height));
        }
    }

    [Fact]
    public void TheGeneratorRejectsASizeThatIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMapGenerator.Generate(1, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMapGenerator.Generate(1, 4, 0));
    }

    private static bool IsTrees(TileMap map, int x, int y) => map.Contains(x, y) && map[x, y] == TileKind.Trees;

    private static IEnumerable<TileKind> Border(TileMap map) =>
        Enumerable.Range(0, map.Width).SelectMany(x => new[] { map[x, 0], map[x, map.Height - 1] })
            .Concat(Enumerable.Range(0, map.Height).SelectMany(y => new[] { map[0, y], map[map.Width - 1, y] }));

    private static int EdgesWithWater(TileMap map) =>
        (Enumerable.Range(0, map.Width).Any(x => map[x, 0] == TileKind.Water) ? 1 : 0)
        + (Enumerable.Range(0, map.Width).Any(x => map[x, map.Height - 1] == TileKind.Water) ? 1 : 0)
        + (Enumerable.Range(0, map.Height).Any(y => map[0, y] == TileKind.Water) ? 1 : 0)
        + (Enumerable.Range(0, map.Height).Any(y => map[map.Width - 1, y] == TileKind.Water) ? 1 : 0);
}
