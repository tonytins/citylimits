using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

// A newer CityLimits's save, made by hand: format 1.8, with a "weather" part this version doesn't
// know, listed in its parts list with the flags each test gives it.
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

    private static JsonObject Part(bool ignorable, bool keepable, string name = "weather", string chunk = "WTHR") => new()
    {
        ["name"] = name,
        ["chunk"] = chunk,
        ["since"] = "1.8",
        ["ignorable"] = ignorable,
        ["keepable"] = keepable,
    };

    // With noise, the save has a second part, which can be left out but not kept.
    private static string FutureFlat(bool ignorable, bool keepable, bool listed = true, bool noise = false)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(City()))!.AsObject();
        save["format"] = "1.8";
        save["weather"] = new JsonObject { ["levels"] = new JsonArray(1, 2, 3) };
        if (listed)
        {
            save["parts"]!.AsArray().Add(Part(ignorable, keepable));
        }

        if (noise)
        {
            save["noise"] = new JsonObject { ["levels"] = new JsonArray(4) };
            save["parts"]!.AsArray().Add(Part(ignorable: true, keepable: false, "noise", "NOIS"));
        }

        return save.ToJsonString();
    }

    [Fact]
    public void AKeepablePartOpensAndIsWrittenBackAsItWas()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true), out var city, out var problem, out var notes), problem);

        Assert.Equal(
            [
                "A newer CityLimits saved this city, in format 1.8; this version reads up to 1.7.",
                "Its weather, from 1.8, is kept as it was when you save it.",
            ],
            notes);
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.8", written["format"]!.GetValue<string>());
        Assert.Equal("[1,2,3]", written["weather"]!["levels"]!.ToJsonString());
        Assert.Contains(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "weather");
        Assert.EndsWith("city-save.v1.8.schema.json", written["$schema"]!.GetValue<string>());

        // Written back, it opens the same way again.
        Assert.True(FlatCityFormat.TryRead(written.ToJsonString(), out _, out problem, out _), problem);
    }

    [Fact]
    public void AnIgnorablePartThatIsntKeepableIsLeftOutAndTheSaveIsThisVersion()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: false), out var city, out var problem, out var notes), problem);

        Assert.Equal("Its weather, from 1.8, will be left out when you save it, and the newer version will rebuild it.", notes[1]);
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.7", written["format"]!.GetValue<string>());
        Assert.Null(written["weather"]);
        Assert.DoesNotContain(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "weather");
    }

    [Fact]
    public void APartThatCantBeLeftOutRefusesTheSave()
    {
        Assert.False(FlatCityFormat.TryRead(FutureFlat(ignorable: false, keepable: true), out var city, out var problem));

        Assert.Null(city);
        Assert.Equal("format: this is format 1.8; this version of CityLimits reads up to 1.7, and the file's weather, from 1.8, can't be left out.", problem);
    }

    [Fact]
    public void AnUnknownPartTheListDoesntNameIsRefused()
    {
        Assert.False(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true, listed: false), out _, out var problem));

        Assert.Equal("weather: is not a part a save has, and the file's parts list doesn't name it.", problem);
    }

    [Fact]
    public void AnUnknownPartInASaveOfThisVersionIsRefusedUnlessItsPartsListNamesIt()
    {
        var save = JsonNode.Parse(FutureFlat(ignorable: true, keepable: true, listed: false))!.AsObject();
        save["format"] = "1.7";

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));

        Assert.Equal("weather: is not a part a save has, and the file's parts list doesn't name it.", problem);
    }

    [Fact]
    public void AKeptPartGoesIntoASaveOfThisVersionAndOpensAgain()
    {
        // Noise can't be kept, so the save falls back to 1.7, but the weather still goes in, named in
        // the parts list, so the newer game gets it back and this one opens its own save again.
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true, noise: true), out var city, out var problem, out _), problem);

        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.7", written["format"]!.GetValue<string>());
        Assert.Null(written["noise"]);
        Assert.Equal("[1,2,3]", written["weather"]!["levels"]!.ToJsonString());
        Assert.Contains(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "weather");

        Assert.True(FlatCityFormat.TryRead(written.ToJsonString(), out var reopened, out problem, out var notes), problem);
        Assert.Equal("This city carries parts a newer CityLimits saved, which this version doesn't read.", notes[0]);
        Assert.Equal("[1,2,3]", JsonNode.Parse(FlatCityFormat.Write(reopened))!["weather"]!["levels"]!.ToJsonString());
    }

    [Fact]
    public void KeptPartsGoBackOnlyIntoTheirOwnFormat()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true), out var city, out var problem, out _), problem);

        var binary = BinaryCityFormat.Write(city);

        Assert.DoesNotContain("WTHR", Encoding.ASCII.GetString(binary));
        Assert.True(BinaryCityFormat.TryRead(binary, out _, out problem, out var notes), problem);
        Assert.Empty(notes);
    }

    [Fact]
    public void ABinarySavesKeepablePartOpensAndIsWrittenBack()
    {
        var future = FutureBinary(ignorable: true, keepable: true);

        Assert.True(BinaryCityFormat.TryRead(future, out var city, out var problem, out var notes), problem);

        Assert.Equal("Its weather, from 1.8, is kept as it was when you save it.", notes[1]);
        var written = BinaryCityFormat.Write(city);
        var chunks = Chunks(written);
        Assert.Equal([0, 8], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Equal([7, 8, 9], chunks.Single(chunk => chunk.Tag == "WTHR").Payload);
        Assert.True(BinaryCityFormat.TryRead(written, out _, out problem, out _), problem);
    }

    [Fact]
    public void ABinarySavesPartThatCantBeLeftOutRefusesIt()
    {
        Assert.False(BinaryCityFormat.TryRead(FutureBinary(ignorable: false, keepable: false), out _, out var problem, out _));

        Assert.Equal("format: this is format 1.8; this version of CityLimits reads up to 1.7, and the file's weather, from 1.8, can't be left out.", problem);
    }

    [Fact]
    public void ABinarySavesPartThatIsntKeepableIsLeftOut()
    {
        Assert.True(BinaryCityFormat.TryRead(FutureBinary(ignorable: true, keepable: false), out var city, out var problem, out _), problem);

        var chunks = Chunks(BinaryCityFormat.Write(city));

        Assert.Equal([0, 7], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "WTHR");
    }

    [Fact]
    public void ABinarySavesKeptPartGoesIntoASaveOfThisVersionAndOpensAgain()
    {
        Assert.True(BinaryCityFormat.TryRead(FutureBinary(ignorable: true, keepable: true, noise: true), out var city, out var problem, out _), problem);

        var written = BinaryCityFormat.Write(city);
        var chunks = Chunks(written);
        Assert.Equal([0, 7], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Equal([7, 8, 9], chunks.Single(chunk => chunk.Tag == "WTHR").Payload);
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "NOIS");

        // Before, the reopened save skipped the chunk, and the next save lost it.
        Assert.True(BinaryCityFormat.TryRead(written, out var reopened, out problem, out var notes), problem);
        Assert.Equal("This city carries parts a newer CityLimits saved, which this version doesn't read.", notes[0]);
        Assert.Equal([7, 8, 9], Chunks(BinaryCityFormat.Write(reopened)).Single(chunk => chunk.Tag == "WTHR").Payload);
    }

    // Bridgeport with rail, opened from 0.3.106's save in each backport (0.3.100.1, 0.3.101.1, and
    // 0.3.102.1), run for 64 steps, and saved: older versions, with the traffic they kept and their
    // parts list, and without the rail they left out.
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
        Assert.Equal("1.7", JsonNode.Parse(FlatCityFormat.Write(city))!["format"]!.GetValue<string>());
    }

    [Fact]
    public void AnAutosaveFollowsItsCitysFormat()
    {
        Assert.Equal("Joffburg (autosave).fcl", Autosave.FileNameFor("Joffburg", 1, FlatCityFormat.Extension));
        Assert.Equal(Path.Combine("Cities", "Joffburg.fcl"), Autosave.CityFileFor(Path.Combine("Cities", "Joffburg (autosave 2).fcl")));
    }

    // The binary save as docs/saving.md lays it out: the header, then chunks, with the minor version
    // made 8, the parts list given the weather part (and noise, when asked), and the parts' own chunks added.
    private static byte[] FutureBinary(bool ignorable, bool keepable, bool noise = false)
    {
        var bytes = BinaryCityFormat.Write(City());
        var rebuilt = new List<byte>(bytes[..8]);
        foreach (var (tag, payload) in Chunks(bytes))
        {
            var changed = tag switch
            {
                "VERS" => [0, 8],
                "PART" => [.. AddParts(payload, ignorable, keepable, noise)],
                _ => payload,
            };
            rebuilt.AddRange(Chunk(tag, changed));
        }

        rebuilt.AddRange(Chunk("WTHR", [7, 8, 9]));
        if (noise)
        {
            rebuilt.AddRange(Chunk("NOIS", [4]));
        }

        return [.. rebuilt];
    }

    // The parts list is a count, then each part: a byte saying it is there, and its fields in order,
    // three texts and two yes-or-no bytes. A whole number is a zigzag varint, and a text its length
    // as one, then its bytes.
    private static IEnumerable<byte> AddParts(byte[] payload, bool ignorable, bool keepable, bool noise)
    {
        var count = payload[0] >> 1;
        List<byte> parts = [.. Varint(count + (noise ? 2 : 1)), .. payload[1..], .. PartBytes("weather", "WTHR", ignorable, keepable)];
        if (noise)
        {
            parts.AddRange(PartBytes("noise", "NOIS", ignorable: true, keepable: false));
        }

        return parts;
    }

    private static byte[] PartBytes(string name, string chunk, bool ignorable, bool keepable) =>
        [1, .. Text(name), .. Text(chunk), .. Text("1.8"), (byte)(ignorable ? 1 : 0), (byte)(keepable ? 1 : 0)];

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
