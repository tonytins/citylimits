namespace CityLimits.Core.World;

/// <summary>
/// How a zone sits on the map: 3 by 3 tiles around a centre, as in Micropolis. Each tile of a
/// zone knows its part, numbered 0 to 8 across and then down, so the centre is part 4.
/// </summary>
public static class ZoneLayout
{
    /// <summary>The width and height of a zone, in tiles.</summary>
    public const int Size = 3;

    /// <summary>The number of tiles in a zone.</summary>
    public const int Parts = Size * Size;

    /// <summary>The part number of a zone's centre, where the simulation works on it.</summary>
    public const int CenterPart = Parts / 2;

    /// <summary>The part of a zone a tile is, from its offset to the zone's top-left tile.</summary>
    public static int PartAt(int column, int row) => (row * Size) + column;

    /// <summary>How far a part of a zone is from the centre, across and down.</summary>
    public static (int Dx, int Dy) OffsetFromCenter(int part) => ((part % Size) - 1, (part / Size) - 1);
}
