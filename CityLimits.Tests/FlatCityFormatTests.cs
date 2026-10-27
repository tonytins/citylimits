using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class FlatCityFormatTests
{
    private const int Year = 48 * 16;

    // A town that exercises everything a save holds: roads, all three zones, a plant, both
    // stations, a park, and water and trees for land value, with the tax and funding changed.
    private static CitySimulation Town()
    {
        var ground = new TileKind[48 * 48];
        for (var y = 0; y < 48; y++)
        {
            ground[(y * 48) + 42] = TileKind.Water;
            ground[(y * 48) + 43] = TileKind.Water;
            for (var x = 36; x < 41; x++)
            {
                ground[(y * 48) + x] = y % 3 == 0 ? TileKind.Trees : TileKind.Grass;
            }
        }

        var city = new CitySimulation(TileMap.FromTerrain(48, 48, ground), 77, funds: 1_000_000);
        Build(city, BuildAction.Road, 2, 10, 34, 10);
        Build(city, BuildAction.ZoneResidential, 4, 8, 31, 8);
        Build(city, BuildAction.ZoneCommercial, 4, 12, 13, 12);
        Build(city, BuildAction.ZoneIndustrial, 19, 12, 31, 12);
        Build(city, BuildAction.CoalPowerPlant, 4, 15, 4, 15);
        Build(city, BuildAction.PowerLine, 2, 7, 2, 13);
        Build(city, BuildAction.FireStation, 22, 15, 22, 15);
        Build(city, BuildAction.PoliceStation, 28, 15, 28, 15);
        Build(city, BuildAction.Park, 8, 2, 14, 4);

        // Built up from the start, so the town has people, jobs, and trips to carry.
        Develop(city, TileUse.Residential, 4, 31, 8, 3);
        Develop(city, TileUse.Commercial, 4, 13, 12, 2);
        Develop(city, TileUse.Industrial, 19, 31, 12, 2);
        city.Budget.SetTaxRate(9);
        city.Budget.SetFunding(80, 100, 90);
        city.Rename("Test Town");
        return city;
    }

    private static void Develop(CitySimulation city, TileUse use, int fromX, int toX, int y, int level)
    {
        for (var x = fromX; x <= toX; x += 3)
        {
            city.Map.SetState(x, y, new TileState(TileKind.Grass, use, development: level, valueClass: 1, powered: true));
        }
    }

    private static void Build(CitySimulation city, BuildAction action, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(action, fromX, fromY);
        stroke.DragTo(toX, toY);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(stroke).Status);
    }

    private static void Run(CitySimulation city, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // Houses or buildings somewhere, so the save has more than bare zones to carry.
    private static bool Grown(CitySimulation city)
    {
        for (var y = 0; y < city.Map.Height; y++)
        {
            for (var x = 0; x < city.Map.Width; x++)
            {
                var state = city.Map.StateAt(x, y);
                if (state.HasHouse || state.Development > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static CitySimulation Load(string text)
    {
        Assert.True(FlatCityFormat.TryRead(text, out var city, out var problem), problem);
        return city;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(Year / 2 + 11)]
    [InlineData(Year - 1)]
    public void ALoadedCityRunsOnTickForTickLikeTheOneNeverSaved(int extraSteps)
    {
        var city = Town();
        Run(city, (2 * Year) + extraSteps);

        var loaded = Load(FlatCityFormat.Write(city));

        // In step, compared often: some state, such as land value's terrain, only lasts a pass, and
        // a difference in it may not outlive two years.
        for (var step = 1; step <= 2 * Year; step++)
        {
            city.Tick();
            loaded.Tick();
            if (step <= 20 || step % 64 == 0)
            {
                Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(loaded));
            }
        }

        Assert.Equal(city.Random.NextUInt64(), loaded.Random.NextUInt64());
    }

    [Fact]
    public void ASaveReadsBackAsTheSameSave()
    {
        var city = Town();
        Run(city, Year + 5);
        Assert.True(Grown(city), "the town should have grown, so the save holds buildings and houses");
        var text = FlatCityFormat.Write(city);

        var loaded = Load(text);

        Assert.Equal(text, FlatCityFormat.Write(loaded));
        Assert.Equal("Test Town", loaded.Name);
        Assert.Equal(77, loaded.Seed);
        Assert.Equal(city.TickCount, loaded.TickCount);
        Assert.Equal(9, loaded.Budget.TaxRate);
    }

    [Fact]
    public void AnUntouchedCitySavesAndLoads()
    {
        var city = new CitySimulation(TileMap.FromTerrain(10, 8, new TileKind[80]), 5);

        var loaded = Load(FlatCityFormat.Write(city));

        Assert.Equal(0, loaded.TickCount);
        Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(loaded));
    }

    [Fact]
    public void TheMapIsWrittenAsRowsOfCharacters()
    {
        var city = Town();

        var map = JsonNode.Parse(FlatCityFormat.Write(city))!["map"]!;

        Assert.Equal(48, map["ground"]!.AsArray().Count);
        Assert.Equal("~", map["ground"]![0]!.GetValue<string>()[42..43]);
        Assert.Equal("=", map["use"]![10]!.GetValue<string>()[5..6]);
        Assert.Equal("x", map["use"]![10]!.GetValue<string>()[2..3]);
        Assert.Equal("4", map["part"]![8]!.GetValue<string>()[4..5]);
        Assert.Contains(map["buildings"]!.AsArray(), building => (int)building!["x"]! == 4 && (int)building["y"]! == 15);
    }

    [Fact]
    public void ACityKeepsTheDefinitionsItWasMadeWith()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var mod = Path.Combine(folder, "cheap-commerce");
            Directory.CreateDirectory(mod);
            File.WriteAllText(Path.Combine(mod, "mod.json"), """{ "format": 1, "id": "cheap-commerce", "name": "Cheap commerce", "version": "2.0" }""");
            File.WriteAllText(Path.Combine(mod, "zones.json"), """{ "format": 1, "zones": [ { "id": "commercial", "price": 40 } ] }""");
            var definitions = CityLimits.Core.Definitions.ModLoader.Load(folder, out _);
            var city = new CitySimulation(TileMap.FromTerrain(10, 8, new TileKind[80]), 5, definitions: definitions);

            var loaded = Load(FlatCityFormat.Write(city));

            Assert.Equal(40, loaded.Definitions.Zone(TileUse.Commercial).Price);
            Assert.Equal([new CityLimits.Core.Definitions.AppliedMod("cheap-commerce", "Cheap commerce", "2.0")], loaded.Definitions.AppliedMods);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // Each change breaks one thing in an otherwise good save; the message names what.
    [Theory]
    [InlineData("format", "2", "format: this is format 2.0; this version of CityLimits reads up to 1.10.")]
    [InlineData("name", "\"\"", "name:")]
    [InlineData("random", "\"xyz\"", "random:")]
    [InlineData("cityTime", "3", "cityTime:")]
    [InlineData("tick", "-1", "tick:")]
    [InlineData("map.width", "2000", "map.width:")]
    [InlineData("budget.taxRate", "40", "budget.taxRate:")]
    [InlineData("demand.residential", "99999", "demand.residential:")]
    [InlineData("census.residentialHistory", "[1,2]", "census.residentialHistory:")]
    [InlineData("cityMaps.terrain", "[]", "cityMaps.terrain:")]
    [InlineData("power.plants", "[1]", "power.plants:")]
    [InlineData("definitions", "{ \"format\": 1 }", "definitions:")]
    public void ADamagedSaveIsRefusedAndSaysWhere(string field, string value, string expected)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Town()))!.AsObject();
        var path = field.Split('.');
        var parent = path.Length == 1 ? save : save[path[0]]!.AsObject();
        parent[path[^1]] = JsonNode.Parse(value);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out var city, out var problem));

        Assert.Null(city);
        Assert.StartsWith(expected, problem);
    }

    [Fact]
    public void AnUnknownOrMissingFieldIsRefused()
    {
        var unknown = JsonNode.Parse(FlatCityFormat.Write(Town()))!.AsObject();
        unknown["cheats"] = true;
        var missing = JsonNode.Parse(FlatCityFormat.Write(Town()))!.AsObject();
        missing.Remove("budget");

        Assert.False(FlatCityFormat.TryRead(unknown.ToJsonString(), out _, out var unknownProblem));
        Assert.False(FlatCityFormat.TryRead(missing.ToJsonString(), out _, out var missingProblem));

        Assert.Contains("cheats", unknownProblem);
        Assert.Contains("budget", missingProblem);
        Assert.False(FlatCityFormat.TryRead("not json", out _, out _));
        Assert.False(FlatCityFormat.TryRead("", out _, out _));
    }

    [Theory]
    [InlineData("ground", 3, 5, '?', "map at 5, 3: '?' isn't a kind of ground.")]
    [InlineData("use", 3, 5, '?', "map at 5, 3: '?' isn't a use.")]
    [InlineData("part", 8, 4, '9', "map at 4, 8: '9' isn't a part of a 3 by 3 Residential.")]
    [InlineData("part", 8, 4, '0', "map at 3, 7: the Residential it belongs to isn't whole")]
    [InlineData("use", 8, 4, '.', "map at 4, 8: a None tile has no footprint part")]
    public void ADamagedMapIsRefusedAndSaysWhere(string layer, int row, int column, char replacement, string expected)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Town()))!.AsObject();
        var rows = save["map"]![layer]!.AsArray();
        var text = rows[row]!.GetValue<string>().ToCharArray();
        text[column] = replacement;
        rows[row] = new string(text);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));

        Assert.StartsWith(expected, problem);
    }

    [Fact]
    public void ABuildingBeyondItsSpecIsRefused()
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(Town()))!.AsObject();
        var building = save["map"]!["buildings"]!.AsArray().First(b => (int)b!["x"]! == 4 && (int)b["y"]! == 8)!;
        building["development"] = 5;

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));

        Assert.StartsWith("map.buildings: the Residential zone at 4, 8 is level 5", problem);
    }

    [Fact]
    public void AFileIsWrittenWholeAndReadBack()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var path = Path.Combine(folder, "Cities", "Test Town.fcl");
            var city = Town();
            Run(city, 100);

            Assert.True(FlatCityFormat.TryWriteFile(city, path, out var problem), problem);
            Assert.True(FlatCityFormat.TryWriteFile(city, path, out problem), problem);
            Assert.True(FlatCityFormat.TryReadFile(path, out var loaded, out problem), problem);
            Assert.True(FlatCityFormat.TryReadSummary(path, out var summary));

            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(loaded));
            Assert.Equal("Test Town", summary.Name);
            Assert.Equal(city.Date, summary.Date);
            Assert.False(FlatCityFormat.TryReadFile(Path.Combine(folder, "missing.fcl"), out _, out _));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

public class CityFilesTests
{
    [Theory]
    [InlineData("Joffburg", "Joffburg.cl")]
    [InlineData("  New Dullsville ", "New Dullsville.cl")]
    [InlineData("A/B:C*D?", "A_B_C_D_.cl")]
    [InlineData("Ends with dot.", "Ends with dot.cl")]
    [InlineData("CON", "_CON.cl")]
    [InlineData("com1", "_com1.cl")]
    public void ACityIsSavedUnderAFileNameItsNameCanMake(string name, string expected)
    {
        Assert.Equal(expected, CityFiles.FileNameFor(name));
    }

    [Fact]
    public void ANameACityCantHaveHasNoFile()
    {
        Assert.Throws<ArgumentException>(() => CityFiles.FileNameFor(" "));
    }

    [Fact]
    public void TheListIsTheFoldersCitiesNewestFirstLeavingOutWhatCantBeRead()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var older = new CitySimulation(TileMap.FromTerrain(6, 6, new TileKind[36]), 1);
            older.Rename("Older");
            var newer = new CitySimulation(TileMap.FromTerrain(6, 6, new TileKind[36]), 2);
            newer.Rename("Newer");
            Assert.True(FlatCityFormat.TryWriteFile(older, Path.Combine(folder, "Older.fcl"), out _));
            Assert.True(FlatCityFormat.TryWriteFile(newer, Path.Combine(folder, "Newer.fcl"), out _));
            File.SetLastWriteTimeUtc(Path.Combine(folder, "Older.fcl"), DateTime.UtcNow.AddHours(-1));
            File.WriteAllText(Path.Combine(folder, "Broken.fcl"), "{ not a city");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a city either");

            var cities = CityFiles.List(folder);

            Assert.Equal(["Newer", "Older"], cities.Select(city => city.Name));
            Assert.Empty(CityFiles.List(Path.Combine(folder, "missing")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
