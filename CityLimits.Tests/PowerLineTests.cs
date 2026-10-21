using CityLimits.Core.Definitions;
using CityLimits.Core.Economy;
using CityLimits.Core.Scenarios;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class PowerLineTests
{
    // A map drawn in rows: '~' water, 'T' trees, '#' a road, '+' a power line, anything else grass.
    private static (TileMap Map, Treasury Treasury, CityBuilder Builder) CityOf(params string[] rows)
    {
        var width = rows.Max(row => row.Length);
        var map = TileMap.FromTerrain(width, rows.Length, new TileKind[width * rows.Length]);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                map.SetState(x, y, rows[y][x] switch
                {
                    '~' => new TileState(TileKind.Water),
                    'T' => new TileState(TileKind.Trees),
                    '#' => new TileState(TileKind.Grass, TileUse.Road),
                    '+' => new TileState(TileKind.Grass, TileUse.PowerLine),
                    _ => new TileState(TileKind.Grass),
                });
            }
        }

        var treasury = new Treasury(10_000);
        return (map, treasury, new CityBuilder(map, treasury, new RoadNetwork()));
    }

    private static BuildStroke Drag(BuildAction action, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(action, fromX, fromY);
        stroke.DragTo(toX, toY);
        return stroke;
    }

    [Fact]
    public void ThePricesAreMicropolis()
    {
        Assert.Equal(5, TileCosts.PowerLine);
        Assert.Equal(25, TileCosts.UnderwaterPowerLine);
    }

    [Fact]
    public void ALineRunsStraightLikeARoadAndClearsTreesForADollarMore()
    {
        var city = CityOf(".....", ".TT..");
        var stroke = Drag(BuildAction.PowerLine, 0, 1, 4, 0);

        var result = city.Builder.Apply(stroke);

        Assert.Equal(new TileRect(0, 1, 5, 1), stroke.Bounds);
        Assert.Equal(5, result.TilesChanged);
        Assert.Equal(10_000 - (5 * TileCosts.PowerLine) - (2 * TileCosts.AutoClear), city.Treasury.Funds);
        Assert.All(Enumerable.Range(0, 5), x => Assert.Equal(new TileState(TileKind.Grass, TileUse.PowerLine), city.Map.StateAt(x, 1)));
    }

    [Fact]
    public void ALineCrossesWaterUnderwaterFromSomethingThatCarriesPower()
    {
        var city = CityOf("..~~~..");

        var result = city.Builder.Apply(Drag(BuildAction.PowerLine, 0, 0, 6, 0));

        Assert.Equal(7, result.TilesChanged);
        Assert.Equal(10_000 - (4 * TileCosts.PowerLine) - (3 * TileCosts.UnderwaterPowerLine), city.Treasury.Funds);
        Assert.Equal(new TileState(TileKind.Water, TileUse.PowerLine), city.Map.StateAt(3, 0));
    }

    [Fact]
    public void WaterWithNothingCarryingPowerBesideItIsNotCrossed()
    {
        var city = CityOf("#~~~");

        var result = city.Builder.Apply(Drag(BuildAction.PowerLine, 1, 0, 3, 0));

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(10_000, city.Treasury.Funds);
    }

    [Fact]
    public void AnUnderwaterLineCanStartFromAZone()
    {
        var city = CityOf("....", "....", "....", "~~~~");
        city.Builder.Apply(BuildAction.ZoneResidential, 1, 1);
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.PowerLine, 1, 3);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(funds - TileCosts.UnderwaterPowerLine, city.Treasury.Funds);
    }

    [Theory]
    [InlineData(new[] { ".....", "#####", "....." }, 2, 0, 2, 2, TileUse.RoadAcrossPowerLine)]
    [InlineData(new[] { "..#..", "..#..", "..#.." }, 1, 1, 3, 1, TileUse.RoadDownPowerLine)]
    public void ALineOverAStraightRoadMakesACrossing(string[] rows, int fromX, int fromY, int toX, int toY, TileUse crossing)
    {
        var city = CityOf(rows);

        var result = city.Builder.Apply(Drag(BuildAction.PowerLine, fromX, fromY, toX, toY));

        Assert.Equal(3, result.TilesChanged);
        Assert.Equal(10_000 - (3 * TileCosts.PowerLine), city.Treasury.Funds);
        Assert.Equal(crossing, city.Map.StateAt(2, 1).Use);
        Assert.True(city.Map.StateAt(2, 1).IsRoad);
        Assert.True(city.Map.StateAt(2, 1).HasPowerLine);
    }

    [Theory]
    [InlineData(new[] { ".....", "+++++", "....." }, 2, 0, 2, 2, TileUse.RoadDownPowerLine)]
    [InlineData(new[] { "..+..", "..+..", "..+.." }, 1, 1, 3, 1, TileUse.RoadAcrossPowerLine)]
    public void ARoadOverAStraightLineMakesACrossingAtTheRoadsPrice(string[] rows, int fromX, int fromY, int toX, int toY, TileUse crossing)
    {
        var city = CityOf(rows);

        city.Builder.Apply(Drag(BuildAction.Road, fromX, fromY, toX, toY));

        Assert.Equal(10_000 - (3 * TileCosts.Road), city.Treasury.Funds);
        Assert.Equal(crossing, city.Map.StateAt(2, 1).Use);
    }

    [Fact]
    public void ACrossingCantGoOnACornerOrABridge()
    {
        var corner = CityOf("##.", "#..");
        Assert.Equal(BuildRefusal.CannotBuildHere, corner.Builder.Apply(BuildAction.PowerLine, 0, 0).Reason);

        var lineCorner = CityOf("++.", "+..");
        Assert.Equal(BuildRefusal.CannotBuildHere, lineCorner.Builder.Apply(BuildAction.Road, 0, 0).Reason);

        var bridge = CityOf("#~#");
        bridge.Builder.Apply(Drag(BuildAction.Road, 0, 0, 2, 0));
        Assert.Equal(BuildRefusal.CannotBuildHere, bridge.Builder.Apply(BuildAction.PowerLine, 1, 0).Reason);
    }

    [Fact]
    public void ALineCantGoOverAZoneOrAPlant()
    {
        var city = CityOf(".....", ".....", ".....", ".....", ".....");
        city.Builder.Apply(BuildAction.CoalPowerPlant, 1, 1);
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.PowerLine, 2, 2);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(funds, city.Treasury.Funds);
    }

    [Fact]
    public void StringingTheSameLineTwiceChangesNothing()
    {
        var city = CityOf("+++");

        var result = city.Builder.Apply(Drag(BuildAction.PowerLine, 0, 0, 2, 0));

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.AlreadyDone, result.Reason);
    }

    [Fact]
    public void ACrossingJoinsRoadsAlongItsRoadAndLinesAlongItsLine()
    {
        var city = CityOf(".+.", "###", ".+.");
        city.Map.SetState(1, 1, new TileState(TileKind.Grass, TileUse.RoadAcrossPowerLine));

        Assert.Equal(RoadConnections.East | RoadConnections.West, RoadNetwork.ConnectionsAt(city.Map, 1, 1));
        Assert.Equal(RoadConnections.North | RoadConnections.South, PowerLines.ConnectionsAt(city.Map, 1, 1));
        Assert.Equal(RoadConnections.South, PowerLines.ConnectionsAt(city.Map, 1, 0));
        Assert.Equal(RoadConnections.None, RoadNetwork.ConnectionsAt(city.Map, 1, 0));
    }

    [Fact]
    public void ARoadBesideACrossingsLineDoesntJoinIt()
    {
        var city = CityOf(".#.", "###");
        city.Map.SetState(1, 1, new TileState(TileKind.Grass, TileUse.RoadAcrossPowerLine));

        Assert.Equal(RoadConnections.None, RoadNetwork.ConnectionsAt(city.Map, 1, 0));
    }

    [Fact]
    public void ABridgeCantStartFromTheLineSideOfACrossing()
    {
        var city = CityOf(".~.", "###");
        city.Map.SetState(1, 1, new TileState(TileKind.Grass, TileUse.RoadAcrossPowerLine));

        Assert.Equal(BuildRefusal.CannotBuildHere, city.Builder.Apply(BuildAction.Road, 1, 0).Reason);
    }

    [Fact]
    public void ALineJoinsZonesAndPlantsButNotRoads()
    {
        var city = CityOf("......", "......", "......", "......", "#+....");
        city.Builder.Apply(BuildAction.ZoneResidential, 1, 2);

        Assert.Equal(RoadConnections.North, PowerLines.ConnectionsAt(city.Map, 1, 4));
    }

    [Theory]
    [InlineData(TileKind.Grass, TileUse.PowerLine, TileKind.Grass)]
    [InlineData(TileKind.Water, TileUse.PowerLine, TileKind.Water)]
    [InlineData(TileKind.Grass, TileUse.RoadAcrossPowerLine, TileKind.Grass)]
    public void TheBulldozerClearsALineForADollar(TileKind kind, TileUse use, TileKind cleared)
    {
        var city = CityOf("...");
        city.Map.SetState(1, 0, new TileState(kind, use));

        var result = city.Builder.Apply(BuildAction.Bulldoze, 1, 0);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(new TileState(cleared), city.Map.StateAt(1, 0));
        Assert.Equal(10_000 - TileCosts.Bulldoze, city.Treasury.Funds);
    }

    [Fact]
    public void ACrossingStillServesAZone()
    {
        var city = CityOf("...", "...", "...", "###");
        city.Map.SetState(1, 3, new TileState(TileKind.Grass, TileUse.RoadAcrossPowerLine));
        var roads = new RoadNetwork();
        roads.Rebuild(city.Map);

        Assert.True(roads.IsZoneServed(1, 1));
    }

    [Fact]
    public void ScenariosCountLineTiles()
    {
        var city = CityOf("+~+", "...");
        city.Map.SetState(1, 0, new TileState(TileKind.Water, TileUse.PowerLine));

        Assert.Equal(3, ScenarioRunner.Count(city.Map, CityDefinitions.Base).PowerLine);
    }

    [Fact]
    public void TheReadoutNamesALine()
    {
        Assert.Equal("Power line - strung on poles", TileInfo.Describe(new TileState(TileKind.Grass, TileUse.PowerLine)));
        Assert.Equal("Power line - laid under water", TileInfo.Describe(new TileState(TileKind.Water, TileUse.PowerLine)));
        Assert.Equal("Road and power line - carries traffic across the line", TileInfo.Describe(new TileState(TileKind.Grass, TileUse.RoadDownPowerLine)));
    }
}
