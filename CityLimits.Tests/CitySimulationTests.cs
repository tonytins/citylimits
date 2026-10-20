using CityLimits.Core.Definitions;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class CitySimulationTests
{
    [Fact]
    public void TheCalendarMovesOnOnceEverySixteenSteps()
    {
        var simulation = new CitySimulation(SmallMap(), 1);

        Run(simulation, CityCalendar.StepsPerCount - 1);
        Assert.Equal(0, simulation.CityTime);

        simulation.Tick();
        Assert.Equal(1, simulation.CityTime);
        Assert.Equal(CityCalendar.StepsPerCount, simulation.TickCount);
    }

    [Fact]
    public void AMonthIsFourCountsAndAYearIsFortyEight()
    {
        var simulation = new CitySimulation(SmallMap(), 1);

        Run(simulation, CityCalendar.StepsPerMonth);
        Assert.Equal(new GameDate(1900, 2), simulation.Date);

        Run(simulation, CityCalendar.StepsPerYear - CityCalendar.StepsPerMonth);
        Assert.Equal(new GameDate(1901, 1), simulation.Date);
        Assert.Equal(CityCalendar.CountsPerYear, simulation.CityTime);
    }

    [Fact]
    public void TheCityStartsInJanuary1900LikeMicropolis()
    {
        var simulation = new CitySimulation(SmallMap(), 1);

        Assert.Equal(new GameDate(1900, 1), simulation.Date);
        Assert.Equal(0, simulation.TickCount);
    }

    [Fact]
    public void EachZoneIsScannedOnceACountInItsColumnsBand()
    {
        // 16 columns make 8 bands of 2: a zone centred in column 1 is in band 0, column 13 in band 6.
        var map = TileMap.FromTerrain(16, 9, new TileKind[16 * 9]);
        var simulation = new CitySimulation(map, 1);
        simulation.Builder.Apply(BuildAction.Road, 1, 6);
        simulation.Builder.Apply(BuildAction.Road, 13, 6);
        simulation.Builder.Apply(BuildAction.ZoneResidential, 1, 4);
        simulation.Builder.Apply(BuildAction.ZoneIndustrial, 13, 4);
        var phases = new List<(int X, int Phase)>();
        map.TileChanged += (x, _) => phases.Add((x, (int)(simulation.TickCount % CityCalendar.StepsPerCount)));

        Run(simulation, 60 * CityCalendar.StepsPerCount);

        // A zone's houses stand on its outer tiles, so a change can be a column either side of its centre.
        Assert.NotEmpty(phases);
        Assert.All(phases, change => Assert.Equal(change.X <= 2 ? 1 : 7, change.Phase));
    }

    [Fact]
    public void TheSameSeedAndTheSameTicksGiveTheSameCity()
    {
        var first = new CitySimulation(TileMapGenerator.Generate(2000, 16, 16), 2000);
        var second = new CitySimulation(TileMapGenerator.Generate(2000, 16, 16), 2000);

        for (var i = 0; i < 100; i++)
        {
            first.Tick();
            second.Tick();
        }

        Assert.Equal(first.Date, second.Date);
        Assert.Equal(first.CityTime, second.CityTime);
        Assert.Equal(first.TickCount, second.TickCount);
        Assert.Equal(first.Random.NextUInt64(), second.Random.NextUInt64());
    }

    [Fact]
    public void TheCityKeepsTheMapItWasGiven()
    {
        var map = SmallMap();
        var simulation = new CitySimulation(map, 3);

        Assert.Same(map, simulation.Map);
        Assert.Equal(3, simulation.Seed);
    }

    [Fact]
    public void ACityWithoutAMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CitySimulation(null!, 1));
    }

    [Fact]
    public void AServedZoneGetsPeople()
    {
        var simulation = new CitySimulation(FlatMap(), 1);
        simulation.Builder.Apply(BuildAction.Road, 3, 1);
        simulation.Builder.Apply(BuildAction.ZoneResidential, 3, 3);

        for (var i = 0; i < 200 * CityCalendar.StepsPerCount; i++)
        {
            simulation.Tick();
        }

        Assert.True(CityDefinitions.Base.Zone(TileUse.Residential).PopulationOf(simulation.Map, 3, 3) > 0);
    }

    [Fact]
    public void AZoneWithNoRoadNeverDevelops()
    {
        var simulation = new CitySimulation(FlatMap(), 1);
        simulation.Builder.Apply(BuildAction.ZoneResidential, 3, 3);
        simulation.Builder.Apply(BuildAction.Road, 1, 1);

        for (var i = 0; i < 50 * CityCalendar.StepsPerCount; i++)
        {
            simulation.Tick();
        }

        // A road at the zone's corner doesn't serve it, so it never gets anyone.
        Assert.Equal(0, CityDefinitions.Base.Zone(TileUse.Residential).PopulationOf(simulation.Map, 3, 3));
    }

    [Fact]
    public void TheSameSeedAndTheSameWorkGiveTheSameCity()
    {
        var first = new CitySimulation(FlatMap(), 7);
        var second = new CitySimulation(FlatMap(), 7);

        foreach (var city in new[] { first, second })
        {
            city.Builder.Apply(BuildAction.Road, 3, 1);
            city.Builder.Apply(BuildAction.Road, 6, 1);
            city.Builder.Apply(BuildAction.ZoneResidential, 3, 3);
            city.Builder.Apply(BuildAction.ZoneCommercial, 6, 3);
        }

        for (var i = 0; i < 40 * CityCalendar.StepsPerCount; i++)
        {
            first.Tick();
            second.Tick();
        }

        Assert.Equal(first.Map.StateAt(3, 3), second.Map.StateAt(3, 3));
        Assert.Equal(first.Map.StateAt(6, 3), second.Map.StateAt(6, 3));
        Assert.Equal(first.Treasury.Funds, second.Treasury.Funds);
    }

    [Fact]
    public void TheCityStartsWithMoney()
    {
        var simulation = new CitySimulation(SmallMap(), 1);

        Assert.Equal(Core.Economy.Treasury.StartingFunds, simulation.Treasury.Funds);
    }

    private static TileMap SmallMap() => TileMapGenerator.Generate(1, 8, 8);

    private static TileMap FlatMap() => TileMap.FromTerrain(9, 9, new TileKind[81]);

    private static void Run(CitySimulation simulation, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            simulation.Tick();
        }
    }
}
