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

    private static TileMap SmallMap() => TileMapGenerator.Generate(1, 8, 8);
}
