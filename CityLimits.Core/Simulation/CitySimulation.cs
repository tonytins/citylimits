using CityLimits.Core.Economy;

namespace CityLimits.Core.Simulation;

/// <summary>The city itself: the map, the calendar, the money, and the tick that advances all of them.</summary>
public sealed class CitySimulation
{

    /// <summary>The chance a served zone develops each time the scan reaches it, until demand decides it.</summary>
    private const float DevelopmentChance = 0.2f;

    /// <summary>
    /// Creates a simulation over the given map, with the money the city starts with and the
    /// definitions it runs on: the base game's when none are given.
    /// </summary>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The starting money is negative.</exception>
    public CitySimulation(TileMap map, int seed, int funds = Treasury.StartingFunds, CityDefinitions? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        Map = map;
        Seed = seed;
        Random = new DeterministicRandom(seed);
        Treasury = new Treasury(funds);
        Roads = new RoadNetwork();
        Definitions = definitions ?? CityDefinitions.Base;
        Builder = new CityBuilder(map, Treasury, Roads, Definitions);
    }

    /// <summary>The definitions the city runs on: the base game's, with any mods applied.</summary>
    public CityDefinitions Definitions { get; }

    /// <summary>The map the city sits on.</summary>
    public TileMap Map { get; }

    /// <summary>The seed the city was created with.</summary>
    public int Seed { get; }

    /// <summary>The generator the simulation owns.</summary>
    public DeterministicRandom Random { get; }

    /// <summary>The current month, from the calendar's count.</summary>
    public GameDate Date => CityCalendar.DateAt(CityTime);

    /// <summary>How many 48ths of a year have passed since the city started.</summary>
    public long CityTime { get; private set; }

    /// <summary>The number of steps that have run; each is one phase of the cycle.</summary>
    public long TickCount { get; private set; }

    /// <summary>The money the city has.</summary>
    public Treasury Treasury { get; }

    /// <summary>The roads on the map, and the tiles they serve.</summary>
    public RoadNetwork Roads { get; }

    /// <summary>The builder that does the player's work on the map.</summary>
    public CityBuilder Builder { get; }

    /// <summary>
    /// Runs one step of the simulation: one phase of Micropolis' 16-phase cycle. Phase 0
    /// advances the calendar by a 48th of a year, and phases 1 to 8 scan the map in vertical
    /// bands, one band each. Phases 9 to 15 are where the census and taxes, decay, power,
    /// pollution and land value, crime, population density, and fire and disasters will run.
    /// </summary>
    public void Tick()
    {
        TickCount++;
        var phase = (int)(TickCount % CityCalendar.StepsPerCount);

        if (phase == 0)
        {
            CityTime++;
        }
        else if (phase <= CityCalendar.ScanBands)
        {
            ScanBand(phase - 1);
        }
    }

    // Growth is decided per zone, at its centre, as in Micropolis, when the scan reaches the
    // centre's column; the centre holds the zone's development. The flat chance is a stand-in
    // until demand and population replace it.
    private void ScanBand(int band)
    {
        Roads.EnsureCurrent(Map);
        var (first, end) = CityCalendar.BandColumns(band, Map.Width);

        for (var y = 0; y < Map.Height; y++)
        {
            for (var x = first; x < end; x++)
            {
                var state = Map.StateAt(x, y);
                if (!state.IsZoneCenter || state.Development >= TileState.MaxDevelopment)
                {
                    continue;
                }

                if (!Roads.IsZoneServed(x, y))
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
