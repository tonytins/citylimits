using CityLimits.Core.Definitions;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class CityMapsTests
{
    private static TileMap Flat(int width, int height) => TileMap.FromTerrain(width, height, new TileKind[width * height]);

    [Fact]
    public void ABlockWithNothingBuiltHasNoLandValue()
    {
        var map = Flat(16, 16);
        var maps = new CityMaps(map);

        maps.UpdateLandValue();

        Assert.Equal(0, maps.LandValueAt(8, 8));
    }

    [Fact]
    public void ABuiltBlockAtTheCentreIsWorth136()
    {
        // The centre of mass starts at the map's middle; a road there is 0 blocks from it.
        var map = Flat(16, 16);
        map.SetState(8, 8, new TileState(TileKind.Grass, TileUse.Road));
        var maps = new CityMaps(map);

        maps.UpdateLandValue();

        Assert.Equal(34 * 4, maps.LandValueAt(8, 8));
        Assert.Equal(34 * 4, maps.LandValueAt(9, 9));
        Assert.Equal(0, maps.LandValueAt(10, 8));
    }

    [Fact]
    public void LandValueFallsWithDistanceToTheCentreButNotBelowEight()
    {
        var map = Flat(128, 128);
        map.SetState(70, 64, new TileState(TileKind.Grass, TileUse.Road));
        map.SetState(0, 0, new TileState(TileKind.Grass, TileUse.Road));
        var maps = new CityMaps(map);

        maps.UpdateLandValue();

        // (70, 64) is 3 blocks across from the centre's block; (0, 0) is more than 32 away.
        Assert.Equal((34 - 3) * 4, maps.LandValueAt(70, 64));
        Assert.Equal((34 - 32) * 4, maps.LandValueAt(0, 0));
    }

    [Fact]
    public void TreesAndWaterNearbyAddToLandValueTheNextTime()
    {
        var map = Flat(16, 16);
        map.SetState(8, 8, new TileState(TileKind.Grass, TileUse.Road));
        map.SetState(9, 9, new TileState(TileKind.Trees));
        map.SetState(9, 8, new TileState(TileKind.Water));
        var maps = new CityMaps(map);

        maps.UpdateLandValue();
        Assert.Equal(136, maps.LandValueAt(8, 8));

        // Two natural tiles in the 4 by 4 block, 15 each, kept at half after smoothing.
        maps.UpdateLandValue();
        Assert.Equal(136 + 15, maps.LandValueAt(8, 8));
    }

    [Fact]
    public void RubbleIsNotBuiltOn()
    {
        var map = Flat(16, 16);
        map.SetState(8, 8, new TileState(TileKind.Rubble));
        var maps = new CityMaps(map);

        maps.UpdateLandValue();

        Assert.Equal(0, maps.LandValueAt(8, 8));
    }

    [Fact]
    public void DensitySpreadsAZonesPopulationAndFindsTheCentre()
    {
        var map = Flat(32, 32);
        var city = new CitySimulation(map, 1);
        city.Builder.Apply(BuildAction.ZoneCommercial, 10, 10);
        city.Builder.Apply(BuildAction.ZoneCommercial, 20, 20);
        map.SetState(10, 10, new TileState(TileKind.Grass, TileUse.Commercial, development: 5));

        city.Maps.UpdateDensity(CityDefinitions.Base);

        Assert.Equal((15, 15), (city.Maps.CenterX, city.Maps.CenterY));
        Assert.True(city.Maps.DensityAt(10, 10) > city.Maps.DensityAt(14, 10));
        Assert.True(city.Maps.DensityAt(12, 10) > 0);
        Assert.Equal(0, city.Maps.DensityAt(20, 20));
    }

    [Fact]
    public void TheCommercialRateIsHighestAtTheCentre()
    {
        var map = Flat(64, 64);
        var maps = new CityMaps(map);
        maps.UpdateDensity(CityDefinitions.Base);

        Assert.Equal(64, maps.CommercialRateAt(32, 32));
        Assert.True(maps.CommercialRateAt(0, 0) < maps.CommercialRateAt(32, 32));
        // The corner is 32 blocks from the centre's block, as far as distance counts, so a quarter of 32 off.
        Assert.Equal(64 - (32 / 4), maps.CommercialRateAt(0, 0));
    }
}
