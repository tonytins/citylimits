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

    /// <summary>The tile of trees with trees on every side.</summary>
    private const int WoodsTile = 37;

    /// <summary>How far each shore's second drawing is from its first.</summary>
    private const int ShoreVariantOffset = 1;

    /// <summary>How far each wood's edge's second drawing is from its first.</summary>
    private const int WoodEdgeVariantOffset = 8;

    /// <summary>The first of the tiles that hold rubble.</summary>
    private const int FirstRubbleTile = 44;

    /// <summary>The number of rubble tiles the sheet holds.</summary>
    private const int RubbleTiles = 4;

    /// <summary>A bridge running across, east to west.</summary>
    private const int BridgeAcrossTile = 64;

    /// <summary>A bridge running down, north to south.</summary>
    private const int BridgeDownTile = 65;

    /// <summary>A road running across over a power line running down.</summary>
    private const int RoadAcrossPowerLineTile = 77;

    /// <summary>A road running down over a power line running across.</summary>
    private const int RoadDownPowerLineTile = 78;

    // Rail, from the sheet: tunnels running across and down, the crossings with a power line and
    // with a road, each named for the way the rail runs, and the track drawn by its joins.
    private const int TunnelAcrossTile = 224;
    private const int TunnelDownTile = 225;
    private const int RailAcrossPowerLineTile = 221;
    private const int RailDownPowerLineTile = 222;
    private const int RailAcrossRoadTile = 237;
    private const int RailDownRoadTile = 238;

    /// <summary>The first of the four wooded park drawings; the look a tile's place picks is added to it.</summary>
    private const int FirstWoodedParkTile = 40;

    /// <summary>A park with a fountain.</summary>
    private const int FountainTile = 840;

    /// <summary>The lightning bolt that blinks over a zone with no power.</summary>
    public const int LightningBoltTile = 827;

    /// <summary>The first of the fire's drawings; each frame of its animation is the next tile.</summary>
    public const int FireTile = 56;

    /// <summary>How many frames a fire is animated in.</summary>
    public const int FireFrames = 8;

    /// <summary>The first of the drawings of a building blowing apart, which an explosion plays through.</summary>
    public const int ExplosionTile = 860;

    /// <summary>How many frames an explosion lasts.</summary>
    public const int ExplosionFrames = 8;

    /// <summary>
    /// The drawing of a fire in an animation frame. Each tile starts its flames at a frame of its own,
    /// picked from where it is, as Micropolis starts each fire at a frame of its own.
    /// </summary>
    public static int FireIndexFor(int x, int y, int frame) => FireTile + ((frame + (x * 3) + (y * 5)) % FireFrames + FireFrames) % FireFrames;

    /// <summary>A power line under water, running down.</summary>
    private const int UnderwaterLineDownTile = 208;

    /// <summary>A power line under water, running across.</summary>
    private const int UnderwaterLineAcrossTile = 209;

    /// <summary>The first of the twelve single-house drawings, three for each land-value class.</summary>
    private const int FirstHouseTile = 249;

    /// <summary>
    /// The first tile of the coal power plant's 4 by 4 drawing. Its sixteen tiles follow each other
    /// in the sheet across and then down, so a part's tile is the first tile plus its part number.
    /// </summary>
    private const int CoalPowerPlantTile = 745;

    /// <inheritdoc cref="CoalPowerPlantTile"/>
    private const int NuclearPowerPlantTile = 811;

    /// <summary>
    /// The first tile of the fire station's 3 by 3 drawing. Its nine tiles follow each other in
    /// the sheet across and then down, so a part's tile is the first tile plus its part number.
    /// </summary>
    private const int FireStationTile = 761;

    /// <inheritdoc cref="FireStationTile"/>
    private const int PoliceStationTile = 770;

    /// <inheritdoc cref="FireStationTile"/>
    private const int HospitalTile = 405;

    /// <inheritdoc cref="FireStationTile"/>
    private const int ChurchTile = 414;

    /// <summary>
    /// Where each zone's drawings are. A drawing is 3 by 3, its nine tiles following each other in
    /// the sheet across and then down, so a part's tile is the drawing's first tile plus its part
    /// number. The empty zone comes first; then the buildings, one per development level within
    /// each land-value class, the lowest class first.
    /// </summary>
    private static readonly ZoneDrawings Residential = new(EmptyZone: 240, FirstBuilding: 261, Levels: 4, ValueClasses: 4);

    /// <inheritdoc cref="Residential"/>
    private static readonly ZoneDrawings Commercial = new(EmptyZone: 423, FirstBuilding: 432, Levels: 5, ValueClasses: 4);

    /// <inheritdoc cref="Residential"/>
    private static readonly ZoneDrawings Industrial = new(EmptyZone: 612, FirstBuilding: 621, Levels: 4, ValueClasses: 2);

    /// <summary>
    /// The power line drawing for each set of sides that join something carrying power, indexed
    /// by <see cref="RoadConnections"/>. A line that joins nothing, or only one side, is drawn
    /// straight along that side. The table was made by looking at the drawings in the sheet.
    /// </summary>
    private static readonly int[] PowerLineTiles =
    [
        210, // none: east-west
        211, // north: north-south
        210, // east: east-west
        212, // north and east: a corner
        211, // south: north-south
        211, // north and south: north-south
        213, // east and south: a corner
        217, // north, east, and south: a junction branching east
        210, // west: east-west
        215, // north and west: a corner
        210, // east and west: east-west
        216, // north, east, and west: a junction branching north
        214, // south and west: a corner
        219, // north, south, and west: a junction branching west
        218, // east, south, and west: a junction branching south
        220, // all four: a crossing
    ];

    /// <summary>
    /// The shore drawing for each set of sides with water beside them, indexed by
    /// <see cref="TerrainSides"/>. Each shore has a second drawing just after it. Water with land
    /// on opposite sides, or on three or four, has no drawing in the sheet and is drawn open.
    /// </summary>
    private static readonly int[] ShoreTiles =
    [
        WaterTile, // land all round
        WaterTile, // water only to the north
        WaterTile, // water only to the east
        15, // water north and east: land south and west
        WaterTile, // water only to the south
        WaterTile, // water north and south: land east and west
        19, // water east and south: land north and west
        17, // land to the west
        WaterTile, // water only to the west
        11, // water north and west: land east and south
        WaterTile, // water east and west: land north and south
        13, // land to the south
        7, // water south and west: land north and east
        9, // land to the east
        5, // land to the north
        WaterTile, // water all round
    ];

    /// <summary>
    /// The drawing of trees at the edge of a wood for each set of sides with trees beside them,
    /// indexed by <see cref="TerrainSides"/>. Each edge has a second drawing eight tiles on. Trees
    /// a map would not grow, such as the last of a wood the bulldozer cleared, are drawn as woods.
    /// </summary>
    private static readonly int[] WoodEdgeTiles =
    [
        WoodsTile, // no trees beside
        WoodsTile, // trees only to the north
        WoodsTile, // trees only to the east
        26, // trees north and east
        WoodsTile, // trees only to the south
        WoodsTile, // trees north and south
        28, // trees east and south
        27, // trees north, east, and south
        WoodsTile, // trees only to the west
        24, // trees north and west
        WoodsTile, // trees east and west
        25, // trees north, east, and west
        22, // trees south and west
        23, // trees north, south, and west
        21, // trees east, south, and west
        WoodsTile, // trees all round
    ];

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

    // The first road drawing; each traffic frame holds a drawing for every road drawing after it,
    // in the same order, so a road's traffic is its offset from here in that frame.
    private const int FirstRoadTile = 64;
    private const int LastRoadTile = RoadDownPowerLineTile;

    /// <summary>How many frames each level of traffic is animated in.</summary>
    public const int TrafficFrames = 4;

    // As Micropolis plays them: each frame becomes the next in this order, which moves the cars on.
    private static readonly int[] LightTrafficFrames = [80, 128, 112, 96];
    private static readonly int[] HeavyTrafficFrames = [144, 192, 176, 160];

    /// <summary>
    /// The sheet index that draws a road's traffic in an animation frame, given the road's own
    /// drawing from <see cref="IndexFor"/>; the road's own drawing for a quiet road or one the
    /// sheet has no traffic for.
    /// </summary>
    public static int TrafficIndexFor(int roadIndex, TrafficLevel level, int frame)
    {
        if (roadIndex is < FirstRoadTile or > LastRoadTile || level == TrafficLevel.None)
        {
            return roadIndex;
        }

        var frames = level == TrafficLevel.Heavy ? HeavyTrafficFrames : LightTrafficFrames;
        return frames[((frame % TrafficFrames) + TrafficFrames) % TrafficFrames] + (roadIndex - FirstRoadTile);
    }

    /// <summary>
    /// The rail drawing for each set of joined sides, indexed by <see cref="RoadConnections"/>, as
    /// Micropolis lays its rail out. Rail that joins nothing, or only one side, is drawn straight
    /// along that side.
    /// </summary>
    private static readonly int[] RailTiles =
    [
        226, // none: east-west
        227, // north: north-south
        226, // east: east-west
        228, // north and east: a corner
        227, // south: north-south
        227, // north and south: north-south
        229, // east and south: a corner
        233, // north, east, and south: a junction branching east
        226, // west: east-west
        231, // north and west: a corner
        226, // east and west: east-west
        232, // north, east, and west: a junction branching north
        230, // south and west: a corner
        235, // north, south, and west: a junction branching west
        234, // east, south, and west: a junction branching south
        236, // all four: a crossing
    ];

    /// <summary>
    /// The sheet index that draws a tile, given the sides of it that join a road, the sides that
    /// join something carrying power, the sides that join rail, the sides with the same ground
    /// beside them, and, for a zone, the state of its centre, which holds the zone's building.
    /// </summary>
    public static int IndexFor(TileState state, RoadConnections roads, RoadConnections lines, RoadConnections rails, TerrainSides ground, TileState zoneCenter, int tileX, int tileY)
    {
        switch (state.Use)
        {
            case TileUse.RailAcrossRoad:
                return RailAcrossRoadTile;
            case TileUse.RailDownRoad:
                return RailDownRoadTile;
            case TileUse.RailAcrossPowerLine:
                return RailAcrossPowerLineTile;
            case TileUse.RailDownPowerLine:
                return RailDownPowerLineTile;
            case TileUse.Rail when state.Kind == TileKind.Water:
                // A tunnel runs down only when it joins nothing across, as a bridge does.
                var tunnelAcross = (rails & (RoadConnections.East | RoadConnections.West)) != 0;
                var tunnelDown = (rails & (RoadConnections.North | RoadConnections.South)) != 0;
                return tunnelDown && !tunnelAcross ? TunnelDownTile : TunnelAcrossTile;
            case TileUse.Rail:
                return RailTiles[(int)rails & 0b1111];
            case TileUse.RoadAcrossPowerLine:
                return RoadAcrossPowerLineTile;
            case TileUse.RoadDownPowerLine:
                return RoadDownPowerLineTile;
            case TileUse.PowerLine when state.Kind == TileKind.Water:
                // Under water a line runs down only when it joins nothing across, as a bridge does.
                var across = (lines & (RoadConnections.East | RoadConnections.West)) != 0;
                var down = (lines & (RoadConnections.North | RoadConnections.South)) != 0;
                return down && !across ? UnderwaterLineDownTile : UnderwaterLineAcrossTile;
            case TileUse.PowerLine:
                return PowerLineTiles[(int)lines & 0b1111];
            case TileUse.Park:
                var look = Parks.LookAt(tileX, tileY);
                return look == Parks.FountainLook ? FountainTile : FirstWoodedParkTile + look;
        }

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

        if (state.Use == TileUse.CoalPowerPlant)
        {
            return CoalPowerPlantTile + state.ZonePart;
        }

        if (state.Use == TileUse.NuclearPowerPlant)
        {
            return NuclearPowerPlantTile + state.ZonePart;
        }

        if (state.Use == TileUse.FireStation)
        {
            return FireStationTile + state.ZonePart;
        }

        if (state.Use == TileUse.PoliceStation)
        {
            return PoliceStationTile + state.ZonePart;
        }

        if (state.Use == TileUse.Hospital)
        {
            return HospitalTile + state.ZonePart;
        }

        if (state.Use == TileUse.Church)
        {
            return ChurchTile + state.ZonePart;
        }

        if (state.IsZoned)
        {
            if (zoneCenter.Development == 0 && state.HasHouse)
            {
                return FirstHouseTile + state.House - 1;
            }

            var drawings = state.Use switch
            {
                TileUse.Commercial => Commercial,
                TileUse.Industrial => Industrial,
                _ => Residential,
            };

            return drawings.FirstTile(zoneCenter.Development, zoneCenter.ValueClass) + state.ZonePart;
        }

        // The same tile always picks the same drawing, so the ground doesn't flicker as the view moves.
        var variant = (tileX * 7) + (tileY * 13);
        return state.Kind switch
        {
            TileKind.Water => Varied(ShoreTiles[(int)ground & 0b1111], WaterTile, ShoreVariantOffset, variant),
            TileKind.Trees => Varied(WoodEdgeTiles[(int)ground & 0b1111], WoodsTile, WoodEdgeVariantOffset, variant),
            TileKind.Rubble => FirstRubbleTile + (variant % RubbleTiles),
            _ => LandTile,
        };
    }

    // An edge drawing, or its second drawing on every other tile; the open drawing has no second.
    private static int Varied(int tile, int open, int offset, int variant) =>
        tile == open ? tile : tile + (offset * (variant % 2));

    /// <summary>The part of the sheet that holds the tile at an index.</summary>
    public static Rectangle SourceFor(int index) => new()
    {
        X = index % Columns * CellSize,
        Y = index / Columns * CellSize,
        Width = CellSize,
        Height = CellSize,
    };

    // A zone's drawings: its empty zone, and its buildings by land-value class and level.
    private readonly record struct ZoneDrawings(int EmptyZone, int FirstBuilding, int Levels, int ValueClasses)
    {
        public int FirstTile(int development, int valueClass)
        {
            if (development <= 0)
            {
                return EmptyZone;
            }

            var level = Math.Min(development, Levels) - 1;
            var valueClassIndex = Math.Clamp(valueClass, 0, ValueClasses - 1);
            return FirstBuilding + (ZoneLayout.Parts * ((valueClassIndex * Levels) + level));
        }
    }
}
