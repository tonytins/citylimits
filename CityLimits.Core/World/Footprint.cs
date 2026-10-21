namespace CityLimits.Core.World;

/// <summary>
/// How much of the map a use takes up. As in Micropolis, a zone is 3 by 3 and a power plant 4 by 4,
/// and either is worked on at its centre, one tile in from its top-left corner. Each of its tiles
/// knows its part, numbered across and then down. Anything else takes one tile.
/// </summary>
public static class Footprint
{
    /// <summary>The widest footprint any use has, in tiles.</summary>
    public const int Largest = 4;

    /// <summary>The width and height of a use's footprint, in tiles.</summary>
    public static int SizeOf(TileUse use) => use switch
    {
        TileUse.Residential or TileUse.Commercial or TileUse.Industrial => ZoneLayout.Size,
        TileUse.CoalPowerPlant or TileUse.NuclearPowerPlant => 4,
        _ => 1,
    };

    /// <summary>The number of tiles in a footprint of a size.</summary>
    public static int Parts(int size) => size * size;

    /// <summary>The part number of a footprint's centre: one tile in from the top-left, or the only tile.</summary>
    public static int CenterPart(int size) => size == 1 ? 0 : size + 1;

    /// <summary>The part a tile is, from its offset to the footprint's top-left tile.</summary>
    public static int PartAt(int column, int row, int size) => (row * size) + column;

    /// <summary>How far a part is from the footprint's centre, across and down.</summary>
    public static (int Dx, int Dy) OffsetFromCenter(int part, int size) =>
        size == 1 ? (0, 0) : ((part % size) - 1, (part / size) - 1);
}
