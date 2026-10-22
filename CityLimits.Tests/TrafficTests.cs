using System.Text.Json.Nodes;
using CityLimits.Core.Definitions;
using CityLimits.Core.Randomness;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TrafficTests
{
    // A flat map with a road along row 6, from column 1 to 15, a house zone at one end and shops at the other.
    private static CitySimulation Street()
    {
        var city = new CitySimulation(TileMap.FromTerrain(20, 9, new TileKind[20 * 9]), 1, funds: 100_000);
        var road = new BuildStroke(BuildAction.Road, 1, 6);
        road.DragTo(15, 6);
        city.Builder.Apply(road);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        city.Builder.Apply(BuildAction.ZoneCommercial, 12, 4);
        return city;
    }

    [Fact]
    public void ATripThatArrivesLeavesFiftyOnEachRoadItRememberedPassing()
    {
        var city = Street();
        var traffic = new TrafficMap(city.Map);
        var trips = new TripPlanner(traffic);

        Assert.Equal(TripOutcome.Arrived, trips.Drive(city.Map, CityDefinitions.Base, new DeterministicRandom(7), 2, 4));

        Assert.NotEmpty(trips.Route);
        var blocks = trips.Route.Select(tile => (tile.X / 2, tile.Y / 2)).ToList();
        foreach (var (x, y) in trips.Route)
        {
            Assert.Equal(TrafficMap.PerTrip * blocks.Count(block => block == (x / 2, y / 2)), traffic.DensityAt(x, y));
        }

        Assert.Equal(0, traffic.DensityAt(2, 2));
    }

    [Fact]
    public void ATripThatFindsNowhereLeavesNoTraffic()
    {
        var city = new CitySimulation(TileMap.FromTerrain(20, 9, new TileKind[20 * 9]), 1, funds: 100_000);
        var road = new BuildStroke(BuildAction.Road, 1, 6);
        road.DragTo(15, 6);
        city.Builder.Apply(road);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        var traffic = new TrafficMap(city.Map);

        Assert.NotEqual(TripOutcome.Arrived, new TripPlanner(traffic).Drive(city.Map, CityDefinitions.Base, new DeterministicRandom(7), 2, 4));

        Assert.All(Enumerable.Range(0, 20), x => Assert.Equal(0, traffic.DensityAt(x, 6)));
    }

    [Fact]
    public void OnlyRoadsTakeTrafficAndABlockStopsAt240()
    {
        var city = Street();
        var traffic = new TrafficMap(city.Map);
        (int X, int Y)[] route = [(4, 6), (4, 7)];

        // (4, 7) is grass beside the road, in the same block as (4, 6), so one trip adds only the road's 50.
        traffic.AddRoute(city.Map, route);
        Assert.Equal(TrafficMap.PerTrip, traffic.DensityAt(4, 6));

        for (var i = 0; i < 10; i++)
        {
            traffic.AddRoute(city.Map, route);
        }

        Assert.Equal(TrafficMap.Most, traffic.DensityAt(4, 6));
        Assert.Equal(TrafficMap.Most, traffic.DensityAt(5, 7));
        Assert.Equal(0, traffic.DensityAt(4, 2));
        Assert.Equal(0, traffic.DensityAt(-1, 6));
    }

    [Fact]
    public void TheFadeStepsAreMicropolis()
    {
        var city = Street();
        var traffic = new TrafficMap(city.Map);
        for (var i = 0; i < 5; i++)
        {
            traffic.AddRoute(city.Map, [(4, 6)]);
        }

        var seen = new List<int> { traffic.DensityAt(4, 6) };
        while (seen[^1] > 0)
        {
            traffic.Fade();
            seen.Add(traffic.DensityAt(4, 6));
        }

        // 240 loses 34 twice (to 206, then 172), then 24 a fade until 24 or less, then nothing.
        Assert.Equal([240, 206, 172, 148, 124, 100, 76, 52, 28, 4, 0], seen);
    }

    [Fact]
    public void AGrowingCitysRoadsCarryTrafficAndItFadesAtPhaseTen()
    {
        var city = Street();
        for (var x = 2; x <= 14; x += 3)
        {
            city.Map.SetState(x, 4, new TileState(TileKind.Grass, x < 9 ? TileUse.Residential : TileUse.Commercial, development: 3, valueClass: 1, powered: true));
        }

        var busiest = 0;
        for (var step = 0; step < 48 * 16; step++)
        {
            city.Tick();
            busiest = Math.Max(busiest, Enumerable.Range(1, 15).Max(x => city.Traffic.DensityAt(x, 6)));
            if (city.TickCount % CityCalendar.StepsPerCount == 9)
            {
                // Phase 10 runs on the next step: the traffic it sees fades as Micropolis' does.
                var before = Enumerable.Range(1, 15).Select(x => city.Traffic.DensityAt(x, 6)).ToArray();
                var expected = before.Select(d => d > 200 ? d - 34 : d > 24 ? d - 24 : 0).ToArray();
                city.Tick();
                step++;
                var after = Enumerable.Range(1, 15).Select(x => city.Traffic.DensityAt(x, 6)).ToArray();
                Assert.Equal(expected, after);
            }
        }

        Assert.True(busiest > 0, "the street's trips should leave traffic");
        Assert.Equal(Enumerable.Range(0, 20).SelectMany(x => Enumerable.Range(0, 9).Select(y => city.Traffic.DensityAt(x, y))).Max(), city.Traffic.Busiest);
        Assert.All(Enumerable.Range(0, 20), x => Assert.Equal(0, city.Traffic.DensityAt(x, 1)));
    }

    [Fact]
    public void ASaveFromBeforeTrafficLoadsWithQuietRoadsAndSavesAs11()
    {
        var city = Street();
        for (var i = 0; i < 300; i++)
        {
            city.Tick();
        }

        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["format"] = "1.0";
        save.Remove("traffic");

        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var upgraded, out var problem), problem);

        Assert.All(Enumerable.Range(0, 20), x => Assert.Equal(0, upgraded.Traffic.DensityAt(x, 6)));
        Assert.Equal("1.1", JsonNode.Parse(FlatCityFormat.Write(upgraded))!["format"]!.GetValue<string>());
    }

    [Fact]
    public void ASave11WithoutTrafficIsRefused()
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Street()))!.AsObject();
        save.Remove("traffic");

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal("traffic: missing.", problem);
    }

    [Fact]
    public void TrafficIsSavedAndADamagedMapOfItRefused()
    {
        var city = Street();
        city.Traffic.AddRoute(city.Map, [(4, 6), (10, 6)]);

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var loaded, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);
        Assert.Equal(TrafficMap.PerTrip, loaded.Traffic.DensityAt(10, 6));
        Assert.Equal(TrafficMap.PerTrip, binary.Traffic.DensityAt(4, 6));

        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["traffic"]!["density"]![0] = 999;
        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out problem));
        Assert.Equal("traffic.density: needs 0 to 240, not 999.", problem);
    }
}
