using System.Buffers.Binary;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class MicropolisCityImportTests
{
    private const int Width = 120;
    private const int Height = 100;
    private const int MapOffset = ((6 * 240) + 120) * 2;

    private static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "Assets", file);

    private static CitySimulation Bridgeport()
    {
        Assert.True(FlatCityFormat.TryReadFile(Fixture("Bridgeport.fcl"), out var city, out var problem), problem);
        return city;
    }

    private static CitySimulation Import(byte[] bytes, out IReadOnlyList<string> notes, string name = "Imported")
    {
        Assert.True(MicropolisCityImport.TryImport(bytes, name, out var city, out var problem, out notes), problem);
        return city;
    }

    [Fact]
    public void BridgeportsMicropolisFileIsTheOneTheTestWriterMakes()
    {
        var bytes = MicropolisCityWriter.Write(Bridgeport());
        var fixture = Fixture("Bridgeport.cty");
        if (!File.Exists(fixture))
        {
            File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "Bridgeport.cty.new"), bytes);
            Assert.Fail($"No Bridgeport.cty fixture: one was written to {AppContext.BaseDirectory}Bridgeport.cty.new to look at and add.");
        }

        Assert.Equal(bytes, File.ReadAllBytes(fixture));
    }

    [Fact]
    public void BridgeportComesBackFromMicropolisAsItWas()
    {
        var original = Bridgeport();

        Assert.True(MicropolisCityImport.TryImportFile(Fixture("Bridgeport.cty"), out var city, out var problem, out var notes), problem);

        Assert.Empty(notes);
        Assert.Equal("Bridgeport", city.Name);
        Assert.Equal(original.Date, city.Date);
        Assert.Equal(original.Treasury.Funds, city.Treasury.Funds);
        Assert.Equal(original.Budget.TaxRate, city.Budget.TaxRate);
        Assert.Equal((original.Budget.RoadFunding, original.Budget.FireFunding, original.Budget.PoliceFunding), (city.Budget.RoadFunding, city.Budget.FireFunding, city.Budget.PoliceFunding));
        Assert.Equal(original.Census.CityPopulation, city.Census.CityPopulation);
        Assert.Equal(original.Census.ResidentialHistory(3), city.Census.ResidentialHistory(3));
        Assert.Equal(original.Demand.Residential, city.Demand.Residential);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var (was, now) = (original.Map.StateAt(x, y), city.Map.StateAt(x, y));
                Assert.True(
                    (was.Kind, was.Use, was.ZonePart, was.Development, was.ValueClass, was.House) == (now.Kind, now.Use, now.ZonePart, now.Development, now.ValueClass, now.House),
                    $"At {x}, {y}: {was} became {now}");
            }
        }
    }

    [Fact]
    public void AnImportedCityHasPowerFromItsFirstStepAndRunsOn()
    {
        var city = Import(MicropolisCityWriter.Write(Bridgeport()), out _);

        Assert.True(city.Power.PoweredTiles > 0);
        Assert.True(city.Map.StateAt(PlantCentre(city).X, PlantCentre(city).Y).Powered);
        for (var i = 0; i < 48 * 16; i++)
        {
            city.Tick();
        }

        Assert.True(city.Census.CityPopulation > 0);
    }

    [Fact]
    public void TheSameFileAlwaysImportsAsTheSameCity()
    {
        var bytes = MicropolisCityWriter.Write(Bridgeport());
        var first = Import(bytes, out _);
        var second = Import(bytes, out _);

        Assert.Equal(first.Seed, second.Seed);
        for (var i = 0; i < 200; i++)
        {
            first.Tick();
            second.Tick();
        }

        Assert.Equal(FlatCityFormat.Write(first), FlatCityFormat.Write(second));
    }

    [Fact]
    public void RailComesInAsRailAndWhatCityLimitsDoesntHaveYetBecomesBareGroundAndIsListed()
    {
        var bytes = MicropolisCityWriter.Write(new CitySimulation(TileMap.FromTerrain(Width, Height, new TileKind[Width * Height]), 1));
        SetTile(bytes, 5, 5, 226);        // rail
        SetTile(bytes, 6, 5, 227);        // rail
        SetTile(bytes, 7, 5, 237);        // rail across a road
        SetTile(bytes, 8, 5, 221);        // rail across a power line
        SetTile(bytes, 12, 5, 238);       // rail down a road
        SetTile(bytes, 13, 5, 222);       // rail down a power line
        SetTile(bytes, 14, 5, 224);       // a tunnel
        SetTile(bytes, 9, 5, 48);         // flood
        SetTile(bytes, 10, 5, 56);        // fire
        SetTile(bytes, 11, 5, 52);        // radiation
        Stamp(bytes, 20, 20, 709, 6);     // an airport
        Stamp(bytes, 30, 30, 779, 4);     // a stadium
        Stamp(bytes, 40, 40, 693, 4);     // a seaport
        SetTile(bytes, 60, 60, 252);      // a house with no zone

        var city = Import(bytes, out var notes);

        Assert.Equal(new TileState(TileKind.Grass, TileUse.Rail), city.Map.StateAt(5, 5));
        Assert.Equal(new TileState(TileKind.Grass, TileUse.Rail), city.Map.StateAt(6, 5));
        Assert.Equal(TileUse.RailAcrossRoad, city.Map.StateAt(7, 5).Use);
        Assert.Equal(TileUse.RailAcrossPowerLine, city.Map.StateAt(8, 5).Use);
        Assert.Equal(TileUse.RailDownRoad, city.Map.StateAt(12, 5).Use);
        Assert.Equal(TileUse.RailDownPowerLine, city.Map.StateAt(13, 5).Use);
        Assert.Equal(new TileState(TileKind.Water, TileUse.Rail), city.Map.StateAt(14, 5));
        Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(21, 21));
        Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(60, 60));
        Assert.Equal(
            [
                "1 airport became bare ground: CityLimits doesn't have them yet.",
                "1 burning tile became bare ground: CityLimits doesn't have them yet.",
                "1 flooded tile became bare ground: CityLimits doesn't have them yet.",
                "1 part of a building with no centre became bare ground: CityLimits doesn't have them yet.",
                "1 radioactive tile became bare ground: CityLimits doesn't have them yet.",
                "1 seaport became bare ground: CityLimits doesn't have them yet.",
                "1 stadium became bare ground: CityLimits doesn't have them yet.",
            ],
            notes);
    }

    [Fact]
    public void ZonesComeInAtTheirLevelAndClassWithTheirHouses()
    {
        var bytes = MicropolisCityWriter.Write(new CitySimulation(TileMap.FromTerrain(Width, Height, new TileKind[Width * Height]), 1));
        Stamp(bytes, 10, 10, 261 + (9 * ((2 * 4) + 2)), 3); // residential, class 2, level 3
        Stamp(bytes, 20, 10, 432 + (9 * ((3 * 5) + 4)), 3); // commercial, class 3, level 5
        Stamp(bytes, 30, 10, 621 + (9 * ((1 * 4) + 0)), 3); // industrial, class 1, level 1
        Stamp(bytes, 40, 10, 240, 3);                       // an empty residential zone
        SetTile(bytes, 39, 9, 249 + 4);                     // with a house in its corner
        SetTile(bytes, 40, 10, 244 | (1 << 15));            // its centre, powered

        var city = Import(bytes, out var notes);

        Assert.Empty(notes);
        Assert.Equal((TileUse.Residential, 3, 2), Building(city, 10, 10));
        Assert.Equal((TileUse.Commercial, 5, 3), Building(city, 20, 10));
        Assert.Equal((TileUse.Industrial, 1, 1), Building(city, 30, 10));
        Assert.Equal((TileUse.Residential, 0, 0), Building(city, 40, 10));
        Assert.Equal(5, city.Map.StateAt(39, 9).House);
        Assert.Equal(8, city.Map.StateAt(41, 11).ZonePart);
    }

    [Theory]
    [InlineData(66, TileKind.Grass, TileUse.Road)]                 // a road
    [InlineData(66 + 16, TileKind.Grass, TileUse.Road)]            // with light traffic
    [InlineData(66 + 80, TileKind.Grass, TileUse.Road)]            // with heavy traffic
    [InlineData(77 + 16, TileKind.Grass, TileUse.RoadAcrossPowerLine)]
    [InlineData(78 + 80, TileKind.Grass, TileUse.RoadDownPowerLine)]
    [InlineData(65 + 16, TileKind.Water, TileUse.Road)]            // a bridge with traffic
    [InlineData(79, TileKind.Water, TileUse.Road)]                 // an open bridge
    [InlineData(830, TileKind.Water, TileUse.Road)]                // a drawbridge
    [InlineData(209, TileKind.Water, TileUse.PowerLine)]
    [InlineData(215, TileKind.Grass, TileUse.PowerLine)]
    [InlineData(42, TileKind.Grass, TileUse.Park)]
    [InlineData(841, TileKind.Grass, TileUse.Park)]                // a fountain
    [InlineData(30, TileKind.Trees, TileUse.None)]
    [InlineData(12, TileKind.Water, TileUse.None)]
    [InlineData(46, TileKind.Rubble, TileUse.None)]
    public void EachTileComesInAsWhatItIs(int number, TileKind kind, TileUse use)
    {
        var bytes = MicropolisCityWriter.Write(new CitySimulation(TileMap.FromTerrain(Width, Height, new TileKind[Width * Height]), 1));
        SetTile(bytes, 50, 50, number);

        var city = Import(bytes, out var notes);

        Assert.Empty(notes);
        Assert.Equal((kind, use), (city.Map.StateAt(50, 50).Kind, city.Map.StateAt(50, 50).Use));
    }

    [Fact]
    public void AFileWithSimCitysHeaderImportsTheSame()
    {
        var bytes = MicropolisCityWriter.Write(Bridgeport());

        var withHeader = Import([.. new byte[MicropolisCityImport.HeaderBytes], .. bytes], out _);
        var without = Import(bytes, out _);

        Assert.Equal(FlatCityFormat.Write(without), FlatCityFormat.Write(withHeader));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(27119)]
    [InlineData(27121)]
    public void AFileOfTheWrongSizeIsRefused(int size)
    {
        Assert.False(MicropolisCityImport.TryImport(new byte[size], "Wrong", out var city, out var problem, out _));

        Assert.Null(city);
        Assert.StartsWith("A Micropolis city is 27120 bytes", problem);
    }

    [Fact]
    public void ATaxMicropolisWouldntHaveIsSevenPercentAsMicropolisMakesIt()
    {
        var bytes = MicropolisCityWriter.Write(Bridgeport());
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan((6 * 240 * 2) + (56 * 2)), 40);

        Assert.Equal(7, Import(bytes, out _).Budget.TaxRate);
    }

    [Fact]
    public void ABuildingRunningOffTheMapIsRefused()
    {
        var bytes = MicropolisCityWriter.Write(new CitySimulation(TileMap.FromTerrain(Width, Height, new TileKind[Width * Height]), 1));
        SetTile(bytes, 0, 0, 244);

        Assert.False(MicropolisCityImport.TryImport(bytes, "Edge", out _, out var problem, out _));

        Assert.Equal("map at 0, 0: the building centred here runs off the map.", problem);
    }

    [Fact]
    public void MicropolisCitiesAreListedAndImportedButNeverWritten()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var path = Path.Combine(folder, "Bridgeport.cty");
            File.WriteAllBytes(path, MicropolisCityWriter.Write(Bridgeport()));

            var listed = Assert.Single(CityFiles.List(folder));
            Assert.True(CityFiles.TryRead(path, out var city, out var problem, out _), problem);

            Assert.True(CityFiles.IsImport(listed.Path));
            Assert.Equal("Bridgeport", listed.Name);
            Assert.Equal(Bridgeport().Census.CityPopulation, listed.Population);
            Assert.Equal("Bridgeport", city.Name);
            Assert.Throws<ArgumentException>(() => CityFiles.TryWrite(city, path, out _));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static (TileUse Use, int Development, int ValueClass) Building(CitySimulation city, int x, int y)
    {
        var state = city.Map.StateAt(x, y);
        Assert.True(state.IsBuildingCenter);
        return (state.Use, state.Development, state.ValueClass);
    }

    private static (int X, int Y) PlantCentre(CitySimulation city)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var state = city.Map.StateAt(x, y);
                if (state.IsBuildingCenter && state.Use is TileUse.CoalPowerPlant or TileUse.NuclearPowerPlant)
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException("Bridgeport has a plant.");
    }

    // The map is column by column, a 16-bit number a tile.
    private static void SetTile(byte[] bytes, int x, int y, int number) =>
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(MapOffset + (((x * Height) + y) * 2)), (ushort)number);

    // A building's drawing, its tiles numbered across and then down from its first.
    private static void Stamp(byte[] bytes, int centreX, int centreY, int first, int size)
    {
        for (var part = 0; part < size * size; part++)
        {
            SetTile(bytes, centreX + (part % size) - 1, centreY + (part / size) - 1, first + part);
        }
    }
}

/// <summary>
/// Writes a city as a Micropolis city file, for these tests only: CityLimits imports Micropolis
/// cities and never writes them. It uses Micropolis' tile numbers the importer reads.
/// </summary>
internal static class MicropolisCityWriter
{
    public static byte[] Write(CitySimulation city)
    {
        var bytes = new byte[MicropolisCityImport.FileBytes];
        var span = bytes.AsSpan();
        for (var month = 0; month < 120; month++)
        {
            BinaryPrimitives.WriteInt16BigEndian(span[(month * 2)..], (short)city.Census.ResidentialHistory(month));
            BinaryPrimitives.WriteInt16BigEndian(span[((240 + month) * 2)..], (short)city.Census.CommercialHistory(month));
            BinaryPrimitives.WriteInt16BigEndian(span[((480 + month) * 2)..], (short)city.Census.IndustrialHistory(month));
        }

        var misc = span[(6 * 240 * 2)..];
        BinaryPrimitives.WriteInt16BigEndian(misc[(2 * 2)..], (short)(city.Census.ResidentPopulation / 20));
        BinaryPrimitives.WriteInt16BigEndian(misc[(3 * 2)..], (short)(city.Census.CommercialPopulation / 160));
        BinaryPrimitives.WriteInt16BigEndian(misc[(4 * 2)..], (short)(city.Census.IndustrialPopulation / 160));
        BinaryPrimitives.WriteInt16BigEndian(misc[(5 * 2)..], (short)city.Demand.Residential);
        BinaryPrimitives.WriteInt16BigEndian(misc[(6 * 2)..], (short)city.Demand.Commercial);
        BinaryPrimitives.WriteInt16BigEndian(misc[(7 * 2)..], (short)city.Demand.Industrial);
        BinaryPrimitives.WriteInt32BigEndian(misc[(8 * 2)..], (int)city.CityTime);
        BinaryPrimitives.WriteInt16BigEndian(misc[(12 * 2)..], (short)city.Maps.LandValueAverage);
        BinaryPrimitives.WriteInt32BigEndian(misc[(50 * 2)..], city.Treasury.Funds);
        BinaryPrimitives.WriteInt16BigEndian(misc[(56 * 2)..], (short)city.Budget.TaxRate);
        BinaryPrimitives.WriteInt32BigEndian(misc[(58 * 2)..], city.Budget.PoliceFunding * 65536 / 100);
        BinaryPrimitives.WriteInt32BigEndian(misc[(60 * 2)..], city.Budget.FireFunding * 65536 / 100);
        BinaryPrimitives.WriteInt32BigEndian(misc[(62 * 2)..], city.Budget.RoadFunding * 65536 / 100);

        var map = span[(((6 * 240) + 120) * 2)..];
        var i = 0;
        for (var x = 0; x < city.Map.Width; x++)
        {
            for (var y = 0; y < city.Map.Height; y++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(map[(i++ * 2)..], (ushort)Number(city.Map, x, y));
            }
        }

        return bytes;
    }

    private static int Number(TileMap map, int x, int y)
    {
        var state = map.StateAt(x, y);
        var centre = map.ZoneStateAt(x, y);
        var number = state.Use switch
        {
            TileUse.None => state.Kind switch
            {
                TileKind.Water => 2,
                TileKind.Trees => 37,
                TileKind.Rubble => 44,
                _ => 0,
            },
            TileUse.Road => state.Kind == TileKind.Water ? 64 : 66,
            TileUse.RoadAcrossPowerLine => 77,
            TileUse.RoadDownPowerLine => 78,
            TileUse.PowerLine => state.Kind == TileKind.Water ? 208 : 210,
            TileUse.Park => 40,
            TileUse.CoalPowerPlant => 745 + state.ZonePart,
            TileUse.NuclearPowerPlant => 811 + state.ZonePart,
            TileUse.FireStation => 761 + state.ZonePart,
            TileUse.PoliceStation => 770 + state.ZonePart,
            TileUse.Hospital => 405 + state.ZonePart,
            TileUse.Church => 414 + state.ZonePart,
            TileUse.Residential when centre.Development == 0 && state.HasHouse => 249 + state.House - 1,
            TileUse.Residential => Zone(240, 261, 4, centre) + state.ZonePart,
            TileUse.Commercial => Zone(423, 432, 5, centre) + state.ZonePart,
            TileUse.Industrial => Zone(612, 621, 4, centre) + state.ZonePart,
            _ => throw new InvalidOperationException($"No Micropolis tile for {state.Use}."),
        };

        return state.IsBuildingCenter && state.Powered ? number | (1 << 15) : number;
    }

    private static int Zone(int empty, int firstBuilding, int levels, TileState centre) =>
        centre.Development == 0 ? empty : firstBuilding + (9 * ((centre.ValueClass * levels) + centre.Development - 1));
}
