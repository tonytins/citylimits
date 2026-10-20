namespace CityLimits.Game;

/// <summary>Runs the city with no window, so the simulation can be checked on a machine with no screen.</summary>
public sealed class HeadlessRun
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly int _seed;
    private readonly int _ticks;

    /// <summary>Creates a headless run of the given seed and length.</summary>
    public HeadlessRun(int seed, int ticks)
    {
        _seed = seed;
        _ticks = ticks;
    }

    /// <summary>Runs the city, and writes what happened to the log.</summary>
    public int Run()
    {
        var map = TileMapGenerator.Generate(_seed, GameLoop.MapWidth, GameLoop.MapHeight);
        var simulation = new CitySimulation(map, _seed);

        Logger.Info(
            "Headless run: seed {Seed}, map {Width} by {Height}, {Ticks} ticks",
            _seed,
            map.Width,
            map.Height,
            _ticks);
        Logger.Info("The city starts with ${Funds}", simulation.Treasury.Funds);

        BuildDemoTown(simulation);

        for (var i = 0; i < _ticks; i++)
        {
            simulation.Tick();
            Logger.Info("tick {Tick}: {Date}   ${Funds}", simulation.TickCount, simulation.Date, simulation.Treasury.Funds);
        }

        LogSummary(simulation);
        return 0;
    }

    private static void BuildDemoTown(CitySimulation simulation)
    {
        var map = simulation.Map;
        var spot = FindSpotNearTheMiddle(map);

        if (spot is null)
        {
            Logger.Warn("No room for a demo town on this map");
            return;
        }

        var (x, y) = spot.Value;
        simulation.Builder.Apply(BuildAction.Road, x, y);
        simulation.Builder.Apply(BuildAction.Road, x + 1, y);
        simulation.Builder.Apply(BuildAction.Road, x + 2, y);
        simulation.Builder.Apply(BuildAction.ZoneResidential, x, y - 1);
        simulation.Builder.Apply(BuildAction.ZoneResidential, x + 1, y - 1);
        simulation.Builder.Apply(BuildAction.ZoneCommercial, x + 2, y - 1);

        Logger.Info("Built a road and three zones at {X}, {Y}", x, y);
    }

    private static (int X, int Y)? FindSpotNearTheMiddle(TileMap map)
    {
        (int X, int Y)? best = null;
        var bestDistance = int.MaxValue;
        var middleX = map.Width / 2;
        var middleY = map.Height / 2;

        for (var y = 1; y < map.Height - 1; y++)
        {
            for (var x = 1; x < map.Width - 4; x++)
            {
                if (!CanBuildOn(map, x, y) || !CanBuildOn(map, x + 1, y) || !CanBuildOn(map, x + 2, y))
                {
                    continue;
                }

                if (!CanBuildOn(map, x, y - 1) || !CanBuildOn(map, x + 1, y - 1) || !CanBuildOn(map, x + 2, y - 1))
                {
                    continue;
                }

                var distance = ((x - middleX) * (x - middleX)) + ((y - middleY) * (y - middleY));
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = (x, y);
            }
        }

        return best;
    }

    private static bool CanBuildOn(TileMap map, int x, int y) =>
        map.Contains(x, y) && TileRules.CanBuildOn(map[x, y]);

    private static void LogSummary(CitySimulation simulation)
    {
        var map = simulation.Map;
        var roads = 0;
        var zones = 0;
        var developed = 0;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                if (state.IsRoad)
                {
                    roads++;
                }
                else if (state.IsZoned)
                {
                    zones++;

                    if (state.Development > 0)
                    {
                        developed++;
                    }
                }
            }
        }

        Logger.Info(
            "Headless run finished: {Ticks} ticks, {Date}, {Roads} road tiles, {Zones} zoned tiles, {Developed} developed, ${Funds}",
            simulation.TickCount,
            simulation.Date,
            roads,
            zones,
            developed,
            simulation.Treasury.Funds);
    }
}
