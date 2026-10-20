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

    /// <summary>The first of the tiles that hold rubble; rock borrows the first of them until the new terrain.</summary>
    private const int FirstRubbleTile = 44;

    /// <summary>The number of rubble tiles the sheet holds.</summary>
    private const int RubbleTiles = 4;

    /// <summary>A bridge running across, east to west.</summary>
    private const int BridgeAcrossTile = 64;

    /// <summary>A bridge running down, north to south.</summary>
    private const int BridgeDownTile = 65;

    /// <summary>
    /// The first tile of each zone's 3 by 3 drawings, by development level: the empty zone, then
    /// the buildings it grows into. A zone's nine tiles follow each other in the sheet, across and
    /// then down, so a part's tile is the first tile plus its part number. Levels 1 to 3 show the
    /// first, lowest-value buildings until population and land value choose among them.
    /// </summary>
    private static readonly int[] ResidentialTiles = [240, 261, 270, 279];

    /// <inheritdoc cref="ResidentialTiles"/>
    private static readonly int[] CommercialTiles = [423, 432, 441, 450];

    /// <inheritdoc cref="ResidentialTiles"/>
    private static readonly int[] IndustrialTiles = [612, 621, 630, 639];

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

    /// <summary>
    /// The sheet index that draws a tile, given the sides of it that join a road and, for a zone,
    /// the development its centre holds.
    /// </summary>
    public static int IndexFor(TileState state, RoadConnections roads, int zoneDevelopment, int tileX, int tileY)
    {
        if (state.IsBridge)
        {
            // A bridge runs down only when it joins nothing across, as in Micropolis.
            var across = (roads & (RoadConnections.East | RoadConnections.West)) != 0;
            var down = (roads & (RoadConnections.North | RoadConnections.South)) != 0;
            return down && !across ? BridgeDownTile : BridgeAcrossTile;
        }

        if (state.IsRoad)
        {
            return RoadTiles[(int)roads & 0b1111];
        }

        if (state.IsZoned)
        {
            var tiles = state.Use switch
            {
                TileUse.Commercial => CommercialTiles,
                TileUse.Industrial => IndustrialTiles,
                _ => ResidentialTiles,
            };

            return tiles[Math.Clamp(zoneDevelopment, 0, TileState.MaxDevelopment)] + state.ZonePart;
        }

        // The same tile always picks the same drawing, so the ground doesn't flicker as the view moves.
        var variant = (tileX * 7) + (tileY * 13);
        return state.Kind switch
        {
            TileKind.Water => WaterTile,
            TileKind.Trees => FirstTreeTile + (variant % TreeTiles),
            TileKind.Rubble => FirstRubbleTile + (variant % RubbleTiles),
            TileKind.Rock => FirstRubbleTile,
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
