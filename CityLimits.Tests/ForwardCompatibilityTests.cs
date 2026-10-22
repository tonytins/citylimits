using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

// A newer CityLimits's save, made by hand: format 1.4, with a "pollution" part this version doesn't
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

    private static JsonObject Part(bool ignorable, bool keepable) => new()
    {
        ["name"] = "pollution",
        ["chunk"] = "POLL",
        ["since"] = "1.4",
        ["ignorable"] = ignorable,
        ["keepable"] = keepable,
    };

    private static string FutureFlat(bool ignorable, bool keepable, bool listed = true)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(City()))!.AsObject();
        save["format"] = "1.4";
        save["pollution"] = new JsonObject { ["levels"] = new JsonArray(1, 2, 3) };
        if (listed)
        {
            save["parts"]!.AsArray().Add(Part(ignorable, keepable));
        }

        return save.ToJsonString();
    }

    [Fact]
    public void AKeepablePartOpensAndIsWrittenBackAsItWas()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true), out var city, out var problem, out var notes), problem);

        Assert.Equal(
            [
                "A newer CityLimits saved this city, in format 1.4; this version reads up to 1.3.",
                "Its pollution, from 1.4, is kept as it was when you save it.",
            ],
            notes);
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.4", written["format"]!.GetValue<string>());
        Assert.Equal("[1,2,3]", written["pollution"]!["levels"]!.ToJsonString());
        Assert.Contains(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "pollution");
        Assert.EndsWith("city-save.v1.4.schema.json", written["$schema"]!.GetValue<string>());

        // Written back, it opens the same way again.
        Assert.True(FlatCityFormat.TryRead(written.ToJsonString(), out _, out problem, out _), problem);
    }

    [Fact]
    public void AnIgnorablePartThatIsntKeepableIsLeftOutAndTheSaveIsThisVersion()
    {
        Assert.True(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: false), out var city, out var problem, out var notes), problem);

        Assert.Equal("Its pollution, from 1.4, will be left out when you save it, and the newer version will rebuild it.", notes[1]);
        var written = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal("1.3", written["format"]!.GetValue<string>());
        Assert.Null(written["pollution"]);
        Assert.DoesNotContain(written["parts"]!.AsArray(), part => part!["name"]!.GetValue<string>() == "pollution");
    }

    [Fact]
    public void APartThatCantBeLeftOutRefusesTheSave()
    {
        Assert.False(FlatCityFormat.TryRead(FutureFlat(ignorable: false, keepable: true), out var city, out var problem));

        Assert.Null(city);
        Assert.Equal("format: this is format 1.4; this version of CityLimits reads up to 1.3, and the file's pollution, from 1.4, can't be left out.", problem);
    }

    [Fact]
    public void AnUnknownPartTheListDoesntNameIsRefused()
    {
        Assert.False(FlatCityFormat.TryRead(FutureFlat(ignorable: true, keepable: true, listed: false), out _, out var problem));

        Assert.Equal("pollution: is not a part a save has, and the file's parts list doesn't name it.", problem);
    }

    [Fact]
    public void AnUnknownPartInASaveOfThisVersionIsRefused()
    {
        var save = JsonNode.Parse(FutureFlat(ignorable: true, keepable: true))!.AsObject();
        save["format"] = "1.3";

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));

        Assert.Equal("pollution: is not a part a save has.", problem);
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

        Assert.Equal("Its pollution, from 1.4, is kept as it was when you save it.", notes[1]);
        var written = BinaryCityFormat.Write(city);
        var chunks = Chunks(written);
        Assert.Equal([0, 4], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.Equal([7, 8, 9], chunks.Single(chunk => chunk.Tag == "POLL").Payload);
        Assert.True(BinaryCityFormat.TryRead(written, out _, out problem, out _), problem);
    }

    [Fact]
    public void ABinarySavesPartThatCantBeLeftOutRefusesIt()
    {
        Assert.False(BinaryCityFormat.TryRead(FutureBinary(ignorable: false, keepable: false), out _, out var problem, out _));

        Assert.Equal("format: this is format 1.4; this version of CityLimits reads up to 1.3, and the file's pollution, from 1.4, can't be left out.", problem);
    }

    [Fact]
    public void ABinarySavesPartThatIsntKeepableIsLeftOut()
    {
        Assert.True(BinaryCityFormat.TryRead(FutureBinary(ignorable: true, keepable: false), out var city, out var problem, out _), problem);

        var chunks = Chunks(BinaryCityFormat.Write(city));

        Assert.Equal([0, 3], chunks.Single(chunk => chunk.Tag == "VERS").Payload);
        Assert.DoesNotContain(chunks, chunk => chunk.Tag == "POLL");
    }

    [Fact]
    public void AnAutosaveFollowsItsCitysFormat()
    {
        Assert.Equal("Joffburg (autosave).fcl", Autosave.FileNameFor("Joffburg", 1, FlatCityFormat.Extension));
        Assert.Equal(Path.Combine("Cities", "Joffburg.fcl"), Autosave.CityFileFor(Path.Combine("Cities", "Joffburg (autosave 2).fcl")));
    }

    // The binary save as docs/saving.md lays it out: the header, then chunks, with the minor version
    // made 4, the parts list given the pollution part, and the part's own chunk added.
    private static byte[] FutureBinary(bool ignorable, bool keepable)
    {
        var bytes = BinaryCityFormat.Write(City());
        var rebuilt = new List<byte>(bytes[..8]);
        foreach (var (tag, payload) in Chunks(bytes))
        {
            var changed = tag switch
            {
                "VERS" => [0, 4],
                "PART" => [.. AddPart(payload, ignorable, keepable)],
                _ => payload,
            };
            rebuilt.AddRange(Chunk(tag, changed));
        }

        rebuilt.AddRange(Chunk("POLL", [7, 8, 9]));
        return [.. rebuilt];
    }

    // The parts list is a count, then each part: a byte saying it is there, and its fields in order,
    // three texts and two yes-or-no bytes. A whole number is a zigzag varint, and a text its length
    // as one, then its bytes.
    private static IEnumerable<byte> AddPart(byte[] payload, bool ignorable, bool keepable)
    {
        var count = payload[0] >> 1;
        return
        [
            .. Varint(count + 1),
            .. payload[1..],
            1,
            .. Text("pollution"),
            .. Text("POLL"),
            .. Text("1.4"),
            (byte)(ignorable ? 1 : 0),
            (byte)(keepable ? 1 : 0),
        ];
    }

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
