using System.Globalization;
using CityLimits.Core.Economy;

namespace CityLimits.Core.Simulation;

/// <summary>The city itself: the map, the calendar, the money, and the tick that advances all of them.</summary>
public sealed class CitySimulation
{
    /// <summary>The name a city has until the player gives it one.</summary>
    public const string DefaultName = "CityLimits";

    /// <summary>The longest name a city can have, in characters.</summary>
    public const int MaxNameLength = 32;

    // The phases of the cycle that do more than scan the map.
    private const int CensusPhase = 9;
    private const int MessagePhase = 10;
    private const int PowerPhase = 11;
    private const int LandValuePhase = 12;
    private const int PolicePhase = 13;
    private const int DensityPhase = 14;
    private const int FirePhase = 15;

    // The messages are checked at set counts in a 64-count round, as in Micropolis.
    private const int MessageRound = 64;
    private const int NeedRailCount = 18;
    private const int NeedPowerCount = 22;
    private const int NeedStadiumCount = 26;
    private const int NeedSeaportCount = 28;
    private const int NeedAirportCount = 30;
    private const int BlackoutsCount = 32;
    private const int HighPollutionCount = 35;
    private const int HighCrimeCount = 42;
    private const int NeedFireStationCount = 45;
    private const int NeedPoliceStationCount = 48;
    private const int HighTaxesCount = 51;
    private const int RoadsNeedFundingCount = 54;
    private const int FireNeedsFundingCount = 57;
    private const int PoliceNeedsFundingCount = 60;
    private const int TrafficJamsCount = 63;

    // A city of more than this many people needs a fire and a police station, and minds when they
    // are funded below this effect.
    private const int PeopleBeforeStationsAreNeeded = 60;
    private const int PeopleBeforeStationFundingMatters = 20;
    private const int ServiceEffectNeedingFunding = 700;

    // Taxes above this are too high; roads funded below this, with more of them than this, need funding.
    private const int HighestFairTax = 12;
    private const int RoadEffectNeedingFunding = 20;
    private const int RoadsBeforeFundingMatters = 30;

    // Roads funded below this crumble now and then: a road tile has a 1 in 512 chance a cycle to be
    // weighed, and crumbles if a roll from 0 to 31 is above its funding.
    private const int RoadEffectBeforeDecay = 30;
    private const int DecayChance = 512;

    // A bridge counts as five road tiles toward the roads' upkeep.
    private const int BridgeRoadTiles = 5;

    // Micropolis counts each tile of rail as two of road toward the roads' upkeep, and asks for
    // more rail once a city has more than 50 zones and fewer rail tiles than zones.
    private const int RoadTilesPerRail = 2;
    private const int ZonesBeforeRailIsNeeded = 50;

    // A city needs power once it has more zones than this, and has blackouts below this share powered.
    private const int ZonesBeforePowerIsNeeded = 10;
    private const double PoweredShareBeforeBlackouts = 0.7;

    // Micropolis reports traffic jams when the year's traffic average is above this.
    private const int MostTrafficBeforeJams = 60;

    // As Micropolis on Easy: once a cycle a disaster comes one time in 481, and is one of nine kinds:
    // two a fire, two a flood, one an earthquake, and the rest a tornado, a monster, or nothing,
    // which CityLimits doesn't have, so they pass by.
    private const int DisasterChance = 481;
    private const int DisasterKinds = 9;
    private const int FireKinds = 2;
    private const int FloodKinds = 2;
    private const int EarthquakeKind = 6;

    // A flood looks this many times for a shore with land beside it to rise onto; a flooded tile
    // spreads to each side one time in eight while the flood lasts, and drains one time in sixteen after.
    private const int FloodSearches = 300;
    private const int FloodSpreadChance = 8;
    private const int FloodDrainChance = 16;

    // An earthquake shakes 300 to 1 000 tiles, and wrecks an outer tile of a building it shakes, to
    // rubble three times in four and to fire otherwise.
    private const int FewestQuakeTiles = 300;
    private const int QuakeTileRange = 701;
    private const int QuakeFireChance = 4;

    // A fire started on purpose, as Micropolis' Disasters menu starts one, takes up to 40 tries to
    // find something that can burn and isn't a building's centre.
    private const int FireSearches = 40;

    // Plants, stations, and airports redraw their bare holes every 8 counts; hospitals, churches,
    // stadiums, and seaports every 16, as in Micropolis.
    private const int BuildingRepairCounts = 8;
    private const int CommunityRepairCounts = 16;

    // A burning tile does anything at all one visit in four, spreads to each side one time in eight,
    // and burns out sooner the better the fire stations cover it, as in Micropolis.
    private const int FireActsChance = 4;
    private const int FireSpreadChance = 8;
    private const int StrongFireCover = 100;
    private const int SomeFireCover = 20;

    // An explosion sets fire to the four tiles at the corners of its centre.
    private static readonly (int Dx, int Dy)[] ExplosionCorners = [(-1, -1), (1, -1), (-1, 1), (1, 1)];

    // Micropolis reports high pollution and crime when their averages are above these.
    private const int MostPollutionBeforeComplaints = 60;
    private const int MostCrimeBeforeComplaints = 100;

    private readonly ZoneGrowth _growth;

    /// <summary>
    /// Creates a simulation over the given map, with the money the city starts with, the definitions
    /// it runs on (the base game's when none are given), and the year its calendar starts in, January
    /// 1900 unless another is given. A city that starts later starts as if it had run every step
    /// since 1900, so its calendar's count is always its steps divided by 16, as every save holds.
    /// </summary>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The starting money is negative, or the year is before 1900 or after 9999.</exception>
    public CitySimulation(TileMap map, int seed, int funds = Treasury.StartingFunds, CityDefinitions? definitions = null, int startYear = CityCalendar.StartYear)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(startYear, CityCalendar.StartYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startYear, CityCalendar.LatestStartYear);
        TickCount = (long)(startYear - CityCalendar.StartYear) * CityCalendar.StepsPerYear;
        CityTime = TickCount / CityCalendar.StepsPerCount;
        FoundedYear = startYear;

        Map = map;
        Seed = seed;
        Random = new DeterministicRandom(seed);
        Treasury = new Treasury(funds);
        Roads = new RoadNetwork();
        Definitions = definitions ?? CityDefinitions.Base;
        Hazards = new Hazards(map);
        Builder = new CityBuilder(map, Treasury, Roads, Definitions, Hazards);
        Maps = new CityMaps(map);
        Services = new ServiceCoverage(map);
        Traffic = new TrafficMap(map);
        Trips = new TripPlanner(Traffic);
        Budget = new CityBudget(Definitions.Service(TileUse.FireStation).Upkeep, Definitions.Service(TileUse.PoliceStation).Upkeep);
        _growth = new ZoneGrowth(map, Random, Trips, Definitions, Demand, Maps, Community, Hazards);
    }

    /// <summary>
    /// Tells whether a name can be a city's: once trimmed, it isn't empty, it is no longer than
    /// <see cref="MaxNameLength"/>, and it has no control characters.
    /// </summary>
    public static bool IsValidName(string? name)
    {
        var trimmed = name?.Trim();
        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaxNameLength && !trimmed.Any(char.IsControl);
    }

    /// <summary>Gives the city a new name, trimmed of the spaces around it.</summary>
    /// <exception cref="ArgumentException">The name isn't one a city can have (<see cref="IsValidName"/>).</exception>
    public void Rename(string name)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException($"A city's name needs 1 to {MaxNameLength} characters and no control characters.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>Raised when the city has something to tell the player, as often as Micropolis would say it.</summary>
    public event Action<CityMessage>? MessageSent;

    /// <summary>Raised when the city settles a year's taxes and upkeep, with what it came to.</summary>
    public event Action<BudgetYear>? YearSettled;

    /// <summary>Raised when a burning building explodes, with where, so the game can show it; the fires it set are already burning.</summary>
    public event Action<int, int>? Exploded;

    /// <summary>Raised when an earthquake strikes, after its damage is done, so the game can shake the view.</summary>
    public event Action? EarthquakeStruck;

    /// <summary>The fires and floods on the map, and the holes they leave in buildings.</summary>
    public Hazards Hazards { get; }

    /// <summary>The fires burning on the map.</summary>
    public Fires Fires => Hazards.Fires;

    /// <summary>The floods on the map.</summary>
    public Floods Floods => Hazards.Floods;

    /// <summary>
    /// Whether fires start on their own, as Micropolis' disasters start them. The player's settings
    /// decide it in the game, and a scenario's file in a scenario; it isn't saved with the city. A
    /// fire already burning spreads either way, and an earthquake's fires are its own. When a
    /// disaster is off, its dice are rolled as if it had found nothing to harm, so in a city it
    /// wouldn't have harmed, turning it off changes nothing else the city does.
    /// </summary>
    public bool RandomFires { get; set; } = true;

    /// <summary>Whether floods rise on their own, as <see cref="RandomFires"/> says for fires. A flood already rising goes on either way.</summary>
    public bool RandomFloods { get; set; } = true;

    /// <summary>Whether earthquakes strike, as <see cref="RandomFires"/> says for fires.</summary>
    public bool RandomEarthquakes { get; set; } = true;

    /// <summary>
    /// The scenario being played in the city, if it is a challenge: it makes what is due, and judges
    /// its goals, at the top of each step, and it is saved with the city.
    /// </summary>
    public Scenarios.Challenge? Challenge { get; set; }

    /// <summary>The definitions the city runs on: the base game's, with any mods applied.</summary>
    public CityDefinitions Definitions { get; }

    /// <summary>The map the city sits on.</summary>
    public TileMap Map { get; }

    /// <summary>The seed the city was created with.</summary>
    public int Seed { get; }

    /// <summary>
    /// The parts of a newer save the city was opened from that this version doesn't know, to write
    /// back when it is saved in the same format; null for a city that came from no such save.
    /// </summary>
    internal KeptParts? Kept { get; set; }

    /// <summary>The city's name; <see cref="DefaultName"/> until it is renamed.</summary>
    public string Name { get; private set; } = DefaultName;

    /// <summary>The generator the simulation owns.</summary>
    public DeterministicRandom Random { get; }

    /// <summary>The current month, from the calendar's count.</summary>
    public GameDate Date => CityCalendar.DateAt(CityTime);

    /// <summary>The year the city was founded, which its age is counted from: its calendar's first year.</summary>
    public int FoundedYear { get; private set; }

    /// <summary>Tells whether the city is at the moment it was founded, not yet having taken a step.</summary>
    public bool IsAtFounding => TickCount == (long)(FoundedYear - CityCalendar.StartYear) * CityCalendar.StepsPerYear;

    /// <summary>
    /// Moves the city's founding to another year, as a scenario's editor does when its start year
    /// changes: the calendar moves by whole years, and the map and everything else stay as they are,
    /// so the city is as one founded in that year would be. Only a city at its founding can move.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The year is before 1900 or after 9999.</exception>
    /// <exception cref="InvalidOperationException">The city has taken a step since it was founded.</exception>
    public void MoveFounding(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, CityCalendar.StartYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, CityCalendar.LatestStartYear);
        if (!IsAtFounding)
        {
            throw new InvalidOperationException("Only a city at its founding can move to another year.");
        }

        TickCount = (long)(year - CityCalendar.StartYear) * CityCalendar.StepsPerYear;
        CityTime = TickCount / CityCalendar.StepsPerCount;
        FoundedYear = year;
    }

    /// <summary>How many 48ths of a year have passed since the city started.</summary>
    public long CityTime { get; private set; }

    /// <summary>The number of steps that have run; each is one phase of the cycle.</summary>
    public long TickCount { get; private set; }

    /// <summary>The census: the population counted as the scan passes each zone, and its monthly history.</summary>
    public CityCensus Census { get; } = new();

    /// <summary>How much the city wants more of each kind of zone.</summary>
    public CityDemand Demand { get; } = new();

    /// <summary>Sends zones' trips out along the roads, reusing one route buffer.</summary>
    public TripPlanner Trips { get; }

    /// <summary>How busy the roads are: the traffic arriving trips leave, fading each cycle.</summary>
    public TrafficMap Traffic { get; }

    /// <summary>The city's land value, population density, and commercial rate, and its centre of mass.</summary>
    public CityMaps Maps { get; }

    /// <summary>Which tiles have power, and the plants and zones the scan counted.</summary>
    public PowerGrid Power { get; } = new();

    /// <summary>The money the city has.</summary>
    public Treasury Treasury { get; }

    /// <summary>The city's tax rate and funding, and the year's taxes and upkeep.</summary>
    public CityBudget Budget { get; }

    /// <summary>The city's hospitals and churches, and how many it needs.</summary>
    public CommunityBuildings Community { get; } = new();

    /// <summary>How well fire and police stations cover the city, and how many there are.</summary>
    public ServiceCoverage Services { get; }

    /// <summary>The city's stadiums, seaports, and airports, and the demand held back for want of one.</summary>
    public Facilities Facilities { get; } = new();

    /// <summary>
    /// The road tiles the scan counted this cycle, as the roads' upkeep counts them: a bridge counts
    /// five, and a road showing heavy traffic one more.
    /// </summary>
    public int RoadTiles { get; private set; }

    /// <summary>The rail tiles the scan counted this cycle, tunnels and crossings included; each counts as two road tiles toward the roads' upkeep.</summary>
    public int RailTiles { get; private set; }

    // The road tiles, rail tiles, and stations as of the last count's budget, which a fresh estimate works from.
    private int _budgetedRoadTiles;
    private int _budgetedRailTiles;
    private int _budgetedFireStations;
    private int _budgetedPoliceStations;

    /// <summary>The roads on the map, and the tiles they serve.</summary>
    public RoadNetwork Roads { get; }

    /// <summary>The builder that does the player's work on the map.</summary>
    public CityBuilder Builder { get; }

    /// <summary>
    /// Runs one step of the simulation: one phase of Micropolis' 16-phase cycle. Phase 0
    /// advances the calendar by a 48th of a year, updates demand every second count from the
    /// census the last cycle took, and starts the census again. Phases 1 to 8 scan the map in
    /// vertical bands, one band each, counting each zone and deciding whether it grows or
    /// declines, and counting each road tile. Phase 9 records the census once a month and sets a
    /// 48th of the year's taxes and upkeep aside, settling the year when it comes round; phase 10 fades the traffic and checks the city's messages,
    /// phase 11 traces power from the plants, phase 12 works out land value and pollution, phase 13
    /// spreads the police stations' coverage and works out crime, and phase 14 population density
    /// and the city's centre. Phase 15 spreads the fire stations' coverage and rolls for a disaster.
    /// The scan also visits each burning tile, which may spread its fire or burn out.
    /// </summary>
    public void Tick()
    {
        Challenge?.BeforeStep(this);
        TickCount++;
        var phase = (int)(TickCount % CityCalendar.StepsPerCount);

        if (phase == 0)
        {
            CityTime++;
            if (CityTime % 2 == 0)
            {
                Demand.Update(Census, Budget.TaxEffect, Facilities.Caps);
            }

            Census.Clear();
            Power.StartCount();
            Services.StartCount();
            Facilities.StartCount();
            Community.StartCount();
            RoadTiles = 0;
            RailTiles = 0;
        }
        else if (phase <= CityCalendar.ScanBands)
        {
            ScanBand(phase - 1);
        }
        else if (phase == CensusPhase)
        {
            if (CityTime % CityCalendar.CountsPerMonth == 0)
            {
                Census.RecordMonth();
                Community.UpdateNeeds(Census.Residential);
            }

            CollectTaxes();
        }
        else if (phase == MessagePhase)
        {
            // As in Micropolis, the traffic fades before the city has its say.
            Traffic.Fade();
            SendMessages();
        }
        else if (phase == PowerPhase)
        {
            Power.Trace(Map, Definitions);
            if (Power.IsBrownedOut)
            {
                MessageSent?.Invoke(CityMessage.Brownouts);
            }
        }
        else if (phase == LandValuePhase)
        {
            Maps.UpdateLandValue(Traffic, Fires);
        }
        else if (phase == PolicePhase)
        {
            Services.SpreadPolice();
            Maps.UpdateCrime(Services);
        }
        else if (phase == FirePhase)
        {
            Services.SpreadFire();

            // As in Micropolis, the flood's time runs down before the dice are rolled.
            Floods.Recede();
            RollForDisaster();
        }
        else if (phase == DensityPhase)
        {
            Maps.UpdateDensity(Definitions);
        }
    }

    // As in Micropolis' fire: one visit in four, the fire may spread to each side, one time in eight,
    // and then may burn out: one time in two where fire stations cover it well, in three where
    // they cover it some, in four where they barely do, and in eleven where they don't.
    private void VisitFire(int x, int y)
    {
        if (Random.NextInt(0, FireActsChance) != 0)
        {
            return;
        }

        foreach (var (_, dx, dy) in Sides.All)
        {
            if (Random.NextInt(0, FireSpreadChance) == 0 && Fires.CanBurn(x + dx, y + dy))
            {
                Ignite(x + dx, y + dy);
            }
        }

        var cover = Services.FireCoverageAt(x, y);
        var odds = cover > StrongFireCover ? 1 : cover > SomeFireCover ? 2 : cover != 0 ? 3 : 10;
        if (Random.NextInt(0, odds + 1) == 0)
        {
            Fires.PutOut(x, y);
        }
    }

    // A building whose centre catches may explode: as in Micropolis, industry with a building,
    // power plants, fire and police stations, stadiums, seaports, and airports do.
    private void Ignite(int x, int y)
    {
        if (Fires.Ignite(x, y) is not { } building)
        {
            return;
        }

        var explodes = building.Use switch
        {
            TileUse.Industrial => building.Development > 0,
            TileUse.CoalPowerPlant or TileUse.NuclearPowerPlant or TileUse.FireStation or TileUse.PoliceStation
                or TileUse.Stadium or TileUse.Seaport or TileUse.Airport => true,
            _ => false,
        };
        if (explodes)
        {
            Explode(x, y);
        }
    }

    // The explosion sets fire to the tiles at the centre's corners that can burn, or are bare
    // ground, but never to another building's centre. It does so at once, as Tony Bark chose;
    // Micropolis waits for its drawing of the explosion to finish.
    private void Explode(int x, int y)
    {
        foreach (var (dx, dy) in ExplosionCorners)
        {
            var (cx, cy) = (x + dx, y + dy);
            var state = Map.StateAtOrDefault(cx, cy, new TileState(TileKind.Water));
            var bare = state.Use == TileUse.None && state.Kind == TileKind.Grass && !Floods.IsFlooded(cx, cy);
            if (!state.IsBuildingCenter && (bare || Fires.CanBurn(cx, cy)))
            {
                Fires.Ignite(cx, cy);
            }
        }

        MessageSent?.Invoke(CityMessage.ExplosionReported);
        Exploded?.Invoke(x, y);
    }

    // As Micropolis does at the end of each cycle: a disaster one time in 481, and then which kind.
    // A kind that is off rolls its dice as if it found nothing to harm, so the switches never change
    // the dice anything else rolls in a city the disaster would have left alone.
    private void RollForDisaster()
    {
        if (Random.NextInt(0, DisasterChance) != 0)
        {
            return;
        }

        var kind = Random.NextInt(0, DisasterKinds);
        if (kind < FireKinds)
        {
            var (x, y) = (Random.NextInt(0, Map.Width), Random.NextInt(0, Map.Height));
            if (RandomFires && IsArsonable(x, y))
            {
                Ignite(x, y);
                MessageSent?.Invoke(CityMessage.FireReported);
            }
        }
        else if (kind < FireKinds + FloodKinds)
        {
            StartFlood(RandomFloods);
        }
        else if (kind == EarthquakeKind)
        {
            Quake(RandomEarthquakes);
        }
    }

    /// <summary>
    /// Strikes the city with a disaster now, as a scenario's event does, whatever the switches for
    /// disasters on their own say: a fire on something that can burn, as Micropolis' Disasters menu
    /// starts one, a flood from a shore, or an earthquake. Each is reported as one on its own is.
    /// </summary>
    /// <returns>Whether it found anywhere to strike: a fire something to burn, a flood a shore with land beside it.</returns>
    public bool Strike(Disaster disaster) => disaster switch
    {
        Disaster.Fire => StartFireOnPurpose(),
        Disaster.Flood => StartFlood(allowed: true),
        Disaster.Earthquake => Quake(allowed: true),
        _ => throw new ArgumentOutOfRangeException(nameof(disaster), disaster, null),
    };

    private bool StartFireOnPurpose()
    {
        for (var tries = 0; tries < FireSearches; tries++)
        {
            var (x, y) = (Random.NextInt(0, Map.Width), Random.NextInt(0, Map.Height));
            if (!Map.StateAt(x, y).IsBuildingCenter && Fires.CanBurn(x, y))
            {
                Ignite(x, y);
                MessageSent?.Invoke(CityMessage.FireReported);
                return true;
            }
        }

        return false;
    }

    // As Micropolis does: up to 300 tries at a shore, a stretch of water with land beside it, and
    // the first side of it with land the water can rise onto, if it is bare ground, trees, or a
    // road, rail, a line, or a park; a flood can't start on a building or rubble, but spreads onto them.
    private bool StartFlood(bool allowed)
    {
        for (var tries = 0; tries < FloodSearches; tries++)
        {
            var (x, y) = (Random.NextInt(0, Map.Width), Random.NextInt(0, Map.Height));
            if (!IsShore(x, y))
            {
                continue;
            }

            foreach (var (_, dx, dy) in Sides.All)
            {
                var (landX, landY) = (x + dx, y + dy);
                if (!CanFloodStart(landX, landY))
                {
                    continue;
                }

                if (allowed)
                {
                    Floods.Flood(landX, landY);
                    Floods.Rise();
                    MessageSent?.Invoke(CityMessage.FloodReported);
                }

                return true;
            }
        }

        return false;
    }

    private bool IsShore(int x, int y)
    {
        if (Map.StateAt(x, y) is not { Kind: TileKind.Water, Use: TileUse.None })
        {
            return false;
        }

        foreach (var (_, dx, dy) in Sides.All)
        {
            if (Map.Contains(x + dx, y + dy) && Map.StateAt(x + dx, y + dy).Kind != TileKind.Water)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanFloodStart(int x, int y)
    {
        if (!Floods.CanFlood(x, y))
        {
            return false;
        }

        var state = Map.StateAt(x, y);
        return state.Use == TileUse.None
            ? state.Kind is TileKind.Grass or TileKind.Trees
            : !state.HasFootprint;
    }

    // As Micropolis does: 300 to 1 000 tiles shaken at random, and each outer tile of a building
    // among them wrecked, to rubble or to fire. With earthquakes off, the same tiles are picked,
    // and nothing happens to them. The fires are the earthquake's, whatever the fire switch says.
    private bool Quake(bool allowed)
    {
        var shaken = Random.NextInt(0, QuakeTileRange) + FewestQuakeTiles;
        for (var i = 0; i < shaken; i++)
        {
            var (x, y) = (Random.NextInt(0, Map.Width), Random.NextInt(0, Map.Height));
            var state = Map.StateAt(x, y);
            if (!state.HasFootprint || state.IsBuildingCenter)
            {
                continue;
            }

            var burns = Random.NextInt(0, QuakeFireChance) == 0;
            if (!allowed)
            {
                continue;
            }

            if (burns)
            {
                Fires.Ignite(x, y);
            }
            else
            {
                Map.SetState(x, y, new TileState(TileKind.Rubble));
            }
        }

        if (allowed)
        {
            MessageSent?.Invoke(CityMessage.EarthquakeReported);
            EarthquakeStruck?.Invoke();
        }

        return true;
    }

    // As in Micropolis' flood: while it lasts, a flooded tile spreads to each side one time in
    // eight; after, it drains one time in sixteen, leaving bare ground.
    private void VisitFlood(int x, int y)
    {
        if (Floods.CyclesLeft > 0)
        {
            foreach (var (_, dx, dy) in Sides.All)
            {
                if (Random.NextInt(0, FloodSpreadChance) == 0)
                {
                    Floods.Flood(x + dx, y + dy);
                }
            }

            return;
        }

        if (Random.NextInt(0, FloodDrainChance) == 0)
        {
            Floods.Drain(x, y);
        }
    }

    // A fire starts only on an outer tile of a building with something on it, as Micropolis' does:
    // a house, a zone's building or a shop's or a factory's lot, a plant, a station, a hospital, or a church.
    private bool IsArsonable(int x, int y)
    {
        var state = Map.StateAt(x, y);
        if (!state.HasFootprint || state.IsBuildingCenter)
        {
            return false;
        }

        return state.Use != TileUse.Residential || state.HasHouse || Map.ZoneStateAt(x, y).Development > 0;
    }

    // As in Micropolis, a 48th of the year's budget is set aside each count, and the year is
    // settled when the count comes round to a whole year, the first time at the very start.
    private void CollectTaxes()
    {
        (_budgetedRoadTiles, _budgetedRailTiles, _budgetedFireStations, _budgetedPoliceStations) = (RoadTiles, RailTiles, Services.FireStations, Services.PoliceStations);
        Budget.Accrue(EstimateBudget());
        if (CityTime % CityBudget.CountsPerYear == 0)
        {
            var year = Budget.Settle(Treasury);

            // Micropolis' yearly evaluation works the traffic average out after the taxes, and
            // only for a city with people in it; an empty one keeps the last.
            if (Demand.TaxablePopulation != 0)
            {
                Traffic.UpdateAverage(Maps);
            }

            YearSettled?.Invoke(year);
        }
    }

    /// <summary>
    /// The year's budget as it would be estimated now, at the tax rate and funding set now, from
    /// the city as the last count saw it. The budget panel shows it as the player changes them.
    /// </summary>
    public BudgetYear EstimateBudget() =>
        Budget.Estimate(Demand.TaxablePopulation, Maps.LandValueAverage, _budgetedRoadTiles + (_budgetedRailTiles * RoadTilesPerRail), _budgetedFireStations, _budgetedPoliceStations, Treasury.Funds);

    private void SendMessages()
    {
        var count = CityTime % MessageRound;
        if (count == NeedRailCount && Power.Zones > ZonesBeforeRailIsNeeded && Power.Zones > RailTiles)
        {
            MessageSent?.Invoke(CityMessage.NeedRail);
        }

        if (count == NeedPowerCount && Power.Zones > ZonesBeforePowerIsNeeded && Power.CoalPlants + Power.NuclearPlants == 0)
        {
            MessageSent?.Invoke(CityMessage.NeedPower);
        }

        if (count == NeedStadiumCount && Facilities.CheckStadium(Census))
        {
            MessageSent?.Invoke(CityMessage.NeedStadium);
        }

        if (count == NeedSeaportCount && Facilities.CheckSeaport(Census))
        {
            MessageSent?.Invoke(CityMessage.NeedSeaport);
        }

        if (count == NeedAirportCount && Facilities.CheckAirport(Census))
        {
            MessageSent?.Invoke(CityMessage.NeedAirport);
        }

        var buildings = Power.PoweredBuildings + Power.UnpoweredBuildings;
        if (count == BlackoutsCount && buildings > 0 && (double)Power.PoweredBuildings / buildings < PoweredShareBeforeBlackouts)
        {
            MessageSent?.Invoke(CityMessage.Blackouts);
        }

        if (count == HighPollutionCount && Maps.PollutionAverage > MostPollutionBeforeComplaints)
        {
            MessageSent?.Invoke(CityMessage.HighPollution);
        }

        if (count == HighCrimeCount && Maps.CrimeAverage > MostCrimeBeforeComplaints)
        {
            MessageSent?.Invoke(CityMessage.HighCrime);
        }

        if (count == HighTaxesCount && Budget.TaxRate > HighestFairTax)
        {
            MessageSent?.Invoke(CityMessage.HighTaxes);
        }

        if (count == RoadsNeedFundingCount && Budget.RoadEffect < RoadEffectNeedingFunding && RoadTiles > RoadsBeforeFundingMatters)
        {
            MessageSent?.Invoke(CityMessage.RoadsNeedFunding);
        }

        var people = Demand.TaxablePopulation;
        if (count == NeedFireStationCount && people > PeopleBeforeStationsAreNeeded && Services.FireStations == 0)
        {
            MessageSent?.Invoke(CityMessage.NeedFireStation);
        }

        if (count == NeedPoliceStationCount && people > PeopleBeforeStationsAreNeeded && Services.PoliceStations == 0)
        {
            MessageSent?.Invoke(CityMessage.NeedPoliceStation);
        }

        if (count == FireNeedsFundingCount && Budget.FireEffect < ServiceEffectNeedingFunding && people > PeopleBeforeStationFundingMatters)
        {
            MessageSent?.Invoke(CityMessage.FireNeedsFunding);
        }

        if (count == PoliceNeedsFundingCount && Budget.PoliceEffect < ServiceEffectNeedingFunding && people > PeopleBeforeStationFundingMatters)
        {
            MessageSent?.Invoke(CityMessage.PoliceNeedsFunding);
        }

        if (count == TrafficJamsCount && Traffic.Average > MostTrafficBeforeJams)
        {
            MessageSent?.Invoke(CityMessage.TrafficJams);
        }
    }

    // The scan reaches each zone and plant at its centre, as in Micropolis: it counts plants for
    // the power trace, and gives each zone its power, then its census and its chance to grow.
    private void ScanBand(int band)
    {
        Roads.EnsureCurrent(Map);
        var (first, end) = CityCalendar.BandColumns(band, Map.Width);

        for (var y = 0; y < Map.Height; y++)
        {
            for (var x = first; x < end; x++)
            {
                if (Fires.IsBurning(x, y))
                {
                    VisitFire(x, y);
                    continue;
                }

                if (Floods.IsFlooded(x, y))
                {
                    VisitFlood(x, y);
                    continue;
                }

                var state = Map.StateAt(x, y);
                if (state.IsRoad)
                {
                    VisitRoad(x, y, state);
                    continue;
                }

                if (state.IsRail)
                {
                    VisitRail(x, y, state);
                    continue;
                }

                if (!state.IsBuildingCenter)
                {
                    continue;
                }

                var powered = Power.Visit(Map, x, y);
                if (TileRules.IsCommunityBuilding(state.Use))
                {
                    RepairNowAndThen(x, y, CommunityRepairCounts);
                    Community.Visit(Map, Random, x, y, Hazards);
                    continue;
                }

                if (TileRules.IsPowerPlant(state.Use))
                {
                    RepairNowAndThen(x, y, BuildingRepairCounts);
                    continue;
                }

                if (TileRules.IsFacility(state.Use))
                {
                    RepairNowAndThen(x, y, state.Use == TileUse.Airport ? BuildingRepairCounts : CommunityRepairCounts);
                    Facilities.Count(state.Use);
                    continue;
                }

                if (TileRules.IsService(state.Use))
                {
                    RepairNowAndThen(x, y, BuildingRepairCounts);
                    var effect = state.Use == TileUse.FireStation ? Budget.FireEffect : Budget.PoliceEffect;
                    Services.AddStation(x, y, state.Use, effect, powered, Roads.IsZoneServed(x, y));
                    continue;
                }

                if (!state.IsZoneCenter)
                {
                    continue;
                }

                Census.Add(state.Use, Definitions.Zone(state.Use).PopulationOf(Map, x, y));
                _growth.Visit(x, y);
            }
        }
    }

    // As in Micropolis, the scan counts each road tile for the roads' upkeep, and when the roads
    // are underfunded a tile now and then crumbles: to rubble on land, back to water as a bridge.
    // A road crossing a power line never crumbles. A road that stands shows the traffic its block
    // has now, and one that showed heavy traffic counts again.
    private void VisitRoad(int x, int y, TileState state)
    {
        RoadTiles++;
        if (Budget.RoadEffect < RoadEffectBeforeDecay
            && Random.NextInt(0, DecayChance) == 0
            && !state.HasPowerLine
            && Budget.RoadEffect < Random.NextInt(0, CityBudget.FullRoadEffect))
        {
            Map.SetState(x, y, new TileState(state.IsBridge ? TileKind.Water : TileKind.Rubble));
            Roads.MarkDirty();
            return;
        }

        if (state.IsBridge)
        {
            RoadTiles += BridgeRoadTiles - 1;
        }

        if (Traffic.Show(x, y) == TrafficLevel.Heavy)
        {
            RoadTiles++;
        }
    }

    // As in Micropolis, the scan counts each rail tile, and underfunded rail crumbles as roads
    // do: to rubble on land, back to water in a tunnel, never where it crosses a power line.
    // Micropolis also rolls here for a train to set out; CityLimits' train is drawn by the game
    // alone, so the city's own rolls stay the city's.
    private void VisitRail(int x, int y, TileState state)
    {
        RailTiles++;
        if (Budget.RoadEffect < RoadEffectBeforeDecay
            && Random.NextInt(0, DecayChance) == 0
            && !state.HasPowerLine
            && Budget.RoadEffect < Random.NextInt(0, CityBudget.FullRoadEffect))
        {
            Map.SetState(x, y, new TileState(state.IsTunnel ? TileKind.Water : TileKind.Rubble));
            Roads.MarkDirty();
        }
    }

    /// <summary>Captures everything the city needs to carry on exactly where it is, for a save.</summary>
    internal CitySave Capture() => new()
    {
        Format = SaveUpgrades.Current,
        Name = Name,
        Seed = Seed,
        Random = Random.State.ToString("x16", CultureInfo.InvariantCulture),
        Tick = TickCount,
        CityTime = CityTime,
        Funds = Treasury.Funds,
        Definitions = DefinitionWriter.Write(Definitions),
        Mods = [.. Definitions.AppliedMods.Select(mod => new ModSave { Id = mod.Id, Name = mod.Name, Version = mod.Version })],
        Map = MapCodec.Encode(FireCodec.WholeMap(Map, Hazards)),
        Budget = Budget.Capture(),
        Census = Census.Capture(),
        Demand = Demand.Capture(),
        Power = Power.Capture(),
        Services = Services.Capture(),
        Community = Community.Capture(),
        CityMaps = Maps.Capture(),
        Cycle = new CycleSave
        {
            RoadTiles = RoadTiles,
            BudgetedRoadTiles = _budgetedRoadTiles,
            BudgetedFireStations = _budgetedFireStations,
            BudgetedPoliceStations = _budgetedPoliceStations,
        },
        Traffic = Traffic.Capture(),
        RoadTraffic = Traffic.CaptureRoads(),
        Rail = RailCodec.Capture(Map, RailTiles, _budgetedRailTiles),
        Pollution = Maps.CapturePollution(),
        Crime = Maps.CaptureCrime(),
        Fire = FireCodec.Capture(Map, Hazards),
        Flood = FloodCodec.Capture(Map, Floods),
        Facilities = FacilityCodec.Capture(Map, Hazards, Facilities),
        Founding = new FoundingSave { Year = FoundedYear },
        Challenge = Challenge?.Capture(),
    };

    /// <summary>Makes a city from a save, checking each part as it goes.</summary>
    /// <exception cref="InvalidSave">Something in the save is wrong; the message says what and where.</exception>
    internal static CitySimulation Restore(CitySave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        SaveUpgrades.ToCurrent(save);

        if (!IsValidName(save.Name))
        {
            throw new InvalidSave($"name: needs 1 to {MaxNameLength} characters and no control characters.");
        }

        if (save.Random is not { Length: 16 } || !ulong.TryParse(save.Random, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var random))
        {
            throw new InvalidSave("random: needs 16 hexadecimal digits.");
        }

        var tick = SaveChecks.InRange(save.Tick, 0, long.MaxValue, "tick");
        if (save.CityTime != tick / CityCalendar.StepsPerCount)
        {
            throw new InvalidSave($"cityTime: a city {tick} steps old is at count {tick / CityCalendar.StepsPerCount}, not {save.CityTime}.");
        }

        var definitions = RestoreDefinitions(save);
        var map = MapCodec.Decode(save.Map);
        var rail = save.Rail ?? throw new InvalidSave("rail: missing.");
        RailCodec.Apply(map, rail);
        CheckZones(map, definitions);

        var city = new CitySimulation(map, save.Seed, funds: 0, definitions);
        FacilityCodec.Apply(map, city.Facilities, save.Facilities ?? throw new InvalidSave("facilities: missing."));
        city.Name = save.Name.Trim();
        city.Random.State = random;
        city.TickCount = tick;
        city.CityTime = save.CityTime;
        city.Treasury.Restore(save.Funds);
        city.Budget.Restore(save.Budget ?? throw new InvalidSave("budget: missing."));
        city.Census.Restore(save.Census ?? throw new InvalidSave("census: missing."));
        city.Demand.Restore(save.Demand ?? throw new InvalidSave("demand: missing."));
        city.Power.Restore(save.Power ?? throw new InvalidSave("power: missing."), map);
        city.Services.Restore(save.Services ?? throw new InvalidSave("services: missing."));
        city.Community.Restore(save.Community ?? throw new InvalidSave("community: missing."));
        city.Maps.Restore(save.CityMaps ?? throw new InvalidSave("cityMaps: missing."));
        city.Maps.RestorePollution(save.Pollution ?? throw new InvalidSave("pollution: missing."));
        city.Maps.RestoreCrime(save.Crime ?? throw new InvalidSave("crime: missing."));
        FireCodec.Apply(map, city.Fires, save.Fire ?? throw new InvalidSave("fire: missing."));
        FloodCodec.Apply(map, city.Hazards, save.Flood ?? throw new InvalidSave("flood: missing."));
        var founding = save.Founding ?? throw new InvalidSave("founding: missing.");
        city.FoundedYear = SaveChecks.InRange(founding.Year, CityCalendar.StartYear, city.Date.Year, "founding.year");
        city.Challenge = save.Challenge is { } challenge ? Scenarios.Challenge.Restore(challenge) : null;
        var cycle = save.Cycle ?? throw new InvalidSave("cycle: missing.");
        city.RoadTiles = SaveChecks.InRange(cycle.RoadTiles, 0, int.MaxValue, "cycle.roadTiles");
        city._budgetedRoadTiles = SaveChecks.InRange(cycle.BudgetedRoadTiles, 0, int.MaxValue, "cycle.budgetedRoadTiles");
        city._budgetedFireStations = SaveChecks.InRange(cycle.BudgetedFireStations, 0, int.MaxValue, "cycle.budgetedFireStations");
        city._budgetedPoliceStations = SaveChecks.InRange(cycle.BudgetedPoliceStations, 0, int.MaxValue, "cycle.budgetedPoliceStations");
        city.Traffic.Restore(save.Traffic ?? throw new InvalidSave("traffic: missing."));
        city.Traffic.RestoreRoads(save.RoadTraffic ?? throw new InvalidSave("roadTraffic: missing."));
        city.RailTiles = SaveChecks.InRange(rail.Tiles, 0, int.MaxValue, "rail.tiles");
        city._budgetedRailTiles = SaveChecks.InRange(rail.BudgetedTiles, 0, int.MaxValue, "rail.budgetedTiles");
        return city;
    }

    // The definitions are read by the same loader as the base game's, so a save can't hold a spec a
    // mod couldn't.
    private static CityDefinitions RestoreDefinitions(CitySave save)
    {
        if (save.Definitions.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            throw new InvalidSave("definitions: needs the definitions as an object.");
        }

        if (!DefinitionLoader.TryParseBase(save.Definitions.GetRawText(), out var definitions, out var error))
        {
            throw new InvalidSave($"definitions: {error}");
        }

        var mods = new List<AppliedMod>();
        foreach (var mod in save.Mods ?? [])
        {
            if (string.IsNullOrWhiteSpace(mod.Id) || string.IsNullOrWhiteSpace(mod.Name))
            {
                throw new InvalidSave("mods: each needs an id and a name.");
            }

            mods.Add(new AppliedMod(mod.Id, mod.Name, mod.Version));
        }

        return definitions.WithAppliedMods(mods);
    }

    // A zone's building can't be a level or a land-value class its spec doesn't have.
    private static void CheckZones(TileMap map, CityDefinitions definitions)
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                if (!state.IsZoneCenter)
                {
                    continue;
                }

                var spec = definitions.Zone(state.Use);
                if (state.Development > spec.Levels || state.ValueClass >= Math.Max(spec.ValueClasses, 1))
                {
                    throw new InvalidSave($"map.buildings: the {spec.Name} at {x}, {y} is level {state.Development}, class {state.ValueClass}; its spec has {spec.Levels} levels and {spec.ValueClasses} classes.");
                }
            }
        }
    }

    /// <summary>
    /// Traces power at once, as Micropolis does when it loads a city, so a city made from a map has
    /// the power its plants give from its first step. The counts the scan keeps start again after,
    /// as they do at the top of a cycle.
    /// </summary>
    internal void TracePowerNow()
    {
        Power.StartCount();
        VisitBuildings();
        Power.Trace(Map, Definitions);

        // The second visit records on each centre the power the trace just gave it.
        Power.StartCount();
        VisitBuildings();
        Power.StartCount();
    }

    // As Micropolis repairs its plants, stations, hospitals, churches, stadiums, seaports, and airports: every so many counts,
    // the holes in one that are bare ground are redrawn, at no cost.
    private void RepairNowAndThen(int x, int y, int counts)
    {
        if (CityTime % counts == 0)
        {
            Hazards.RepairBareHoles(x, y);
        }
    }

    private void VisitBuildings()
    {
        for (var y = 0; y < Map.Height; y++)
        {
            for (var x = 0; x < Map.Width; x++)
            {
                if (Map.StateAt(x, y).IsBuildingCenter)
                {
                    Power.Visit(Map, x, y);
                }
            }
        }
    }
}
