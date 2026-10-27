using System.Buffers.Binary;
using System.Text;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;

namespace CityLimits.Tests;

public class BinaryCityFormatTests
{
    private const int Year = 48 * 16;

    // Bridgeport, a city the owner built in the game, saved in each format.
    private static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "Assets", file);

    private static CitySimulation Read(byte[] bytes)
    {
        Assert.True(BinaryCityFormat.TryRead(bytes, out var city, out var problem, out _), problem);
        return city;
    }

    [Fact]
    public void BridgeportReadsTheSameFromEitherFormat()
    {
        Assert.True(FlatCityFormat.TryReadFile(Fixture("Bridgeport.fcl"), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryReadFile(Fixture("Bridgeport.cl"), out var binary, out problem, out var notes), problem);

        Assert.Equal(FlatCityFormat.Write(flat), FlatCityFormat.Write(binary));
        Assert.Empty(notes);
        Assert.Equal("Bridgeport", binary.Name);
        Assert.True(new FileInfo(Fixture("Bridgeport.cl")).Length < new FileInfo(Fixture("Bridgeport.fcl")).Length);
    }

    [Fact]
    public void ACityWrittenBinaryReadsBackAsTheSameCity()
    {
        Assert.True(FlatCityFormat.TryReadFile(Fixture("Bridgeport.fcl"), out var city, out var problem), problem);

        var loaded = Read(BinaryCityFormat.Write(city));

        Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(loaded));
        Assert.Equal(BinaryCityFormat.Write(city), BinaryCityFormat.Write(loaded));
    }

    [Fact]
    public void BridgeportLoadedFromBinaryRunsOnLikeTheOriginal()
    {
        Assert.True(FlatCityFormat.TryReadFile(Fixture("Bridgeport.fcl"), out var city, out var problem), problem);
        var loaded = Read(BinaryCityFormat.Write(city));

        for (var step = 1; step <= Year; step++)
        {
            city.Tick();
            loaded.Tick();
            if (step <= 20 || step % 64 == 0)
            {
                Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(loaded));
            }
        }
    }

    [Fact]
    public void AChunkThisVersionDoesntKnowIsSkippedAndNoted()
    {
        var bytes = BinaryCityFormat.Write(Bridgeport());
        var withExtra = bytes.Concat(Chunk("NEWS", "from a later version"u8.ToArray())).ToArray();

        Assert.True(BinaryCityFormat.TryRead(withExtra, out var city, out var problem, out var notes), problem);

        Assert.Equal("Bridgeport", city.Name);
        Assert.Equal(["Skipped a chunk this version doesn't know: 'NEWS'."], notes);
    }

    [Theory]
    [InlineData("magic", "The file isn't a binary CityLimits city")]
    [InlineData("format", "format: this is format 9.0; this version of CityLimits reads up to 1.10.")]
    [InlineData("checksum", "BUDG: is damaged: its checksum doesn't match.")]
    [InlineData("truncated", "The file ends partway through a chunk")]
    [InlineData("cut short", "The file ends partway through a chunk")]
    [InlineData("length", "CYCL: says it is")]
    [InlineData("duplicate", "CYCL: the file holds it twice.")]
    [InlineData("missing", "CYCL: the file is missing it.")]
    [InlineData("leftover", "CYCL: has bytes left over")]
    public void ADamagedFileIsRefusedAndSaysWhere(string damage, string expected)
    {
        var bytes = BinaryCityFormat.Write(Bridgeport());
        var chunks = Chunks(bytes);
        var cycle = chunks.Single(chunk => chunk.Tag == "CYCL");
        var afterCycle = cycle.Offset + 12 + cycle.Payload.Length;
        var damaged = damage switch
        {
            "magic" => [.. "NOTCITY"u8, .. bytes[7..]],
            "format" => Header(9).Concat(chunks.SelectMany(chunk => Chunk(chunk.Tag, chunk.Payload))).ToArray(),
            "checksum" => Flip(bytes, chunks.Single(chunk => chunk.Tag == "BUDG").PayloadOffset),
            "truncated" => bytes[..(cycle.Offset + 6)],
            "cut short" => bytes[..^3],
            "length" => SetLength(bytes, cycle.Offset, int.MaxValue),
            "duplicate" => bytes.Concat(Chunk("CYCL", cycle.Payload)).ToArray(),
            "missing" => bytes[..cycle.Offset],
            "leftover" => bytes[..cycle.Offset].Concat(Chunk("CYCL", [.. cycle.Payload, 0])).Concat(bytes[afterCycle..]).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(damage)),
        };

        Assert.False(BinaryCityFormat.TryRead(damaged, out var city, out var problem, out _));

        Assert.Null(city);
        Assert.StartsWith(expected, problem);
    }

    [Fact]
    public void ADamagedMapWordIsRefusedWithItsTile()
    {
        var bytes = BinaryCityFormat.Write(Bridgeport());
        var map = Chunks(bytes).Single(chunk => chunk.Tag == "MAP ");
        var payload = map.Payload.ToArray();

        // The first tile's word, after the map's width and height: a use there isn't.
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(4), 0b1111_00);
        var damaged = bytes[..map.Offset].Concat(Chunk("MAP ", payload)).Concat(bytes[(map.Offset + 12 + map.Payload.Length)..]).ToArray();

        Assert.False(BinaryCityFormat.TryRead(damaged, out _, out var problem, out _));

        Assert.StartsWith("map at 0, 0:", problem);
    }

    [Fact]
    public void FilesAreReadAndListedInEitherFormat()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var city = Bridgeport();
            Assert.True(CityFiles.TryWrite(city, Path.Combine(folder, CityFiles.FileNameFor("Bridgeport")), out var problem), problem);
            Assert.True(CityFiles.TryWrite(city, Path.Combine(folder, CityFiles.FileNameFor("Bridgeport", FlatCityFormat.Extension)), out problem), problem);

            Assert.True(CityFiles.TryRead(Path.Combine(folder, "Bridgeport.cl"), out var binary, out problem, out _), problem);
            var listed = CityFiles.List(folder);

            Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(binary));
            Assert.Equal(["Bridgeport.cl", "Bridgeport.fcl"], listed.Select(saved => Path.GetFileName(saved.Path)).Order());
            Assert.All(listed, saved => Assert.Equal(city.Date, saved.Date));
            Assert.Equal("Bridgeport.cl", CityFiles.FileNameFor("Bridgeport"));
            Assert.Throws<ArgumentException>(() => CityFiles.FileNameFor("Bridgeport", ".txt"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static CitySimulation Bridgeport()
    {
        Assert.True(FlatCityFormat.TryReadFile(Fixture("Bridgeport.fcl"), out var city, out var problem), problem);
        return city;
    }

    // The test's own reading of the layout docs/saving.md describes: a header, then chunks of a tag,
    // a length, the payload, and a CRC-32 of the tag and payload.
    private static List<(string Tag, byte[] Payload, int Offset, int PayloadOffset)> Chunks(byte[] bytes)
    {
        var chunks = new List<(string, byte[], int, int)>();
        var position = 8;
        while (position < bytes.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position + 4));
            chunks.Add((Encoding.ASCII.GetString(bytes, position, 4), bytes[(position + 8)..(position + 8 + length)], position, position + 8));
            position += 12 + length;
        }

        return chunks;
    }

    private static byte[] Header(int format)
    {
        var header = new byte[8];
        "CLCITY"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), (ushort)format);
        return header;
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

    private static byte[] Flip(byte[] bytes, int offset)
    {
        var copy = bytes.ToArray();
        copy[offset] ^= 0xFF;
        return copy;
    }

    private static byte[] SetLength(byte[] bytes, int chunkOffset, int length)
    {
        var copy = bytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(chunkOffset + 4), (uint)length);
        return copy;
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
