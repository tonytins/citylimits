using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class FloodAndQuakeTests
{
    private static void RunCounts(CitySimulation city, int counts)
    {
        for (var i = 0; i < counts * CityCalendar.StepsPerCount; i++)
        {
            city.Tick();
        }
    }

    // Bare land with a lake down the left side, columns 0 to 3.
    private static CitySimulation Lakeside(int seed = 1, int width = 20, int height = 20)
    {
        var terrain = new TileKind[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                terrain[(y * width) + x] = TileKind.Water;
            }
        }

        return new CitySimulation(TileMap.FromTerrain(width, height, terrain), seed);
    }

    // Only the disaster a test is about happens on its own.
    private static CitySimulation Only(CitySimulation city, bool fires = false, bool floods = false, bool earthquakes = false)
    {
        city.RandomFires = fires;
        city.RandomFloods = floods;
        city.RandomEarthquakes = earthquakes;
        return city;
    }

    [Fact]
    public void AFloodSpreadsOverTheLandWhileItLastsAndThenDrainsToBareGround()
    {
        var city = Only(Lakeside());
        var road = new BuildStroke(BuildAction.Road, 4, 10);
        road.DragTo(19, 10);
        city.Builder.Apply(road);

        city.Floods.Flood(4, 10);
        city.Floods.Rise();
        RunCounts(city, Floods.Duration);

        Assert.True(city.Floods.Count > 1, $"{city.Floods.Count} flooded");
        Assert.All(Enumerable.Range(0, 20), y => Assert.False(city.Floods.IsFlooded(0, y)));

        RunCounts(city, 1000);
        Assert.Equal(0, city.Floods.Count);
        Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(4, 10));
    }

    [Fact]
    public void WaterNeverFloodsAFire()
    {
        var city = Only(Lakeside());
        city.Map.SetState(5, 10, new TileState(TileKind.Trees));
        city.Fires.Ignite(5, 10);

        Assert.False(city.Floods.CanFlood(5, 10));
        city.Floods.Flood(5, 10);
        Assert.False(city.Floods.IsFlooded(5, 10));
    }

    [Fact]
    public void AFloodedOuterTileIsAHoleAndAFloodedCentreTakesTheWholeBuilding()
    {
        var city = Only(Lakeside());
        city.Builder.Apply(BuildAction.ZoneCommercial, 10, 10);
        city.Builder.Apply(BuildAction.ZoneCommercial, 14, 10);

        city.Floods.Flood(9, 9);
        Assert.True(city.Hazards.IsHole(9, 9));
        Assert.Equal(BuildRefusal.Flooded, city.Builder.Apply(BuildAction.Road, 9, 9).Reason);
        Assert.Equal(BuildRefusal.Flooded, city.Builder.Apply(BuildAction.Bulldoze, 9, 9).Reason);
        Assert.True(city.Hazards.AnyActiveIn(10, 10));

        city.Floods.Flood(14, 10);
        Assert.Equal(1 + 9, city.Floods.Count);
        Assert.False(city.Map.StateAt(14, 10).IsZoneCenter);
    }

    [Fact]
    public void FloodsRiseFromAShoreOnlyWhenAllowed()
    {
        var allowed = Only(Lakeside(seed: 3), floods: true);
        var heard = new List<CityMessage>();
        allowed.MessageSent += heard.Add;
        for (var counts = 0; counts < 30_000 && !heard.Contains(CityMessage.FloodReported); counts++)
        {
            RunCounts(allowed, 1);
        }

        Assert.Contains(CityMessage.FloodReported, heard);
        Assert.True(allowed.Floods.Count > 0);

        var refused = Only(Lakeside(seed: 3));
        var heardNothing = new List<CityMessage>();
        refused.MessageSent += heardNothing.Add;
        RunCounts(refused, (int)allowed.CityTime);
        Assert.DoesNotContain(CityMessage.FloodReported, heardNothing);
        Assert.Equal(0, refused.Floods.Count);
    }

    [Fact]
    public void AnEarthquakeWrecksOuterTilesButNeverCentresAndShakesTheView()
    {
        var city = Only(new CitySimulation(TileMap.FromTerrain(30, 30, new TileKind[900]), 5), earthquakes: true);
        var zones = new BuildStroke(BuildAction.ZoneCommercial, 1, 1);
        zones.DragTo(28, 28);
        city.Builder.Apply(zones);
        int Centres() => Enumerable.Range(0, 900).Count(i => city.Map.StateAt(i % 30, i / 30).IsZoneCenter);
        var built = Centres();
        var heard = new List<CityMessage>();
        var shaken = 0;
        var centresWhenShaken = 0;
        city.MessageSent += heard.Add;
        city.EarthquakeStruck += () =>
        {
            shaken++;
            centresWhenShaken = Centres();
        };

        for (var counts = 0; counts < 40_000 && shaken == 0; counts++)
        {
            RunCounts(city, 1);
        }

        Assert.Equal(1, shaken);
        Assert.Contains(CityMessage.EarthquakeReported, heard);
        var holes = 0;
        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 30; x++)
            {
                holes += city.Hazards.IsHole(x, y) ? 1 : 0;
            }
        }

        // The quake wrecks only outer tiles; its fires may reach a centre later, as any fire may.
        Assert.True(holes > 0);
        Assert.Equal(built, centresWhenShaken);
    }

    [Fact]
    public void TurningEveryDisasterOffChangesNothingWhereNoneWouldHarmAnything()
    {
        // Roads only, and no water: nothing a fire, a flood, or an earthquake could start on.
        static CitySimulation Street(bool on)
        {
            var city = new CitySimulation(TileMap.FromTerrain(20, 9, new TileKind[180]), 1);
            var road = new BuildStroke(BuildAction.Road, 0, 4);
            road.DragTo(19, 4);
            city.Builder.Apply(road);
            return Only(city, on, on, on);
        }

        var on = Street(on: true);
        var off = Street(on: false);

        RunCounts(on, 5000);
        RunCounts(off, 5000);

        Assert.Equal(on.Random.NextInt(0, int.MaxValue), off.Random.NextInt(0, int.MaxValue));
    }

    [Fact]
    public void AStationRedrawsAHoleOfBareGroundButNotOfRubble()
    {
        var city = Only(new CitySimulation(TileMap.FromTerrain(12, 12, new TileKind[144]), 1));
        city.Builder.Apply(BuildAction.FireStation, 5, 5);
        city.Map.SetState(4, 4, new TileState(TileKind.Rubble));
        city.Map.SetState(6, 6, new TileState(TileKind.Grass));

        RunCounts(city, 9);

        Assert.Equal(TileUse.FireStation, city.Map.StateAt(6, 6).Use);
        Assert.Equal(TileKind.Rubble, city.Map.StateAt(4, 4).Kind);

        // Clearing the rubble lets the station repair it.
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.Bulldoze, 4, 4).Status);
        RunCounts(city, 9);
        Assert.Equal(TileUse.FireStation, city.Map.StateAt(4, 4).Use);
    }

    [Fact]
    public void FloodsAreSavedAndLoadedInBothFormats()
    {
        var city = Only(Lakeside());
        city.Builder.Apply(BuildAction.ZoneCommercial, 10, 10);
        city.Floods.Flood(9, 9);
        city.Floods.Flood(5, 15);
        city.Floods.Rise();

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);

        foreach (var loaded in new[] { flat, binary })
        {
            Assert.True(loaded.Floods.IsFlooded(9, 9) && loaded.Hazards.IsHole(9, 9));
            Assert.True(loaded.Floods.IsFlooded(5, 15));
            Assert.Equal(2, loaded.Floods.Count);
            Assert.Equal(Floods.Duration, loaded.Floods.CyclesLeft);
            Assert.Equal(new TileState(TileKind.Grass), loaded.Map.StateAt(9, 9));
        }
    }

    [Fact]
    public void ASaveWithAFloodedHoleOpensInAVersionWithoutFloodsAsAHole()
    {
        var city = Only(Lakeside());
        city.Builder.Apply(BuildAction.ZoneCommercial, 10, 10);
        city.Floods.Flood(9, 9);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();

        Assert.Equal('C', save["map"]!["use"]![9]!.GetValue<string>()[9]);
        Assert.Equal('x', save["fire"]!["tiles"]![9]!.GetValue<string>()[9]);
        Assert.Equal('w', save["flood"]!["tiles"]![9]!.GetValue<string>()[9]);

        save["format"] = "1.6";
        save.Remove("flood");
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var older, out var problem), problem);
        Assert.Equal(0, older.Floods.Count);
        Assert.True(older.Hazards.IsHole(9, 9));
    }

    [Theory]
    [InlineData(10, 10, 'w', "flood.tiles[10]: the flood at 10 is on Grass with Commercial, not bare ground.")]
    [InlineData(0, 0, 'w', "flood.tiles[0]: the flood at 0 is on Water with None, not bare ground.")]
    [InlineData(5, 5, '?', "flood.tiles[5]: needs '.' or 'w' for each tile, not '?' at 5.")]
    public void AFloodPartThatDoesntFitTheMapIsRefusedAndSaysWhere(int x, int y, char mark, string expected)
    {
        var city = Only(Lakeside());
        city.Builder.Apply(BuildAction.ZoneCommercial, 10, 10);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        var rows = save["flood"]!["tiles"]!.AsArray();
        var row = rows[y]!.GetValue<string>().ToCharArray();
        row[x] = mark;
        rows[y] = new string(row);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }
}
