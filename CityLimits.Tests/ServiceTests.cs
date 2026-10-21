using CityLimits.Core.Definitions;
using CityLimits.Core.Economy;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ServiceTests
{
    private static CitySimulation City(int width, int height) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, funds: 1_000_000);

    // It stops a step short of the next cycle, whose first phase starts the counts again.
    private static void RunCycles(CitySimulation city, int cycles)
    {
        for (var i = 0; i < (cycles * CityCalendar.StepsPerCount) - 1; i++)
        {
            city.Tick();
        }
    }

    // A fire station centred at (20, 20), with a plant against it for power and a road along
    // its bottom edge, unless asked for neither.
    private static CitySimulation CityWithFireStation(bool powered = true, bool served = true)
    {
        var city = City(48, 48);
        city.Builder.Apply(BuildAction.FireStation, 20, 20);
        if (powered)
        {
            city.Builder.Apply(BuildAction.CoalPowerPlant, 23, 20);
        }

        if (served)
        {
            var road = new BuildStroke(BuildAction.Road, 18, 22);
            road.DragTo(21, 22);
            city.Builder.Apply(road);
        }

        return city;
    }

    [Fact]
    public void TheBaseGamesStationsAreMicropolis()
    {
        Assert.Equal(new ServiceSpec("fire", TileUse.FireStation, "Fire station", 500, 100), CityDefinitions.Base.Service(TileUse.FireStation));
        Assert.Equal(new ServiceSpec("police", TileUse.PoliceStation, "Police station", 500, 100), CityDefinitions.Base.Service(TileUse.PoliceStation));
        Assert.Equal(500, CityDefinitions.Base.CostOf(BuildAction.PoliceStation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CityDefinitions.Base.Service(TileUse.Road));
    }

    [Theory]
    [InlineData(BuildAction.FireStation, TileUse.FireStation)]
    [InlineData(BuildAction.PoliceStation, TileUse.PoliceStation)]
    public void AStationIsThreeByThreeAndKnockedDownThroughItsCentre(BuildAction action, TileUse use)
    {
        var city = City(9, 9);
        var funds = city.Treasury.Funds;

        city.Builder.Apply(action, 4, 4);

        Assert.Equal(funds - 500, city.Treasury.Funds);
        Assert.All(Enumerable.Range(3, 3), y => Assert.All(Enumerable.Range(3, 3), x => Assert.Equal(use, city.Map.StateAt(x, y).Use)));
        Assert.True(city.Map.StateAt(4, 4).IsBuildingCenter);
        Assert.False(city.Map.StateAt(4, 4).IsZoned);
        Assert.Equal(BuildRefusal.PartOfAZone, city.Builder.Apply(BuildAction.Bulldoze, 3, 3).Reason);

        city.Builder.Apply(BuildAction.Bulldoze, 4, 4);

        Assert.Equal(new TileState(TileKind.Rubble), city.Map.StateAt(5, 5));
    }

    [Fact]
    public void DraggingAStationMovesIt()
    {
        var stroke = new BuildStroke(BuildAction.PoliceStation, 1, 1);
        stroke.DragTo(10, 7);

        Assert.Equal(new TileRect(9, 6, 3, 3), stroke.Bounds);
    }

    [Fact]
    public void AStationsCoverIsStrongestOnItsBlockAndFadesWithDistance()
    {
        var city = CityWithFireStation();

        RunCycles(city, 2);

        var services = city.Services;
        Assert.Equal(1, services.FireStations);
        var here = services.FireCoverageAt(20, 20);
        var next = services.FireCoverageAt(28, 20);
        var farther = services.FireCoverageAt(36, 20);
        Assert.True(here > next && next > farther && farther > 0, $"{here}, {next}, {farther}");
        Assert.Equal(0, services.FireCoverageAt(4, 44));
        Assert.Equal(0, services.PoliceCoverageAt(20, 20));
    }

    [Fact]
    public void AStationWithFullFundingPowerAndARoadSpreadsMicropolisWay()
    {
        // One block of 1 000, smoothed three times: half of itself plus an eighth of each neighbour,
        // as whole numbers. The block goes 500, 312, then 218; the block beside it 125, 125, then 111.
        var city = CityWithFireStation();

        RunCycles(city, 2);

        Assert.Equal(218, city.Services.FireCoverageAt(20, 20));
        Assert.Equal(111, city.Services.FireCoverageAt(28, 20));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AStationWithoutPowerOrARoadCoversHalfAsWell(bool powered, bool served)
    {
        var full = CityWithFireStation();
        var half = CityWithFireStation(powered, served);

        RunCycles(full, 2);
        RunCycles(half, 2);

        Assert.InRange(half.Services.FireCoverageAt(20, 20), (full.Services.FireCoverageAt(20, 20) / 2) - 2, (full.Services.FireCoverageAt(20, 20) / 2) + 2);
    }

    [Fact]
    public void FundingScalesAStationsCover()
    {
        var full = CityWithFireStation();
        var half = CityWithFireStation();
        half.Budget.SetFunding(roads: 100, fire: 50, police: 100);

        RunCycles(full, 3);
        RunCycles(half, 3);

        Assert.Equal(500, half.Budget.FireEffect);
        Assert.InRange(half.Services.FireCoverageAt(20, 20), (full.Services.FireCoverageAt(20, 20) / 2) - 2, (full.Services.FireCoverageAt(20, 20) / 2) + 2);
    }

    [Fact]
    public void StationsAreKeptUpAtTheirSpecsUpkeep()
    {
        var city = City(20, 9);
        city.Builder.Apply(BuildAction.FireStation, 4, 4);
        city.Builder.Apply(BuildAction.FireStation, 8, 4);
        city.Builder.Apply(BuildAction.PoliceStation, 12, 4);

        RunCycles(city, 1);

        var estimate = city.EstimateBudget();
        Assert.Equal((200, 100), (estimate.FireRequest, estimate.PoliceRequest));
        Assert.Equal((2, 1), (city.Services.FireStations, city.Services.PoliceStations));
    }

    [Fact]
    public void ScenariosCountStations()
    {
        var city = City(20, 9);
        city.Builder.Apply(BuildAction.FireStation, 4, 4);
        city.Builder.Apply(BuildAction.PoliceStation, 12, 4);

        var counts = ScenarioRunner.Count(city.Map, CityDefinitions.Base);

        Assert.Equal((1, 1), (counts.FireStation, counts.PoliceStation));
    }

    [Fact]
    public void TheReadoutNamesAStation() =>
        Assert.Equal("Police station - bulldoze its centre to clear it", TileInfo.Describe(new TileState(TileKind.Grass, TileUse.PoliceStation), CityDefinitions.Base));

    // Twenty residential buildings of 40 people, powered and on a road: 800 residents, 100 as
    // taxes count them, enough that a city with no jobs stays above 60 until the message comes.
    private static CitySimulation PeopledCity()
    {
        var city = City(72, 12);
        var zones = new BuildStroke(BuildAction.ZoneResidential, 1, 1);
        zones.DragTo(58, 1);
        city.Builder.Apply(zones);
        for (var x = 1; x <= 58; x += 3)
        {
            city.Map.SetState(x, 1, new TileState(TileKind.Grass, TileUse.Residential, development: 4, valueClass: 1));
        }

        city.Builder.Apply(BuildAction.CoalPowerPlant, 61, 1);
        var road = new BuildStroke(BuildAction.Road, 0, 3);
        road.DragTo(59, 3);
        city.Builder.Apply(road);
        return city;
    }

    [Fact]
    public void ACityOfMoreThanSixtyPeopleIsToldItNeedsStations()
    {
        var city = PeopledCity();
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCycles(city, 64);

        Assert.True(city.Demand.TaxablePopulation > 60, $"{city.Demand.TaxablePopulation}");
        Assert.Contains(CityMessage.NeedFireStation, heard);
        Assert.Contains(CityMessage.NeedPoliceStation, heard);
    }

    [Fact]
    public void ACityWithStationsIsntToldItNeedsThemButIsToldWhenTheyAreUnderfunded()
    {
        var city = PeopledCity();
        city.Builder.Apply(BuildAction.FireStation, 4, 6);
        city.Builder.Apply(BuildAction.PoliceStation, 8, 6);
        city.Budget.SetFunding(roads: 100, fire: 60, police: 100);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCycles(city, 64);

        Assert.DoesNotContain(CityMessage.NeedFireStation, heard);
        Assert.DoesNotContain(CityMessage.NeedPoliceStation, heard);
        Assert.Contains(CityMessage.FireNeedsFunding, heard);
        Assert.DoesNotContain(CityMessage.PoliceNeedsFunding, heard);
    }
}
