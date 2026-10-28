using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using CityLimits.Core.Definitions;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

// A newer CityLimits's save, made by hand: format 2.0 still, since a feature is known by its name,
// not by a version. It has a "weather" part this version doesn't know, which it leaves out.
public class ForwardCompatibilityTests
{
    private static CitySimulation City()
    {
        var city = new CitySimulation(TileMap.FromTerrain(10, 8, new TileKind[80]), 3);
        for (var i = 0; i < 40; i++)
        {
            city.Tick();
        }

        return city;
    }

    private static string FutureFlat(bool listed = true)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(City()))!.AsObject();
        save["weather"] = new JsonObject { ["levels"] = new JsonArray(1, 2, 3) };
        save["features"]!.AsArray().Add(JsonValue.Create("weather"));

        if (!listed)
        {
            save.Remove("features");
        }

        return save.ToJsonString();
    }

    private static bool Names(JsonNode? list, string name) => list!.AsArray().Any(feature => feature!.GetValue<string>() == name);

    [Fact]
    public void ASaveNamesItsFeaturesAndNothingElse()
    {
        var written = JsonNode.Parse(FlatCityFormat.Write(City()))!.AsObject();

        Assert.Equal(
            ["traffic", "rail", "pollution", "crime", "fire", "flood", "challenge", "facilities", "founding", "ordinances", "connections"],
            written["features"]!.AsArray().Select(feature => feature!.GetValue<string>()));
        Assert.EndsWith("city-save.v2.0.schema.json", written["$schema"]!.GetValue<string>());
    }

    [Fact]
    public void APartThisVersionDoesntKnowIsLeftOutHereAndWhenSaved()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(), out var city, out var problem, out var notes), problem);

        Assert.Equal(
            [
                "A newer CityLimits saved this city with parts this version doesn't use.",
                "Its weather part is left out, and is left out when you save it.",
            ],
            notes);
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("2.0", written["format"]!.GetValue<string>());
        Assert.Null(written["weather"]);
        Assert.False(Names(written["features"], "weather"));
    }

    [Fact]
    public void TheFeaturesListMayBeLeftOut()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(listed: false), out _, out var problem, out var notes), problem);

        Assert.Equal("Its weather part is left out, and is left out when you save it.", notes[1]);
    }

    [Fact]
    public void AFileFor04IsFormat110WithItsDefinitionsAndPartsAndRunsInStepWithTheCity()
    {
        var city = City();

        var written = JsonNode.Parse(FlatCityFormat.Write(city, legacy: true))!.AsObject();

        Assert.Equal("1.10", written["format"]!.GetValue<string>());
        Assert.EndsWith("city-save.v1.10.schema.json", written["$schema"]!.GetValue<string>());
        Assert.Null(written["features"]);
        Assert.Contains(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "founding");
        Assert.All(written["parts"]!.AsArray(), part => Assert.NotNull(part!["since"]));
        Assert.True(DefinitionLoader.TryParseBase(written["definitions"]!.ToJsonString(), out _, out var error), error);

        Assert.True(FlatCityFormat.TryRead(written.ToJsonString(), out var reopened, out var problem, out _), problem);
        for (var i = 0; i < CityCalendar.StepsPerYear; i++)
        {
            city.Tick();
            reopened.Tick();
        }

        Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(reopened));
    }

    [Fact]
    public void AFileFor04LeavesOutTheFeaturesFormat1CantHold()
    {
        var written = JsonNode.Parse(FlatCityFormat.Write(City(), legacy: true))!.AsObject();

        Assert.Equal("1.10", written["format"]!.GetValue<string>());
        Assert.Null(written["connections"]);
        Assert.Null(written["ordinances"]);
    }

    [Fact]
    public void ABinaryFileFor04IsFormat110WithItsDefinitionsAndParts()
    {
        var city = City();

        var written = BinaryCityFormat.Write(city, legacy: true);
        var chunks = Chunks(written);

        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(written.AsSpan(6)));
        Assert.Equal([0, 10], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Contains(chunks, chunk => chunk.Tag == "PART");
        Assert.Contains(chunks, chunk => chunk.Tag == "DEFS");
        Assert.DoesNotContain(chunks, chunk => chunk.Tag is "FEAT" or "MODS");
        Assert.True(BinaryCityFormat.TryRead(written, out var reopened, out var problem, out _), problem);
        Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(reopened));
    }

    [Fact]
    public void ABinarySavesUnknownChunkIsLeftOut()
    {
        Assert.True(BinaryCityFormat.TryRead(FutureBinary(), out var city, out var problem, out var notes), problem);

        Assert.Contains("Its weather part is left out, and is left out when you save it.", notes);
        var written = BinaryCityFormat.Write(city);
        var chunks = Chunks(written);
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(written.AsSpan(6)));
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "WTHR");
        Assert.Single(chunks, chunk => chunk.Tag == "CONN");
    }

    // Bridgeport with rail, opened from 0.3.106's save in each backport (0.3.100.1, 0.3.101.1, and
    // 0.3.102.1), run for 64 steps, and saved: older versions, with the traffic they kept and their
    // parts list, and without the rail they left out. They open as any 1.x save does, and save as 2.0.
    [Theory]
    [InlineData("Bridgeport.from-0.3.100.1.fcl")]
    [InlineData("Bridgeport.from-0.3.100.1.cl")]
    [InlineData("Bridgeport.from-0.3.101.1.fcl")]
    [InlineData("Bridgeport.from-0.3.101.1.cl")]
    [InlineData("Bridgeport.from-0.3.102.1.fcl")]
    [InlineData("Bridgeport.from-0.3.102.1.cl")]
    public void ASaveABackportWroteOpensAsAnOlderOne(string file)
    {
        Assert.True(CityFiles.TryRead(Path.Combine(AppContext.BaseDirectory, "Assets", file), out var city, out var problem, out var notes), problem);

        Assert.Empty(notes);
        Assert.Equal(0, city.RailTiles);
        Assert.Equal("2.0", JsonNode.Parse(FlatCityFormat.Write(city))!["format"]!.GetValue<string>());
    }

    [Fact]
    public void AnAutosaveFollowsItsCitysFormat()
    {
        Assert.Equal("Joffburg (autosave).fcl", Autosave.FileNameFor("Joffburg", 1, FlatCityFormat.Extension));
        Assert.Equal(Path.Combine("Cities", "Joffburg.fcl"), Autosave.CityFileFor(Path.Combine("Cities", "Joffburg (autosave 2).fcl")));
    }

    // The binary save as docs/saving.md lays it out: the header, then chunks, with the features list
    // given weather, and its own chunk added.
    private static byte[] FutureBinary()
    {
        var bytes = BinaryCityFormat.Write(City());
        var rebuilt = new List<byte>(bytes[..8]);
        foreach (var (tag, payload) in Chunks(bytes))
        {
            var changed = tag == "FEAT" ? [.. AddFeatures(payload)] : payload;
            rebuilt.AddRange(Chunk(tag, changed));
        }

        rebuilt.AddRange(Chunk("WTHR", [1, 2, 3]));
        return [.. rebuilt];
    }

    // The features list is a count, then each feature: a byte saying it is there, then its name and its
    // chunk. A whole number is a zigzag varint, and a text its length as one, then its bytes.
    private static IEnumerable<byte> AddFeatures(byte[] payload)
    {
        var count = payload[0] >> 1;
        return [.. Varint(count + 1), .. payload[1..], .. FeatureBytes("weather", "WTHR")];
    }

    private static byte[] FeatureBytes(string name, string chunk) => [1, .. Text(name), .. Text(chunk)];

    private static byte[] Varint(long value)
    {
        var zigzag = (ulong)((value << 1) ^ (value >> 63));
        var bytes = new List<byte>();
        while (zigzag >= 0x80)
        {
            bytes.Add((byte)(zigzag | 0x80));
            zigzag >>= 7;
        }

        bytes.Add((byte)zigzag);
        return [.. bytes];
    }

    private static byte[] Text(string text) => [.. Varint(Encoding.UTF8.GetByteCount(text)), .. Encoding.UTF8.GetBytes(text)];

    private static List<(string Tag, byte[] Payload)> Chunks(byte[] bytes)
    {
        var chunks = new List<(string, byte[])>();
        for (var position = 8; position < bytes.Length;)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position + 4));
            chunks.Add((Encoding.ASCII.GetString(bytes, position, 4), bytes[(position + 8)..(position + 8 + length)]));
            position += 12 + length;
        }

        return chunks;
    }

    private static byte[] Chunk(string tag, byte[] payload)
    {
        var tagBytes = Encoding.ASCII.GetBytes(tag);
        var chunk = new byte[12 + payload.Length];
        tagBytes.CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(chunk, 8);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + payload.Length), Crc([.. tagBytes, .. payload]));
        return chunk;
    }

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
