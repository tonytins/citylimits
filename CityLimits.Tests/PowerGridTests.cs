using CityLimits.Core.Definitions;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class PowerGridTests
{
    private static CitySimulation City(int width, int height, CityDefinitions? definitions = null) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, funds: 1_000_000, definitions: definitions);

    private static void Line(CitySimulation city, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(BuildAction.PowerLine, fromX, fromY);
        stroke.DragTo(toX, toY);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(stroke).Status);
    }

    // Two cycles: the first finds the plants and traces power, the second gives the zones theirs.
    // It stops a step short of the next cycle, whose first phase starts the counts again.
    private static void RunCycles(CitySimulation city, int cycles = 2)
    {
        for (var i = 0; i < (cycles * CityCalendar.StepsPerCount) - 1; i++)
        {
            city.Tick();
        }
    }

    // The base game with the coal plant's capacity changed, applied through a mod folder.
    private static CityDefinitions WithCoalCapacity(int capacity)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var mod = Directory.CreateDirectory(Path.Combine(folder, "small-coal")).FullName;
            File.WriteAllText(Path.Combine(mod, "mod.json"), """{ "format": 1, "id": "small-coal", "name": "Small coal" }""");
            File.WriteAllText(Path.Combine(mod, "plants.json"), $$"""{ "format": 1, "plants": [ { "id": "coal", "capacity": {{capacity}} } ] }""");
            var definitions = ModLoader.Load(folder, out var problems);
            Assert.Empty(problems);
            return definitions;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void APlantPowersAZoneItTouches()
    {
        var city = City(10, 6);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 2);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 5, 2);

        RunCycles(city);

        Assert.True(city.Map.StateAt(2, 2).Powered);
        Assert.True(city.Map.StateAt(5, 2).Powered);
        Assert.Equal(9 + 16, city.Power.PoweredTiles);
        Assert.True(city.Power.HasPower(1, 1));
        Assert.False(city.Power.HasPower(0, 0));
    }

    [Fact]
    public void PowerRunsAlongALineButNotAcrossAGapOrARoad()
    {
        // A plant, a line to a zone, then a road, then another zone.
        var city = City(24, 6);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 2, 2);
        Line(city, 5, 2, 8, 2);
        city.Builder.Apply(BuildAction.ZoneResidential, 10, 2);
        city.Builder.Apply(BuildAction.Road, 12, 2);
        city.Builder.Apply(BuildAction.ZoneResidential, 14, 2);
        city.Builder.Apply(BuildAction.ZoneResidential, 19, 2);

        RunCycles(city);

        Assert.True(city.Map.StateAt(10, 2).Powered);
        Assert.False(city.Map.StateAt(14, 2).Powered);
        Assert.False(city.Map.StateAt(19, 2).Powered);
        Assert.False(city.Power.HasPower(12, 2));
    }

    [Fact]
    public void PowerCrossesARoadThroughACrossing()
    {
        // A line runs down from the plant, through a road, to a zone on the far side.
        var city = City(14, 14);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 6, 1);
        var road = new BuildStroke(BuildAction.Road, 0, 6);
        road.DragTo(13, 6);
        city.Builder.Apply(road);
        Line(city, 6, 4, 6, 8);
        city.Builder.Apply(BuildAction.ZoneResidential, 6, 10);

        RunCycles(city);

        Assert.Equal(TileUse.RoadAcrossPowerLine, city.Map.StateAt(6, 6).Use);
        Assert.True(city.Power.HasPower(6, 6));
        Assert.False(city.Power.HasPower(5, 6));
        Assert.True(city.Map.StateAt(6, 10).Powered);
    }

    [Fact]
    public void PowerDoesntLeaveACrossingThroughItsRoad()
    {
        // The line comes up from the plant and ends in a crossing; a zone against the crossing's
        // road side, east of it, touches nothing else that carries power.
        var city = City(12, 14);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 6, 11);
        var road = new BuildStroke(BuildAction.Road, 0, 6);
        road.DragTo(6, 6);
        city.Builder.Apply(road);
        Line(city, 6, 6, 6, 9);
        city.Builder.Apply(BuildAction.ZoneResidential, 8, 5);

        RunCycles(city);

        Assert.Equal(TileUse.RoadAcrossPowerLine, city.Map.StateAt(6, 6).Use);
        Assert.True(city.Power.HasPower(6, 6));
        Assert.False(city.Map.StateAt(8, 5).Powered);
    }

    [Fact]
    public void APlantHasPowerFromTheFirstScanBeforeAnyTrace()
    {
        var city = City(10, 6);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 5, 2);

        for (var i = 0; i < CityCalendar.ScanBands; i++)
        {
            city.Tick();
        }

        Assert.Equal(0, city.Power.PoweredTiles);
        Assert.True(city.Map.StateAt(5, 2).Powered);
    }

    [Fact]
    public void ThePlantsCapacityIsHowManyTilesTheyPower()
    {
        // 16 plant tiles, then a line of 20; a plant that powers 25 tiles runs out partway.
        var city = City(30, 6, WithCoalCapacity(25));
        city.Builder.Apply(BuildAction.CoalPowerPlant, 2, 2);
        Line(city, 5, 2, 24, 2);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCycles(city, 1);

        Assert.Equal(25, city.Power.Capacity);
        Assert.Equal(25, city.Power.PoweredTiles);
        Assert.True(city.Power.IsBrownedOut);
        Assert.Contains(CityMessage.Brownouts, heard);
        Assert.False(city.Power.HasPower(24, 2));
    }

    [Fact]
    public void ACoalPlantPowersSevenHundredTiles()
    {
        // A 4 by 4 plant and a long line under it: 16 + 700 tiles want power.
        var city = City(120, 12);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 2, 2);
        for (var row = 5; row < 11; row++)
        {
            Line(city, 0, row, 119, row);
        }

        RunCycles(city, 1);

        Assert.Equal(700, city.Power.Capacity);
        Assert.Equal(700, city.Power.PoweredTiles);
        Assert.True(city.Power.IsBrownedOut);
    }

    [Fact]
    public void EveryPlantOnTheMapAddsToTheCapacityConnectedOrNot()
    {
        // As in Micropolis: the second plant stands alone, but its capacity still counts. The
        // first plant and its line need 32 tiles, more than its own 30 could power.
        var city = City(30, 6, WithCoalCapacity(30));
        city.Builder.Apply(BuildAction.CoalPowerPlant, 2, 2);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 25, 2);
        Line(city, 5, 2, 20, 2);

        RunCycles(city, 1);

        Assert.Equal(60, city.Power.Capacity);
        Assert.False(city.Power.IsBrownedOut);
        Assert.Equal(16 + 16 + 16, city.Power.PoweredTiles);
        Assert.True(city.Power.HasPower(20, 2));
    }

    [Fact]
    public void AZoneLosesPowerWhenItsPlantIsKnockedDown()
    {
        var city = City(10, 6);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 2);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 5, 2);
        RunCycles(city);
        Assert.True(city.Map.StateAt(2, 2).Powered);

        city.Builder.Apply(BuildAction.Bulldoze, 5, 2);
        RunCycles(city);

        Assert.False(city.Map.StateAt(2, 2).Powered);
        Assert.Equal(0, city.Power.PoweredTiles);
    }

    [Fact]
    public void AZoneWithNoPowerNeverGrows()
    {
        var city = City(9, 9);
        var road = new BuildStroke(BuildAction.Road, 2, 6);
        road.DragTo(6, 6);
        city.Builder.Apply(road);
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);
        var grew = false;
        city.Map.TileChanged += (x, y) => grew |= city.Map.StateAt(x, y).HasHouse || city.Map.ZoneStateAt(x, y).Development > 0;

        RunCycles(city, 400);

        Assert.False(grew);
        Assert.False(city.Map.StateAt(4, 4).Powered);
    }

    [Fact]
    public void ACityWithManyZonesAndNoPlantIsToldItNeedsPower()
    {
        var city = City(40, 6);
        var zones = new BuildStroke(BuildAction.ZoneResidential, 1, 1);
        zones.DragTo(33, 1);
        city.Builder.Apply(zones);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCycles(city, 64);

        Assert.Equal(11, city.Power.Zones);
        Assert.Contains(CityMessage.NeedPower, heard);
        Assert.Contains(CityMessage.Blackouts, heard);
        Assert.DoesNotContain(CityMessage.Brownouts, heard);
    }

    [Fact]
    public void ACityWithFewZonesIsntToldItNeedsPower()
    {
        var city = City(40, 6);
        var zones = new BuildStroke(BuildAction.ZoneResidential, 1, 1);
        zones.DragTo(30, 1);
        city.Builder.Apply(zones);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCycles(city, 64);

        Assert.Equal(10, city.Power.Zones);
        Assert.DoesNotContain(CityMessage.NeedPower, heard);
    }

    [Fact]
    public void APoweredCityHearsNothing()
    {
        var city = City(40, 6);
        var zones = new BuildStroke(BuildAction.ZoneResidential, 1, 1);
        zones.DragTo(33, 1);
        city.Builder.Apply(zones);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 34, 1);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCycles(city, 64);

        Assert.Empty(heard);
        Assert.Equal(12, city.Power.PoweredBuildings);
        Assert.Equal(0, city.Power.UnpoweredBuildings);
    }
}
