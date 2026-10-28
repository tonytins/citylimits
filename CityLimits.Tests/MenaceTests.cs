using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Settings;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class MenaceTests
{
    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // A city as its save holds it, with its menaces, and its vehicles when given, set as given.
    private static CitySimulation With(CitySimulation city, JsonObject menaces, JsonObject? vehicles = null)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["menaces"] = menaces;
        if (vehicles is not null)
        {
            save["vehicles"] = vehicles;
        }

        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var edited, out var problem), problem);
        return edited;
    }

    // A map with a river running across it, three tiles wide, rows 20 to 22.
    private static TileMap River(int width = 64, int height = 48, TileKind land = TileKind.Grass)
    {
        var kinds = new TileKind[width * height];
        for (var i = 0; i < kinds.Length; i++)
        {
            kinds[i] = i / width is >= 20 and <= 22 ? TileKind.Water : land;
        }

        return TileMap.FromTerrain(width, height, kinds);
    }

    [Fact]
    public void ATornadoTouchesDownAwayFromTheEdgesWrecksWhatItPassesAndBlowsOutTheSameEveryRun()
    {
        static (int Rubble, long Lasted) Blow()
        {
            var city = new CitySimulation(River(land: TileKind.Trees), 9);
            var heard = new List<CityMessage>();
            city.MessageSent += heard.Add;

            Assert.True(city.Strike(Disaster.Tornado));
            Assert.Contains(CityMessage.TornadoReported, heard);
            var tornado = city.Menaces.Tornado!;
            Assert.InRange(tornado.X / 16, 10, 64 - 10);
            Assert.InRange(tornado.Y / 16, 10, 48 - 10);

            var steps = 0L;
            for (; steps < 20 * CityCalendar.StepsPerYear && city.Menaces.Tornado is not null; steps++)
            {
                city.Tick();
            }

            Assert.Null(city.Menaces.Tornado);
            var rubble = 0;
            for (var y = 0; y < city.Map.Height; y++)
            {
                for (var x = 0; x < city.Map.Width; x++)
                {
                    rubble += city.Map.StateAt(x, y).Kind == TileKind.Rubble || city.Fires.IsBurning(x, y) ? 1 : 0;
                }
            }

            return (rubble, steps);
        }

        var blown = Blow();

        Assert.True(blown.Rubble > 0);
        Assert.True(blown.Lasted >= Core.Sprites.Menaces.TornadoMoves / 2);
        Assert.Equal(blown, Blow());
    }

    [Fact]
    public void ATornadoOnAMapTooSmallToKeepAwayFromTheEdgesTouchesDownInTheMiddle()
    {
        var city = new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[256]), 9);

        Assert.True(city.Strike(Disaster.Tornado));
        Assert.Equal((8 * 16) + 8, city.Menaces.Tornado!.X);
        Assert.True(city.Strike(Disaster.Tornado));
    }

    [Fact]
    public void TheMonsterComesUpOutOfTheWaterGoesToThePollutionAndHome()
    {
        // A coal plant's smoke gives it somewhere to go; with none, it makes for the map's corner,
        // as in Micropolis, and walks off an edge first.
        var city = new CitySimulation(River(), 9, funds: 1_000_000);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.CoalPowerPlant, 44, 34).Status);
        city.RandomTornadoes = city.RandomMonsters = false;
        Run(city, 2 * CityCalendar.StepsPerCount);
        Assert.InRange(city.Maps.MostPolluted.X, 38, 50);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        Assert.True(city.Strike(Disaster.Monster));
        var monster = city.Menaces.Monster!;
        Assert.Equal(TileKind.Water, city.Map.StateAt(monster.X / 16, monster.Y / 16).Kind);
        Assert.Equal(city.Maps.MostPolluted, ((monster.DestinationX - 8) / 16, (monster.DestinationY - 8) / 16));

        var wentHome = false;
        for (var i = 0; i < 20 * CityCalendar.StepsPerYear && city.Menaces.Monster is not null; i++)
        {
            city.Tick();
            wentHome |= city.Menaces.Monster?.HeadingHome == true;
        }

        Assert.True(wentHome);
        Assert.Null(city.Menaces.Monster);
        Assert.Single(heard, message => message == CityMessage.MonsterReported);
    }

    [Fact]
    public void CallingTheMonsterAgainSendsTheOneAboutBackToThePollution()
    {
        var city = With(new CitySimulation(River(), 9), new JsonObject { ["monster"] = new JsonArray(400, 200, 1, 600, 300, 100, 100, 1, 1) });
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        Assert.True(city.Strike(Disaster.Monster));

        Assert.False(city.Menaces.Monster!.HeadingHome);
        Assert.Equal(400, city.Menaces.Monster.X);
        Assert.Empty(heard);
    }

    [Fact]
    public void TheMonsterTakesABridgeIntoTheWaterAndABuildingDown()
    {
        var city = new CitySimulation(River(), 9, funds: 1_000_000);
        var bridge = new BuildStroke(BuildAction.Road, 30, 18);
        bridge.DragTo(30, 24);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(bridge).Status);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.PoliceStation, 40, 30).Status);
        Assert.True(city.Map.StateAt(30, 21).HasRoad);

        var onBridge = With(city, new JsonObject { ["monster"] = new JsonArray((30 * 16) + 8, (21 * 16) + 8, 1, 0, 0, 0, 0, 1, 0) });
        onBridge.Tick();
        Assert.Equal(new TileState(TileKind.Water), onBridge.Map.StateAt(30, 21));

        var onStation = With(city, new JsonObject { ["monster"] = new JsonArray((40 * 16) + 8, (30 * 16) + 8, 1, 0, 0, 0, 0, 1, 0) });
        onStation.Tick();
        Assert.True(onStation.Fires.IsBurning(40, 30));
    }

    [Fact]
    public void AMenaceBringsDownTheVehiclesItMeetsUnlessTheirSwitchesAreOff()
    {
        var menaces = new JsonObject { ["tornado"] = new JsonArray(300, 300, 1, 200, 0) };
        var vehicles = new JsonObject { ["plane"] = new JsonArray(310, 300, 3, 900, 300) };

        var caught = With(new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 9), menaces, vehicles);
        var heard = new List<CityMessage>();
        caught.MessageSent += heard.Add;
        caught.Tick();
        Assert.Null(caught.Vehicles.Plane);
        Assert.Contains(CityMessage.PlaneCrashReported, heard);

        var spared = With(new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 9), menaces.DeepClone().AsObject(), vehicles.DeepClone().AsObject());
        spared.RandomAirCrashes = false;
        spared.Tick();
        Assert.NotNull(spared.Vehicles.Plane);
    }

    [Fact]
    public void TheHelicopterChasesTheTornadoOnceItsFlightIsOver()
    {
        var city = With(
            new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 9),
            new JsonObject { ["tornado"] = new JsonArray(800, 600, 1, 200, 0) },
            new JsonObject { ["helicopter"] = new JsonArray(200, 200, 1, 300, 300, 100, 100, 1) });

        city.Tick();

        var (copter, tornado) = (city.Vehicles.Helicopter!, city.Menaces.Tornado!);
        Assert.InRange(Math.Abs(copter.DestinationX - tornado.X) + Math.Abs(copter.DestinationY - tornado.Y), 0, 6);
    }

    [Fact]
    public void ACitySavedWithMenacesRunsInStepWithOneThatWasnt()
    {
        var city = With(
            new CitySimulation(River(land: TileKind.Trees), 9),
            new JsonObject { ["tornado"] = new JsonArray(300, 500, 2, 150, 1), ["monster"] = new JsonArray(500, 344, 4, 100, 100, 500, 344, 1, 0) });
        Run(city, 9);

        foreach (var reopened in new[] { Reflat(city), Rebinary(city) })
        {
            Assert.Equal(city.Menaces.Monster!.X, reopened.Menaces.Monster!.X);
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
    [InlineData("tornado", "[1, 2, 3]", "menaces.tornado: needs 5 numbers.")]
    [InlineData("monster", "[1, 2, 3, 0, 0, 0, 0, 2, 0]", "menaces.monster step: needs -1 or 1, not 2.")]
    public void AMenaceThatIsWrongIsRefusedAndSaysWhere(string menace, string numbers, string expected)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[256]), 1)))!.AsObject();
        save["menaces"]![menace] = JsonNode.Parse(numbers);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void TornadoesAndTheMonsterHaveSwitchesOfTheirOwnUnderTheMasterOne()
    {
        Assert.True(GameSettings.Default.RandomTornadoes && GameSettings.Default.RandomMonsters);
        Assert.False((GameSettings.Default with { Tornadoes = false }).RandomTornadoes);
        Assert.False((GameSettings.Default with { Disasters = false }).RandomMonsters);

        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "settings.json");
        try
        {
            Assert.True(SettingsStore.TrySave(path, GameSettings.Default with { Monsters = false }, out _));
            var loaded = SettingsStore.Load(path, out _);
            Assert.False(loaded.Monsters);
            Assert.True(loaded.Tornadoes);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }

        var for04 = JsonNode.Parse(FlatCityFormat.Write(new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[256]), 1), legacy: true))!.AsObject();
        Assert.Null(for04["menaces"]);
    }
}
