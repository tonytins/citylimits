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

        for (var i = 0; i < _ticks; i++)
        {
            simulation.Tick();
            Logger.Info("tick {Tick}: {Date}", simulation.TickCount, simulation.Date);
        }

        Logger.Info("Headless run finished: {Ticks} ticks, {Date}", simulation.TickCount, simulation.Date);
        return 0;
    }
}
