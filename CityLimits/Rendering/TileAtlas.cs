namespace CityLimits.Rendering;

/// <summary>
/// Where each tile's drawing sits in the tile sheet. Every sheet index the game uses lives in
/// this class, so new art is a change to this table and to <c>Assets/README.md</c>, not to the renderer.
/// </summary>
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

    /// <summary>
    /// The road drawing for each set of joined sides, indexed by <see cref="RoadConnections"/>.
    /// A road that joins nothing, or only one side, is drawn straight along that side.
    /// </summary>
    private static readonly int[] RoadTiles =
    [
        66, // none: east-west
        67, // north: north-south
        66, // east: east-west
        68, // north and east: a corner
        67, // south: north-south
        67, // north and south: north-south
        69, // east and south: a corner
        73, // north, east, and south: a junction branching east
        66, // west: east-west
        71, // north and west: a corner
        66, // east and west: east-west
        72, // north, east, and west: a junction branching north
        70, // south and west: a corner
        75, // north, south, and west: a junction branching west
        74, // east, south, and west: a junction branching south
        76, // all four: a crossroads
    ];

    /// <summary>The sheet index that draws a tile, given the sides of it that join a road.</summary>
    public static int IndexFor(TileState state, RoadConnections roads, int tileX, int tileY)
    {
        if (state.IsRoad)
        {
            return RoadTiles[(int)roads & 0b1111];
        }

        return state.Kind switch
        {
            TileKind.Water => WaterTile,
            // The same tile always picks the same tree, so the woods don't flicker as the view moves.
            TileKind.Trees => FirstTreeTile + (((tileX * 7) + (tileY * 13)) % TreeTiles),
            TileKind.Rock => RockTile,
            _ => LandTile,
        };
    }

    /// <summary>The part of the sheet that holds the tile at an index.</summary>
    public static Rectangle SourceFor(int index) => new()
    {
        X = index % Columns * CellSize,
        Y = index / Columns * CellSize,
        Width = CellSize,
        Height = CellSize,
    };
}
