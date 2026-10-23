namespace CityLimits.Core.Saving;

/// <summary>
/// Writes the city's floods as a part of their own, as its fires are (<see cref="FireCodec"/>): for
/// each row of the map, a character a tile, <c>.</c> for none and <c>w</c> for a tile under water, and
/// how many more cycles the water spreads for. Beneath the water the map holds bare ground; a
/// flooded hole in a building is written whole in the map and marked a hole in the fire part, so a
/// version that doesn't know floods opens it as one, and one that doesn't know fire opens the
/// building whole.
/// </summary>
internal static class FloodCodec
{
    private const char Nothing = '.';
    private const char Water = 'w';

    /// <summary>The floods on the map, a row of characters a row of the map.</summary>
    public static FloodSave Capture(TileMap map, Floods floods)
    {
        var rows = new string[map.Height];
        var row = new char[map.Width];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                row[x] = floods.IsFlooded(x, y) ? Water : Nothing;
            }

            rows[y] = new string(row);
        }

        return new FloodSave { Tiles = rows, CyclesLeft = floods.CyclesLeft };
    }

    /// <summary>
    /// Puts a save's floods back on a map whose fire part has been read: each flooded tile is bare
    /// ground under water. A flood must be on bare ground, or on a hole the fire part marked.
    /// </summary>
    /// <exception cref="InvalidSave">The part doesn't fit the map; the message says where.</exception>
    public static void Apply(TileMap map, Hazards hazards, FloodSave save)
    {
        if (save.Tiles is not { } rows || rows.Length != map.Height)
        {
            throw new InvalidSave($"flood.tiles: needs {map.Height} rows.");
        }

        hazards.Floods.RestoreCyclesLeft(SaveChecks.InRange(save.CyclesLeft, 0, Floods.Duration, "flood.cyclesLeft"));
        for (var y = 0; y < map.Height; y++)
        {
            if (rows[y] is not { } row || row.Length != map.Width)
            {
                throw new InvalidSave($"flood.tiles[{y}]: needs {map.Width} characters.");
            }

            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                switch (row[x])
                {
                    case Nothing:
                        break;
                    case Water when hazards.Fires.IsBurning(x, y):
                        throw new InvalidSave($"flood.tiles[{y}]: the flood at {x} is on a fire.");
                    case Water when state is { Use: TileUse.None, Kind: TileKind.Grass } || (state is { Use: TileUse.None, Kind: TileKind.Rubble } && hazards.IsHole(x, y)):
                        map.SetState(x, y, new TileState(TileKind.Grass));
                        hazards.Floods.RestoreFlooded(x, y);
                        break;
                    case Water:
                        throw new InvalidSave($"flood.tiles[{y}]: the flood at {x} is on {state.Kind} with {state.Use}, not bare ground.");
                    default:
                        throw new InvalidSave($"flood.tiles[{y}]: needs '{Nothing}' or '{Water}' for each tile, not '{row[x]}' at {x}.");
                }
            }
        }
    }
}
