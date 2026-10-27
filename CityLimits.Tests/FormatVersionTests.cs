using System.Text.Json.Nodes;
using CityLimits.Core.Definitions;
using CityLimits.Core.Formats;
using CityLimits.Core.Saving;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class FormatVersionTests
{
    [Theory]
    [InlineData("1.0", 1, 0)]
    [InlineData("1.1", 1, 1)]
    [InlineData("1.10", 1, 10)]
    [InlineData("2", 2, 0)]
    public void AVersionReadsAsMajorAndMinor(string text, int major, int minor)
    {
        Assert.True(FormatVersion.TryParse(text, out var version));
        Assert.Equal(new FormatVersion(major, minor), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.")]
    [InlineData("1.0.3")]
    [InlineData("-1.0")]
    [InlineData("one")]
    public void ATextThatIsntAVersionIsRefused(string text)
    {
        Assert.False(FormatVersion.TryParse(text, out _));
    }

    [Fact]
    public void AReaderReadsItsOwnMajorUpToItsMinor()
    {
        var reader = new FormatVersion(1, 1);

        Assert.Null(new FormatVersion(1, 0).ProblemReadingWith(reader));
        Assert.Null(new FormatVersion(1, 1).ProblemReadingWith(reader));
        Assert.Equal("this is format 1.2; this version of CityLimits reads up to 1.1.", new FormatVersion(1, 2).ProblemReadingWith(reader));
        Assert.Equal("this is format 2.0; this version of CityLimits reads up to 1.1.", new FormatVersion(2, 0).ProblemReadingWith(reader));
        Assert.Equal("this is format 0.9; this version of CityLimits reads 1.x.", new FormatVersion(0, 9).ProblemReadingWith(reader));
        Assert.True(new FormatVersion(1, 10) > new FormatVersion(1, 9));
    }

    [Fact]
    public void AFlatSaveWritesItsVersionAndSchemaAndOlderSavesStillRead()
    {
        var city = new CitySimulation(TileMap.FromTerrain(6, 6, new TileKind[36]), 3);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();

        Assert.Equal("1.10", save["format"]!.GetValue<string>());
        Assert.Equal(FormatSchemas.UrlFor("city-save", FlatCityFormat.Format), save["$schema"]!.GetValue<string>());

        // A save from before major and minor versions wrote a whole number, and no schema.
        save["format"] = 1;
        save.Remove("$schema");
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem), problem);
    }

    [Fact]
    public void ABinarySaveCarriesItsMinorVersionAndANewerOneWithNothingUnknownOpens()
    {
        var city = new CitySimulation(TileMap.FromTerrain(6, 6, new TileKind[36]), 3);
        var bytes = BinaryCityFormat.Write(city);
        Assert.Equal("VERS"u8.ToArray(), bytes[8..12]);

        // The version chunk's minor, after its tag and length (bytes 16 and 17), made newer than the game reads.
        var newer = bytes.ToArray();
        newer[16] = 0;
        newer[17] = 7;
        var crc = Crc(newer[8..12].Concat(newer[16..18]).ToArray());
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(newer.AsSpan(18), crc);

        // A newer save with no parts this version doesn't know holds nothing past this version: it opens,
        // says a newer CityLimits saved it, and saves back as this version.
        Assert.True(BinaryCityFormat.TryRead(newer, out var loaded, out var problem, out var notes), problem);
        Assert.Empty(notes);
        Assert.Equal(bytes, BinaryCityFormat.Write(loaded));
    }

    [Fact]
    public void ScenariosAndModsMayNameTheirSchemaAndWriteTheirVersion()
    {
        var scenario = """{ "$schema": "https://example.com/scenario.json", "format": "1.0", "name": "A", "seed": 1, "ticks": 1 }""";
        var manifest = """{ "$schema": "https://example.com/mod.json", "format": "1.0", "id": "cheap", "name": "Cheap" }""";
        var zones = """{ "$schema": "https://example.com/zones.json", "format": "1.0", "zones": [ { "id": "commercial", "price": 40 } ] }""";

        Assert.True(ScenarioLoader.TryParse(scenario, out _, out var error), error);
        Assert.True(DefinitionLoader.TryParseMod("cheap", manifest, zones, out _, out error), error);
    }

    // The standard CRC-32, written out here so the test checks the game's against its own.
    private static uint Crc(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }
}

public class FormatSchemaTests
{
    // The repository's docs/schemas folder, found from where the tests run.
    private static string SchemasFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "CityLimits.slnx")))
            {
                return Path.Combine(folder.FullName, "docs", "schemas");
            }
        }

        throw new InvalidOperationException("The tests aren't running inside the repository.");
    }

    [Fact]
    public void TheCommittedSchemasAreTheOnesTheCodeMakes()
    {
        var folder = SchemasFolder();
        var generated = FormatSchemas.Generate();
        var stale = new List<string>();
        foreach (var (file, schema) in generated)
        {
            var path = Path.Combine(folder, file);
            if (!File.Exists(path) || File.ReadAllText(path).ReplaceLineEndings() != schema.ReplaceLineEndings())
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, file + ".new"), schema);
                stale.Add(file);
            }
        }

        Assert.True(stale.Count == 0, $"Out of date in docs/schemas: {string.Join(", ", stale)}; the new ones are beside the tests as .new files.");

        // Older versions' schemas stay, since files of those versions name them; nothing else is there.
        var kinds = generated.Keys.Select(file => file[..file.IndexOf(".v", StringComparison.Ordinal)]).ToHashSet();
        Assert.All(
            Directory.EnumerateFiles(folder, "*.schema.json").Select(path => Path.GetFileName(path)),
            file => Assert.Contains(file[..file.IndexOf(".v", StringComparison.Ordinal)], kinds));
    }

    [Fact]
    public void EachSchemaSaysWhatItIsAndShutsOutUnknownFields()
    {
        foreach (var (file, schema) in FormatSchemas.Generate())
        {
            var root = JsonNode.Parse(schema)!.AsObject();
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", root["$schema"]!.GetValue<string>());
            Assert.Equal(FormatSchemas.BaseUrl + file, root["$id"]!.GetValue<string>());
            Assert.False(root["additionalProperties"]!.GetValue<bool>(), file);
            Assert.NotNull(root["properties"]!["format"]);
        }
    }
}
