using System.Text.Json.Nodes;
using CityLimits.Core.Definitions;
using CityLimits.Core.Economy;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class SpecTests
{
    private const string Manifest = """{ "format": "2.0", "id": "m", "name": "M" }""";

    private static CitySimulation Flat(CityDefinitions definitions, int width = 32, int height = 32) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, definitions: definitions);

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // A folder for the test, deleted after it.
    private static void InFolder(Action<string> test)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // The definitions with one mod, "m", made of the spec files given by their paths within it.
    private static CityDefinitions WithMod(params (string Path, string Json)[] files)
    {
        CityDefinitions? definitions = null;
        InFolder(folder =>
        {
            var mod = Path.Combine(folder, "m");
            Directory.CreateDirectory(mod);
            File.WriteAllText(Path.Combine(mod, ModLoader.ManifestFile), Manifest);
            foreach (var (path, json) in files)
            {
                var file = Path.Combine(mod, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, json);
            }

            definitions = ModLoader.Load(folder, out var problems);
            Assert.Empty(problems);
        });
        return definitions!;
    }

    [Fact]
    public void TheBaseGamesFilesAreWrittenOnceAVersionAndPlayAsTheBaseGame()
    {
        InFolder(folder =>
        {
            Assert.True(BaseSpecFiles.Refresh(folder, "0.5.101"));
            Assert.False(BaseSpecFiles.Refresh(folder, "0.5.101"));
            Assert.Equal(
                ["economy.json", "facilities.json", "infrastructure.json", "power.json", "services.json", "version.txt", "zones.json"],
                Directory.EnumerateFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));

            File.WriteAllText(Path.Combine(folder, "economy.json"), """{ "format": "2.0", "startingFunds": 5 }""");
            Assert.True(BaseSpecFiles.Refresh(folder, "0.5.102"));
            Assert.Contains("20000", File.ReadAllText(Path.Combine(folder, "economy.json")));

            var definitions = BaseSpecFiles.Load(folder, out var problem);
            Assert.Null(problem);
            Assert.Empty(definitions.Mods);

            var city = Flat(definitions);
            var plain = Flat(CityDefinitions.Base);
            Run(city, CityCalendar.StepsPerYear);
            Run(plain, CityCalendar.StepsPerYear);
            Assert.Equal(FlatCityFormat.Write(plain), FlatCityFormat.Write(city));
        });
    }

    [Fact]
    public void AChangedCommonFileChangesTheBaseGameAndABrokenOneChangesNothing()
    {
        InFolder(folder =>
        {
            File.WriteAllText(Path.Combine(folder, "economy.json"), """{ "format": "2.0", "startingFunds": 5000, "taxRate": 9 }""");

            var changed = BaseSpecFiles.Load(folder, out var problem);
            var city = Flat(changed);

            Assert.Null(problem);
            Assert.Empty(changed.Mods);
            Assert.Equal(5000, city.Treasury.Funds);
            Assert.Equal(9, city.Budget.TaxRate);

            File.WriteAllText(Path.Combine(folder, "services.json"), """{ "format": "2.0", "services": [ { "id": "fire", "reach": 99 } ] }""");

            var broken = BaseSpecFiles.Load(folder, out problem);

            Assert.NotNull(problem);
            Assert.Contains("services.json", problem);
            Assert.Equal(Treasury.StartingFunds, Flat(broken).Treasury.Funds);
        });
    }

    [Fact]
    public void AModsSpecFilesAreFoundInItsFoldersAndTheLaterPathWins()
    {
        var definitions = WithMod(
            ("roads/a/infrastructure.json", """{ "format": "2.0", "infrastructure": [ { "id": "road", "price": 20 }, { "id": "bulldozer", "price": 3 } ] }"""),
            ("roads/b/infrastructure.json", """{ "format": "2.0", "infrastructure": [ { "id": "road", "price": 30 } ] }"""),
            ("notes/readme.json", "not a spec, so not read"));
        var city = Flat(definitions);

        Assert.Equal(30, city.Builder.Apply(BuildAction.Road, 5, 5).Cost);
        Assert.Equal(3, definitions.Infrastructure.Bulldozer);
        Assert.Equal(["m"], definitions.Mods);
    }

    [Fact]
    public void PlantsJsonIsStillReadAsPowerJson()
    {
        var definitions = WithMod(("plants.json", """{ "format": 1, "plants": [ { "id": "coal", "price": 1000 } ] }"""));

        Assert.Equal(1000, definitions.Plant(TileUse.CoalPowerPlant).Price);
    }

    [Theory]
    [InlineData("plants.json", "2.0", "plants.json: format 2.0 names this file power.json; plants.json is format 1.0.")]
    [InlineData("power.json", "1.0", "power.json: format 1.0 names this file plants.json; power.json is from 2.0.")]
    public void APowerFileUnderTheOtherFormatsNameSaysSo(string name, string format, string expected)
    {
        Assert.False(DefinitionLoader.TryParseMod("m", Manifest, [new SpecFile(name, $$"""{ "format": "{{format}}", "plants": [] }""")], out _, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void AStationsStrengthScalesItsCoverAndItsReachHowFarItSpreads()
    {
        static ServiceCoverage Station(int strength, int reach)
        {
            var services = $$"""{ "format": "2.0", "services": [ { "id": "fire", "strength": {{strength}}, "reach": {{reach}} } ] }""";
            var coverage = new ServiceCoverage(TileMap.FromTerrain(64, 64, new TileKind[64 * 64]), WithMod(("services.json", services)));
            coverage.StartCount();
            coverage.AddStation(28, 28, TileUse.FireStation, fundingEffect: 100, powered: true, served: true);
            coverage.SpreadFire();
            return coverage;
        }

        var near = Station(100, 0);
        var strong = Station(200, 0);
        var far = Station(100, 3);

        Assert.Equal(2 * near.FireCoverageAt(28, 28), strong.FireCoverageAt(28, 28));
        Assert.Equal(0, near.FireCoverageAt(28 + (2 * ServiceCoverage.BlockSize), 28));
        Assert.True(far.FireCoverageAt(28 + (2 * ServiceCoverage.BlockSize), 28) > 0);
        Assert.Equal(0, Station(0, 3).FireCoverageAt(28, 28));
    }

    [Fact]
    public void StrengthAndReachAreRefusedInAFormat1File()
    {
        Assert.False(DefinitionLoader.TryParseMod("m", Manifest, [new SpecFile("services.json", """{ "format": 1, "services": [ { "id": "fire", "reach": 2 } ] }""")], out _, out var error));
        Assert.Equal("services.json: services[0]: strength and reach are from format 2.0; this file is 1.0.", error);
    }

    [Fact]
    public void AFacilitysSpecSetsTheSizeThatWantsItAndItsPollution()
    {
        var definitions = WithMod(("facilities.json", """{ "format": "2.0", "facilities": [ { "id": "stadium", "demandCap": 600 }, { "id": "seaport", "pollution": 0 } ] }"""));

        var city = Flat(definitions);
        while (!(city.CityTime % 64 == 26 && city.TickCount % CityCalendar.StepsPerCount == 9))
        {
            city.Tick();
        }

        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["census"]!["residential"] = 501;
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), definitions, out var counted, out var problem, out _), problem);
        Run(counted, 1);
        Assert.Equal(DemandCaps.None, counted.Facilities.Caps);

        var port = Flat(definitions);
        port.Builder.Apply(BuildAction.Seaport, 3, 3);
        Run(port, CityCalendar.StepsPerCount);
        Assert.Equal(0, port.Maps.PollutionAt(3, 3));
    }
}
