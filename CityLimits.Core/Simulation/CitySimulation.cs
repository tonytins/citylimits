using CityLimits.Core.Economy;

namespace CityLimits.Core.Simulation;

/// <summary>The city itself: the map, the calendar, the money, and the tick that advances all of them.</summary>
public sealed class CitySimulation
{

    // The phases of the cycle that do more than scan the map.
    private const int CensusPhase = 9;
    private const int LandValuePhase = 12;
    private const int DensityPhase = 14;

    private readonly ZoneGrowth _growth;

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
        Maps = new CityMaps(map);
        _growth = new ZoneGrowth(map, Random, Trips, Definitions, Demand, Maps);
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

    /// <summary>The census: the population counted as the scan passes each zone, and its monthly history.</summary>
    public CityCensus Census { get; } = new();

    /// <summary>How much the city wants more of each kind of zone.</summary>
    public CityDemand Demand { get; } = new();

    /// <summary>Sends zones' trips out along the roads, reusing one route buffer.</summary>
    public TripPlanner Trips { get; } = new();

    /// <summary>The city's land value, population density, and commercial rate, and its centre of mass.</summary>
    public CityMaps Maps { get; }

    /// <summary>The money the city has.</summary>
    public Treasury Treasury { get; }

    /// <summary>The roads on the map, and the tiles they serve.</summary>
    public RoadNetwork Roads { get; }

    /// <summary>The builder that does the player's work on the map.</summary>
    public CityBuilder Builder { get; }

    /// <summary>
    /// Runs one step of the simulation: one phase of Micropolis' 16-phase cycle. Phase 0
    /// advances the calendar by a 48th of a year, updates demand every second count from the
    /// census the last cycle took, and starts the census again. Phases 1 to 8 scan the map in
    /// vertical bands, one band each, counting each zone and deciding whether it grows or
    /// declines. Phase 9 records the census once a month, phase 12 works out land value, and
    /// phase 14 population density and the city's centre. Phases 10, 11, 13, and 15 are where
    /// decay, power, crime, and fire and disasters will run.
    /// </summary>
    public void Tick()
    {
        TickCount++;
        var phase = (int)(TickCount % CityCalendar.StepsPerCount);

        if (phase == 0)
        {
            CityTime++;
            if (CityTime % 2 == 0)
            {
                Demand.Update(Census);
            }

            Census.Clear();
        }
        else if (phase <= CityCalendar.ScanBands)
        {
            ScanBand(phase - 1);
        }
        else if (phase == CensusPhase && CityTime % CityCalendar.CountsPerMonth == 0)
        {
            Census.RecordMonth();
        }
        else if (phase == LandValuePhase)
        {
            Maps.UpdateLandValue();
        }
        else if (phase == DensityPhase)
        {
            Maps.UpdateDensity(Definitions);
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
                if (!state.IsZoneCenter)
                {
                    continue;
                }

                Census.Add(state.Use, Definitions.Zone(state.Use).PopulationOf(Map, x, y));
                _growth.Visit(x, y);
            }
        }
    }
}
