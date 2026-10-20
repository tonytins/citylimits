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
    public void TheGeneratorRejectsASizeThatIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMapGenerator.Generate(1, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMapGenerator.Generate(1, 4, 0));
    }
}
