namespace CityLimits.Core.Simulation;

/// <summary>The city itself: the map, the calendar, and the tick that advances both.</summary>
public sealed class CitySimulation
{
    /// <summary>The year the city starts in.</summary>
    public const int StartYear = 2000;

    /// <summary>The month the city starts in.</summary>
    public const int StartMonth = 1;

    /// <summary>Creates a simulation over the given map.</summary>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    public CitySimulation(TileMap map, int seed)
    {
        ArgumentNullException.ThrowIfNull(map);

        Map = map;
        Seed = seed;
        Random = new DeterministicRandom(seed);
        Date = new GameDate(StartYear, StartMonth);
    }

    /// <summary>The map the city sits on.</summary>
    public TileMap Map { get; }

    /// <summary>The seed the city was created with.</summary>
    public int Seed { get; }

    /// <summary>The generator the simulation owns.</summary>
    public DeterministicRandom Random { get; }

    /// <summary>The current date.</summary>
    public GameDate Date { get; private set; }

    /// <summary>The number of ticks that have run.</summary>
    public long TickCount { get; private set; }

    /// <summary>Advances the city by one tick.</summary>
    public void Tick()
    {
        TickCount++;
        Date = Date.AddMonths(1);
    }
}
