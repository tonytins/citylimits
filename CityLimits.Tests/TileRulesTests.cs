using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TileRulesTests
{
    [Fact]
    public void GrassIsReadyToBuildOn()
    {
        Assert.True(TileRules.CanBuildOn(TileKind.Grass));
        Assert.False(TileRules.NeedsClearing(TileKind.Grass));
        Assert.True(TileRules.IsClearGround(TileKind.Grass));
    }

    [Fact]
    public void TreesHaveToBeClearedFirst()
    {
        Assert.True(TileRules.CanBuildOn(TileKind.Trees));
        Assert.True(TileRules.NeedsClearing(TileKind.Trees));
        Assert.False(TileRules.IsClearGround(TileKind.Trees));
    }

    [Theory]
    [InlineData(TileKind.Water)]
    [InlineData(TileKind.Rock)]
    public void WaterAndRockCannotBeBuiltOn(TileKind kind)
    {
        Assert.False(TileRules.CanBuildOn(kind));
        Assert.False(TileRules.NeedsClearing(kind));
        Assert.False(TileRules.IsClearGround(kind));
    }

    [Fact]
    public void EveryKindOfGroundHasAName()
    {
        Assert.Equal("Grass", TileInfo.Name(TileKind.Grass));
        Assert.Equal("Water", TileInfo.Name(TileKind.Water));
        Assert.Equal("Trees", TileInfo.Name(TileKind.Trees));
        Assert.Equal("Rock", TileInfo.Name(TileKind.Rock));
    }

    [Fact]
    public void TheAdviceFollowsTheRules()
    {
        Assert.Equal("ready to build on", TileInfo.BuildAdvice(TileKind.Grass));
        Assert.Equal("clear it before building", TileInfo.BuildAdvice(TileKind.Trees));
        Assert.Equal("cannot be built on", TileInfo.BuildAdvice(TileKind.Water));
        Assert.Equal("cannot be built on", TileInfo.BuildAdvice(TileKind.Rock));
    }

    [Fact]
    public void TheDescriptionNamesTheGroundAndTheAdvice()
    {
        Assert.Equal("Trees - clear it before building", TileInfo.Describe(TileKind.Trees));
        Assert.Equal("Rock - cannot be built on", TileInfo.Describe(TileKind.Rock));
    }
}
