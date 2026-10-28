using System.Text.Json.Nodes;
using CityLimits.Core.Definitions;
using CityLimits.Core.Economy;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class OrdinanceTests
{
    private static CitySimulation Flat(int width = 32, int height = 32) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, funds: 1_000_000);

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // A city as its save holds it, with some of the save changed first.
    private static CitySimulation Edited(CitySimulation city, Action<JsonObject> edit)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        edit(save);
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var edited, out var problem), problem);
        return edited;
    }

    [Fact]
    public void TheBaseGameHasItsTenOrdinancesNoneEnacted()
    {
        var city = Flat();

        Assert.Equal(
            ["salesTax", "incomeTax", "legalizedGambling", "parkingFines", "neighbourhoodWatch", "smokeDetectors", "pollutionControls", "energyConservation", "tourismPromotion", "industrialPromotion"],
            city.Ordinances.All.Select(ordinance => ordinance.Id));
        Assert.Empty(city.Ordinances.Enacted);
        Assert.All(Enum.GetValues<OrdinanceEffect>(), effect => Assert.Equal(0, city.Ordinances.Effect(effect)));
    }

    [Fact]
    public void EnactingAndRepealingSaySoAndChangeWhatTheyAddUp()
    {
        var city = Flat();
        var news = new List<OrdinanceNews>();
        city.OrdinanceNews += news.Add;

        Assert.True(city.SetOrdinance("salesTax", true));
        Assert.True(city.SetOrdinance("tourismPromotion", true));
        Assert.False(city.SetOrdinance("salesTax", true));

        Assert.Equal(10, city.Ordinances.Effect(OrdinanceEffect.CommercialDemand));
        Assert.Equal((6 * 10, 5 * 10), city.Ordinances.YearlyMoney(10_000));

        Assert.True(city.SetOrdinance("salesTax", false));
        Assert.Equal(50, city.Ordinances.Effect(OrdinanceEffect.CommercialDemand));
        Assert.Equal(
            [("salesTax", OrdinanceNewsKind.Enacted), ("tourismPromotion", OrdinanceNewsKind.Enacted), ("salesTax", OrdinanceNewsKind.Repealed)],
            news.Select(item => (item.Ordinance.Id, item.Kind)));
        Assert.Throws<ArgumentException>(() => city.SetOrdinance("curfew", true));
    }

    [Fact]
    public void TheBudgetCollectsWhatOrdinancesEarnAndPaysWhatTheyCost()
    {
        var budget = new CityBudget();

        var estimate = budget.Estimate(population: 100, landValueAverage: 120, roadTiles: 0, fireStations: 0, policeStations: 0, funds: 0, ordinanceIncome: 60, ordinanceCost: 50);

        Assert.Equal(60, estimate.OrdinanceIncome);
        Assert.Equal(50, estimate.OrdinanceCost);
        Assert.Equal(estimate.TaxIncome + 60 - 50, estimate.CashFlow);

        var broke = budget.Estimate(population: 0, landValueAverage: 0, roadTiles: 0, fireStations: 0, policeStations: 0, funds: 0, ordinanceIncome: 0, ordinanceCost: 50);
        Assert.Equal(0, broke.OrdinanceCost);
        Assert.True(broke.Cut);

        for (var count = 0; count < CityBudget.CountsPerYear; count++)
        {
            budget.Accrue(estimate);
        }

        var treasury = new Treasury(0);
        var year = budget.Settle(treasury);
        Assert.Equal((60, 50), (year.OrdinanceIncome, year.OrdinanceCost));
        Assert.Equal(year.CashFlow, treasury.Funds);
    }

    [Fact]
    public void EachEffectReachesWhatItChanges()
    {
        var demand = new CityDemand();
        var plain = new CityDemand();
        demand.Update(new CityCensus(), nudge: (100, -100, 50));
        plain.Update(new CityCensus());
        Assert.Equal((plain.Residential + 100, plain.Commercial - 100, plain.Industrial + 50), (demand.Residential, demand.Commercial, demand.Industrial));

        var city = Flat();
        city.SetOrdinance("parkingFines", true);
        city.SetOrdinance("pollutionControls", true);
        Assert.Equal(90, city.Traffic.TripPercent);
        Assert.Equal(70, city.Maps.IndustryPollutionPercent);

        city.SetOrdinance("energyConservation", true);
        Assert.Equal(115, city.Ordinances.Scaled(OrdinanceEffect.PowerCapacity, 100));
        city.Builder.Apply(BuildAction.CoalPowerPlant, 10, 10);
        Run(city, 2 * CityCalendar.StepsPerCount);
        Assert.Equal(CityDefinitions.Base.Plant(TileUse.CoalPowerPlant).Capacity * 115 / 100, city.Power.Capacity);
    }

    [Fact]
    public void ANeighbourhoodWatchLowersCrimeEverywhere()
    {
        var map = TileMap.FromTerrain(128, 128, new TileKind[128 * 128]);
        map.SetState(0, 0, new TileState(TileKind.Grass, TileUse.Road));
        var maps = new CityMaps(map);
        maps.UpdateLandValue(new TrafficMap(map), new Fires(map));

        maps.UpdateCrime(new ServiceCoverage(map), ordinances: -15);

        Assert.Equal(120 - 15, maps.CrimeAt(0, 0));
    }

    [Theory]
    [InlineData(true, 0, 0, null, "energyConservation")]
    [InlineData(false, 0, 0, null, null)]
    [InlineData(false, -600, 0, null, "tourismPromotion")]
    [InlineData(false, 0, -600, null, "industrialPromotion")]
    [InlineData(true, 0, 0, "energyConservation", null)]
    public void TheCityAsksForTheOrdinanceThatWouldHelpItMost(bool brownedOut, int commercial, int industrial, string? enacted, string? wanted)
    {
        var city = Edited(Flat(), save =>
        {
            save["census"]!["residentPopulation"] = 5000;
            save["power"]!["brownedOut"] = brownedOut;
            save["demand"]!["commercial"] = commercial;
            save["demand"]!["industrial"] = industrial;
        });
        if (enacted is not null)
        {
            city.SetOrdinance(enacted, true);
        }

        Assert.Equal(wanted, city.Ordinances.MostWanted(city)?.Id);
    }

    [Fact]
    public void ASmallCityAsksForNothing()
    {
        var city = Edited(Flat(), save =>
        {
            save["census"]!["residentPopulation"] = CityOrdinances.PetitionPopulation - 1;
            save["power"]!["brownedOut"] = true;
        });

        Assert.Null(city.Ordinances.MostWanted(city));
    }

    [Fact]
    public void ACitySavedWithOrdinancesPartwayThroughAYearRunsInStepWithOneThatWasnt()
    {
        var city = Flat();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 10, 10);
        city.SetOrdinance("smokeDetectors", true);
        city.SetOrdinance("salesTax", true);
        Run(city, (5 * CityCalendar.StepsPerCount) + 3);

        foreach (var reopened in new[] { Reflat(city), Rebinary(city) })
        {
            Assert.Equal(["salesTax", "smokeDetectors"], reopened.Ordinances.Enacted);
            var copy = Reflat(city);
            Run(copy, CityCalendar.StepsPerYear);
            Run(reopened, CityCalendar.StepsPerYear);
            Assert.Equal(FlatCityFormat.Write(copy), FlatCityFormat.Write(reopened));
        }

        static CitySimulation Reflat(CitySimulation city)
        {
            Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var reopened, out var problem), problem);
            return reopened;
        }

        static CitySimulation Rebinary(CitySimulation city)
        {
            Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var reopened, out var problem, out _), problem);
            return reopened;
        }
    }

    [Fact]
    public void ASaveWithoutOrdinancesOpensWithNoneAndOneFor04LeavesThemOut()
    {
        var city = Flat();
        city.SetOrdinance("incomeTax", true);

        var without = Edited(city, save => save.Remove("ordinances"));
        Assert.Empty(without.Ordinances.Enacted);

        var for04 = JsonNode.Parse(FlatCityFormat.Write(city, legacy: true))!.AsObject();
        Assert.Null(for04["ordinances"]);
        Assert.DoesNotContain(for04["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "ordinances");
    }

    [Fact]
    public void ASavedOrdinanceTheDefinitionsDontHaveIsRefused()
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Flat()))!.AsObject();
        save["ordinances"]!["enacted"] = new JsonArray(JsonValue.Create("curfew"));

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal("ordinances.enacted: 'curfew' is not an ordinance.", problem);
    }
}
