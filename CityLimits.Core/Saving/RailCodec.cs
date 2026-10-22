using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>
/// Writes the city's rail as a part of its own, from format 1.4, apart from the map: the map holds
/// what lies beneath the rail (the ground, and the road or the power line at a crossing), and the
/// rail part holds the rail on it. A version that doesn't know rail can leave the part out and open
/// the city as if it had none, as an import of a Micropolis city with rail did before rail existed.
/// </summary>
internal static class RailCodec
{
    // A row of the rail part, a character a tile.
    private const char NoRail = '.';
    private const char Track = 'r';
    private const char Across = 'a';
    private const char Down = 'd';

    /// <summary>What a tile's map layer holds: the tile itself, or for rail, what lies beneath it.</summary>
    public static TileState Beneath(TileState state) => state.Use switch
    {
        TileUse.Rail => new TileState(state.Kind),
        TileUse.RailAcrossRoad or TileUse.RailDownRoad => new TileState(TileKind.Grass, TileUse.Road),
        TileUse.RailAcrossPowerLine or TileUse.RailDownPowerLine => new TileState(TileKind.Grass, TileUse.PowerLine),
        _ => state,
    };

    /// <summary>Captures the map's rail, with the scan's count of it and the count the budget last used.</summary>
    public static RailSave Capture(TileMap map, int tiles, int budgetedTiles)
    {
        ArgumentNullException.ThrowIfNull(map);

        var rows = new string[map.Height];
        var row = new char[map.Width];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                row[x] = map.StateAt(x, y).Use switch
                {
                    TileUse.Rail => Track,
                    TileUse.RailAcrossRoad or TileUse.RailAcrossPowerLine => Across,
                    TileUse.RailDownRoad or TileUse.RailDownPowerLine => Down,
                    _ => NoRail,
                };
            }

            rows[y] = new string(row);
        }

        return new RailSave { Track = rows, Tiles = tiles, BudgetedTiles = budgetedTiles };
    }

    /// <summary>
    /// Lays a save's rail on its map: track on clear ground or water, and a crossing on a road or a
    /// power line, which runs the other way to the rail. Anything else is refused, with where.
    /// </summary>
    /// <exception cref="InvalidSave">The part's rows don't fit the map, or rail is where it can't be.</exception>
    public static void Apply(TileMap map, RailSave save)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(save);

        if (save.Track is not { } rows || rows.Length != map.Height)
        {
            throw new InvalidSave($"rail.track: needs {map.Height} rows.");
        }

        for (var y = 0; y < map.Height; y++)
        {
            if (rows[y] is not { } row || row.Length != map.Width)
            {
                throw new InvalidSave($"rail.track[{y}]: needs {map.Width} characters.");
            }

            for (var x = 0; x < map.Width; x++)
            {
                if (row[x] == NoRail)
                {
                    continue;
                }

                var beneath = map.StateAt(x, y);
                TileUse? rail = (row[x], beneath.Use, beneath.Kind) switch
                {
                    (Track, TileUse.None, TileKind.Grass or TileKind.Water) => TileUse.Rail,
                    (Across, TileUse.Road, TileKind.Grass) => TileUse.RailAcrossRoad,
                    (Down, TileUse.Road, TileKind.Grass) => TileUse.RailDownRoad,
                    (Across, TileUse.PowerLine, TileKind.Grass) => TileUse.RailAcrossPowerLine,
                    (Down, TileUse.PowerLine, TileKind.Grass) => TileUse.RailDownPowerLine,
                    _ => null,
                };

                if (rail is null)
                {
                    throw new InvalidSave($"rail.track[{y}]: '{row[x]}' at {x} can't go on {beneath.Kind} {beneath.Use}.");
                }

                map.SetState(x, y, new TileState(beneath.Kind, rail.Value));
            }
        }
    }
}
