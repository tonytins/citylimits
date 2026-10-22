namespace CityLimits.Core.Saving;

/// <summary>
/// Writes a map as rows of characters, a layer for each part of a tile, and reads it back with every
/// tile and every building checked: the ground, the use, and the part of a building's footprint as
/// layers, and what only a building's centre or a house's tile holds as lists beside them.
/// </summary>
internal static class MapCodec
{
    /// <summary>The largest map a save may hold, either way, so a damaged file can't ask for any amount of memory.</summary>
    public const int MaxSide = 1024;

    private const char NoPart = '.';

    // One character a kind of ground, and one a use; docs/saving.md lists them.
    private static readonly Dictionary<TileKind, char> GroundChars = new()
    {
        [TileKind.Grass] = '.',
        [TileKind.Water] = '~',
        [TileKind.Trees] = 'T',
        [TileKind.Rubble] = '#',
    };

    private static readonly Dictionary<TileUse, char> UseChars = new()
    {
        [TileUse.None] = '.',
        [TileUse.Road] = '=',
        [TileUse.Residential] = 'R',
        [TileUse.Commercial] = 'C',
        [TileUse.Industrial] = 'I',
        [TileUse.CoalPowerPlant] = 'c',
        [TileUse.NuclearPowerPlant] = 'n',
        [TileUse.PowerLine] = '+',
        [TileUse.RoadAcrossPowerLine] = 'x',
        [TileUse.RoadDownPowerLine] = 'y',
        [TileUse.FireStation] = 'F',
        [TileUse.PoliceStation] = 'P',
        [TileUse.Park] = 'k',
        [TileUse.Hospital] = 'H',
        [TileUse.Church] = 'U',
    };

    private static readonly Dictionary<char, TileKind> GroundOf = GroundChars.ToDictionary(pair => pair.Value, pair => pair.Key);
    private static readonly Dictionary<char, TileUse> UseOf = UseChars.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>Writes a map in layers.</summary>
    public static MapSave Encode(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var ground = new string[map.Height];
        var use = new string[map.Height];
        var part = new string[map.Height];
        var buildings = new List<BuildingSave>();
        var houses = new List<HouseSave>();
        var groundRow = new char[map.Width];
        var useRow = new char[map.Width];
        var partRow = new char[map.Width];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                groundRow[x] = GroundChars[state.Kind];
                useRow[x] = UseChars[state.Use];
                partRow[x] = state.HasFootprint ? char.ToLowerInvariant(Convert.ToString(state.ZonePart, 16)[0]) : NoPart;
                if (state.IsBuildingCenter)
                {
                    buildings.Add(new BuildingSave { X = x, Y = y, Development = state.Development, ValueClass = state.ValueClass, Powered = state.Powered });
                }
                else if (state.HasHouse)
                {
                    houses.Add(new HouseSave { X = x, Y = y, House = state.House });
                }
            }

            ground[y] = new string(groundRow);
            use[y] = new string(useRow);
            part[y] = new string(partRow);
        }

        return new MapSave
        {
            Width = map.Width,
            Height = map.Height,
            Ground = ground,
            Use = use,
            Part = part,
            Buildings = [.. buildings],
            Houses = [.. houses],
        };
    }

    /// <summary>Reads a map back, checking every tile, every footprint, and every building and house.</summary>
    /// <exception cref="InvalidSave">Something in the map is wrong; the message says what and where.</exception>
    public static TileMap Decode(MapSave save)
    {
        ArgumentNullException.ThrowIfNull(save);

        var width = SaveChecks.InRange(save.Width, 1, MaxSide, "map.width");
        var height = SaveChecks.InRange(save.Height, 1, MaxSide, "map.height");
        CheckRows(save.Ground, "map.ground", width, height);
        CheckRows(save.Use, "map.use", width, height);
        CheckRows(save.Part, "map.part", width, height);

        var centres = new Dictionary<(int X, int Y), BuildingSave>();
        foreach (var building in save.Buildings ?? [])
        {
            if (!centres.TryAdd((building.X, building.Y), building))
            {
                throw new InvalidSave($"map.buildings: {building.X}, {building.Y} is listed twice.");
            }
        }

        var houses = new Dictionary<(int X, int Y), int>();
        foreach (var house in save.Houses ?? [])
        {
            if (!houses.TryAdd((house.X, house.Y), house.House))
            {
                throw new InvalidSave($"map.houses: {house.X}, {house.Y} is listed twice.");
            }
        }

        var tiles = new TileState[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                tiles[(y * width) + x] = Tile(save, x, y, centres, houses);
            }
        }

        var map = new TileMap(width, height, tiles);
        CheckFootprints(map);

        foreach (var (x, y) in centres.Keys.Where(at => !map.Contains(at.X, at.Y) || !map.StateAt(at.X, at.Y).IsBuildingCenter))
        {
            throw new InvalidSave($"map.buildings: {x}, {y} isn't the centre of a building.");
        }

        foreach (var (x, y) in houses.Keys.Where(at => !map.Contains(at.X, at.Y) || map.StateAt(at.X, at.Y).House == 0))
        {
            throw new InvalidSave($"map.houses: {x}, {y} isn't a residential zone's outer tile.");
        }

        return map;
    }

    private static TileState Tile(MapSave save, int x, int y, Dictionary<(int X, int Y), BuildingSave> centres, Dictionary<(int X, int Y), int> houses)
    {
        var where = $"map at {x}, {y}";
        if (!GroundOf.TryGetValue(save.Ground[y][x], out var kind))
        {
            throw new InvalidSave($"{where}: '{save.Ground[y][x]}' isn't a kind of ground.");
        }

        if (!UseOf.TryGetValue(save.Use[y][x], out var use))
        {
            throw new InvalidSave($"{where}: '{save.Use[y][x]}' isn't a use.");
        }

        var size = Footprint.SizeOf(use);
        var partChar = save.Part[y][x];
        int part;
        if (size == 1)
        {
            part = partChar == NoPart ? 0 : throw new InvalidSave($"{where}: a {use} tile has no footprint part, so its part is '{NoPart}'.");
        }
        else if (!int.TryParse(partChar.ToString(), System.Globalization.NumberStyles.HexNumber, null, out part) || part >= Footprint.Parts(size))
        {
            throw new InvalidSave($"{where}: '{partChar}' isn't a part of a {size} by {size} {use}.");
        }

        centres.TryGetValue((x, y), out var building);
        houses.TryGetValue((x, y), out var house);
        try
        {
            return new TileState(kind, use, building?.Development ?? 0, part, building?.ValueClass ?? 0, house, building?.Powered ?? false);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidSave($"{where}: {ex.ParamName ?? "a value"} is out of range.");
        }
    }

    private static void CheckRows(string[]? rows, string field, int width, int height)
    {
        if (rows is null || rows.Length != height)
        {
            throw new InvalidSave($"{field}: needs {height} rows, not {rows?.Length ?? 0}.");
        }

        for (var y = 0; y < height; y++)
        {
            if (rows[y] is null || rows[y].Length != width)
            {
                throw new InvalidSave($"{field}[{y}]: needs {width} characters, not {rows[y]?.Length ?? 0}.");
            }
        }
    }

    // Every tile of a building has to belong to a whole one, of its use, around a centre on the map;
    // the simulation finds a building's centre from any of its tiles, and trusts what it finds.
    private static void CheckFootprints(TileMap map)
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                if (!state.HasFootprint)
                {
                    continue;
                }

                var size = Footprint.SizeOf(state.Use);
                var (dx, dy) = Footprint.OffsetFromCenter(state.ZonePart, size);
                var (centreX, centreY) = (x - dx, y - dy);
                for (var p = 0; p < Footprint.Parts(size); p++)
                {
                    var (px, py) = Footprint.OffsetFromCenter(p, size);
                    var (tileX, tileY) = (centreX + px, centreY + py);
                    if (!map.Contains(tileX, tileY) || map.StateAt(tileX, tileY).Use != state.Use || map.StateAt(tileX, tileY).ZonePart != p)
                    {
                        throw new InvalidSave($"map at {x}, {y}: the {state.Use} it belongs to isn't whole; its part {p} should be at {tileX}, {tileY}.");
                    }
                }
            }
        }
    }
}
