using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class CitySimulationTests
{
    [Fact]
    public void ATickMovesTheCalendarOneMonth()
    {
        var simulation = new CitySimulation(SmallMap(), 1);

        simulation.Tick();

        Assert.Equal(new GameDate(CitySimulation.StartYear, CitySimulation.StartMonth + 1), simulation.Date);
        Assert.Equal(1, simulation.TickCount);
    }

    [Fact]
    public void TheCityStartsInJanuaryOfTheFirstYear()
    {
        var simulation = new CitySimulation(SmallMap(), 1);

        Assert.Equal(new GameDate(2000, 1), simulation.Date);
        Assert.Equal(0, simulation.TickCount);
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
    public void AZoneWithARoadBesideItDevelops()
    {
        var simulation = new CitySimulation(FlatMap(), 1);
        simulation.Builder.Apply(BuildAction.Road, 3, 1);
        simulation.Builder.Apply(BuildAction.ZoneResidential, 3, 3);

        for (var i = 0; i < 50; i++)
        {
            simulation.Tick();
        }

        Assert.Equal(TileState.MaxDevelopment, simulation.Map.StateAt(3, 3).Development);
        Assert.Equal(0, simulation.Map.StateAt(2, 2).Development);
    }

    [Fact]
    public void AZoneWithNoRoadNeverDevelops()
    {
        var simulation = new CitySimulation(FlatMap(), 1);
        simulation.Builder.Apply(BuildAction.ZoneResidential, 3, 3);
        simulation.Builder.Apply(BuildAction.Road, 1, 1);

        for (var i = 0; i < 50; i++)
        {
            simulation.Tick();
        }

        // A road at the zone's corner doesn't serve it.
        Assert.Equal(0, simulation.Map.StateAt(3, 3).Development);
    }

    [Fact]
    public void AZoneNeverDevelopsPastTheLastLevel()
    {
        var simulation = new CitySimulation(FlatMap(), 1);
        simulation.Builder.Apply(BuildAction.Road, 3, 1);
        simulation.Builder.Apply(BuildAction.ZoneResidential, 3, 3);

        for (var i = 0; i < 200; i++)
        {
            simulation.Tick();
        }

        Assert.Equal(TileState.MaxDevelopment, simulation.Map.StateAt(3, 3).Development);
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

        for (var i = 0; i < 40; i++)
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
}
