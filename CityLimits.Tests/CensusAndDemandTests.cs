using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class CensusAndDemandTests
{
    private static CityCensus Census(int residential, int commercial, int industrial)
    {
        var census = new CityCensus();
        census.Add(TileUse.Residential, residential);
        census.Add(TileUse.Commercial, commercial);
        census.Add(TileUse.Industrial, industrial);
        return census;
    }

    private static void Run(CitySimulation city, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    [Fact]
    public void AnEmptyCityWantsResidentsAndIndustryButNotCommerce()
    {
        var demand = new CityDemand();

        demand.Update(new CityCensus());

        Assert.Equal(180, demand.Residential);
        Assert.Equal(-600, demand.Commercial);
        Assert.Equal(600, demand.Industrial);
    }

    [Fact]
    public void ResidentsWithNoJobsLeaveAndTheCityWantsBusiness()
    {
        var demand = new CityDemand();

        // 160 residents are 20 in Micropolis' eighths; no jobs, so nearly all of them would move out.
        demand.Update(Census(160, 0, 0));

        Assert.Equal(-588, demand.Residential);
        Assert.Equal(600, demand.Commercial);
        Assert.Equal(600, demand.Industrial);
    }

    [Fact]
    public void TheDemandSaysWhatMovedIt()
    {
        var demand = new CityDemand();

        demand.Update(Census(160, 0, 0), taxRate: 0);

        Assert.Equal(-588, demand.ResidentialPull);
        Assert.Equal(600, demand.CommercialPull);
        Assert.Equal(600, demand.IndustrialPull);
        Assert.Equal(200, demand.TaxPushApplied);
        Assert.Equal(0.0, demand.JobsPerResident);
        Assert.Equal(-588 + 200, demand.Residential);
    }

    [Fact]
    public void AnEmptyCitysPullsAreItsFirstMoves()
    {
        var demand = new CityDemand();

        demand.Update(new CityCensus());

        Assert.Equal((demand.Residential, demand.Commercial, demand.Industrial), (demand.ResidentialPull, demand.CommercialPull, demand.IndustrialPull));
        Assert.Equal(0, demand.TaxPushApplied);
        Assert.Equal(1.0, demand.JobsPerResident);
        Assert.Equal(1.0, demand.WorkersPerJob);
    }

    [Fact]
    public void DemandStopsAtItsLimits()
    {
        var demand = new CityDemand();
        var census = Census(160, 0, 0);

        for (var i = 0; i < 10; i++)
        {
            demand.Update(census);
        }

        Assert.Equal(-CityDemand.ResidentialLimit, demand.Residential);
        Assert.Equal(CityDemand.BusinessLimit, demand.Commercial);
        Assert.Equal(CityDemand.BusinessLimit, demand.Industrial);
    }

    [Fact]
    public void TheMonthIsRecordedAndTheOlderMonthsMoveBack()
    {
        var census = Census(160, 3, 2);
        census.RecordMonth();
        census.Clear();
        census.Add(TileUse.Commercial, 7);

        census.RecordMonth();

        Assert.Equal(7, census.CommercialHistory(0));
        Assert.Equal(3, census.CommercialHistory(1));
        Assert.Equal(20, census.ResidentialHistory(1));
        Assert.Equal(2, census.IndustrialHistory(1));
        Assert.Equal(7 * 8 * 20, census.CityPopulation);
    }

    [Fact]
    public void TheCityPopulationIsResidentsAndEightPerJobTimesTwenty()
    {
        var census = Census(40, 5, 4);

        census.RecordMonth();

        Assert.Equal((40 + (5 * 8) + (4 * 8)) * 20, census.CityPopulation);
        Assert.Equal(40 * 20, census.ResidentPopulation);
        Assert.Equal(5 * 8 * 20, census.CommercialPopulation);
        Assert.Equal(4 * 8 * 20, census.IndustrialPopulation);
    }

    [Fact]
    public void DemandMovesEverySecondCountFromTheLastCyclesCensus()
    {
        var city = new CitySimulation(TileMap.FromTerrain(9, 9, new TileKind[81]), 1);

        Run(city, CityCalendar.StepsPerCount);
        Assert.Equal(0, city.Demand.Residential);

        Run(city, CityCalendar.StepsPerCount);
        Assert.Equal(180, city.Demand.Residential);
        Assert.Equal(-600, city.Demand.Commercial);
    }

    [Fact]
    public void TheScanCountsEachZoneOnceACycle()
    {
        var map = TileMap.FromTerrain(16, 9, new TileKind[16 * 9]);
        var city = new CitySimulation(map, 1);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 2);
        city.Builder.Apply(BuildAction.ZoneCommercial, 13, 2);
        city.Builder.Apply(BuildAction.ZoneIndustrial, 7, 6);
        map.SetState(2, 2, new TileState(TileKind.Grass, TileUse.Residential, development: 2));
        map.SetState(13, 2, new TileState(TileKind.Grass, TileUse.Commercial, development: 3));
        map.SetState(7, 6, new TileState(TileKind.Grass, TileUse.Industrial, development: 4));

        // Phases 1 to 8 scan the map, and phase 9 records the month: in the first cycle, at
        // count 0, as Micropolis does, and then every fourth count.
        Run(city, 9);

        Assert.Equal((24, 3, 4), (city.Census.Residential, city.Census.Commercial, city.Census.Industrial));
        Assert.Equal(3, city.Census.ResidentialHistory(0));
        Assert.Equal(3, city.Census.CommercialHistory(0));

        Run(city, (3 * CityCalendar.StepsPerCount) + 1);
        Assert.Equal(0, city.Census.CommercialHistory(1));

        Run(city, CityCalendar.StepsPerCount);

        // The first month moves back one; the zones, with no road, have declined since.
        Assert.Equal(3, city.Census.CommercialHistory(1));
    }
}
