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

        maps.UpdateLandValue(new TrafficMap(map));

        Assert.Equal(0, maps.LandValueAt(8, 8));
    }

    [Fact]
    public void ABuiltBlockAtTheCentreIsWorth136()
    {
        // The centre of mass starts at the map's middle; a road there is 0 blocks from it.
        var map = Flat(16, 16);
        map.SetState(8, 8, new TileState(TileKind.Grass, TileUse.Road));
        var maps = new CityMaps(map);

        maps.UpdateLandValue(new TrafficMap(map));

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

        maps.UpdateLandValue(new TrafficMap(map));

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

        maps.UpdateLandValue(new TrafficMap(map));
        Assert.Equal(136, maps.LandValueAt(8, 8));

        // Two natural tiles in the 4 by 4 block, 15 each, kept at half after smoothing.
        maps.UpdateLandValue(new TrafficMap(map));
        Assert.Equal(136 + 15, maps.LandValueAt(8, 8));
    }

    [Fact]
    public void RubbleIsNotBuiltOn()
    {
        var map = Flat(16, 16);
        map.SetState(8, 8, new TileState(TileKind.Rubble));
        var maps = new CityMaps(map);

        maps.UpdateLandValue(new TrafficMap(map));

        Assert.Equal(0, maps.LandValueAt(8, 8));
    }

    [Fact]
    public void ACoalPlantPollutesEveryTileItCoversSmoothedTwice()
    {
        // The plant covers tiles 4 to 7 each way: blocks 2 and 3, four tiles of 100 each, held at 255.
        // The first smoothing gives each of them (255 * 3) / 4 = 191 and each block beside them 63;
        // the second gives block (2, 2) (191 * 3 + 63 * 2) / 4 = 174.
        var map = Flat(16, 16);
        new CitySimulation(map, 1).Builder.Apply(BuildAction.CoalPowerPlant, 5, 5);
        var maps = new CityMaps(map);

        maps.UpdateLandValue(new TrafficMap(map));

        Assert.Equal(174, maps.PollutionAt(4, 4));
        Assert.Equal(174, maps.PollutionAt(7, 7));
        Assert.Equal(0, maps.PollutionAt(0, 0));
        Assert.True(maps.PollutionAverage > 0);
    }

    [Fact]
    public void PollutionComesOffLandValueTheNextTime()
    {
        var map = Flat(16, 16);
        new CitySimulation(map, 1).Builder.Apply(BuildAction.CoalPowerPlant, 9, 9);
        var maps = new CityMaps(map);

        maps.UpdateLandValue(new TrafficMap(map));
        var clean = maps.LandValueAt(8, 8);
        maps.UpdateLandValue(new TrafficMap(map));

        Assert.Equal(Math.Max(clean - maps.PollutionAt(8, 8), 1), maps.LandValueAt(8, 8));
        Assert.True(maps.LandValueAt(8, 8) < clean);
    }

    [Fact]
    public void AnIndustrialZonePollutesOnlyOnceItHasABuilding()
    {
        var map = Flat(16, 16);
        new CitySimulation(map, 1).Builder.Apply(BuildAction.ZoneIndustrial, 8, 8);
        var maps = new CityMaps(map);

        maps.UpdateLandValue(new TrafficMap(map));
        Assert.Equal(0, maps.PollutionAt(8, 8));

        map.SetState(8, 8, new TileState(TileKind.Grass, TileUse.Industrial, development: 1));
        maps.UpdateLandValue(new TrafficMap(map));
        Assert.True(maps.PollutionAt(8, 8) > 0);
    }

    [Fact]
    public void ARoadPollutesByTheTrafficItShows()
    {
        // Four trips make 200, which the scan shows as heavy: 75 on the block, a quarter of it to
        // each block beside it, then (18 * 5) / 4 = 22 back on its own.
        var map = Flat(16, 16);
        var city = new CitySimulation(map, 1);
        city.Builder.Apply(BuildAction.Road, 8, 8);
        for (var trip = 0; trip < 4; trip++)
        {
            city.Traffic.AddRoute(map, [(8, 8)]);
        }

        for (var step = 0; step < CityCalendar.StepsPerCount; step++)
        {
            city.Tick();
        }

        Assert.Equal(TrafficLevel.Heavy, city.Traffic.LevelAt(8, 8));
        Assert.Equal(22, city.Maps.PollutionAt(8, 8));
    }

    [Fact]
    public void CrimeIs128LessLandValueWhereThereIsLandValue()
    {
        // A road at the centre is worth 136, more than enough to keep crime off; one in the corner
        // is worth 8, so its crime is 120.
        var map = Flat(128, 128);
        map.SetState(64, 64, new TileState(TileKind.Grass, TileUse.Road));
        map.SetState(0, 0, new TileState(TileKind.Grass, TileUse.Road));
        var maps = new CityMaps(map);

        maps.UpdateLandValue(new TrafficMap(map));
        maps.UpdateCrime(new ServiceCoverage(map));

        Assert.Equal(0, maps.CrimeAt(64, 64));
        Assert.Equal(120, maps.CrimeAt(0, 0));
        Assert.Equal(0, maps.CrimeAt(30, 30));
        Assert.Equal(60, maps.CrimeAverage);
    }

    [Fact]
    public void HighCrimeTakes20OffLandValue()
    {
        // Busy shops in far corners, keeping the centre of mass in the middle: the corner block is
        // worth 8, and the density lifts its crime past 190.
        var map = Flat(128, 128);
        var city = new CitySimulation(map, 1);
        foreach (var (x, y) in new[] { (1, 1), (4, 1), (1, 4), (4, 4), (126, 126), (123, 126), (126, 123), (123, 123) })
        {
            city.Builder.Apply(BuildAction.ZoneCommercial, x, y);
            map.SetState(x, y, new TileState(TileKind.Grass, TileUse.Commercial, development: 5));
        }

        var maps = new CityMaps(map);

        maps.UpdateDensity(CityDefinitions.Base);
        maps.UpdateLandValue(new TrafficMap(map));
        Assert.Equal(8, maps.LandValueAt(0, 0));
        maps.UpdateCrime(new ServiceCoverage(map));
        Assert.True(maps.CrimeAt(0, 0) > 190, $"crime {maps.CrimeAt(0, 0)}");
        maps.UpdateLandValue(new TrafficMap(map));

        Assert.Equal(1, maps.LandValueAt(0, 0));
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
