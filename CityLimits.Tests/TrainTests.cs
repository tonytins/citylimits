using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.Sprites;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TrainTests
{
    private const int People = Train.PeopleBeforeTrains + 1;

    // A map with the given rail laid on it, a tunnel where the river is.
    private static TileMap Track(int width, int height, params (int FromX, int FromY, int ToX, int ToY)[] lines)
    {
        var city = new CitySimulation(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, funds: 1_000_000);
        foreach (var (fromX, fromY, toX, toY) in lines)
        {
            var stroke = new BuildStroke(BuildAction.Rail, fromX, fromY);
            stroke.DragTo(toX, toY);
            city.Builder.Apply(stroke);
        }

        return city.Map;
    }

    private static Train SetOut(TileMap map, Random random)
    {
        var train = new Train();
        for (var i = 0; i < 10_000 && !train.IsRunning; i++)
        {
            train.Step(map, People, random);
        }

        Assert.True(train.IsRunning);
        return train;
    }

    [Fact]
    public void NoTrainSetsOutInACityOf20PeopleOrWithNoRail()
    {
        var train = new Train();
        var random = new Random(1);
        var rail = Track(10, 10, (1, 5, 8, 5));
        var empty = Track(10, 10);

        for (var i = 0; i < 2_000; i++)
        {
            train.Step(rail, Train.PeopleBeforeTrains, random);
            train.Step(empty, People, random);
        }

        Assert.False(train.IsRunning);
    }

    [Fact]
    public void ATrainSetsOutOnRailAndOnlyEverRunsOnIt()
    {
        // A loop of track with a branch.
        var map = Track(12, 12, (1, 1, 9, 1), (9, 1, 9, 9), (9, 9, 1, 9), (1, 9, 1, 1), (5, 1, 5, 5));
        var random = new Random(2);
        var train = SetOut(map, random);

        for (var i = 0; i < 2_000; i++)
        {
            train.Step(map, People, random);
            Assert.True(train.IsRunning);
            Assert.True(map.StateAt(train.X / 16, train.Y / 16).IsRail, $"off the rail at {train.X}, {train.Y}");
        }
    }

    [Fact]
    public void ItMovesAQuarterOfATileAStepAndTurnsOnlyAtATilesMiddle()
    {
        var map = Track(12, 4, (1, 2, 10, 2));
        var random = new Random(3);
        var train = SetOut(map, random);

        var positions = new List<(int X, int Y)>();
        for (var i = 0; i < 40; i++)
        {
            train.Step(map, People, random);
            positions.Add((train.X, train.Y));
        }

        Assert.All(positions, position => Assert.Equal((2 * 16) + 8, position.Y));
        Assert.All(positions.Zip(positions.Skip(1)), pair => Assert.True(Math.Abs(pair.Second.X - pair.First.X) is 0 or 4));
        Assert.Equal(TrainFrame.Across, train.Frame);
    }

    [Fact]
    public void AtADeadEndItStopsThenTurnsBack()
    {
        var map = Track(8, 3, (1, 1, 6, 1));
        var random = new Random(4);
        var train = SetOut(map, random);

        var xs = new List<int>();
        for (var i = 0; i < 200; i++)
        {
            train.Step(map, People, random);
            xs.Add(train.X);
        }

        // It never leaves the line, reaches both ends, and never turns straight back between them.
        Assert.All(xs, x => Assert.InRange(x, (1 * 16) + 8, (6 * 16) + 8));
        Assert.Contains((1 * 16) + 8, xs);
        Assert.Contains((6 * 16) + 8, xs);
        var moves = xs.Zip(xs.Skip(1), (from, to) => (From: from, Way: Math.Sign(to - from))).Where(move => move.Way != 0).ToList();
        foreach (var (before, after) in moves.Zip(moves.Skip(1)))
        {
            if (before.Way != after.Way)
            {
                Assert.Contains(after.From, new[] { (1 * 16) + 8, (6 * 16) + 8 });
            }
        }

        Assert.True(train.IsRunning);
    }

    [Fact]
    public void ATrainWithNowhereToGoRetires()
    {
        var map = Track(5, 5, (2, 2, 2, 2));
        var random = new Random(5);
        var train = SetOut(map, random);

        train.Step(map, People, random);

        Assert.False(train.IsRunning);
    }

    [Fact]
    public void ItShowsTheWayItRunsTurnsAndHidesInATunnel()
    {
        var kinds = new TileKind[12 * 12];
        kinds[(1 * 12) + 6] = TileKind.Water;
        var city = new CitySimulation(TileMap.FromTerrain(12, 12, kinds), 1, funds: 1_000_000);
        var across = new BuildStroke(BuildAction.Rail, 1, 1);
        across.DragTo(9, 1);
        city.Builder.Apply(across);
        var down = new BuildStroke(BuildAction.Rail, 9, 1);
        down.DragTo(9, 9);
        city.Builder.Apply(down);

        var random = new Random(6);
        var train = SetOut(city.Map, random);
        var frames = new HashSet<TrainFrame>();
        for (var i = 0; i < 3_000; i++)
        {
            train.Step(city.Map, People, random);
            frames.Add(train.Frame);
            // As in Micropolis, the tunnel's drawing is chosen as the train sets off toward the
            // tunnel, so it shows only within a tile of it.
            if (train.Frame == TrainFrame.Tunnel)
            {
                Assert.InRange(Math.Abs(train.X - ((6 * 16) + 8)) + Math.Abs(train.Y - ((1 * 16) + 8)), 0, 16);
            }
        }

        Assert.Contains(TrainFrame.Across, frames);
        Assert.Contains(TrainFrame.Down, frames);
        Assert.Contains(TrainFrame.Tunnel, frames);
        Assert.True(frames.Contains(TrainFrame.TurningNortheast) || frames.Contains(TrainFrame.TurningNorthwest));
    }

    [Fact]
    public void TheTrainNeverChangesTheCity()
    {
        var city = new CitySimulation(TileMap.FromTerrain(12, 12, new TileKind[144]), 1, funds: 1_000_000);
        var rail = new BuildStroke(BuildAction.Rail, 1, 5);
        rail.DragTo(10, 5);
        city.Builder.Apply(rail);
        var before = FlatCityFormat.Write(city);

        var random = new Random(7);
        var train = new Train();
        for (var i = 0; i < 5_000; i++)
        {
            train.Step(city.Map, People, random);
        }

        Assert.Equal(before, FlatCityFormat.Write(city));
    }
}
