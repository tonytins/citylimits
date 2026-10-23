namespace CityLimits.Core.Saving;

/// <summary>
/// Writes the city's fires as a part of their own, and the map with every standing building whole,
/// as Tony Bark chose: a version that doesn't know fire leaves the part out and opens the city as if
/// its fires had burned out and its buildings had been repaired. The part holds, for each row of
/// the map, a character a tile: <c>.</c> for none, <c>f</c> for a tile burning, <c>x</c> for a hole fire
/// left in a standing building, and <c>b</c> for a hole that is still burning. Beneath a burning
/// tile the map holds rubble, and a hole is rubble once the save is read.
/// </summary>
internal static class FireCodec
{
    private const char Nothing = '.';
    private const char Burning = 'f';
    private const char Hole = 'x';
    private const char BurningHole = 'b';

    /// <summary>The fires on the map, a row of characters a row of the map.</summary>
    public static FireSave Capture(TileMap map, Fires fires)
    {
        var rows = new string[map.Height];
        var row = new char[map.Width];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var hole = fires.IsHole(x, y);
                row[x] = fires.IsBurning(x, y) ? (hole ? BurningHole : Burning) : (hole ? Hole : Nothing);
            }

            rows[y] = new string(row);
        }

        return new FireSave { Tiles = rows };
    }

    /// <summary>The map to write: the city's own when no building has a hole, or a copy with each hole filled in.</summary>
    public static TileMap WholeMap(TileMap map, Fires fires)
    {
        TileState[]? tiles = null;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (!fires.IsHole(x, y))
                {
                    continue;
                }

                tiles ??= AllStates(map);
                tiles[(y * map.Width) + x] = fires.WholeStateAt(x, y);
            }
        }

        return tiles is null ? map : new TileMap(map.Width, map.Height, tiles);
    }

    /// <summary>
    /// Puts a save's fires back on a map read whole: each hole becomes rubble again, and each burning
    /// tile burns. A hole must be an outer tile of a building, and a fire outside one must be on rubble.
    /// </summary>
    /// <exception cref="InvalidSave">The part doesn't fit the map; the message says where.</exception>
    public static void Apply(TileMap map, Fires fires, FireSave save)
    {
        if (save.Tiles is not { } rows || rows.Length != map.Height)
        {
            throw new InvalidSave($"fire.tiles: needs {map.Height} rows.");
        }

        for (var y = 0; y < map.Height; y++)
        {
            if (rows[y] is not { } row || row.Length != map.Width)
            {
                throw new InvalidSave($"fire.tiles[{y}]: needs {map.Width} characters.");
            }

            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                switch (row[x])
                {
                    case Nothing:
                        break;
                    case Burning when state.Kind == TileKind.Rubble && state.Use == TileUse.None:
                        fires.RestoreBurning(x, y);
                        break;
                    case Hole or BurningHole when state.HasFootprint && !state.IsBuildingCenter:
                        map.SetState(x, y, new TileState(TileKind.Rubble));
                        if (row[x] == BurningHole)
                        {
                            fires.RestoreBurning(x, y);
                        }

                        break;
                    case Burning:
                        throw new InvalidSave($"fire.tiles[{y}]: the fire at {x} is on {state.Kind} with {state.Use}, not rubble.");
                    case Hole or BurningHole:
                        throw new InvalidSave($"fire.tiles[{y}]: the hole at {x} isn't an outer tile of a building.");
                    default:
                        throw new InvalidSave($"fire.tiles[{y}]: needs '{Nothing}', '{Burning}', '{Hole}', or '{BurningHole}' for each tile, not '{row[x]}' at {x}.");
                }
            }
        }
    }

    private static TileState[] AllStates(TileMap map)
    {
        var tiles = new TileState[map.Width * map.Height];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                tiles[(y * map.Width) + x] = map.StateAt(x, y);
            }
        }

        return tiles;
    }
}
