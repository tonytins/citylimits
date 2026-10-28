using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Settings;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class MeltdownTests
{
    // A flat city with a nuclear plant in the middle, a road beside it, and zones around.
    private static CitySimulation WithPlant()
    {
        var city = new CitySimulation(TileMap.FromTerrain(64, 48, new TileKind[64 * 48]), 3, funds: 1_000_000);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.NuclearPowerPlant, 30, 22).Status);
        var road = new BuildStroke(BuildAction.Road, 10, 28);
        road.DragTo(50, 28);
        city.Builder.Apply(road);
        for (var x = 12; x <= 48; x += 6)
        {
            city.Builder.Apply(BuildAction.ZoneResidential, x, 30);
        }

        return city;
    }

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    [Fact]
    public void AMeltdownSetsThePlantAblazeAndLeavesRadioactiveGroundAroundIt()
    {
        var city = WithPlant();
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        Assert.True(city.Strike(Disaster.Meltdown));

        Assert.Contains(CityMessage.MeltdownReported, heard);
        Assert.True(city.Fires.IsBurning(30, 22) || city.Radiation.IsRadioactive(30, 22) || city.Map.StateAt(30, 22).Kind == TileKind.Rubble);
        Assert.InRange(city.Radiation.Count, 1, 200);
        for (var y = 0; y < city.Map.Height; y++)
        {
            for (var x = 0; x < city.Map.Width; x++)
            {
                if (city.Radiation.IsRadioactive(x, y))
                {
                    Assert.InRange(x, 30 - 20, 30 + 20);
                    Assert.InRange(y, 22 - 15, 22 + 15);
                    Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(x, y));
                    Assert.False(city.Fires.IsBurning(x, y));
                }
            }
        }
    }

    [Fact]
    public void ACityWithoutANuclearPlantHasNoMeltdown()
    {
        var city = new CitySimulation(TileMap.FromTerrain(32, 32, new TileKind[32 * 32]), 3, funds: 1_000_000);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 10, 10);

        Assert.False(city.Strike(Disaster.Meltdown));
        Assert.Equal(0, city.Radiation.Count);
    }

    [Fact]
    public void NothingCanBeBuiltOnRadioactiveGroundAndTheBulldozerCantClearIt()
    {
        var city = new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[16 * 16]), 3, funds: 1_000_000);
        city.Radiation.Contaminate(5, 5);

        var road = city.Builder.Apply(BuildAction.Road, 5, 5);
        var dozed = city.Builder.Apply(BuildAction.Bulldoze, 5, 5);

        Assert.Equal((BuildStatus.Refused, BuildRefusal.Radioactive), (road.Status, road.Reason));
        Assert.NotEqual(BuildStatus.Built, dozed.Status);
        Assert.True(city.Radiation.IsRadioactive(5, 5));
        Assert.Contains("Radioactive", TileInfo.Describe(city.Map, 5, 5, city.Definitions, city.Hazards), StringComparison.Ordinal);
    }

    [Fact]
    public void RadioactiveGroundDecaysAtMicropolisPaceAndTheSameEveryRun()
    {
        static int Left()
        {
            var city = WithPlant();
            city.Strike(Disaster.Meltdown);
            Run(city, 300 * CityCalendar.StepsPerCount);
            return city.Radiation.Count;
        }

        var start = WithPlant();
        start.Strike(Disaster.Meltdown);
        var left = Left();

        Assert.Equal(left, Left());
        Assert.InRange(left, 1, start.Radiation.Count - 1);
    }

    [Fact]
    public void ACitySavedWithRadiationRunsInStepWithOneThatWasnt()
    {
        var city = WithPlant();
        city.Strike(Disaster.Meltdown);
        Run(city, 7);

        foreach (var reopened in new[] { Reflat(city), Rebinary(city) })
        {
            Assert.Equal(city.Radiation.Count, reopened.Radiation.Count);
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
    public void ASaveWithoutRadiationHasNoneAndOneFor04LeavesItOutAsBareGround()
    {
        var city = new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[16 * 16]), 3);
        city.Radiation.Contaminate(4, 4);

        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save.Remove("radiation");
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var without, out var problem), problem);
        Assert.Equal(0, without.Radiation.Count);

        var for04 = JsonNode.Parse(FlatCityFormat.Write(city, legacy: true))!.AsObject();
        Assert.Null(for04["radiation"]);
    }

    [Fact]
    public void RadiationOnABuildingIsRefusedAndSaysWhere()
    {
        var city = new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[16 * 16]), 3, funds: 1_000_000);
        city.Builder.Apply(BuildAction.Road, 2, 3);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        var rows = save["radiation"]!["tiles"]!.AsArray();
        rows[3] = "..r" + new string('.', 13);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal("radiation.tiles[3]: the radiation at 2 is on Grass with Road, not bare ground.", problem);
    }

    [Fact]
    public void MeltdownsHaveASwitchOfTheirOwnUnderTheMasterOne()
    {
        Assert.True(GameSettings.Default.Meltdowns);
        Assert.True(GameSettings.Default.RandomMeltdowns);
        Assert.False((GameSettings.Default with { Meltdowns = false }).RandomMeltdowns);
        Assert.False((GameSettings.Default with { Disasters = false }).RandomMeltdowns);

        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "settings.json");
        try
        {
            Assert.True(SettingsStore.TrySave(path, GameSettings.Default with { Meltdowns = false }, out _));
            Assert.False(SettingsStore.Load(path, out _).Meltdowns);
            File.WriteAllText(path, """{ "WindowWidth": 1280, "WindowHeight": 720, "Speed": 1 }""");
            Assert.True(SettingsStore.Load(path, out _).Meltdowns);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
