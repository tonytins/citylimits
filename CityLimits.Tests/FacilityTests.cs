using System.Text.Json.Nodes;
using CityLimits.Core.Economy;
using CityLimits.Core.Saving;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class FacilityTests
{
    private static CitySimulation Flat(int width = 32, int height = 32, int funds = Treasury.StartingFunds) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, funds);

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // Runs the city to the step before its messages, in the cycle of the given count of the 64.
    private static void RunToMessagesAt(CitySimulation city, int count)
    {
        while (!(city.CityTime % 64 == count && city.TickCount % CityCalendar.StepsPerCount == 9))
        {
            city.Tick();
        }
    }

    // The city with a census counted as given, as a save holds it, so the next messages read it.
    private static CitySimulation WithCensus(CitySimulation city, int residential = 0, int commercial = 0, int industrial = 0)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["census"]!["residential"] = residential;
        save["census"]!["commercial"] = commercial;
        save["census"]!["industrial"] = industrial;
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var counted, out var problem), problem);
        return counted;
    }

    [Theory]
    [InlineData(BuildAction.Stadium, TileUse.Stadium, 4, TileCosts.Stadium)]
    [InlineData(BuildAction.Seaport, TileUse.Seaport, 4, TileCosts.Seaport)]
    [InlineData(BuildAction.Airport, TileUse.Airport, 6, TileCosts.Airport)]
    public void EachIsPlacedWholeCentredOnThePointerAtMicropolisPrice(BuildAction action, TileUse use, int size, int price)
    {
        var city = Flat();

        var result = city.Builder.Apply(action, 10, 10);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(price, result.Cost);
        Assert.Equal(Treasury.StartingFunds - price, city.Treasury.Funds);
        Assert.Equal(price, city.Definitions.CostOf(action));
        for (var part = 0; part < size * size; part++)
        {
            var state = city.Map.StateAt(9 + (part % size), 9 + (part / size));
            Assert.Equal(use, state.Use);
            Assert.Equal(part, state.ZonePart);
        }

        Assert.True(city.Map.StateAt(10, 10).IsBuildingCenter);
        Assert.Equal(TileUse.None, city.Map.StateAt(9 + size, 10).Use);
    }

    [Fact]
    public void AnAirportGoesOnlyWhereAllSixBySixIsClearAndComesDownThroughItsCentre()
    {
        var city = Flat();
        var water = city.Map.StateAt(14, 14);
        city.Map.SetState(14, 14, new TileState(TileKind.Water));

        Assert.Equal(BuildStatus.Refused, city.Builder.Apply(BuildAction.Airport, 10, 10).Status);
        Assert.Equal(TileUse.None, city.Map.StateAt(10, 10).Use);

        city.Map.SetState(14, 14, water);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.Airport, 10, 10).Status);

        var corner = new BuildStroke(BuildAction.Bulldoze, 14, 14);
        Assert.Equal(BuildRefusal.PartOfAZone, city.Builder.Apply(corner).Reason);

        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.Bulldoze, 10, 10).Status);
        Assert.Equal(new TileState(TileKind.Rubble), city.Map.StateAt(14, 14));
        Assert.Equal(new TileState(TileKind.Rubble), city.Map.StateAt(9, 9));
    }

    [Fact]
    public void ABigCityWithoutAStadiumHasItsResidentialDemandHeldAtZero()
    {
        var city = Flat();
        RunToMessagesAt(city, 26);
        var counted = WithCensus(city, residential: 501);
        var heard = new List<CityMessage>();
        counted.MessageSent += heard.Add;

        Run(counted, 1);

        Assert.Contains(CityMessage.NeedStadium, heard);
        Assert.Equal(DemandCaps.Residential, counted.Facilities.Caps);

        // Held until the city checks again, so every update in between keeps it at zero or below.
        var free = Flat();
        RunToMessagesAt(free, 26);
        Run(free, 1);
        Run(counted, CityCalendar.StepsPerCount * 8);
        Run(free, CityCalendar.StepsPerCount * 8);
        Assert.True(free.Demand.Residential > 0, $"free {free.Demand.Residential}");
        Assert.True(counted.Demand.Residential <= 0, $"held {counted.Demand.Residential}");
    }

    [Fact]
    public void AStadiumLiftsTheHold()
    {
        var city = Flat();
        city.Builder.Apply(BuildAction.Stadium, 10, 10);
        RunToMessagesAt(city, 26);
        var counted = WithCensus(city, residential: 501);
        var heard = new List<CityMessage>();
        counted.MessageSent += heard.Add;

        Run(counted, 1);

        Assert.Equal(1, counted.Facilities.Stadiums);
        Assert.DoesNotContain(CityMessage.NeedStadium, heard);
        Assert.Equal(DemandCaps.None, counted.Facilities.Caps);
    }

    [Theory]
    [InlineData(28, 0, 71, CityMessage.NeedSeaport, DemandCaps.Industrial)]
    [InlineData(30, 101, 0, CityMessage.NeedAirport, DemandCaps.Commercial)]
    public void BusinessWantsASeaportAndAnAirportAsMicropolisSays(int count, int commercial, int industrial, CityMessage message, DemandCaps cap)
    {
        var city = Flat();
        RunToMessagesAt(city, count);
        var counted = WithCensus(city, commercial: commercial, industrial: industrial);
        var heard = new List<CityMessage>();
        counted.MessageSent += heard.Add;

        Run(counted, 1);

        Assert.Contains(message, heard);
        Assert.Equal(cap, counted.Facilities.Caps);
    }

    [Fact]
    public void AtTheirSizesExactlyNothingIsHeldBack()
    {
        foreach (var (count, residential, commercial, industrial) in new[] { (26, 500, 0, 0), (28, 0, 0, 70), (30, 0, 100, 0) })
        {
            var city = Flat();
            RunToMessagesAt(city, count);
            var counted = WithCensus(city, residential, commercial, industrial);
            Run(counted, 1);
            Assert.Equal(DemandCaps.None, counted.Facilities.Caps);
        }
    }

    [Fact]
    public void ACappedDemandOnlyStopsRising()
    {
        var census = new CityCensus();
        var demand = new CityDemand();

        // An empty city wants residents; held back, it wants none, and a fall still falls.
        demand.Update(census, caps: DemandCaps.Residential);
        Assert.Equal(0, demand.Residential);
        demand.Update(census, taxRate: 20, caps: DemandCaps.Residential);
        Assert.True(demand.Residential < 0);
    }

    [Fact]
    public void ASeaportAndAnAirportPolluteAndAStadiumDoesnt()
    {
        var city = Flat(48, 16);
        city.Builder.Apply(BuildAction.Seaport, 3, 3);
        city.Builder.Apply(BuildAction.Airport, 20, 3);
        city.Builder.Apply(BuildAction.Stadium, 40, 3);

        Run(city, CityCalendar.StepsPerCount);

        Assert.True(city.Maps.PollutionAt(3, 3) > 0);
        Assert.True(city.Maps.PollutionAt(21, 4) > 0);
        Assert.Equal(0, city.Maps.PollutionAt(41, 3));
    }

    [Fact]
    public void EachIsCountedByTheScanAndNeedsPower()
    {
        var city = Flat(48, 16);
        city.Builder.Apply(BuildAction.Seaport, 3, 3);
        city.Builder.Apply(BuildAction.Airport, 20, 3);
        city.Builder.Apply(BuildAction.Stadium, 40, 3);

        Run(city, 9);

        Assert.Equal((1, 1, 1), (city.Facilities.Stadiums, city.Facilities.Seaports, city.Facilities.Airports));
        Assert.False(city.Map.StateAt(41, 4).Powered || city.Map.StateAt(41, 3).Powered);
        Assert.Equal(3, city.Power.UnpoweredBuildings);
    }

    [Theory]
    [InlineData(BuildAction.Stadium)]
    [InlineData(BuildAction.Seaport)]
    [InlineData(BuildAction.Airport)]
    public void ABurningCentreExplodesAsMicropolisDoes(BuildAction action)
    {
        var city = Flat();
        city.Builder.Apply(action, 10, 10);
        var exploded = new List<(int, int)>();
        city.Exploded += (x, y) => exploded.Add((x, y));

        // The fire reaches the centre from the burning tile beside it; spreading is a roll, so keep
        // that tile alight until it does.
        for (var i = 0; i < 4000 && exploded.Count == 0; i++)
        {
            if (!city.Fires.IsBurning(9, 10))
            {
                city.Fires.Ignite(9, 10);
            }

            city.Tick();
        }

        Assert.Equal([(10, 10)], exploded);
        Assert.False(city.Map.StateAt(10, 10).IsBuildingCenter);
    }

    // A stadium with a hole burning, a hole fire left, and a hole a flood drained away, saved and
    // opened again in both formats.
    private static CitySimulation DamagedStadium()
    {
        var city = Flat();
        city.Builder.Apply(BuildAction.Stadium, 10, 10);
        city.Builder.Apply(BuildAction.Airport, 20, 20);
        city.Fires.Ignite(9, 9);
        city.Fires.Ignite(12, 9);
        city.Fires.PutOut(12, 9);
        city.Floods.Flood(9, 12);
        city.Floods.Drain(9, 12);
        city.Floods.Flood(12, 12);
        return city;
    }

    [Fact]
    public void ADamagedOneSavesAndOpensAsItWasInBothFormats()
    {
        var city = DamagedStadium();
        Assert.True(city.Hazards.IsHole(9, 9) && city.Hazards.IsHole(12, 9) && city.Hazards.IsHole(9, 12) && city.Hazards.IsHole(12, 12));

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);
        foreach (var reopened in new[] { flat, binary })
        {
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    Assert.Equal(city.Map.StateAt(x, y), reopened.Map.StateAt(x, y));
                    Assert.Equal(city.Fires.IsBurning(x, y), reopened.Fires.IsBurning(x, y));
                    Assert.Equal(city.Floods.IsFlooded(x, y), reopened.Floods.IsFlooded(x, y));
                    Assert.Equal(city.Hazards.IsHole(x, y), reopened.Hazards.IsHole(x, y));
                }
            }
        }
    }

    [Fact]
    public void AVersionThatDoesntKnowThemSeesBareGroundAndWhatWasLostFromThem()
    {
        // What 1.8 reads: the map, fire, and flood parts as written, without the facilities part.
        var save = JsonNode.Parse(FlatCityFormat.Write(DamagedStadium()))!.AsObject();
        Assert.DoesNotContain("x", string.Concat(save["fire"]!["tiles"]!.AsArray().Select(row => row!.GetValue<string>())));
        save["format"] = "1.8";
        save.Remove("facilities");
        save["parts"] = new JsonArray([.. save["parts"]!.AsArray().Where(part => part!["name"]!.GetValue<string>() != "facilities").Select(part => part!.DeepClone())]);

        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var older, out var problem), problem);

        Assert.Equal(new TileState(TileKind.Grass), older.Map.StateAt(10, 10));
        Assert.Equal(new TileState(TileKind.Grass), older.Map.StateAt(20, 20));
        Assert.True(older.Fires.IsBurning(9, 9));
        Assert.Equal(new TileState(TileKind.Rubble), older.Map.StateAt(12, 9));
        Assert.True(older.Floods.IsFlooded(12, 12));
        Assert.False(older.Hazards.IsHole(12, 9));
    }

    [Theory]
    [InlineData("use", "Park", "facilities.buildings[0].use: 'Park' isn't a stadium, a seaport, or an airport (Stadium, Seaport, or Airport).")]
    [InlineData("use", "18", "facilities.buildings[0].use: '18' isn't a stadium, a seaport, or an airport (Stadium, Seaport, or Airport).")]
    [InlineData("x", "16", "facilities.buildings[0]: the Stadium at 16, 10 can't stand on Grass with Road at 15, 9.")]
    [InlineData("x", "1", "facilities.buildings[1]: the Airport at 3, 10 runs off the map or onto another one at 2, 9.")]
    [InlineData("x", "0", "facilities.buildings[0]: the Stadium at 0, 10 runs off the map or onto another one at -1, 9.")]
    public void ABuildingThatIsWrongIsRefusedAndSaysWhere(string field, string value, string expected)
    {
        var city = Flat();
        city.Builder.Apply(BuildAction.Stadium, 10, 10);
        city.Builder.Apply(BuildAction.Airport, 3, 10);
        var road = new BuildStroke(BuildAction.Road, 14, 9);
        road.DragTo(18, 9);
        city.Builder.Apply(road);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        var building = save["facilities"]!["buildings"]!.AsArray().Single(b => b!["use"]!.GetValue<string>() == "Stadium")!;
        save["facilities"]!["buildings"] = new JsonArray(building.DeepClone(), save["facilities"]!["buildings"]!.AsArray().Single(b => b!["use"]!.GetValue<string>() == "Airport")!.DeepClone());
        save["facilities"]!["buildings"]![0]![field] = field == "use" ? value : int.Parse(value);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void ASavePartwayThroughACycleRunsOnInStep()
    {
        var city = Flat();
        city.Builder.Apply(BuildAction.Seaport, 10, 10);
        RunToMessagesAt(city, 28);
        var counted = WithCensus(city, industrial: 71);
        Run(counted, 3);

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(counted), out var reopened, out var problem), problem);
        Run(counted, CityCalendar.StepsPerYear);
        Run(reopened, CityCalendar.StepsPerYear);

        Assert.Equal(counted.Facilities.Caps, reopened.Facilities.Caps);
        Assert.Equal(counted.Facilities.Seaports, reopened.Facilities.Seaports);
        Assert.Equal(counted.Demand.Industrial, reopened.Demand.Industrial);
        Assert.Equal(FlatCityFormat.Write(counted), FlatCityFormat.Write(reopened));
    }

    [Fact]
    public void AScenarioBuildsAndCountsThem()
    {
        const string json = """
            { "format": "2.1", "name": "Ports", "seed": 2000, "ends": "1900-02",
              "actions": [
                { "at": "1900-01", "tool": "Stadium", "from": { "x": 38, "y": 46 }, "expect": "Built" },
                { "at": "1900-01", "tool": "Seaport", "from": { "x": 44, "y": 46 }, "expect": "Built" },
                { "at": "1900-01", "tool": "Airport", "from": { "x": 40, "y": 54 }, "expect": "Built" }
              ],
              "checks": [
                { "at": "1900-01", "week": 2, "count": "Stadium", "equals": 1 },
                { "at": "1900-01", "week": 2, "count": "Seaport", "equals": 1 },
                { "at": "1900-01", "week": 2, "count": "Airport", "equals": 1 },
                { "at": "1900-01", "week": 2, "tile": { "x": 43, "y": 57 }, "use": "Airport" }
              ] }
            """;
        Assert.True(ScenarioLoader.TryParse(json, out var scenario, out var error), error);

        var report = new ScenarioRunner().Run(scenario, "ports.json");

        Assert.True(report.Passed, string.Join("; ", report.Checks.Where(check => !check.Passed).Select(check => $"{check.Expected} / {check.Actual}")));
        Assert.Equal(1, report.Final.Counts.Airport);
    }
}
