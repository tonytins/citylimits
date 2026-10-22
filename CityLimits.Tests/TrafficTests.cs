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
    public void ASaveFromBeforeTrafficLoadsWithQuietRoadsAndSavesAsTheCurrentVersion()
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
        Assert.Equal("1.3", JsonNode.Parse(FlatCityFormat.Write(upgraded))!["format"]!.GetValue<string>());
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

    // Runs the city on to the given step, where the scan has passed every band of the cycle at step 8.
    private static void RunTo(CitySimulation city, long tick)
    {
        while (city.TickCount < tick)
        {
            city.Tick();
        }
    }

    private static void AddTraffic(CitySimulation city, int x, int trips)
    {
        for (var i = 0; i < trips; i++)
        {
            city.Traffic.AddRoute(city.Map, [(x, 6)]);
        }
    }

    [Fact]
    public void TheScanShowsEachRoadsTrafficByItsBlock()
    {
        var city = Street();
        AddTraffic(city, 4, 1);
        AddTraffic(city, 8, 2);
        AddTraffic(city, 12, 4);

        RunTo(city, 9);

        // 50 is quiet, 100 is light, and 200 heavy, for both road tiles in each block.
        Assert.Equal(TrafficLevel.None, city.Traffic.LevelAt(4, 6));
        Assert.Equal(TrafficLevel.Light, city.Traffic.LevelAt(8, 6));
        Assert.Equal(TrafficLevel.Light, city.Traffic.LevelAt(9, 6));
        Assert.Equal(TrafficLevel.Heavy, city.Traffic.LevelAt(12, 6));
        Assert.Equal(TrafficLevel.Heavy, city.Traffic.LevelAt(13, 6));
        Assert.Equal(TrafficLevel.None, city.Traffic.LevelAt(12, 7));
        Assert.Equal(TrafficLevel.None, city.Traffic.LevelAt(-1, 6));
    }

    [Fact]
    public void ARoadThatShowedHeavyTrafficCountsTwiceTowardUpkeep()
    {
        var city = Street();
        AddTraffic(city, 12, 4);

        // As in Micropolis, the scan counts the level a road showed before it sets the new one, so
        // the first scan counts the 15 tiles once each and shows the heavy traffic.
        RunTo(city, 9);
        Assert.Equal(15, city.RoadTiles);
        Assert.Equal(TrafficLevel.Heavy, city.Traffic.LevelAt(12, 6));

        // The traffic fades to 176 at phase 10, so the next scan counts the two heavy tiles again
        // and shows them light.
        RunTo(city, 25);
        Assert.Equal(17, city.RoadTiles);
        Assert.Equal(TrafficLevel.Light, city.Traffic.LevelAt(12, 6));

        RunTo(city, 41);
        Assert.Equal(15, city.RoadTiles);
    }

    [Fact]
    public void ARoadThatChangesStartsAgainQuiet()
    {
        var city = Street();
        AddTraffic(city, 12, 4);
        RunTo(city, 9);

        city.Builder.Apply(BuildAction.Bulldoze, 12, 6);
        Assert.Equal(TrafficLevel.None, city.Traffic.LevelAt(12, 6));

        city.Builder.Apply(BuildAction.Road, 12, 6);
        Assert.Equal(TrafficLevel.None, city.Traffic.LevelAt(12, 6));
        Assert.Equal(TrafficLevel.Heavy, city.Traffic.LevelAt(13, 6));
    }

    [Fact]
    public void TheTrafficAverageIsWorkedOutEachYearOverTheLandWithValue()
    {
        var city = Street();
        for (var x = 2; x <= 14; x += 3)
        {
            city.Map.SetState(x, 4, new TileState(TileKind.Grass, x < 9 ? TileUse.Residential : TileUse.Commercial, development: 3, valueClass: 1, powered: true));
        }

        var averages = new List<(int Expected, int Found)>();
        city.YearSettled += _ =>
        {
            long total = 0;
            var count = 1;
            for (var y = 0; y < city.Map.Height; y++)
            {
                for (var x = 0; x < city.Map.Width; x++)
                {
                    if (city.Maps.LandValueAt(x, y) != 0)
                    {
                        total += city.Traffic.DensityAt(x, y);
                        count++;
                    }
                }
            }

            var expected = city.Demand.TaxablePopulation == 0 ? 0 : (int)Math.Round((double)total / count * 2.4, MidpointRounding.AwayFromZero);
            averages.Add((expected, city.Traffic.Average));
        };

        RunTo(city, (3 * 48 * 16) + 10);

        Assert.Equal(4, averages.Count);
        Assert.All(averages, average => Assert.Equal(average.Expected, average.Found));
        Assert.Contains(averages, average => average.Found > 0);
    }

    [Theory]
    [InlineData(61, true)]
    [InlineData(60, false)]
    public void ACityWithATrafficAverageAbove60HearsOfTrafficJams(int average, bool jams)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Street()))!.AsObject();
        save["roadTraffic"]!["average"] = average;
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var city, out var problem), problem);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        // The jams are checked on the 63rd count of every 64, before a year can work the average out again.
        RunTo(city, (63 * 16) + 11);

        Assert.Equal(jams, heard.Contains(CityMessage.TrafficJams));
    }

    [Fact]
    public void EachRoadsTrafficAndTheAverageAreSavedInBothFormats()
    {
        var city = Street();
        AddTraffic(city, 8, 2);
        AddTraffic(city, 12, 4);
        RunTo(city, 9);

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);

        foreach (var loaded in new[] { flat, binary })
        {
            Assert.Equal(TrafficLevel.Light, loaded.Traffic.LevelAt(8, 6));
            Assert.Equal(TrafficLevel.Heavy, loaded.Traffic.LevelAt(13, 6));
            Assert.Equal(TrafficLevel.None, loaded.Traffic.LevelAt(4, 6));
        }

        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal(new string('0', 8) + "11002200", save["roadTraffic"]!["levels"]![6]!.GetValue<string>()[..16]);
    }

    [Fact]
    public void ASaveFromBefore13ShowsQuietRoadsAndASaveOf13WithoutThemIsRefused()
    {
        var city = Street();
        AddTraffic(city, 12, 4);
        RunTo(city, 9);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save.Remove("roadTraffic");

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal("roadTraffic: missing.", problem);

        save["format"] = "1.2";
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var upgraded, out problem), problem);
        Assert.Equal(TrafficLevel.None, upgraded.Traffic.LevelAt(12, 6));
        Assert.Equal(0, upgraded.Traffic.Average);
    }

    [Theory]
    [InlineData("3", "roadTraffic.levels[6]: needs 0, 1, or 2 for each tile, not '3' at 0.")]
    [InlineData("short", "roadTraffic.levels[6]: needs 20 characters.")]
    [InlineData("rows", "roadTraffic.levels: needs 9 rows.")]
    [InlineData("average", "roadTraffic.average: needs 0 to 576, not 577.")]
    public void ADamagedRoadTrafficPartIsRefused(string damage, string expected)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Street()))!.AsObject();
        var roads = save["roadTraffic"]!.AsObject();
        var levels = roads["levels"]!.AsArray();
        switch (damage)
        {
            case "3":
                levels[6] = "3" + new string('0', 19);
                break;
            case "short":
                levels[6] = "000";
                break;
            case "rows":
                levels.RemoveAt(0);
                break;
            default:
                roads["average"] = 577;
                break;
        }

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void ALevelLeftOnATileThatIsNoLongerARoadIsDropped()
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Street()))!.AsObject();

        // Row 7 is grass; an older game that kept this part while the road was bulldozed could leave this.
        save["roadTraffic"]!["levels"]![7] = new string('2', 20);
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var city, out var problem), problem);

        Assert.All(Enumerable.Range(0, 20), x => Assert.Equal(TrafficLevel.None, city.Traffic.LevelAt(x, 7)));
    }
}
