using System.Text.Json.Nodes;
using CityLimits.Core.Definitions;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;
using Side = CityLimits.Core.Simulation.CityNeighbours.Side;

namespace CityLimits.Tests;

public class NeighbourTests
{
    private static TileMap Map(int width = 40, int height = 30) => TileMap.FromTerrain(width, height, new TileKind[width * height]);

    private static CitySimulation City(int seed = 7) => new(Map(), seed, funds: 1_000_000);

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    private static void Stroke(CitySimulation city, BuildAction action, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(action, fromX, fromY);
        stroke.DragTo(toX, toY);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(stroke).Status);
    }

    [Fact]
    public void FourNeighboursAreNamedFromTheSeedTheSameEveryTime()
    {
        var first = new CityNeighbours(Map(), 7);
        var again = new CityNeighbours(Map(), 7);

        Assert.Equal(first.All, again.All);
        Assert.Equal([Side.North, Side.East, Side.South, Side.West], first.All.Select(neighbour => neighbour.Side));
        Assert.Equal(4, first.All.Select(neighbour => neighbour.Name).Distinct().Count());
        Assert.All(first.All, neighbour => Assert.InRange(neighbour.PopulationBeside(0), 2000, 10_000));
        Assert.True(first.Of(Side.East).PopulationBeside(10_000) > first.Of(Side.East).PopulationBeside(0));
    }

    [Fact]
    public void WhatReachesAnEdgeConnectsToThatNeighbourAndRoadsAndRailLiftDemand()
    {
        var city = City();
        Stroke(city, BuildAction.Road, 0, 10, 5, 10);
        Stroke(city, BuildAction.Rail, 39, 12, 35, 12);
        Stroke(city, BuildAction.PowerLine, 20, 0, 20, 4);

        Assert.Equal(Connections.Road, city.Neighbours.ConnectionsTo(Side.West));
        Assert.Equal(Connections.Rail, city.Neighbours.ConnectionsTo(Side.East));
        Assert.Equal(Connections.Power, city.Neighbours.ConnectionsTo(Side.North));
        Assert.Equal(Connections.None, city.Neighbours.ConnectionsTo(Side.South));
        Assert.Equal((0, CityNeighbours.RoadDemand, CityNeighbours.RoadDemand + CityNeighbours.RailDemand), city.Neighbours.DemandNudge());
    }

    [Fact]
    public void ANeighbourOffersPowerToACityRunningShortAndBuysWhatOneHasSpare()
    {
        var neighbours = new CityNeighbours(MapWithPowerTo(Side.East), 7);

        Assert.Null(neighbours.Propose(CityNeighbours.DealPopulation - 1, brownedOut: true, 700, 700, 0));
        var buy = neighbours.Propose(5000, brownedOut: true, 700, 700, 0)!;
        Assert.Equal((Side.East, DealKind.CityBuys, CityNeighbours.PowerSold, 180), (buy.Side, buy.Kind, buy.Power, buy.Money));
        Assert.Null(neighbours.Propose(5000, brownedOut: true, 700, 700, 0));

        neighbours.Decline();
        var sell = neighbours.Propose(5000, brownedOut: false, 2000, 1100, 0)!;
        Assert.Equal((DealKind.CitySells, 450, 4 * CityNeighbours.SellPricePer100), (sell.Kind, sell.Power, sell.Money));

        neighbours.Decline();
        Assert.Null(neighbours.Propose(5000, brownedOut: false, 2000, 1700, 0));
        Assert.Null(new CityNeighbours(Map(), 7).Propose(5000, brownedOut: true, 700, 700, 0));
    }

    [Fact]
    public void ADealTradesPowerAndMoneyWhileItsLineIsUpAndEndsWithItsTerm()
    {
        var map = MapWithPowerTo(Side.East);
        var neighbours = new CityNeighbours(map, 7);
        neighbours.Propose(5000, brownedOut: true, 700, 700, 100);
        var deal = neighbours.Accept(100)!;

        Assert.Null(neighbours.Offer);
        Assert.Equal(100 + ((long)CityNeighbours.TermYears * CityCalendar.StepsPerYear), deal.EndStep);
        Assert.Equal(CityNeighbours.PowerSold, neighbours.PowerTraded());
        Assert.Equal((0, 180), neighbours.YearlyMoney());

        map.SetState(39, 5, new TileState(TileKind.Grass));
        Assert.Equal(0, neighbours.PowerTraded());
        Assert.Equal((0, 0), neighbours.YearlyMoney());

        var (ended, _) = neighbours.Expire(deal.EndStep);
        Assert.Equal([deal], ended);
        Assert.Empty(neighbours.Deals);
    }

    [Fact]
    public void AnOfferLapsesUnanswered()
    {
        var neighbours = new CityNeighbours(MapWithPowerTo(Side.East), 7);
        var offer = neighbours.Propose(5000, brownedOut: true, 700, 700, 0)!;

        Assert.Null(neighbours.Expire(offer.ExpiresStep - 1).Lapsed);
        Assert.Equal(offer, neighbours.Expire(offer.ExpiresStep).Lapsed);
        Assert.Null(neighbours.Offer);
    }

    [Fact]
    public void BreakingADealCostsAYearOfItsMoneyAndTheNeighboursGoodwill()
    {
        var city = City();
        Stroke(city, BuildAction.PowerLine, 39, 5, 35, 5);
        var news = new List<NeighbourNews>();
        city.NeighbourNews += news.Add;
        city.Neighbours.Propose(5000, brownedOut: true, 700, 700, city.TickCount);
        Assert.True(city.AcceptOffer());
        var funds = city.Treasury.Funds;

        Assert.True(city.BreakDeal(Side.East));

        Assert.Equal(funds - 180, city.Treasury.Funds);
        Assert.True(city.Neighbours.HoldsGrudge(Side.East, city.TickCount));
        Assert.Null(city.Neighbours.Propose(5000, brownedOut: true, 700, 700, city.TickCount));
        Assert.False(city.Neighbours.HoldsGrudge(Side.East, city.TickCount + ((long)CityNeighbours.GrudgeYears * CityCalendar.StepsPerYear)));
        Assert.Equal([NeighbourNewsKind.Accepted, NeighbourNewsKind.Broken], news.Select(item => item.Kind));
        Assert.False(city.BreakDeal(Side.East));
    }

    [Fact]
    public void BoughtPowerAddsToThePlantsCapacityAndTheBudgetPaysForIt()
    {
        var city = City();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 10, 10);
        Stroke(city, BuildAction.PowerLine, 39, 5, 35, 5);
        city.Neighbours.Propose(5000, brownedOut: true, 700, 700, city.TickCount);
        city.AcceptOffer();

        Run(city, 2 * CityCalendar.StepsPerCount);

        Assert.Equal(CityDefinitions.Base.Plant(TileUse.CoalPowerPlant).Capacity + CityNeighbours.PowerSold, city.Power.Capacity);
        Assert.Equal((0, 180), (city.EstimateBudget().DealIncome, city.EstimateBudget().DealCost));
    }

    [Fact]
    public void ACitySavedWithADealAndAnOfferRunsInStepWithOneThatWasnt()
    {
        var city = City();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 10, 10);
        Stroke(city, BuildAction.PowerLine, 39, 5, 35, 5);
        Stroke(city, BuildAction.PowerLine, 20, 0, 20, 4);
        city.Neighbours.Propose(5000, brownedOut: true, 700, 700, city.TickCount);
        city.AcceptOffer();
        city.Neighbours.Propose(5000, brownedOut: false, 2000, 100, city.TickCount);
        Run(city, (3 * CityCalendar.StepsPerCount) + 5);

        foreach (var reopened in new[] { Reflat(city), Rebinary(city) })
        {
            Assert.Single(reopened.Neighbours.Deals);
            Assert.Equal(city.Neighbours.Offer, reopened.Neighbours.Offer);
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

    [Theory]
    [InlineData("side", "up", "connections.deals[0].side: 'up' is not north, east, south, or west.")]
    [InlineData("kind", "gift", "connections.deals[0].kind: 'gift' is not cityBuys or citySells.")]
    public void ADealThatIsWrongIsRefusedAndSaysWhere(string field, string value, string expected)
    {
        var city = City();
        Stroke(city, BuildAction.PowerLine, 39, 5, 35, 5);
        city.Neighbours.Propose(5000, brownedOut: true, 700, 700, 0);
        city.AcceptOffer();
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["connections"]!["deals"]![0]![field] = value;

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    // A map with a power line running off its edge on one side, five tiles from the corner.
    private static TileMap MapWithPowerTo(Side side)
    {
        var map = Map();
        var (x, y) = side switch
        {
            Side.North => (5, 0),
            Side.South => (5, 29),
            Side.West => (0, 5),
            _ => (39, 5),
        };
        map.SetState(x, y, new TileState(TileKind.Grass, TileUse.PowerLine));
        return map;
    }
}
