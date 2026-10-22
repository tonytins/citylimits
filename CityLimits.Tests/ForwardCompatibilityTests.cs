using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

// A newer CityLimits's save, made by hand: format 1.3, with a "pollution" part this version doesn't
// know, listed in its parts list with the flags each test gives it. This is 0.3.101.1, a backport
// that reads up to 1.0, so the parts list a newer save carries is the only one it meets.
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

    private static JsonObject Part(string name, string chunk, string since, bool ignorable, bool keepable) => new()
    {
        ["name"] = name,
        ["chunk"] = chunk,
        ["since"] = since,
        ["ignorable"] = ignorable,
        ["keepable"] = keepable,
    };

    private static JsonObject Pollution(bool ignorable, bool keepable) => Part("pollution", "POLL", "1.3", ignorable, keepable);

    // The parts list a newer CityLimits writes: every part after 1.0, traffic among them.
    private static string FutureFlat(bool ignorable, bool keepable, bool listed = true, bool noise = false)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(City()))!.AsObject();
        save["format"] = "1.3";
        save["pollution"] = new JsonObject { ["levels"] = new JsonArray(1, 2, 3) };
        var parts = new JsonArray(Part("traffic", "TRAF", "1.1", ignorable: true, keepable: true));
        if (listed)
        {
            parts.Add(Pollution(ignorable, keepable));
        }

        if (noise)
        {
            save["noise"] = new JsonObject { ["levels"] = new JsonArray(4) };
            parts.Add(Part("noise", "NOIS", "1.3", ignorable: true, keepable: false));
        }

        save["parts"] = parts;
        return save.ToJsonString();
    }

    [Fact]
    public void AKeepablePartOpensAndIsWrittenBackAsItWas()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true), out var city, out var problem, out var notes), problem);

        Assert.Equal(
            [
                "A newer CityLimits saved this city, in format 1.3; this version reads up to 1.0.",
                "Its pollution, from 1.3, is kept as it was when you save it.",
            ],
            notes);
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.3", written["format"]!.GetValue<string>());
        Assert.Equal("[1,2,3]", written["pollution"]!["levels"]!.ToJsonString());
        Assert.Contains(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "pollution");
        Assert.EndsWith("city-save.v1.3.schema.json", written["$schema"]!.GetValue<string>());

        // Written back, it opens the same way again.
        Assert.True(FlatCityFormat.TryRead(written.ToJsonString(), out _, out problem, out _), problem);
    }

    [Fact]
    public void AnIgnorablePartThatIsntKeepableIsLeftOutAndTheSaveIsThisVersion()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: false), out var city, out var problem, out var notes), problem);

        Assert.Equal("Its pollution, from 1.3, will be left out when you save it, and the newer version will rebuild it.", notes[1]);
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.0", written["format"]!.GetValue<string>());
        Assert.Null(written["pollution"]);

        // With nothing kept, the save is one 0.3.101 itself can open: no parts list.
        Assert.False(written.ContainsKey("parts"));
    }

    [Fact]
    public void AKeptPartGoesIntoASaveOfThisVersionAndOpensAgain()
    {
        // Noise can't be kept, so the save falls back to 1.0, but pollution still goes in, named in
        // the parts list, so the newer game gets it back and this one opens its own save again.
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true, noise: true), out var city, out var problem, out _), problem);

        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.0", written["format"]!.GetValue<string>());
        Assert.Null(written["noise"]);
        Assert.Equal("[1,2,3]", written["pollution"]!["levels"]!.ToJsonString());
        Assert.Contains(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "pollution");

        Assert.True(FlatCityFormat.TryRead(written.ToJsonString(), out var reopened, out problem, out var notes), problem);
        Assert.Equal("This city carries parts a newer CityLimits saved, which this version doesn't read.", notes[0]);
        Assert.Equal("[1,2,3]", JsonNode.Parse(FlatCityFormat.Write(reopened))!["pollution"]!["levels"]!.ToJsonString());
    }

    [Fact]
    public void APartThatCantBeLeftOutRefusesTheSave()
    {
        Assert.False(FlatCityFormat.TryRead(FutureFlat(ignorable: false, keepable: true), out var city, out var problem));

        Assert.Null(city);
        Assert.Equal("format: this is format 1.3; this version of CityLimits reads up to 1.0, and the file's pollution, from 1.3, can't be left out.", problem);
    }

    [Fact]
    public void AnUnknownPartTheListDoesntNameIsRefused()
    {
        Assert.False(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true, listed: false), out _, out var problem));

        Assert.Equal("pollution: is not a part a save has, and the file's parts list doesn't name it.", problem);
    }

    [Fact]
    public void AnUnknownPartInASaveOfThisVersionWithoutAPartsListIsRefused()
    {
        var save = JsonNode.Parse(FutureFlat(ignorable: true, keepable: true))!.AsObject();
        save["format"] = "1.0";
        save.Remove("parts");

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));

        Assert.Equal("pollution: is not a part a save has.", problem);
    }

    [Fact]
    public void ACityThisVersionMadeSavesWithoutAPartsList()
    {
        Assert.False(JsonNode.Parse(FlatCityFormat.Write(City()))!.AsObject().ContainsKey("parts"));
        Assert.DoesNotContain(Chunks(BinaryCityFormat.Write(City())), chunk => chunk.Tag == "PART");
    }

    [Fact]
    public void KeptPartsGoBackOnlyIntoTheirOwnFormat()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true), out var city, out var problem, out _), problem);

        var binary = BinaryCityFormat.Write(city);

        Assert.DoesNotContain("POLL", Encoding.ASCII.GetString(binary));
        Assert.True(BinaryCityFormat.TryRead(binary, out _, out problem, out var notes), problem);
        Assert.Empty(notes);
    }

    [Fact]
    public void ABinarySavesKeepablePartOpensAndIsWrittenBack()
    {
        var future = FutureBinary(ignorable: true, keepable: true);

        Assert.True(BinaryCityFormat.TryRead(future, out var city, out var problem, out var notes), problem);

        Assert.Equal("Its pollution, from 1.3, is kept as it was when you save it.", notes[1]);
        var written = BinaryCityFormat.Write(city);
        var chunks = Chunks(written);
        Assert.Equal([0, 3], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Equal([7, 8, 9], chunks.Single(chunk => chunk.Tag == "POLL").Payload);
        Assert.True(BinaryCityFormat.TryRead(written, out _, out problem, out _), problem);
    }

    [Fact]
    public void ABinarySavesPartThatCantBeLeftOutRefusesIt()
    {
        Assert.False(BinaryCityFormat.TryRead(FutureBinary(ignorable: false, keepable: false), out _, out var problem, out _));

        Assert.Equal("format: this is format 1.3; this version of CityLimits reads up to 1.0, and the file's pollution, from 1.3, can't be left out.", problem);
    }

    [Fact]
    public void ABinarySavesPartThatIsntKeepableIsLeftOut()
    {
        Assert.True(BinaryCityFormat.TryRead(FutureBinary(ignorable: true, keepable: false), out var city, out var problem, out _), problem);

        var chunks = Chunks(BinaryCityFormat.Write(city));

        Assert.Equal([0, 0], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "POLL");
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "PART");
    }

    [Fact]
    public void ABinarySavesKeptPartGoesIntoASaveOfThisVersionAndOpensAgain()
    {
        Assert.True(BinaryCityFormat.TryRead(FutureBinary(ignorable: true, keepable: true, noise: true), out var city, out var problem, out _), problem);

        var written = BinaryCityFormat.Write(city);
        var chunks = Chunks(written);
        Assert.Equal([0, 0], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Equal([7, 8, 9], chunks.Single(chunk => chunk.Tag == "POLL").Payload);
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "NOIS");

        Assert.True(BinaryCityFormat.TryRead(written, out var reopened, out problem, out var notes), problem);
        Assert.Equal("This city carries parts a newer CityLimits saved, which this version doesn't read.", notes[0]);
        Assert.Equal([7, 8, 9], Chunks(BinaryCityFormat.Write(reopened)).Single(chunk => chunk.Tag == "POLL").Payload);
    }

    // Bridgeport saved by newer versions: as 0.3.103 left it, at 1.2, and with rail laid by 0.3.106,
    // at 1.4, its traffic and each road's traffic in their own parts.
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Assets", name);

    private static IEnumerable<string> PartNames(JsonObject save) => save["parts"]!.AsArray().Select(part => part!["name"]!.GetValue<string>());

    [Fact]
    public void A12SaveFrom0_3_103OpensAndKeepsItsTraffic()
    {
        var original = JsonNode.Parse(File.ReadAllText(Fixture("Bridgeport.v1.2.fcl")))!.AsObject();
        Assert.True(FlatCityFormat.TryReadFile(Fixture("Bridgeport.v1.2.fcl"), out var flat, out var problem, out var notes), problem);

        Assert.Equal(
            [
                "A newer CityLimits saved this city, in format 1.2; this version reads up to 1.0.",
                "Its traffic, from 1.1, is kept as it was when you save it.",
            ],
            notes);

        // Traffic was all it had past 1.0, and it was kept, so it saves back as 1.2.
        var written = JsonNode.Parse(FlatCityFormat.Write(flat))!.AsObject();
        Assert.Equal("1.2", written["format"]!.GetValue<string>());
        Assert.Equal(original["traffic"]!.ToJsonString(), written["traffic"]!.ToJsonString());
        Assert.Equal(["traffic"], PartNames(written));

        Assert.True(BinaryCityFormat.TryReadFile(Fixture("Bridgeport.v1.2.cl"), out var binary, out problem, out notes), problem);
        Assert.Equal(2, notes.Count);
        var chunks = Chunks(BinaryCityFormat.Write(binary));
        Assert.Equal([0, 2], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Contains(chunks, chunk => chunk.Tag == "TRAF");
    }

    [Fact]
    public void A11SaveFrom0_3_102IsRefused()
    {
        var save = JsonNode.Parse(File.ReadAllText(Fixture("Bridgeport.v1.2.fcl")))!.AsObject();
        save["format"] = "1.1";
        save.Remove("parts");

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));

        Assert.Equal("format: this is format 1.1; this version of CityLimits reads up to 1.0.", problem);
    }

    [Fact]
    public void A14SaveWithRailOpensWithoutItAndKeepsTheTraffic()
    {
        var original = JsonNode.Parse(File.ReadAllText(Fixture("Bridgeport.v1.4.fcl")))!.AsObject();
        Assert.True(FlatCityFormat.TryReadFile(Fixture("Bridgeport.v1.4.fcl"), out var city, out var problem, out var notes), problem);

        Assert.Equal(
            [
                "A newer CityLimits saved this city, in format 1.4; this version reads up to 1.0.",
                "Its traffic, from 1.1, is kept as it was when you save it.",
                "Its roadTraffic, from 1.3, is kept as it was when you save it.",
                "Its rail, from 1.4, will be left out when you save it, and the newer version will rebuild it.",
            ],
            notes);

        // Rail can't be kept, so the save is 1.0, carrying both traffic parts as they came.
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.0", written["format"]!.GetValue<string>());
        Assert.Null(written["rail"]);
        Assert.Equal(original["traffic"]!.ToJsonString(), written["traffic"]!.ToJsonString());
        Assert.Equal(original["roadTraffic"]!.ToJsonString(), written["roadTraffic"]!.ToJsonString());
        Assert.Equal(["traffic", "roadTraffic"], PartNames(written));
        Assert.True(FlatCityFormat.TryRead(written.ToJsonString(), out _, out problem, out _), problem);

        Assert.True(BinaryCityFormat.TryReadFile(Fixture("Bridgeport.v1.4.cl"), out var binary, out problem, out notes), problem);
        Assert.Equal(4, notes.Count);
        var chunks = Chunks(BinaryCityFormat.Write(binary));
        Assert.Equal([0, 0], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Contains(chunks, chunk => chunk.Tag == "TRAF");
        Assert.Contains(chunks, chunk => chunk.Tag == "RTRF");
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "RAIL");
    }

    [Fact]
    public void AnAutosaveFollowsItsCitysFormat()
    {
        Assert.Equal("Joffburg (autosave).fcl", Autosave.FileNameFor("Joffburg", 1, FlatCityFormat.Extension));
        Assert.Equal(Path.Combine("Cities", "Joffburg.fcl"), Autosave.CityFileFor(Path.Combine("Cities", "Joffburg (autosave 2).fcl")));
    }

    // The binary save as docs/saving.md lays it out: the header, then chunks, with the minor version
    // made 3, a parts list after it naming traffic and the pollution part (and noise, when asked),
    // and the parts' own chunks added.
    private static byte[] FutureBinary(bool ignorable, bool keepable, bool noise = false)
    {
        var bytes = BinaryCityFormat.Write(City());
        var rebuilt = new List<byte>(bytes[..8]);
        foreach (var (tag, payload) in Chunks(bytes))
        {
            if (tag == "VERS")
            {
                rebuilt.AddRange(Chunk(tag, [0, 3]));
                rebuilt.AddRange(Chunk("PART", PartsList(ignorable, keepable, noise)));
                continue;
            }

            rebuilt.AddRange(Chunk(tag, payload));
        }

        rebuilt.AddRange(Chunk("POLL", [7, 8, 9]));
        if (noise)
        {
            rebuilt.AddRange(Chunk("NOIS", [4]));
        }

        return [.. rebuilt];
    }

    // The parts list is a count, then each part: a byte saying it is there, and its fields in order,
    // three texts and two yes-or-no bytes. A whole number is a zigzag varint, and a text its length
    // as one, then its bytes.
    private static byte[] PartsList(bool ignorable, bool keepable, bool noise)
    {
        List<byte> parts = [.. PartBytes("traffic", "TRAF", "1.1", true, true), .. PartBytes("pollution", "POLL", "1.3", ignorable, keepable)];
        if (noise)
        {
            parts.AddRange(PartBytes("noise", "NOIS", "1.3", true, false));
        }

        return [.. Varint(noise ? 3 : 2), .. parts];
    }

    private static byte[] PartBytes(string name, string chunk, string since, bool ignorable, bool keepable) =>
        [1, .. Text(name), .. Text(chunk), .. Text(since), (byte)(ignorable ? 1 : 0), (byte)(keepable ? 1 : 0)];

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
