using CityLimits.Core.Definitions;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class DefinitionTests
{
    private const string Manifest = """{ "format": 1, "id": "cheap-zones", "name": "Cheap zones", "version": "1.0" }""";

    [Fact]
    public void TheBaseGameIsValidAndHasMicropolisPrices()
    {
        Assert.True(DefinitionLoader.TryParseBase(BaseGame.Json, out var definitions, out var error), error);
        Assert.All([TileUse.Residential, TileUse.Commercial, TileUse.Industrial], use => Assert.Equal(100, definitions.Zone(use).Price));
        Assert.Equal("Residential zone", definitions.Zone(TileUse.Residential).Name);
        Assert.Empty(definitions.Mods);
        Assert.Same(CityDefinitions.Base, CityDefinitions.Base);
    }

    [Fact]
    public void AToolCostsWhatTheDefinitionsSay()
    {
        var definitions = CityDefinitions.Base;

        Assert.Equal(10, definitions.CostOf(BuildAction.Road));
        Assert.Equal(100, definitions.CostOf(BuildAction.ZoneIndustrial));
        Assert.Equal(1, definitions.CostOf(BuildAction.Bulldoze));
        Assert.Throws<ArgumentOutOfRangeException>(() => definitions.Zone(TileUse.Road));
    }

    [Theory]
    [InlineData("""{ "format": 1, "zones": [ { "id": "residential", "name": "R", "price": 1, "populations": [1], "valueClasses": 1, "houses": 0, "trips": ["commercial"] } ] }""", "zones: the base game must define commercial, industrial.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "residential", "name": "R" } ] }""", "zones[0].price: is required.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "residential", "name": "R", "price": 1, "valueClasses": 1, "houses": 0 } ] }""", "zones[0].populations: is required.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "commercial", "name": "C", "price": 1, "populations": [1], "valueClasses": 1, "houses": 2, "trips": ["residential"] } ] }""", "zones[0].houses: only a residential zone grows single houses.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "residential", "name": "R", "price": 1, "populations": [1, 2, 3, 4, 5, 6], "valueClasses": 1, "houses": 0 } ] }""", "zones[0].populations: needs 1 to 5 levels, not 6.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "hotel", "name": "H", "price": 1 } ] }""", "zones[0].id: 'hotel' is not one of residential, commercial, industrial.")]
    [InlineData("""{ "format": 2, "zones": [] }""", "format: this is format 2.0; this version of CityLimits reads up to 1.0.")]
    public void TheBaseGameMustDefineEveryZoneAndField(string json, string expected)
    {
        Assert.False(DefinitionLoader.TryParseBase(json, out _, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void AModChangesOnlyTheFieldsItSets()
    {
        Assert.True(DefinitionLoader.TryParseMod("cheap-zones", Manifest, """{ "format": 1, "zones": [ { "id": "commercial", "price": 40 } ] }""", out var mod, out var error), error);

        var definitions = ModLoaderWith(mod);

        Assert.Equal(40, definitions.Zone(TileUse.Commercial).Price);
        Assert.Equal("Commercial zone", definitions.Zone(TileUse.Commercial).Name);
        Assert.Equal(100, definitions.Zone(TileUse.Residential).Price);
        Assert.Equal(["cheap-zones"], definitions.Mods);
        Assert.Equal([new AppliedMod("cheap-zones", mod!.Name, mod.Version)], definitions.AppliedMods);
    }

    [Theory]
    [InlineData("""{ "format": 1, "id": "other", "name": "X" }""", null, "mod.json: id: 'other' must be the mod's folder name, 'cheap-zones', in lowercase letters, digits, and single hyphens.")]
    [InlineData("""{ "format": 1, "id": "cheap-zones" }""", null, "mod.json: name: is required.")]
    [InlineData("""{ "format": 1, "id": "cheap-zones", "name": "X", "author": "me" }""", null, "mod.json: author: is not a field definitions have.")]
    [InlineData(Manifest, """{ "format": 1, "zones": [ { "id": "commercial", "price": -5 } ] }""", "zones.json: zones[0].price: needs a number from 0 to 1000000, not -5.")]
    [InlineData(Manifest, """{ "format": 1, "zones": [ { "id": "commercial" }, { "id": "commercial" } ] }""", "zones.json: zones[1].id: 'commercial' is defined twice.")]
    [InlineData(Manifest, "{ not json", "zones.json: the file: is not valid JSON, or has the wrong type of value.")]
    public void AModIsCheckedBeforeItIsUsed(string manifest, string? zones, string expected)
    {
        Assert.False(DefinitionLoader.TryParseMod("cheap-zones", manifest, zones, out var mod, out var error));
        Assert.Null(mod);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void ModsInAFolderApplyInIdOrderAndABrokenOneIsSkippedWhole()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            WriteMod(folder, "b-pricey", """{ "format": 1, "zones": [ { "id": "residential", "price": 300 } ] }""");
            WriteMod(folder, "a-cheap", """{ "format": 1, "zones": [ { "id": "residential", "price": 50 }, { "id": "industrial", "price": 70 } ] }""");
            WriteMod(folder, "c-broken", """{ "format": 1, "zones": [ { "id": "industrial", "price": 1 }, { "id": "park", "price": 1 } ] }""");
            Directory.CreateDirectory(Path.Combine(folder, "d-empty"));

            var definitions = ModLoader.Load(folder, out var problems);

            Assert.Equal(["a-cheap", "b-pricey"], definitions.Mods);
            Assert.Equal(300, definitions.Zone(TileUse.Residential).Price);
            Assert.Equal(70, definitions.Zone(TileUse.Industrial).Price);
            Assert.Equal(2, problems.Count);
            Assert.StartsWith("The mod 'c-broken' was skipped: zones.json: zones[1].id: 'park'", problems[0]);
            Assert.Equal("The mod 'd-empty' was skipped: it has no mod.json.", problems[1]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AMissingModsFolderMeansTheBaseGame()
    {
        var definitions = ModLoader.Load("/no/such/mods", out var problems);

        Assert.Same(CityDefinitions.Base, definitions);
        Assert.Empty(problems);
    }

    [Fact]
    public void ACityPaysWhatItsDefinitionsSayForAZone()
    {
        Assert.True(DefinitionLoader.TryParseMod("cheap-zones", Manifest, """{ "format": 1, "zones": [ { "id": "residential", "price": 40 } ] }""", out var mod, out _));
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        var city = new CitySimulation(map, 1, funds: 1000, definitions: ModLoaderWith(mod));

        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal(960, city.Treasury.Funds);
    }

    [Fact]
    public void TheReadoutNamesAZoneAsItsDefinitionsDo()
    {
        Assert.True(DefinitionLoader.TryParseMod("cheap-zones", Manifest, """{ "format": 1, "zones": [ { "id": "industrial", "name": "Factory district" } ] }""", out var mod, out _));

        var line = TileInfo.Describe(new TileState(TileKind.Grass, TileUse.Industrial), ModLoaderWith(mod));

        Assert.Equal("Factory district - not developed yet", line);
    }

    [Fact]
    public void AModChangesAPlantsPriceAndCapacityInItsOwnFile()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var modFolder = Directory.CreateDirectory(Path.Combine(folder, "cheap-coal")).FullName;
            File.WriteAllText(Path.Combine(modFolder, "mod.json"), """{ "format": 1, "id": "cheap-coal", "name": "Cheap coal" }""");
            File.WriteAllText(Path.Combine(modFolder, "plants.json"), """{ "format": 1, "plants": [ { "id": "coal", "name": "Coal works", "price": 1000, "capacity": 900 } ] }""");

            var definitions = ModLoader.Load(folder, out var problems);

            Assert.Empty(problems);
            Assert.Equal(new PlantSpec("coal", TileUse.CoalPowerPlant, "Coal works", 1000, 900), definitions.Plant(TileUse.CoalPowerPlant));
            Assert.Equal(CityDefinitions.Base.Plant(TileUse.NuclearPowerPlant), definitions.Plant(TileUse.NuclearPowerPlant));
            Assert.Equal(1000, definitions.CostOf(BuildAction.CoalPowerPlant));

            var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
            var city = new CitySimulation(map, 1, funds: 5000, definitions: definitions);
            city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);
            Assert.Equal(4000, city.Treasury.Funds);
            Assert.Equal("Coal works - bulldoze its centre to clear it", TileInfo.Describe(map.StateAt(3, 3), definitions));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{ "format": 1, "plants": [ { "id": "coal", "capacity": 0 } ] }""", "plants.json: plants[0].capacity: needs a number from 1 to 1000000, not 0.")]
    [InlineData("""{ "format": 1, "plants": [ { "id": "wind" } ] }""", "plants.json: plants[0].id: 'wind' is not one of coal, nuclear.")]
    [InlineData("""{ "format": 1, "plants": [ { "id": "coal", "price": -1 } ] }""", "plants.json: plants[0].price: needs a number from 0 to 1000000, not -1.")]
    [InlineData("""{ "format": 1, "zones": [] }""", "plants.json: zones: is not a field definitions have.")]
    public void APlantModIsCheckedBeforeItIsUsed(string plants, string expected)
    {
        Assert.False(DefinitionLoader.TryParseMod("cheap-zones", Manifest, null, out var mod, out var error, plants));
        Assert.Null(mod);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void ZonesFilesHoldOnlyZones()
    {
        Assert.False(DefinitionLoader.TryParseMod("cheap-zones", Manifest, """{ "format": 1, "plants": [] }""", out _, out var error));
        Assert.Equal("zones.json: plants: is not a field definitions have.", error);
    }

    [Fact]
    public void TheBaseGameMustDefineEveryPlant()
    {
        var withoutNuclear = BaseGame.Json
            .Replace("""{ "id": "nuclear", "name": "Nuclear power plant", "price": 5000, "capacity": 2000 }""", string.Empty, StringComparison.Ordinal)
            .Replace("\"capacity\": 700 },", "\"capacity\": 700 }", StringComparison.Ordinal);

        Assert.NotEqual(BaseGame.Json, withoutNuclear);
        Assert.False(DefinitionLoader.TryParseBase(withoutNuclear, out _, out var error));
        Assert.Equal("plants: the base game must define nuclear.", error);
    }

    [Fact]
    public void AModChangesAServicesPriceAndUpkeepInItsOwnFile()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var modFolder = Directory.CreateDirectory(Path.Combine(folder, "cheap-police")).FullName;
            File.WriteAllText(Path.Combine(modFolder, "mod.json"), """{ "format": 1, "id": "cheap-police", "name": "Cheap police" }""");
            File.WriteAllText(Path.Combine(modFolder, "services.json"), """{ "format": 1, "services": [ { "id": "police", "name": "Sheriff", "price": 300, "upkeep": 40 } ] }""");

            var definitions = ModLoader.Load(folder, out var problems);

            Assert.Empty(problems);
            Assert.Equal(new ServiceSpec("police", TileUse.PoliceStation, "Sheriff", 300, 40), definitions.Service(TileUse.PoliceStation));
            Assert.Equal(CityDefinitions.Base.Service(TileUse.FireStation), definitions.Service(TileUse.FireStation));

            var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
            var city = new CitySimulation(map, 1, funds: 1000, definitions: definitions);
            city.Builder.Apply(BuildAction.PoliceStation, 4, 4);
            Assert.Equal(700, city.Treasury.Funds);
            for (var i = 0; i < CityCalendar.StepsPerCount - 1; i++)
            {
                city.Tick();
            }

            Assert.Equal(40, city.EstimateBudget().PoliceRequest);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{ "format": 1, "services": [ { "id": "hospital" } ] }""", "services.json: services[0].id: 'hospital' is not one of fire, police.")]
    [InlineData("""{ "format": 1, "services": [ { "id": "fire", "upkeep": -1 } ] }""", "services.json: services[0].upkeep: needs a number from 0 to 1000000, not -1.")]
    public void AServiceModIsCheckedBeforeItIsUsed(string services, string expected)
    {
        Assert.False(DefinitionLoader.TryParseMod("cheap-zones", Manifest, null, out var mod, out var error, servicesJson: services));
        Assert.Null(mod);
        Assert.Equal(expected, error);
    }

    // Applies one mod to the base game the way the loader does, through a folder.
    private static CityDefinitions ModLoaderWith(ModDefinitions mod)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var modFolder = Path.Combine(folder, mod.Id);
            Directory.CreateDirectory(modFolder);
            var version = mod.Version is null ? string.Empty : $", \"version\": \"{mod.Version}\"";
            File.WriteAllText(Path.Combine(modFolder, "mod.json"), $$"""{ "format": 1, "id": "{{mod.Id}}", "name": "{{mod.Name}}"{{version}} }""");
            var zones = mod.Zones.Select(pair =>
                $$"""{ "id": "{{pair.Key.ToString().ToLowerInvariant()}}"{{(pair.Value.Name is null ? "" : $", \"name\": \"{pair.Value.Name}\"")}}{{(pair.Value.Price is null ? "" : $", \"price\": {pair.Value.Price}")}} }""");
            File.WriteAllText(Path.Combine(modFolder, "zones.json"), $$"""{ "format": 1, "zones": [ {{string.Join(", ", zones)}} ] }""");
            return ModLoader.Load(folder, out _);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void WriteMod(string folder, string id, string zones)
    {
        var modFolder = Path.Combine(folder, id);
        Directory.CreateDirectory(modFolder);
        File.WriteAllText(Path.Combine(modFolder, "mod.json"), $$"""{ "format": 1, "id": "{{id}}", "name": "{{id}}" }""");
        File.WriteAllText(Path.Combine(modFolder, "zones.json"), zones);
    }
}
