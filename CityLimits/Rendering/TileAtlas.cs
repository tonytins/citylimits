namespace CityLimits.Rendering;

/// <summary>Where each kind of ground sits in the tile sheet.</summary>
public static class TileAtlas
{
    /// <summary>The width and height of one tile in the sheet, in pixels.</summary>
    public const int CellSize = 16;

    /// <summary>The number of tiles across the sheet.</summary>
    public const int Columns = 16;

    /// <summary>The tile of clear ground.</summary>
    private const int LandTile = 0;

    /// <summary>The tile of open water.</summary>
    private const int WaterTile = 2;

    /// <summary>The first of the tiles that hold a tree.</summary>
    private const int FirstTreeTile = 21;

    /// <summary>The number of tree tiles the sheet holds.</summary>
    private const int TreeTiles = 8;

    /// <summary>The tile of bare rubble.</summary>
    private const int RockTile = 44;

    /// <summary>The part of the sheet that holds a kind of ground at a place on the map.</summary>
    public static Rectangle SourceFor(TileKind kind, int tileX, int tileY)
    {
        var index = kind switch
        {
            TileKind.Water => WaterTile,
            TileKind.Trees => FirstTreeTile + (((tileX * 7) + (tileY * 13)) % TreeTiles),
            TileKind.Rock => RockTile,
            _ => LandTile,
        };

        var column = index % Columns;
        var row = index / Columns;

        return new Rectangle
        {
            X = column * CellSize,
            Y = row * CellSize,
            Width = CellSize,
            Height = CellSize,
        };
    }
}
