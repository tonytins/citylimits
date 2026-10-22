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
    private const int NeedPowerCount = 22;
    private const int BlackoutsCount = 32;
    private const int NeedFireStationCount = 45;
    private const int NeedPoliceStationCount = 48;
    private const int HighTaxesCount = 51;
    private const int RoadsNeedFundingCount = 54;
    private const int FireNeedsFundingCount = 57;
    private const int PoliceNeedsFundingCount = 60;

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

    // A city needs power once it has more zones than this, and has blackouts below this share powered.
    private const int ZonesBeforePowerIsNeeded = 10;
    private const double PoweredShareBeforeBlackouts = 0.7;

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
        Services = new ServiceCoverage(map);
        Traffic = new TrafficMap(map);
        Trips = new TripPlanner(Traffic);
        Budget = new CityBudget(Definitions.Service(TileUse.FireStation).Upkeep, Definitions.Service(TileUse.PoliceStation).Upkeep);
        _growth = new ZoneGrowth(map, Random, Trips, Definitions, Demand, Maps, Community);
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

    /// <summary>The definitions the city runs on: the base game's, with any mods applied.</summary>
    public CityDefinitions Definitions { get; }

    /// <summary>The map the city sits on.</summary>
    public TileMap Map { get; }

    /// <summary>The seed the city was created with.</summary>
    public int Seed { get; }

    /// <summary>The city's name; <see cref="DefaultName"/> until it is renamed.</summary>
    public string Name { get; private set; } = DefaultName;

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

    /// <summary>The road tiles the scan counted this cycle, a bridge counting five, as the roads' upkeep counts them.</summary>
    public int RoadTiles { get; private set; }

    // The road tiles and stations as of the last count's budget, which a fresh estimate works from.
    private int _budgetedRoadTiles;
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
    /// phase 11 traces power from the plants, phase 12 works out land value, and phase 14
    /// population density and the city's centre. Phases 13 and 15 spread the police and the
    /// fire stations' coverage, and are where crime, and fire and disasters, will run.
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
                Demand.Update(Census, Budget.TaxEffect);
            }

            Census.Clear();
            Power.StartCount();
            Services.StartCount();
            Community.StartCount();
            RoadTiles = 0;
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
            Maps.UpdateLandValue();
        }
        else if (phase == PolicePhase)
        {
            Services.SpreadPolice();
        }
        else if (phase == FirePhase)
        {
            Services.SpreadFire();
        }
        else if (phase == DensityPhase)
        {
            Maps.UpdateDensity(Definitions);
        }
    }

    // As in Micropolis, a 48th of the year's budget is set aside each count, and the year is
    // settled when the count comes round to a whole year, the first time at the very start.
    private void CollectTaxes()
    {
        (_budgetedRoadTiles, _budgetedFireStations, _budgetedPoliceStations) = (RoadTiles, Services.FireStations, Services.PoliceStations);
        Budget.Accrue(EstimateBudget());
        if (CityTime % CityBudget.CountsPerYear == 0)
        {
            YearSettled?.Invoke(Budget.Settle(Treasury));
        }
    }

    /// <summary>
    /// The year's budget as it would be estimated now, at the tax rate and funding set now, from
    /// the city as the last count saw it. The budget panel shows it as the player changes them.
    /// </summary>
    public BudgetYear EstimateBudget() =>
        Budget.Estimate(Demand.TaxablePopulation, Maps.LandValueAverage, _budgetedRoadTiles, _budgetedFireStations, _budgetedPoliceStations, Treasury.Funds);

    private void SendMessages()
    {
        var count = CityTime % MessageRound;
        if (count == NeedPowerCount && Power.Zones > ZonesBeforePowerIsNeeded && Power.CoalPlants + Power.NuclearPlants == 0)
        {
            MessageSent?.Invoke(CityMessage.NeedPower);
        }

        var buildings = Power.PoweredBuildings + Power.UnpoweredBuildings;
        if (count == BlackoutsCount && buildings > 0 && (double)Power.PoweredBuildings / buildings < PoweredShareBeforeBlackouts)
        {
            MessageSent?.Invoke(CityMessage.Blackouts);
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
                var state = Map.StateAt(x, y);
                if (state.IsRoad)
                {
                    VisitRoad(x, y, state);
                    continue;
                }

                if (!state.IsBuildingCenter)
                {
                    continue;
                }

                var powered = Power.Visit(Map, x, y);
                if (TileRules.IsCommunityBuilding(state.Use))
                {
                    Community.Visit(Map, Random, x, y);
                    continue;
                }

                if (TileRules.IsService(state.Use))
                {
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
    // A road crossing a power line never crumbles.
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
        Map = MapCodec.Encode(Map),
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
        CheckZones(map, definitions);

        var city = new CitySimulation(map, save.Seed, funds: 0, definitions);
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
        var cycle = save.Cycle ?? throw new InvalidSave("cycle: missing.");
        city.RoadTiles = SaveChecks.InRange(cycle.RoadTiles, 0, int.MaxValue, "cycle.roadTiles");
        city._budgetedRoadTiles = SaveChecks.InRange(cycle.BudgetedRoadTiles, 0, int.MaxValue, "cycle.budgetedRoadTiles");
        city._budgetedFireStations = SaveChecks.InRange(cycle.BudgetedFireStations, 0, int.MaxValue, "cycle.budgetedFireStations");
        city._budgetedPoliceStations = SaveChecks.InRange(cycle.BudgetedPoliceStations, 0, int.MaxValue, "cycle.budgetedPoliceStations");
        city.Traffic.Restore(save.Traffic ?? throw new InvalidSave("traffic: missing."));
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
