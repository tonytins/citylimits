namespace CityLimits.Game;

/// <summary>Runs the city with no window, so the simulation can be checked on a machine with no screen.</summary>
public sealed class HeadlessRun
{
    /// <summary>The most ticks a headless run takes, so a mistyped count cannot run for hours.</summary>
    public const int MaxTicks = CityLimits.Core.Scenarios.Scenario.MaxTicks;

    /// <summary>How many ticks a headless run takes when it isn't told: a year of the city's time.</summary>
    public const int DefaultTicks = CityCalendar.StepsPerYear;

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
        var map = TileMapGenerator.Generate(_seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
        var simulation = new CitySimulation(map, _seed, definitions: GameMods.Load(out _));

        Logger.Info(
            "Headless run: seed {Seed}, map {Width} by {Height}, {Ticks} ticks",
            _seed,
            map.Width,
            map.Height,
            _ticks);
        Logger.Info("The city starts with ${Funds}", simulation.Treasury.Funds);

        BuildDemoTown(simulation);

        // A month is 64 steps, so the log writes a line when the month changes, not every step.
        var month = simulation.Date;
        for (var i = 0; i < _ticks; i++)
        {
            simulation.Tick();
            if (simulation.Date != month)
            {
                month = simulation.Date;
                Logger.Info("tick {Tick}: {Date}   ${Funds}", simulation.TickCount, simulation.Date, simulation.Treasury.Funds);
            }
        }

        LogSummary(simulation);
        return 0;
    }

    // The demo town is a road nine tiles long with a residential, a commercial, and an industrial
    // zone along its north side, built with strokes as a player would build it.
    private const int TownWidth = 9;
    private const int TownHeight = ZoneLayout.Size + 1;

    private static void BuildDemoTown(CitySimulation simulation)
    {
        var map = simulation.Map;
        var spot = FindSpotNearTheMiddle(map);

        if (spot is null)
        {
            Logger.Warn("No room for a demo town on this map");
            return;
        }

        var (left, top) = spot.Value;
        var roadY = top + ZoneLayout.Size;

        var road = new BuildStroke(BuildAction.Road, left, roadY);
        road.DragTo(left + TownWidth - 1, roadY);
        simulation.Builder.Apply(road);
        simulation.Builder.Apply(BuildAction.ZoneResidential, left + 1, top + 1);
        simulation.Builder.Apply(BuildAction.ZoneCommercial, left + 4, top + 1);
        simulation.Builder.Apply(BuildAction.ZoneIndustrial, left + 7, top + 1);

        Logger.Info("Built a road and three zones at {X}, {Y}", left, top);
    }

    // The buildable area closest to the middle that holds the whole town.
    private static (int X, int Y)? FindSpotNearTheMiddle(TileMap map)
    {
        (int X, int Y)? best = null;
        var bestDistance = int.MaxValue;
        var middleX = map.Width / 2;
        var middleY = map.Height / 2;

        for (var y = 0; y + TownHeight <= map.Height; y++)
        {
            for (var x = 0; x + TownWidth <= map.Width; x++)
            {
                var distance = ((x - middleX) * (x - middleX)) + ((y - middleY) * (y - middleY));
                if (distance >= bestDistance || !CanBuildAll(map, x, y))
                {
                    continue;
                }

                bestDistance = distance;
                best = (x, y);
            }
        }

        return best;
    }

    private static bool CanBuildAll(TileMap map, int left, int top)
    {
        for (var y = top; y < top + TownHeight; y++)
        {
            for (var x = left; x < left + TownWidth; x++)
            {
                if (!TileRules.CanBuildOn(map[x, y]))
                {
                    return false;
                }
            }
        }

        return true;
    }

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
                else if (state.IsZoneCenter)
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
            "Headless run finished: {Ticks} ticks, {Date}, {Roads} road tiles, {Zones} zones, {Developed} developed, ${Funds}",
            simulation.TickCount,
            simulation.Date,
            roads,
            zones,
            developed,
            simulation.Treasury.Funds);
    }
}
