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

    [Theory]
    [InlineData(TileKind.Trees)]
    [InlineData(TileKind.Rubble)]
    public void TreesAndRubbleAreClearedByTheTools(TileKind kind)
    {
        Assert.True(TileRules.CanBuildOn(kind));
        Assert.True(TileRules.NeedsClearing(kind));
        Assert.False(TileRules.IsClearGround(kind));
    }

    [Fact]
    public void WaterCannotBeBuiltOn()
    {
        var kind = TileKind.Water;

        Assert.False(TileRules.CanBuildOn(kind));
        Assert.False(TileRules.NeedsClearing(kind));
        Assert.False(TileRules.IsClearGround(kind));
        Assert.False(TileRules.CanLayRoad(kind));
        Assert.False(TileRules.CanZone(kind));
    }

    [Fact]
    public void EveryKindOfGroundHasAName()
    {
        Assert.Equal("Grass", TileInfo.Name(TileKind.Grass));
        Assert.Equal("Water", TileInfo.Name(TileKind.Water));
        Assert.Equal("Trees", TileInfo.Name(TileKind.Trees));
        Assert.Equal("Rubble", TileInfo.Name(TileKind.Rubble));
    }

    [Fact]
    public void TheAdviceFollowsTheRules()
    {
        Assert.Equal("ready to build on", TileInfo.BuildAdvice(TileKind.Grass));
        Assert.Equal("cleared for $1 a tile when built on", TileInfo.BuildAdvice(TileKind.Trees));
        Assert.Equal("cleared for $1 a tile when built on", TileInfo.BuildAdvice(TileKind.Rubble));
        Assert.Equal("only a bridge or a power line can cross it", TileInfo.BuildAdvice(TileKind.Water));
    }

    [Fact]
    public void TheDescriptionNamesTheGroundAndTheAdvice()
    {
        Assert.Equal("Trees - cleared for $1 a tile when built on", TileInfo.Describe(TileKind.Trees));
        Assert.Equal("Water - only a bridge or a power line can cross it", TileInfo.Describe(TileKind.Water));
    }

    [Theory]
    [InlineData(TileUse.Road, false)]
    [InlineData(TileUse.Residential, true)]
    [InlineData(TileUse.Commercial, true)]
    [InlineData(TileUse.Industrial, true)]
    [InlineData(TileUse.None, false)]
    public void TheZonesAreTheThreeKindsOfUse(TileUse use, bool isZone)
    {
        Assert.Equal(isZone, TileRules.IsZone(use));
    }

    [Fact]
    public void TheBulldozerClearsRoadsBridgesTreesAndRubbleButNotZonesTileByTile()
    {
        Assert.False(TileRules.HasSomethingToClear(new TileState(TileKind.Grass)));
        Assert.False(TileRules.HasSomethingToClear(new TileState(TileKind.Water)));
        Assert.True(TileRules.HasSomethingToClear(new TileState(TileKind.Trees)));
        Assert.True(TileRules.HasSomethingToClear(new TileState(TileKind.Rubble)));
        Assert.True(TileRules.HasSomethingToClear(new TileState(TileKind.Grass, TileUse.Road)));
        Assert.True(TileRules.HasSomethingToClear(new TileState(TileKind.Water, TileUse.Road)));
        Assert.False(TileRules.HasSomethingToClear(new TileState(TileKind.Grass, TileUse.Industrial)));
    }
}
