using CityLimits.Core.Economy;

namespace CityLimits.Core.Simulation;

/// <summary>The city itself: the map, the calendar, the money, and the tick that advances all of them.</summary>
public sealed class CitySimulation
{
    /// <summary>The year the city starts in.</summary>
    public const int StartYear = 2000;

    /// <summary>The month the city starts in.</summary>
    public const int StartMonth = 1;

    /// <summary>The chance a served zone develops in a tick.</summary>
    private const float DevelopmentChance = 0.2f;

    /// <summary>Creates a simulation over the given map.</summary>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    public CitySimulation(TileMap map, int seed)
    {
        ArgumentNullException.ThrowIfNull(map);

        Map = map;
        Seed = seed;
        Random = new DeterministicRandom(seed);
        Date = new GameDate(StartYear, StartMonth);
        Treasury = new Treasury();
        Roads = new RoadNetwork();
        Builder = new CityBuilder(map, Treasury, Roads);
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

    /// <summary>The money the city has.</summary>
    public Treasury Treasury { get; }

    /// <summary>The roads on the map, and the tiles they serve.</summary>
    public RoadNetwork Roads { get; }

    /// <summary>The builder that does the player's work on the map.</summary>
    public CityBuilder Builder { get; }

    /// <summary>Advances the city by one tick.</summary>
    public void Tick()
    {
        TickCount++;
        Date = Date.AddMonths(1);
        Grow();
    }

    private void Grow()
    {
        Roads.EnsureCurrent(Map);

        for (var y = 0; y < Map.Height; y++)
        {
            for (var x = 0; x < Map.Width; x++)
            {
                var state = Map.StateAt(x, y);
                if (!state.IsZoned || state.Development >= TileState.MaxDevelopment)
                {
                    continue;
                }

                if (!Roads.IsServed(x, y))
                {
                    continue;
                }

                if (Random.NextFloat() >= DevelopmentChance)
                {
                    continue;
                }

                Map.SetState(x, y, new TileState(state.Kind, state.Use, state.Development + 1));
            }
        }
    }
}
