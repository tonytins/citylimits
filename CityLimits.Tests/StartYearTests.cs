using CityLimits.Core.Economy;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class StartYearTests
{
    private static CitySimulation Town(int startYear)
    {
        var city = new CitySimulation(TileMap.FromTerrain(20, 12, new TileKind[240]), 3, startYear: startYear);
        var road = new BuildStroke(BuildAction.Road, 2, 6);
        road.DragTo(17, 6);
        city.Builder.Apply(road);
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);
        return city;
    }

    [Fact]
    public void ACityStartsIn1900UnlessToldAnotherYear()
    {
        Assert.Equal(new GameDate(1900, 1), Town(CityCalendar.StartYear).Date);
        Assert.Equal(new GameDate(1906, 1), Town(1906).Date);
    }

    [Fact]
    public void ALaterStartCountsAsIfEveryStepSince1900HadRun()
    {
        var city = Town(1906);

        Assert.Equal(6L * CityCalendar.StepsPerYear, city.TickCount);
        Assert.Equal(city.TickCount / CityCalendar.StepsPerCount, city.CityTime);
    }

    [Fact]
    public void ACityStartedLaterRunsItsYearsAsOneStartedIn1900()
    {
        var settled = new List<BudgetYear>();
        var city = Town(1906);
        city.YearSettled += settled.Add;

        for (var i = 0; i < CityCalendar.StepsPerYear + CityCalendar.StepsPerCount; i++)
        {
            city.Tick();
        }

        // As in 1900, the year is settled at the very start and again a year on.
        Assert.Equal(2, settled.Count);
        Assert.Equal(new GameDate(1907, 1), city.Date);
    }

    [Fact]
    public void ACityStartedLaterSavesAndLoadsAtItsDate()
    {
        var city = Town(1944);
        for (var i = 0; i < 100; i++)
        {
            city.Tick();
        }

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);

        Assert.Equal(city.Date, flat.Date);
        Assert.Equal(city.TickCount, binary.TickCount);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(10_000)]
    public void AYearTheCalendarCantShowIsRefused(int year)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CitySimulation(TileMap.FromTerrain(4, 4, new TileKind[16]), 1, startYear: year));
    }
}
