using System.Text.Json.Nodes;
using CityLimits.Core.Definitions;
using CityLimits.Core.Economy;
using CityLimits.Core.Randomness;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class RailTests
{
    // A flat map 20 by 12, with a river down column 10.
    private static CitySimulation River(int funds = 100_000)
    {
        var kinds = new TileKind[20 * 12];
        for (var y = 0; y < 12; y++)
        {
            kinds[(y * 20) + 10] = TileKind.Water;
        }

        return new CitySimulation(TileMap.FromTerrain(20, 12, kinds), 1, funds: funds);
    }

    private static BuildResult Lay(CitySimulation city, BuildAction action, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(action, fromX, fromY);
        stroke.DragTo(toX, toY);
        return city.Builder.Apply(stroke);
    }

    [Fact]
    public void RailIsLaidInAStraightLineAt20ATileAndTunnelsUnderWaterAt100()
    {
        var city = River();

        var result = Lay(city, BuildAction.Rail, 2, 3, 14, 4);

        // The stroke runs along the longer way: across row 3, 12 tiles of track and one tunnel.
        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal((12 * TileCosts.Rail) + TileCosts.Tunnel, result.Cost);
        Assert.Equal(new TileState(TileKind.Grass, TileUse.Rail), city.Map.StateAt(2, 3));
        Assert.Equal(new TileState(TileKind.Water, TileUse.Rail), city.Map.StateAt(10, 3));
        Assert.True(city.Map.StateAt(10, 3).IsTunnel);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 4).Use);
        Assert.Equal(RoadConnections.East | RoadConnections.West, Rails.ConnectionsAt(city.Map, 10, 3));
    }

    [Fact]
    public void ATunnelNeedsRailBesideItToStartFrom()
    {
        var city = River();

        Assert.Equal(BuildStatus.Refused, Lay(city, BuildAction.Rail, 10, 3, 10, 3).Status);
        Assert.Equal(TileUse.None, city.Map.StateAt(10, 3).Use);
    }

    [Fact]
    public void RailCrossesAStraightRoadOrLineAndTheyCrossStraightRail()
    {
        var city = River();
        Lay(city, BuildAction.Road, 1, 5, 8, 5);
        Lay(city, BuildAction.PowerLine, 1, 8, 8, 8);
        Lay(city, BuildAction.Rail, 4, 1, 4, 9);

        // Rail laid down a column runs over the road and the line, which run across.
        Assert.Equal(TileUse.RailDownRoad, city.Map.StateAt(4, 5).Use);
        Assert.Equal(TileUse.RailDownPowerLine, city.Map.StateAt(4, 8).Use);
        Assert.Equal(RoadConnections.North | RoadConnections.South, Rails.ConnectionsAt(city.Map, 4, 5));
        Assert.Equal(RoadConnections.East | RoadConnections.West, RoadNetwork.ConnectionsAt(city.Map, 4, 5));
        Assert.Equal(RoadConnections.East | RoadConnections.West, PowerLines.ConnectionsAt(city.Map, 4, 8));

        // A road and a line laid down a column cross rail that runs across.
        Lay(city, BuildAction.Rail, 12, 2, 17, 2);
        Lay(city, BuildAction.Road, 14, 1, 14, 3);
        Lay(city, BuildAction.PowerLine, 16, 1, 16, 3);
        Assert.Equal(TileUse.RailAcrossRoad, city.Map.StateAt(14, 2).Use);
        Assert.Equal(TileUse.RailAcrossPowerLine, city.Map.StateAt(16, 2).Use);
        Assert.Equal(RoadConnections.North | RoadConnections.South, RoadNetwork.ConnectionsAt(city.Map, 14, 2));
        Assert.Equal(RoadConnections.East | RoadConnections.West, Rails.ConnectionsAt(city.Map, 14, 2));
    }

    [Fact]
    public void ACornerOrAJunctionTakesNoCrossing()
    {
        var city = River();
        Lay(city, BuildAction.Rail, 2, 2, 6, 2);
        Lay(city, BuildAction.Rail, 6, 2, 6, 6);

        Assert.Equal(BuildStatus.Refused, Lay(city, BuildAction.Road, 6, 2, 6, 2).Status);
        Assert.Equal(BuildStatus.Refused, Lay(city, BuildAction.PowerLine, 6, 2, 6, 2).Status);
        Assert.Equal(TileUse.Rail, city.Map.StateAt(6, 2).Use);
    }

    [Fact]
    public void TheBulldozerClearsRailTunnelsAndCrossings()
    {
        var city = River();
        Lay(city, BuildAction.Rail, 8, 3, 12, 3);
        Lay(city, BuildAction.Road, 9, 1, 9, 5);

        Assert.Equal(BuildStatus.Built, Lay(city, BuildAction.Bulldoze, 8, 3, 12, 3).Status);

        Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(8, 3));
        Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(9, 3));
        Assert.Equal(new TileState(TileKind.Water), city.Map.StateAt(10, 3));
    }

    [Fact]
    public void RailServesAZoneAndTripsTravelOnItWithoutTraffic()
    {
        // Homes and shops joined only by rail along their edges.
        var city = new CitySimulation(TileMap.FromTerrain(20, 9, new TileKind[20 * 9]), 1, funds: 100_000);
        Lay(city, BuildAction.Rail, 1, 6, 15, 6);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        city.Builder.Apply(BuildAction.ZoneCommercial, 12, 4);
        city.Map.SetState(2, 4, new TileState(TileKind.Grass, TileUse.Residential, development: 3, valueClass: 1, powered: true));
        city.Roads.EnsureCurrent(city.Map);

        Assert.True(city.Roads.IsZoneServed(2, 4));

        var traffic = new TrafficMap(city.Map);
        var trips = new TripPlanner(traffic);
        Assert.Equal(TripOutcome.Arrived, trips.Drive(city.Map, CityDefinitions.Base, new DeterministicRandom(7), 2, 4));
        Assert.All(trips.Route, tile => Assert.True(city.Map.StateAt(tile.X, tile.Y).IsRail));
        Assert.All(Enumerable.Range(0, 20), x => Assert.Equal(0, traffic.DensityAt(x, 6)));
    }

    [Fact]
    public void EachRailTileCountsAsTwoRoadTilesTowardUpkeep()
    {
        var withRoad = new CitySimulation(TileMap.FromTerrain(40, 6, new TileKind[40 * 6]), 1, funds: 100_000);
        Lay(withRoad, BuildAction.Road, 0, 2, 39, 2);
        var withRail = new CitySimulation(TileMap.FromTerrain(40, 6, new TileKind[40 * 6]), 1, funds: 100_000);
        Lay(withRail, BuildAction.Rail, 0, 2, 19, 2);

        foreach (var city in new[] { withRoad, withRail })
        {
            for (var i = 0; i < 9; i++)
            {
                city.Tick();
            }
        }

        Assert.Equal(20, withRail.RailTiles);
        Assert.Equal(0, withRail.RoadTiles);
        Assert.Equal(40, withRoad.RoadTiles);
        Assert.Equal(withRoad.EstimateBudget().RoadRequest, withRail.EstimateBudget().RoadRequest);
        Assert.True(withRail.EstimateBudget().RoadRequest > 0);
    }

    [Fact]
    public void UnderfundedRailCrumblesButNeverWhereItCrossesALine()
    {
        // Nine crossings with lines down every other column, apart from the river's, plain rail
        // between them, and a tunnel.
        var crossings = new[] { 0, 2, 4, 6, 8, 12, 14, 16, 18 };
        var city = River();
        foreach (var x in crossings)
        {
            Lay(city, BuildAction.PowerLine, x, 4, x, 6);
        }

        Lay(city, BuildAction.Rail, 0, 5, 19, 5);
        city.Budget.SetFunding(roads: 0, fire: 100, police: 100);

        for (var i = 0; i < 48 * 16 * 8; i++)
        {
            city.Tick();
        }

        Assert.All(crossings, x => Assert.Equal(TileUse.RailAcrossPowerLine, city.Map.StateAt(x, 5).Use));
        var rubble = Enumerable.Range(0, 20).Where(x => x % 2 == 1).Count(x => city.Map.StateAt(x, 5).Kind == TileKind.Rubble);
        Assert.True(rubble > 0, "underfunded rail should crumble");
        Assert.True(city.Map.StateAt(10, 5) is { Use: TileUse.Rail, Kind: TileKind.Water } or { Use: TileUse.None, Kind: TileKind.Water });
    }

    [Fact]
    public void ACityOfMoreThan50ZonesAndLessRailThanZonesNeedsRail()
    {
        var city = new CitySimulation(TileMap.FromTerrain(60, 30, new TileKind[60 * 30]), 1, funds: 1_000_000);
        for (var top = 0; top + 3 <= 27; top += 3)
        {
            var zones = new BuildStroke(BuildAction.ZoneResidential, 1, top + 1);
            zones.DragTo(52, top + 1);
            city.Builder.Apply(zones);
        }

        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;
        // Past the 18th count of 64, stopping after a scan, while the cycle's zone count stands.
        for (var i = 0; i < (64 * 16) + 9; i++)
        {
            city.Tick();
        }

        Assert.True(city.Power.Zones > 50);
        Assert.Contains(CityMessage.NeedRail, heard);
    }

    [Fact]
    public void RailIsSavedAsItsOwnPartAndTheMapHoldsWhatIsBeneathIt()
    {
        var city = River();
        Lay(city, BuildAction.Road, 1, 5, 8, 5);
        Lay(city, BuildAction.Rail, 4, 1, 4, 9);
        Lay(city, BuildAction.Rail, 4, 3, 14, 3);
        for (var i = 0; i < 9; i++)
        {
            city.Tick();
        }

        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        Assert.Equal('=', save["map"]!["use"]![5]!.GetValue<string>()[4]);
        Assert.Equal('.', save["map"]!["use"]![3]!.GetValue<string>()[6]);
        Assert.Equal('d', save["rail"]!["track"]![5]!.GetValue<string>()[4]);
        Assert.Equal('r', save["rail"]!["track"]![3]!.GetValue<string>()[10]);

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);
        foreach (var loaded in new[] { flat, binary })
        {
            Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(loaded));
            Assert.Equal(TileUse.RailDownRoad, loaded.Map.StateAt(4, 5).Use);
            Assert.Equal(new TileState(TileKind.Water, TileUse.Rail), loaded.Map.StateAt(10, 3));
            Assert.Equal(city.RailTiles, loaded.RailTiles);
        }
    }

    [Fact]
    public void ACityWithoutItsRailPartOpensAsIfItHadNone()
    {
        // What a version that doesn't know rail sees: the map, with the road at the crossing.
        var city = River();
        Lay(city, BuildAction.Road, 1, 5, 8, 5);
        Lay(city, BuildAction.Rail, 4, 1, 4, 9);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save.Remove("rail");
        save["format"] = "1.3";

        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var older, out var problem), problem);

        Assert.Equal(TileUse.Road, older.Map.StateAt(4, 5).Use);
        Assert.Equal(TileUse.None, older.Map.StateAt(4, 2).Use);
        Assert.Equal(RoadConnections.East | RoadConnections.West, RoadNetwork.ConnectionsAt(older.Map, 4, 5));
    }

    [Fact]
    public void A14SaveWithoutRailIsRefused()
    {
        // A 1.x save had what its version had, so one lacking a part of it is damaged.
        var save = JsonNode.Parse(FlatCityFormat.Write(River(), legacy: true))!.AsObject();
        save.Remove("rail");

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal("rail: missing.", problem);
    }

    [Theory]
    [InlineData(3, 'x', "rail.track[3]: 'x' at 2 can't go on Grass None.")]
    [InlineData(3, 'a', "rail.track[3]: 'a' at 2 can't go on Grass None.")]
    [InlineData(5, 'r', "rail.track[5]: 'r' at 2 can't go on Grass Road.")]
    public void RailWhereItCantBeIsRefused(int row, char rail, string expected)
    {
        var city = River();
        Lay(city, BuildAction.Road, 1, 5, 8, 5);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        var track = save["rail"]!["track"]!.AsArray();
        var line = track[row]!.GetValue<string>().ToCharArray();
        line[2] = rail;
        track[row] = new string(line);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void AnOlderTrackOfTheWrongSizeIsRefused()
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(River()))!.AsObject();
        save["rail"]!["track"]!.AsArray().RemoveAt(0);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal("rail.track: needs 12 rows.", problem);
    }
}
