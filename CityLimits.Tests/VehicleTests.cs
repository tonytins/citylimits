using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Settings;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class VehicleTests
{
    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // A city as its save holds it, with its vehicles set as given.
    private static CitySimulation WithVehicles(CitySimulation city, JsonObject vehicles)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["vehicles"] = vehicles;
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var edited, out var problem), problem);
        return edited;
    }

    // A flat city with a powered airport.
    private static CitySimulation WithAirport()
    {
        var city = new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 11, funds: 1_000_000);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.Airport, 30, 20).Status);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.CoalPowerPlant, 30, 26).Status);
        return city;
    }

    // A map with a river running across it, three tiles wide, so its middle is a channel at both edges.
    private static TileMap River(int width = 64, int height = 48)
    {
        var kinds = new TileKind[width * height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 20; y <= 22; y++)
            {
                kinds[(y * width) + x] = TileKind.Water;
            }
        }

        return TileMap.FromTerrain(width, height, kinds);
    }

    [Fact]
    public void APoweredAirportSendsUpAPlaneAndAHelicopterTheSameEveryRun()
    {
        static (bool Plane, bool Helicopter, int X) Fly()
        {
            var city = WithAirport();
            var (plane, helicopter) = (false, false);
            for (var i = 0; i < 400 * CityCalendar.StepsPerCount && !(plane && helicopter); i++)
            {
                city.Tick();
                plane |= city.Vehicles.Plane is not null;
                helicopter |= city.Vehicles.Helicopter is not null;
            }

            return (plane, helicopter, city.Vehicles.Plane?.X ?? -1);
        }

        var flown = Fly();

        Assert.True(flown.Plane);
        Assert.True(flown.Helicopter);
        Assert.Equal(flown, Fly());
    }

    [Fact]
    public void AnUnpoweredAirportSendsUpNothing()
    {
        var city = new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 11, funds: 1_000_000);
        city.Builder.Apply(BuildAction.Airport, 30, 20);

        Run(city, 100 * CityCalendar.StepsPerCount);

        Assert.Null(city.Vehicles.Plane);
        Assert.Null(city.Vehicles.Helicopter);
    }

    [Fact]
    public void ThePlaneAndTheHelicopterCollidingBothComeDownWithFires()
    {
        var city = WithVehicles(
            new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 11, funds: 1_000_000),
            new JsonObject { ["plane"] = new JsonArray(200, 200, 3, 600, 200), ["helicopter"] = new JsonArray(210, 200, 7, 0, 200, 0, 0, 900) });
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;
        var exploded = new List<(int, int)>();
        city.Exploded += (x, y) => exploded.Add((x, y));

        city.Tick();

        Assert.Null(city.Vehicles.Plane);
        Assert.Null(city.Vehicles.Helicopter);
        Assert.Contains(CityMessage.PlaneCrashReported, heard);
        Assert.Contains(CityMessage.HelicopterCrashReported, heard);
        Assert.Contains((200 / 16, 200 / 16), exploded);
        Assert.True(city.Fires.Count > 0);
    }

    [Fact]
    public void WithAirCrashesOffTheyPassEachOther()
    {
        var city = WithVehicles(
            new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 11, funds: 1_000_000),
            new JsonObject { ["plane"] = new JsonArray(200, 200, 3, 600, 200), ["helicopter"] = new JsonArray(210, 200, 7, 0, 200, 0, 0, 900) });
        city.RandomAirCrashes = false;

        city.Tick();

        Assert.NotNull(city.Vehicles.Plane);
        Assert.NotNull(city.Vehicles.Helicopter);
        Assert.Equal(0, city.Fires.Count);
    }

    [Fact]
    public void TheHelicopterLandsAtHomeWhenItsFlightIsOver()
    {
        var city = WithVehicles(
            new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 11),
            new JsonObject { ["helicopter"] = new JsonArray(300, 300, 1, 600, 600, 310, 300, 1) });

        city.Tick();

        Assert.Null(city.Vehicles.Helicopter);
    }

    [Fact]
    public void APoweredSeaportCallsInAShipFromAChannelAtTheEdge()
    {
        var city = new CitySimulation(River(), 5, funds: 1_000_000);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.Seaport, 30, 26).Status);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.CoalPowerPlant, 30, 30).Status);

        var sailed = false;
        for (var i = 0; i < 200 * CityCalendar.StepsPerCount && !sailed; i++)
        {
            city.Tick();
            sailed = city.Vehicles.Ship is not null;
        }

        Assert.True(sailed);
        var ship = city.Vehicles.Ship!;
        Assert.Equal(TileKind.Water, city.Map.StateAt(ship.X / 16, ship.Y / 16).Kind);
    }

    [Fact]
    public void AShipStuckInAPondRunsAgroundUnlessShipwrecksAreOff()
    {
        // A pond of one tile, its ship about to look for a way and finding only land.
        var kinds = new TileKind[16 * 16];
        kinds[(8 * 16) + 8] = TileKind.Water;
        var pond = new JsonObject { ["ship"] = new JsonArray((8 * 16) + 8, (8 * 16) + 8, 3, 3, 10, 1) };

        var wrecked = WithVehicles(new CitySimulation(TileMap.FromTerrain(16, 16, kinds), 5), pond);
        var heard = new List<CityMessage>();
        wrecked.MessageSent += heard.Add;
        wrecked.Tick();

        Assert.Null(wrecked.Vehicles.Ship);
        Assert.Contains(CityMessage.ShipwreckReported, heard);

        var spared = WithVehicles(new CitySimulation(TileMap.FromTerrain(16, 16, kinds), 5), pond.DeepClone().AsObject());
        spared.RandomShipwrecks = false;
        spared.Tick();

        Assert.NotNull(spared.Vehicles.Ship);
    }

    [Fact]
    public void AScenarioCrashesThePlaneAndWrecksTheShipOnlyWhenOneIsOut()
    {
        var empty = new CitySimulation(River(), 5);
        Assert.False(empty.Strike(Disaster.PlaneCrash));
        Assert.False(empty.Strike(Disaster.Shipwreck));

        var city = WithVehicles(new CitySimulation(River(), 5), new JsonObject { ["plane"] = new JsonArray(200, 200, 3, 600, 200), ["ship"] = new JsonArray((30 * 16) + 8, (21 * 16) + 8, 3, 3, 10, 1) });
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        Assert.True(city.Strike(Disaster.PlaneCrash));
        Assert.True(city.Strike(Disaster.Shipwreck));

        Assert.Null(city.Vehicles.Plane);
        Assert.Null(city.Vehicles.Ship);
        Assert.Contains(CityMessage.PlaneCrashReported, heard);
        Assert.Contains(CityMessage.ShipwreckReported, heard);
        Assert.True(city.Fires.Count > 0);
    }

    [Fact]
    public void ACitySavedWithVehiclesRunsInStepWithOneThatWasnt()
    {
        var city = WithVehicles(WithAirport(), new JsonObject { ["plane"] = new JsonArray(100, 100, 3, 800, 400), ["helicopter"] = new JsonArray(500, 300, 1, 100, 100, 480, 320, 600) });
        Run(city, 9);

        foreach (var reopened in new[] { Reflat(city), Rebinary(city) })
        {
            Assert.Equal(city.Vehicles.Plane!.X, reopened.Vehicles.Plane!.X);
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
    [InlineData("plane", "[1, 2, 3]", "vehicles.plane: needs 5 numbers.")]
    [InlineData("ship", "[1, 2, 9, 3, 10, 1]", "vehicles.ship frame: needs 1 to 8, not 9.")]
    public void AVehicleThatIsWrongIsRefusedAndSaysWhere(string vehicle, string numbers, string expected)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[256]), 1)))!.AsObject();
        save["vehicles"]![vehicle] = JsonNode.Parse(numbers);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void AirCrashesAndShipwrecksHaveSwitchesOfTheirOwnUnderTheMasterOne()
    {
        Assert.True(GameSettings.Default.RandomAirCrashes && GameSettings.Default.RandomShipwrecks);
        Assert.False((GameSettings.Default with { AirCrashes = false }).RandomAirCrashes);
        Assert.False((GameSettings.Default with { Disasters = false }).RandomShipwrecks);

        var for04 = JsonNode.Parse(FlatCityFormat.Write(WithAirport(), legacy: true))!.AsObject();
        Assert.Null(for04["vehicles"]);
    }
}
