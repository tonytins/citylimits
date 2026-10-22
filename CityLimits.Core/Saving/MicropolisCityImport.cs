using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>
/// Imports a Micropolis city file, <c>.cty</c>, the format of SimCity Classic and its open source
/// release. Import only: CityLimits never writes one. The file is 27,120 bytes of big-endian 16-bit
/// numbers (some editions add a 128-byte header first): six histories of 240, a block of 120 for the
/// city's settings, and the 120 by 100 map, a number a tile, column by column, each a Micropolis
/// tile number with flags above it. Tiles are read with Micropolis' numbering, which the owner
/// approved for this, and buildings from their centres, so each comes in whole. Anything CityLimits
/// doesn't have yet (rail, seaports, airports, stadiums, fire, flood, and radiation) becomes bare
/// ground, as the owner chose, and the import lists it. <c>docs/saving.md</c> describes it.
/// </summary>
public static class MicropolisCityImport
{
    /// <summary>The extension of a Micropolis city file.</summary>
    public const string Extension = ".cty";

    /// <summary>A city file's size.</summary>
    public const int FileBytes = 27120;

    /// <summary>The header some editions of SimCity put before the city.</summary>
    public const int HeaderBytes = 128;

    private const int Width = 120;
    private const int Height = 100;
    private const int HistoryLength = 240;
    private const int Histories = 6;
    private const int MiscLength = 120;
    private const int MapOffset = ((Histories * HistoryLength) + MiscLength) * 2;

    // Micropolis' flags sit above the tile number's low ten bits.
    private const int TileMask = 1023;
    private const int PoweredFlag = 1 << 15;

    // The settings block's numbers, by their place in it, as Micropolis writes them.
    private const int ResidentialPopulationAt = 2;
    private const int CommercialPopulationAt = 3;
    private const int IndustrialPopulationAt = 4;
    private const int ResidentialValveAt = 5;
    private const int CommercialValveAt = 6;
    private const int IndustrialValveAt = 7;
    private const int CityTimeAt = 8;
    private const int LandValueAverageAt = 12;
    private const int FundsAt = 50;
    private const int TaxAt = 56;
    private const int PoliceFundingAt = 58;
    private const int FireFundingAt = 60;
    private const int RoadFundingAt = 62;
    private const double FullFundingFixed = 65536;

    // Micropolis' tile numbers, for reading its files; CityLimits' own tile sheet uses the same
    // numbering, so Rendering/TileAtlas.cs agrees with these.
    private const int LastGround = 1;
    private const int FirstWater = 2;
    private const int LastWater = 20;
    private const int FirstTrees = 21;
    private const int LastTrees = 39;
    private const int FirstWoodedPark = 40;
    private const int LastWoodedPark = 43;
    private const int FirstRubble = 44;
    private const int LastRubble = 47;
    private const int FirstFlood = 48;
    private const int LastFlood = 51;
    private const int FirstRadiation = 52;
    private const int LastRadiation = 55;
    private const int FirstFire = 56;
    private const int LastFire = 63;
    private const int FirstRoad = 64;
    private const int LastRoad = 207;
    private const int RoadDrawings = 16;
    private const int BridgeAcross = 64;
    private const int BridgeDown = 65;
    private const int RoadAcrossPowerLine = 77;
    private const int RoadDownPowerLine = 78;
    private const int OpenBridge = 79;
    private const int FirstPowerLine = 208;
    private const int LastUnderwaterPowerLine = 209;
    private const int LastPowerLine = 220;
    private const int RailAcrossPowerLine = 221;
    private const int RailDownPowerLine = 222;
    private const int FirstRail = 224;
    private const int RailAcrossRoad = 237;
    private const int RailDownRoad = 238;
    private const int LastRail = 238;
    private const int FirstHouse = 249;
    private const int LastHouse = 260;
    private const int FirstDrawbridge = 828;
    private const int LastDrawbridge = 831;
    private const int FirstFountain = 840;
    private const int LastFountain = 843;
    private const int FirstDownDrawbridge = 948;
    private const int LastDownDrawbridge = 951;

    // Each building's drawings: where its first tile is, its size, its use in CityLimits (none for
    // one CityLimits doesn't have yet), and for zones, how many levels and value classes they come in.
    private static readonly Building[] Buildings =
    [
        new(240, 3, TileUse.Residential, 0, 1),
        new(261, 3, TileUse.Residential, 4, 4),
        new(405, 3, TileUse.Hospital, 0, 1),
        new(414, 3, TileUse.Church, 0, 1),
        new(423, 3, TileUse.Commercial, 0, 1),
        new(432, 3, TileUse.Commercial, 5, 4),
        new(612, 3, TileUse.Industrial, 0, 1),
        new(621, 3, TileUse.Industrial, 4, 2),
        new(693, 4, TileUse.None, 0, 1, "seaport"),
        new(709, 6, TileUse.None, 0, 1, "airport"),
        new(745, 4, TileUse.CoalPowerPlant, 0, 1),
        new(761, 3, TileUse.FireStation, 0, 1),
        new(770, 3, TileUse.PoliceStation, 0, 1),
        new(779, 4, TileUse.None, 0, 1, "stadium"),
        new(795, 4, TileUse.None, 0, 1, "stadium"),
        new(811, 4, TileUse.NuclearPowerPlant, 0, 1),
    ];

    /// <summary>Imports a city from a Micropolis city file's bytes, named as given, or says what is wrong.</summary>
    public static bool TryImport(ReadOnlySpan<byte> bytes, string name, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        notes = [];
        if (bytes.Length == FileBytes + HeaderBytes)
        {
            bytes = bytes[HeaderBytes..];
        }

        if (bytes.Length != FileBytes)
        {
            problem = $"A Micropolis city is {FileBytes} bytes, or {FileBytes + HeaderBytes} with a header, not {bytes.Length}.";
            return false;
        }

        try
        {
            var replaced = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var map = ReadMap(bytes[MapOffset..], replaced);
            city = Build(bytes, map, CityNameFrom(name), SeedOf(bytes));
            notes = [.. replaced.Select(pair => $"{Plural(pair.Value, pair.Key)} became bare ground: CityLimits doesn't have them yet.")];
            problem = null;
            return true;
        }
        catch (InvalidSave ex)
        {
            problem = ex.Message;
            return false;
        }
    }

    /// <summary>Imports a city from a Micropolis city file, named for the file.</summary>
    public static bool TryImportFile(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        notes = [];
        return SaveFiles.TryReadBytes(path, FileBytes + HeaderBytes, out var bytes, out problem)
            && TryImport(bytes, Path.GetFileNameWithoutExtension(path), out city, out problem, out notes);
    }

    /// <summary>Reads what a list of cities shows about a Micropolis city, from its settings block alone.</summary>
    public static bool TryReadSummary(string path, [NotNullWhen(true)] out SavedCitySummary? summary)
    {
        summary = null;
        if (!SaveFiles.TryReadBytes(path, FileBytes + HeaderBytes, out var bytes, out _))
        {
            return false;
        }

        ReadOnlySpan<byte> city = bytes.Length == FileBytes + HeaderBytes ? bytes.AsSpan(HeaderBytes) : bytes;
        if (city.Length != FileBytes)
        {
            return false;
        }

        var misc = city[(Histories * HistoryLength * 2)..];
        var population = ((long)Word(misc, ResidentialPopulationAt) + (8L * (Word(misc, CommercialPopulationAt) + Word(misc, IndustrialPopulationAt)))) * 20;
        summary = new SavedCitySummary(path, CityNameFrom(Path.GetFileNameWithoutExtension(path)), CityCalendar.DateAt(Math.Max(0, Int(misc, CityTimeAt))), population, Int(misc, FundsAt), File.GetLastWriteTimeUtc(path));
        return true;
    }

    // The tiles a tile at a time, then the buildings from their centres, so each comes in whole.
    private static TileMap ReadMap(ReadOnlySpan<byte> bytes, SortedDictionary<string, int> replaced)
    {
        var numbers = new int[Width, Height];
        var i = 0;
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                numbers[x, y] = BinaryPrimitives.ReadUInt16BigEndian(bytes[(i++ * 2)..]);
            }
        }

        var tiles = new TileState[Width * Height];
        var covered = new bool[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                tiles[(y * Width) + x] = Tile(numbers[x, y] & TileMask, replaced);
            }
        }

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                Stamp(numbers, x, y, tiles, covered, replaced);
            }
        }

        // A building's tile with no centre to belong to is what's left of one, and becomes ground.
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var tile = numbers[x, y] & TileMask;
                if (!covered[(y * Width) + x] && BuildingAt(tile) is not null)
                {
                    tiles[(y * Width) + x] = new TileState(TileKind.Grass);
                    Count(replaced, "part of a building with no centre");
                }
            }
        }

        var map = new TileMap(Width, Height, tiles);
        MapCodec.CheckFootprints(map);
        return map;
    }

    private static TileState Tile(int tile, SortedDictionary<string, int> replaced)
    {
        if (tile >= FirstRoad && tile <= LastRoad)
        {
            // The road's drawings repeat with each level of traffic.
            return (FirstRoad + ((tile - FirstRoad) % RoadDrawings)) switch
            {
                BridgeAcross or BridgeDown or OpenBridge => new TileState(TileKind.Water, TileUse.Road),
                RoadAcrossPowerLine => new TileState(TileKind.Grass, TileUse.RoadAcrossPowerLine),
                RoadDownPowerLine => new TileState(TileKind.Grass, TileUse.RoadDownPowerLine),
                _ => new TileState(TileKind.Grass, TileUse.Road),
            };
        }

        switch (tile)
        {
            case <= LastGround:
                return new TileState(TileKind.Grass);
            case <= LastWater:
                return new TileState(TileKind.Water);
            case <= LastTrees:
                return new TileState(TileKind.Trees);
            case <= LastWoodedPark:
                return new TileState(TileKind.Grass, TileUse.Park);
            case <= LastRubble:
                return new TileState(TileKind.Rubble);
            case <= LastFlood:
                return Replaced(replaced, "flooded tile");
            case <= LastRadiation:
                return Replaced(replaced, "radioactive tile");
            case <= LastFire:
                return Replaced(replaced, "burning tile");
            case >= FirstPowerLine and <= LastUnderwaterPowerLine:
                return new TileState(TileKind.Water, TileUse.PowerLine);
            case >= FirstPowerLine and <= LastPowerLine:
                return new TileState(TileKind.Grass, TileUse.PowerLine);
            case RailAcrossPowerLine or RailDownPowerLine:
                Count(replaced, "rail crossing, kept as a power line,");
                return new TileState(TileKind.Grass, TileUse.PowerLine);
            case RailAcrossRoad or RailDownRoad:
                Count(replaced, "rail crossing, kept as a road,");
                return new TileState(TileKind.Grass, TileUse.Road);
            case >= FirstRail and <= LastRail:
                return Replaced(replaced, "rail tile");
            case >= FirstDrawbridge and <= LastDrawbridge:
            case >= FirstDownDrawbridge and <= LastDownDrawbridge:
                return new TileState(TileKind.Water, TileUse.Road);
            case >= FirstFountain and <= LastFountain:
                return new TileState(TileKind.Grass, TileUse.Park);
        }

        // A building's tiles are stamped from its centre; anything else, such as an animation, is ground.
        return new TileState(TileKind.Grass);
    }

    // Stamps the building whose centre this is, whole: a zone at the level and class its number
    // says, with the houses on an empty residential zone's outer tiles.
    private static void Stamp(int[,] numbers, int x, int y, TileState[] tiles, bool[] covered, SortedDictionary<string, int> replaced)
    {
        // A house belongs to the zone around it and is never a centre, though its number falls where one would.
        var tile = numbers[x, y] & TileMask;
        if (tile is >= FirstHouse and <= LastHouse || BuildingAt(tile) is not { } building)
        {
            return;
        }

        var drawing = (tile - building.First) / Footprint.Parts(building.Size);
        var part = (tile - building.First) % Footprint.Parts(building.Size);
        if (part != Footprint.CenterPart(building.Size))
        {
            return;
        }

        var (development, valueClass) = building.Levels == 0 ? (0, 0) : ((drawing % building.Levels) + 1, drawing / building.Levels);
        var powered = (numbers[x, y] & PoweredFlag) != 0;
        if (building.Missing is not null)
        {
            Count(replaced, building.Missing);
        }

        for (var p = 0; p < Footprint.Parts(building.Size); p++)
        {
            var (dx, dy) = Footprint.OffsetFromCenter(p, building.Size);
            var (tileX, tileY) = (x + dx, y + dy);
            if (tileX < 0 || tileY < 0 || tileX >= Width || tileY >= Height)
            {
                throw new InvalidSave($"map at {x}, {y}: the building centred here runs off the map.");
            }

            var index = (tileY * Width) + tileX;
            covered[index] = true;
            if (building.Use == TileUse.None)
            {
                tiles[index] = new TileState(TileKind.Grass);
                continue;
            }

            var outer = numbers[tileX, tileY] & TileMask;
            var house = building.Use == TileUse.Residential && development == 0 && outer is >= FirstHouse and <= LastHouse ? outer - FirstHouse + 1 : 0;
            tiles[index] = new TileState(TileKind.Grass, building.Use, development, p, valueClass, house, powered);
        }
    }

    private static Building? BuildingAt(int tile)
    {
        foreach (var building in Buildings)
        {
            var drawings = building.Levels == 0 ? 1 : building.Levels * building.ValueClasses;
            if (tile >= building.First && tile < building.First + (drawings * Footprint.Parts(building.Size)))
            {
                return building;
            }
        }

        // A house stands on an empty residential zone's outer tile.
        return tile is >= FirstHouse and <= LastHouse ? Buildings[0] : null;
    }

    // A city made from the map, captured, and given the file's numbers, then restored, so an import
    // passes every check a save does; power is traced at once, as Micropolis does when it loads.
    private static CitySimulation Build(ReadOnlySpan<byte> bytes, TileMap map, string name, int seed)
    {
        var misc = bytes[(Histories * HistoryLength * 2)..];
        var fresh = new CitySimulation(map, seed, funds: 0);
        var save = fresh.Capture();
        var cityTime = Math.Max(0, Int(misc, CityTimeAt));

        save.Name = name;
        save.CityTime = cityTime;
        save.Tick = (long)cityTime * CityCalendar.StepsPerCount;
        save.Funds = Int(misc, FundsAt);

        // Micropolis puts a tax outside 0 to 20 back to 7, and the funding is a fraction in 16.16.
        var tax = Word(misc, TaxAt);
        save.Budget.TaxRate = tax is >= 0 and <= CityLimits.Core.Economy.CityBudget.MaxTaxRate ? tax : CityLimits.Core.Economy.CityBudget.DefaultTaxRate;
        save.Budget.TaxEffect = save.Budget.TaxRate;
        save.Budget.RoadFunding = Percent(Int(misc, RoadFundingAt));
        save.Budget.FireFunding = Percent(Int(misc, FireFundingAt));
        save.Budget.PoliceFunding = Percent(Int(misc, PoliceFundingAt));

        var (residents, commerce, industry) = (Math.Max(0, Word(misc, ResidentialPopulationAt)), Math.Max(0, Word(misc, CommercialPopulationAt)), Math.Max(0, Word(misc, IndustrialPopulationAt)));
        save.Census.ResidentPopulation = residents * 20;
        save.Census.CommercialPopulation = commerce * 8 * 20;
        save.Census.IndustrialPopulation = industry * 8 * 20;
        CopyHistory(bytes, 0, save.Census.ResidentialHistory);
        CopyHistory(bytes, 1, save.Census.CommercialHistory);
        CopyHistory(bytes, 2, save.Census.IndustrialHistory);

        save.Demand.Residential = Math.Clamp(Word(misc, ResidentialValveAt), -CityDemand.ResidentialLimit, CityDemand.ResidentialLimit);
        save.Demand.Commercial = Math.Clamp(Word(misc, CommercialValveAt), -CityDemand.BusinessLimit, CityDemand.BusinessLimit);
        save.Demand.Industrial = Math.Clamp(Word(misc, IndustrialValveAt), -CityDemand.BusinessLimit, CityDemand.BusinessLimit);
        save.CityMaps.LandValueAverage = Math.Clamp(Word(misc, LandValueAverageAt), 0, CityMaps.MaxLandValue);

        var city = CitySimulation.Restore(save);
        city.TracePowerNow();
        return city;
    }

    // The first of a history's 240 numbers are its months, newest first, as CityLimits keeps them.
    private static void CopyHistory(ReadOnlySpan<byte> bytes, int history, int[] months)
    {
        var start = history * HistoryLength * 2;
        for (var month = 0; month < months.Length && month < HistoryLength; month++)
        {
            months[month] = Math.Max(0, (int)BinaryPrimitives.ReadInt16BigEndian(bytes[(start + (month * 2))..]));
        }
    }

    private static int Percent(int fixedPoint) =>
        (int)Math.Clamp(Math.Round(fixedPoint / FullFundingFixed * CityLimits.Core.Economy.CityBudget.FullFunding), 0, CityLimits.Core.Economy.CityBudget.FullFunding);

    private static int Word(ReadOnlySpan<byte> misc, int at) => BinaryPrimitives.ReadInt16BigEndian(misc[(at * 2)..]);

    private static int Int(ReadOnlySpan<byte> misc, int at) => BinaryPrimitives.ReadInt32BigEndian(misc[(at * 2)..]);

    // The file says nothing of a seed, so one is made from its contents: the same file always
    // imports as the same city.
    private static int SeedOf(ReadOnlySpan<byte> bytes)
    {
        var hash = 2166136261u;
        foreach (var b in bytes)
        {
            hash = (hash ^ b) * 16777619u;
        }

        return unchecked((int)hash);
    }

    private static string CityNameFrom(string fileName)
    {
        var name = fileName.Trim();
        if (name.Length > CitySimulation.MaxNameLength)
        {
            name = name[..CitySimulation.MaxNameLength].Trim();
        }

        return CitySimulation.IsValidName(name) ? name : "Micropolis city";
    }

    private static TileState Replaced(SortedDictionary<string, int> replaced, string what)
    {
        Count(replaced, what);
        return new TileState(TileKind.Grass);
    }

    private static void Count(SortedDictionary<string, int> replaced, string what) =>
        replaced[what] = replaced.GetValueOrDefault(what) + 1;

    private static string Plural(int count, string what)
    {
        // "1 airport", "3 rail tiles", "2 rail crossings, kept as roads,".
        var comma = what.IndexOf(',', StringComparison.Ordinal);
        var (noun, rest) = comma < 0 ? (what, string.Empty) : (what[..comma], what[comma..]);
        if (count != 1)
        {
            noun += "s";
            rest = rest.Replace("a power line", "power lines", StringComparison.Ordinal).Replace("a road", "roads", StringComparison.Ordinal);
        }

        return $"{count.ToString(CultureInfo.InvariantCulture)} {noun}{rest}";
    }

    private sealed record Building(int First, int Size, TileUse Use, int Levels, int ValueClasses, string? Missing = null);
}
