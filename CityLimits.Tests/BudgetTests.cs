using CityLimits.Core.Economy;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class BudgetTests
{
    // It stops a step short of the next count, whose first phase starts the road count again.
    private static void RunCounts(CitySimulation city, int counts)
    {
        for (var i = 0; i < (counts * CityCalendar.StepsPerCount) - 1; i++)
        {
            city.Tick();
        }
    }

    private static CitySimulation CityWithRoads(int width, int height, int roadRow)
    {
        var city = new CitySimulation(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1);
        var road = new BuildStroke(BuildAction.Road, 0, roadRow);
        road.DragTo(width - 1, roadRow);
        city.Builder.Apply(road);
        return city;
    }

    [Fact]
    public void TaxesComeFromPopulationAndLandValueAtTheTaxRate()
    {
        // 800 times 60, over 120, is 400; at 7% and Easy's 1.4, 3 920.
        var year = new CityBudget().Estimate(population: 800, landValueAverage: 60, roadTiles: 0, fireStations: 0, policeStations: 0, funds: 0);

        Assert.Equal(3920, year.TaxIncome);
        Assert.Equal(7, year.TaxRate);
    }

    [Fact]
    public void PopulationAndLandValueAreDividedAsWholeNumbersFirst()
    {
        // 7 times 10 is 70, and 70 over 120 is nothing, as in Micropolis.
        Assert.Equal(0, new CityBudget().Estimate(7, 10, 0, 0, 0, 0).TaxIncome);
    }

    [Theory]
    [InlineData(9, 6)]
    [InlineData(10, 7)]
    [InlineData(5, 4)]
    public void RoadUpkeepIsTheRoadTilesTimesEasysFactor(int roadTiles, int upkeep)
    {
        var year = new CityBudget().Estimate(0, 0, roadTiles, 0, 0, 1000);

        Assert.Equal(upkeep, year.RoadRequest);
        Assert.Equal(upkeep, year.RoadFunded);
    }

    [Fact]
    public void StationsCostAHundredAYearAndFundingScalesWhatIsPaid()
    {
        var budget = new CityBudget();
        budget.SetFunding(roads: 50, fire: 100, police: 25);

        var year = budget.Estimate(0, 0, roadTiles: 20, fireStations: 2, policeStations: 3, funds: 10_000);

        Assert.Equal((14, 7), (year.RoadRequest, year.RoadFunded));
        Assert.Equal((200, 200), (year.FireRequest, year.FireFunded));
        Assert.Equal((300, 75), (year.PoliceRequest, year.PoliceFunded));
        Assert.False(year.Cut);
    }

    [Fact]
    public void WhenTheMoneyRunsOutRoadsArePaidFirstThenFireThenPolice()
    {
        var budget = new CityBudget();

        var roadsShort = budget.Estimate(0, 0, roadTiles: 100, fireStations: 1, policeStations: 1, funds: 30);
        var fireShort = budget.Estimate(0, 0, roadTiles: 100, fireStations: 1, policeStations: 1, funds: 120);
        var policeShort = budget.Estimate(0, 0, roadTiles: 100, fireStations: 1, policeStations: 1, funds: 200);

        Assert.Equal((30, 0, 0, true), (roadsShort.RoadFunded, roadsShort.FireFunded, roadsShort.PoliceFunded, roadsShort.Cut));
        Assert.Equal((70, 50, 0, true), (fireShort.RoadFunded, fireShort.FireFunded, fireShort.PoliceFunded, fireShort.Cut));
        Assert.Equal((70, 100, 30, true), (policeShort.RoadFunded, policeShort.FireFunded, policeShort.PoliceFunded, policeShort.Cut));
    }

    [Fact]
    public void TheYearsTaxesHelpPayItsUpkeep()
    {
        // No money, but the year's taxes, 120 times 10 over 120, times 7 and 1.4, cover the roads.
        var year = new CityBudget().Estimate(population: 120, landValueAverage: 10, roadTiles: 10, 0, 0, funds: 0);

        Assert.Equal(98, year.TaxIncome);
        Assert.Equal(7, year.RoadFunded);
        Assert.False(year.Cut);
    }

    [Fact]
    public void FundingSetsHowWellEachKindOfUpkeepWorks()
    {
        var budget = new CityBudget();
        budget.SetFunding(roads: 50, fire: 100, police: 0);

        budget.Accrue(budget.Estimate(0, 0, roadTiles: 20, fireStations: 0, policeStations: 1, funds: 10_000));

        Assert.Equal(16, budget.RoadEffect);
        Assert.Equal(CityBudget.FullServiceEffect, budget.FireEffect);
        Assert.Equal(0, budget.PoliceEffect);
    }

    [Fact]
    public void AYearIsSettledFromAFortyEighthOfEachCountsEstimate()
    {
        var budget = new CityBudget();
        var treasury = new Treasury(1000);
        for (var count = 0; count < CityBudget.CountsPerYear; count++)
        {
            budget.Accrue(budget.Estimate(population: 120, landValueAverage: 60, roadTiles: 100, 0, 0, treasury.Funds));
        }

        var year = budget.Settle(treasury);

        // Taxes 120 times 60 over 120, times 7 and 1.4, are 588; roads are 70.
        Assert.Equal((588, 70, 518), (year.TaxIncome, year.RoadFunded, year.CashFlow));
        Assert.Equal(1518, treasury.Funds);
        Assert.Same(year, budget.LastYear);
        Assert.Equal(0, budget.Settle(treasury).CashFlow);
    }

    [Fact]
    public void SettlingCanTakeTheCityBelowZero()
    {
        var budget = new CityBudget();
        var treasury = new Treasury(100);
        for (var count = 0; count < CityBudget.CountsPerYear; count++)
        {
            budget.Accrue(budget.Estimate(0, 0, roadTiles: 100, 0, 0, funds: 100));
        }

        treasury.TrySpend(100);
        var year = budget.Settle(treasury);

        Assert.Equal(-70, treasury.Funds);
        Assert.True(year.IsDeficit);
        Assert.True(year.NeedsAttention);
    }

    [Fact]
    public void AYearNeedsAttentionOnADeficitOrACut()
    {
        Assert.False(new BudgetYear(7, 100, 0, 50, 0, 0, 0, 0, Cut: false).NeedsAttention);
        Assert.True(new BudgetYear(7, 10, 0, 50, 0, 0, 0, 0, Cut: false).NeedsAttention);
        Assert.True(new BudgetYear(7, 100, 0, 50, 0, 0, 0, 0, Cut: true).NeedsAttention);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void ATaxRateTheCityDoesntHaveIsRefused(int rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new CityBudget().SetTaxRate(rate));

    [Fact]
    public void AFundingLevelTheCityDoesntHaveIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CityBudget().SetFunding(101, 100, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CityBudget().SetFunding(100, -1, 100));
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(7, 0)]
    [InlineData(12, -200)]
    [InlineData(20, -600)]
    public void TheTaxRatePushesEveryDemand(int rate, int push)
    {
        // An empty city wants no commerce (-600) and the least industry (+600), whole numbers both,
        // and residents at 1.3 (about +180), which is truncated after the push, as in Micropolis.
        var atDefault = new CityDemand();
        atDefault.Update(new CityCensus());
        var atRate = new CityDemand();
        atRate.Update(new CityCensus(), rate);

        Assert.InRange(atRate.Residential - (atDefault.Residential + push), -1, 1);
        Assert.Equal(Math.Clamp(atDefault.Commercial + push, -1500, 1500), atRate.Commercial);
        Assert.Equal(Math.Clamp(atDefault.Industrial + push, -1500, 1500), atRate.Industrial);
    }

    [Fact]
    public void ACityPaysForItsRoadsOnceAYear()
    {
        // 40 road tiles cost 28 a year; nothing is taxed yet.
        var city = CityWithRoads(40, 3, 1);
        var settled = new List<BudgetYear>();
        city.YearSettled += settled.Add;
        var funds = city.Treasury.Funds;

        RunCounts(city, CityBudget.CountsPerYear + 1);

        Assert.Equal(2, settled.Count);
        Assert.Equal(-28, settled[1].CashFlow);
        Assert.Equal(funds - 28, city.Treasury.Funds);
        Assert.Equal(40, city.RoadTiles);
    }

    [Fact]
    public void AFreshEstimateFollowsTheRatesSetSinceTheLastCount()
    {
        // 40 road tiles ask 28; the estimate is worked from the roads the last count saw.
        var city = CityWithRoads(40, 3, 1);
        RunCounts(city, 2);

        city.Budget.SetTaxRate(15);
        city.Budget.SetFunding(roads: 50, fire: 100, police: 100);
        var estimate = city.EstimateBudget();

        Assert.Equal(15, estimate.TaxRate);
        Assert.Equal((28, 14), (estimate.RoadRequest, estimate.RoadFunded));
        Assert.Equal(CityBudget.DefaultTaxRate, city.Budget.TaxEffect);
    }

    [Fact]
    public void ABridgeCountsAsFiveRoadTiles()
    {
        var map = TileMap.FromTerrain(5, 1, [TileKind.Grass, TileKind.Grass, TileKind.Water, TileKind.Grass, TileKind.Grass]);
        var city = new CitySimulation(map, 1);
        var road = new BuildStroke(BuildAction.Road, 0, 0);
        road.DragTo(4, 0);
        city.Builder.Apply(road);

        RunCounts(city, 1);

        Assert.Equal(4 + 5, city.RoadTiles);
    }

    [Fact]
    public void UnfundedRoadsCrumbleButCrossingsDont()
    {
        var city = CityWithRoads(60, 5, 2);
        city.Builder.Apply(BuildAction.PowerLine, 30, 1);
        var line = new BuildStroke(BuildAction.PowerLine, 30, 1);
        line.DragTo(30, 3);
        city.Builder.Apply(line);
        city.Budget.SetFunding(roads: 0, fire: 100, police: 100);
        var crumbled = new List<(int X, int Y, TileState State)>();
        city.Map.TileChanged += (x, y) => crumbled.Add((x, y, city.Map.StateAt(x, y)));

        RunCounts(city, 400);

        Assert.Equal(0, city.Budget.RoadEffect);
        Assert.NotEmpty(crumbled);
        Assert.All(crumbled, change => Assert.Equal(new TileState(TileKind.Rubble), change.State));
        Assert.Equal(TileUse.RoadAcrossPowerLine, city.Map.StateAt(30, 2).Use);
    }

    [Fact]
    public void AnUnfundedBridgeCrumblesBackIntoWater()
    {
        // A bridge 50 tiles long, with land at both ends.
        var kinds = Enumerable.Range(0, 60).Select(x => x is >= 5 and < 55 ? TileKind.Water : TileKind.Grass).ToArray();
        var city = new CitySimulation(TileMap.FromTerrain(60, 1, kinds), 1);
        var road = new BuildStroke(BuildAction.Road, 0, 0);
        road.DragTo(59, 0);
        city.Builder.Apply(road);
        city.Budget.SetFunding(roads: 0, fire: 100, police: 100);
        var crumbledOverWater = new List<TileState>();
        city.Map.TileChanged += (x, _) =>
        {
            if (x is >= 5 and < 55)
            {
                crumbledOverWater.Add(city.Map.StateAt(x, 0));
            }
        };

        RunCounts(city, 400);

        Assert.NotEmpty(crumbledOverWater);
        Assert.All(crumbledOverWater, state => Assert.Equal(new TileState(TileKind.Water), state));
    }

    [Fact]
    public void FundedRoadsNeverCrumble()
    {
        var city = CityWithRoads(60, 5, 2);
        var changed = false;
        city.Map.TileChanged += (_, _) => changed = true;

        RunCounts(city, 400);

        Assert.False(changed);
    }

    [Fact]
    public void ACityIsToldWhenTaxesAreTooHighAndRoadsNeedFunding()
    {
        var city = CityWithRoads(40, 3, 1);
        city.Budget.SetTaxRate(13);
        city.Budget.SetFunding(roads: 0, fire: 100, police: 100);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCounts(city, 64);

        Assert.Contains(CityMessage.HighTaxes, heard);
        Assert.Contains(CityMessage.RoadsNeedFunding, heard);
    }

    [Fact]
    public void AFairlyTaxedFundedCityHearsNothingAboutItsBudget()
    {
        var city = CityWithRoads(40, 3, 1);
        city.Budget.SetTaxRate(12);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCounts(city, 64);

        Assert.DoesNotContain(CityMessage.HighTaxes, heard);
        Assert.DoesNotContain(CityMessage.RoadsNeedFunding, heard);
    }
}
