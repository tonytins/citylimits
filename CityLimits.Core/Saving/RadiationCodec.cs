namespace CityLimits.Core.Saving;

/// <summary>
/// Writes the city's radioactive ground as a part of its own, as its floods are
/// (<see cref="FloodCodec"/>): for each row of the map, a character a tile, <c>.</c> for none and
/// <c>r</c> for radioactive ground. Beneath it the map holds bare ground, or, for a hole in a
/// building, what the fire part marks a hole, so a version that doesn't know radiation opens the
/// city as if it had decayed.
/// </summary>
internal static class RadiationCodec
{
    private const char Nothing = '.';
    private const char Radioactive = 'r';

    /// <summary>The radioactive ground on the map, a row of characters a row of the map.</summary>
    public static RadiationSave Capture(TileMap map, Radiation radiation)
    {
        var rows = new string[map.Height];
        var row = new char[map.Width];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                row[x] = radiation.IsRadioactive(x, y) ? Radioactive : Nothing;
            }

            rows[y] = new string(row);
        }

        return new RadiationSave { Tiles = rows };
    }

    /// <summary>
    /// Puts a save's radioactive ground back on a map whose fires and floods have been read: each
    /// radioactive tile is bare ground, or a hole the fire part marked, neither burning nor flooded.
    /// </summary>
    /// <exception cref="InvalidSave">The part doesn't fit the map; the message says where.</exception>
    public static void Apply(TileMap map, Hazards hazards, RadiationSave save)
    {
        if (save.Tiles is not { } rows || rows.Length != map.Height)
        {
            throw new InvalidSave($"radiation.tiles: needs {map.Height} rows.");
        }

        for (var y = 0; y < map.Height; y++)
        {
            if (rows[y] is not { } row || row.Length != map.Width)
            {
                throw new InvalidSave($"radiation.tiles[{y}]: needs {map.Width} characters.");
            }

            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                switch (row[x])
                {
                    case Nothing:
                        break;
                    case Radioactive when hazards.Fires.IsBurning(x, y) || hazards.Floods.IsFlooded(x, y):
                        throw new InvalidSave($"radiation.tiles[{y}]: the radiation at {x} is on a fire or a flood.");
                    case Radioactive when state is { Use: TileUse.None, Kind: TileKind.Grass } || (state is { Use: TileUse.None, Kind: TileKind.Rubble } && hazards.IsHole(x, y)):
                        map.SetState(x, y, new TileState(TileKind.Grass));
                        hazards.Radiation.RestoreRadioactive(x, y);
                        break;
                    case Radioactive:
                        throw new InvalidSave($"radiation.tiles[{y}]: the radiation at {x} is on {state.Kind} with {state.Use}, not bare ground.");
                    default:
                        throw new InvalidSave($"radiation.tiles[{y}]: needs '{Nothing}' or '{Radioactive}' for each tile, not '{row[x]}' at {x}.");
                }
            }
        }
    }
}
