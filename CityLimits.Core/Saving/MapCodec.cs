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

    // A tile as one 16-bit word, as Micropolis' city files hold their tiles: bits 0 and 1 the
    // ground, 2 to 5 the use, 6 to 9 the footprint part, 10 power, and 11 to 15 a building
    // centre's level (11 to 13) and land-value class (14 and 15), or a house tile's house.
    private const int UseShift = 2;
    private const int PartShift = 6;
    private const int PoweredBit = 1 << 10;
    private const int ExtraShift = 11;
    private const int ClassShift = 14;

    /// <summary>Writes a map as one word a tile, column by column, as Micropolis' city files lay theirs out.</summary>
    public static ushort[] EncodeWords(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (Enum.GetValues<TileUse>().Length > 16)
        {
            throw new InvalidOperationException("A tile word holds 16 uses; the binary format needs another bit for more.");
        }

        var words = new ushort[map.Width * map.Height];
        var i = 0;
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                var state = map.StateAt(x, y);
                var extra = state.IsBuildingCenter
                    ? state.Development | (state.ValueClass << (ClassShift - ExtraShift))
                    : state.House;
                words[i++] = (ushort)((int)state.Kind
                    | ((int)state.Use << UseShift)
                    | ((state.HasFootprint ? state.ZonePart : 0) << PartShift)
                    | (state.Powered ? PoweredBit : 0)
                    | (extra << ExtraShift));
            }
        }

        return words;
    }

    /// <summary>Reads words written by <see cref="EncodeWords"/> back into layers, for <see cref="Decode"/> to check.</summary>
    /// <exception cref="InvalidSave">A word holds a ground or a use there isn't.</exception>
    public static MapSave DecodeWords(int width, int height, ReadOnlySpan<ushort> words)
    {
        SaveChecks.InRange(width, 1, MaxSide, "map.width");
        SaveChecks.InRange(height, 1, MaxSide, "map.height");
        if (words.Length != width * height)
        {
            throw new InvalidSave($"map: needs {width * height} tiles, not {words.Length}.");
        }

        var ground = new char[height][];
        var use = new char[height][];
        var part = new char[height][];
        for (var y = 0; y < height; y++)
        {
            (ground[y], use[y], part[y]) = (new char[width], new char[width], new char[width]);
        }

        var buildings = new List<BuildingSave>();
        var houses = new List<HouseSave>();
        var i = 0;
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                int word = words[i++];
                var kind = (TileKind)(word & 0b11);
                var tileUse = (TileUse)((word >> UseShift) & 0b1111);
                if (!GroundChars.TryGetValue(kind, out var groundChar) || !UseChars.TryGetValue(tileUse, out var useChar))
                {
                    throw new InvalidSave($"map at {x}, {y}: the tile's word, {word}, holds a ground or a use there isn't.");
                }

                var size = Footprint.SizeOf(tileUse);
                var tilePart = (word >> PartShift) & 0b1111;
                var extra = word >> ExtraShift;
                (ground[y][x], use[y][x]) = (groundChar, useChar);
                part[y][x] = size > 1 ? char.ToLowerInvariant(Convert.ToString(tilePart, 16)[0]) : NoPart;
                if (size > 1 && tilePart == Footprint.CenterPart(size))
                {
                    buildings.Add(new BuildingSave { X = x, Y = y, Development = extra & 0b111, ValueClass = extra >> (ClassShift - ExtraShift), Powered = (word & PoweredBit) != 0 });
                }
                else if (extra != 0)
                {
                    houses.Add(new HouseSave { X = x, Y = y, House = extra });
                }
            }
        }

        return new MapSave
        {
            Width = width,
            Height = height,
            Ground = [.. ground.Select(row => new string(row))],
            Use = [.. use.Select(row => new string(row))],
            Part = [.. part.Select(row => new string(row))],
            Buildings = [.. buildings],
            Houses = [.. houses],
        };
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

    /// <summary>
    /// Checks that every tile of a building belongs to a whole one, of its use, around a centre on
    /// the map; the simulation finds a building's centre from any of its tiles, and trusts what it finds.
    /// </summary>
    /// <exception cref="InvalidSave">A building isn't whole; the message says where.</exception>
    public static void CheckFootprints(TileMap map)
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
