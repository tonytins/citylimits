namespace CityLimits.Core.Saving;

/// <summary>
/// The CRC-32 checksum (the one zip and PNG use), which guards each chunk of a binary city file.
/// .NET's own is a separate package, and the core library references none.
/// </summary>
internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320;

    private static readonly uint[] Table = BuildTable();

    /// <summary>The checksum of the bytes, continuing from an earlier one.</summary>
    public static uint Compute(ReadOnlySpan<byte> bytes, uint crc = 0)
    {
        crc = ~crc;
        foreach (var b in bytes)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
