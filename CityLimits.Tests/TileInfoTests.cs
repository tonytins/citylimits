using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TileInfoTests
{
    [Fact]
    public void EveryUseHasAName()
    {
        Assert.Equal("Empty", TileInfo.Name(TileUse.None));
        Assert.Equal("Road", TileInfo.Name(TileUse.Road));
        Assert.Equal("Residential zone", TileInfo.Name(TileUse.Residential));
        Assert.Equal("Commercial zone", TileInfo.Name(TileUse.Commercial));
        Assert.Equal("Industrial zone", TileInfo.Name(TileUse.Industrial));
    }

    [Fact]
    public void APlainTileIsDescribedByItsGround()
    {
        var state = new TileState(TileKind.Trees);

        Assert.Equal("Trees - clear it before building", TileInfo.Describe(state));
    }

    [Fact]
    public void ARoadIsDescribedByWhatItCarries()
    {
        var state = new TileState(TileKind.Grass, TileUse.Road);

        Assert.Equal("Road - carries traffic", TileInfo.Describe(state));
    }

    [Fact]
    public void AZoneThatHasNotDevelopedIsWaitingForARoad()
    {
        var state = new TileState(TileKind.Grass, TileUse.Residential);

        Assert.Equal("Residential zone - waiting for a road", TileInfo.Describe(state));
    }

    [Fact]
    public void AZoneThatHasDevelopedSaysHowFar()
    {
        var state = new TileState(TileKind.Grass, TileUse.Commercial, 2);

        Assert.Equal("Commercial zone - developed to level 2", TileInfo.Describe(state));
    }
}
