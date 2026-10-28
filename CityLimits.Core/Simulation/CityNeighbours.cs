namespace CityLimits.Core.Simulation;

/// <summary>
/// The cities beyond the map's edges, after SimCity 3000's neighbours: one on each side, named and
/// sized from the city's seed, so they are the same every time and never touch its dice. A road,
/// rail, or power line on an edge tile connects to that side's neighbour; roads and rail lift
/// demand, and a power line lets the two trade power. A neighbour offers a deal, now and then,
/// as SimCity 3000's do: to sell power to a city that runs short, or to buy what one has spare,
/// for a sum each year over a term. Deals are paid with the taxes, and a deal broken costs a
/// year's money and that neighbour's goodwill for five years. The connections are read from the
/// map whenever they are needed; the deals, the offer standing, and the grudges are saved.
/// </summary>
public sealed class CityNeighbours
{
    /// <summary>How many people a city needs before its neighbours deal with it.</summary>
    public const int DealPopulation = 2000;

    /// <summary>How long a deal runs, in years.</summary>
    public const int TermYears = 10;

    /// <summary>How long an offer stands before it lapses, in counts: three months.</summary>
    public const int OfferCounts = CityCalendar.CountsPerMonth * 3;

    /// <summary>How long a neighbour won't deal after a deal with it was broken, in years.</summary>
    public const int GrudgeYears = 5;

    /// <summary>The power a neighbour sells a city that runs short, in tiles of capacity.</summary>
    public const int PowerSold = 300;

    /// <summary>What a city pays a year for each 100 tiles of power it buys.</summary>
    public const int BuyPricePer100 = 60;

    /// <summary>What a neighbour pays a year for each 100 tiles of power it buys.</summary>
    public const int SellPricePer100 = 40;

    /// <summary>The spare capacity a city needs before a neighbour asks to buy some.</summary>
    public const int LeastSpare = 400;

    /// <summary>The most power a neighbour buys in one deal.</summary>
    public const int MostBought = 500;

    /// <summary>What a road to a neighbour adds to commercial and industrial demand each time demand moves.</summary>
    public const int RoadDemand = 20;

    /// <summary>What rail to a neighbour adds to industrial demand each time demand moves.</summary>
    public const int RailDemand = 30;

    // Names for the cities beyond the edges, picked by the seed.
    private static readonly string[] Names =
    [
        "Westbrook", "Eastmoor", "Northgate", "Southvale", "Riverton", "Pinecrest", "Ashford", "Millbury",
        "Clearwater", "Oakridge", "Fairhaven", "Stonebridge", "Brookfield", "Lakemont", "Hillcrest", "Maplewood",
        "Kingsport", "Elmstead", "Redfield", "Silverton", "Greenbay", "Harborview", "Copperton", "Windmere",
    ];

    private readonly TileMap _map;
    private readonly Neighbour[] _neighbours;
    private readonly List<Deal> _deals = [];
    private readonly long[] _grudgeUntil = new long[4];

    /// <summary>Creates the neighbours of a city from its map and seed.</summary>
    public CityNeighbours(TileMap map, int seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;

        // Four names, none twice, and a size for each, from a hash of the seed alone.
        var taken = new HashSet<int>();
        _neighbours = new Neighbour[4];
        foreach (var side in Enum.GetValues<Side>())
        {
            var hash = Mix(((ulong)(uint)seed << 2) | (ulong)side);
            var name = (int)(hash % (ulong)Names.Length);
            while (!taken.Add(name))
            {
                name = (name + 1) % Names.Length;
            }

            var start = 2000 + (int)((hash >> 16) % 8001);
            var share = 50 + (int)((hash >> 32) % 101);
            _neighbours[(int)side] = new Neighbour(side, Names[name], start, share);
        }
    }

    /// <summary>A side of the map, and the neighbour beyond it.</summary>
    public enum Side
    {
        /// <summary>The top edge.</summary>
        North,

        /// <summary>The right edge.</summary>
        East,

        /// <summary>The bottom edge.</summary>
        South,

        /// <summary>The left edge.</summary>
        West,
    }

    /// <summary>The four neighbours, north, east, south, and west.</summary>
    public IReadOnlyList<Neighbour> All => _neighbours;

    /// <summary>The deals in force, in the order they were made.</summary>
    public IReadOnlyList<Deal> Deals => _deals;

    /// <summary>The offer a neighbour has made and the player hasn't answered, or null.</summary>
    public Offer? Offer { get; private set; }

    /// <summary>The neighbour on a side.</summary>
    public Neighbour Of(Side side) => _neighbours[(int)side];

    /// <summary>The deal with a neighbour, or null when there is none.</summary>
    public Deal? DealWith(Side side) => _deals.Find(deal => deal.Side == side);

    /// <summary>Tells whether a neighbour won't deal at a step, for a deal broken with it.</summary>
    public bool HoldsGrudge(Side side, long step) => step < _grudgeUntil[(int)side];

    /// <summary>What a side's edge has that crosses it: a road, rail, a power line, any of them.</summary>
    public Connections ConnectionsTo(Side side)
    {
        var found = Connections.None;
        var count = side is Side.North or Side.South ? _map.Width : _map.Height;
        for (var i = 0; i < count; i++)
        {
            var (x, y) = side switch
            {
                Side.North => (i, 0),
                Side.South => (i, _map.Height - 1),
                Side.West => (0, i),
                _ => (_map.Width - 1, i),
            };
            var state = _map.StateAt(x, y);
            found |= (state.HasRoad ? Connections.Road : 0) | (state.IsRail ? Connections.Rail : 0) | (state.HasPowerLine ? Connections.Power : 0);
        }

        return found;
    }

    /// <summary>What the connections add to residential, commercial, and industrial demand each time it moves.</summary>
    public (int Residential, int Commercial, int Industrial) DemandNudge()
    {
        var (commercial, industrial) = (0, 0);
        foreach (var side in Enum.GetValues<Side>())
        {
            var connections = ConnectionsTo(side);
            if (connections.HasFlag(Connections.Road))
            {
                commercial += RoadDemand;
                industrial += RoadDemand;
            }

            if (connections.HasFlag(Connections.Rail))
            {
                industrial += RailDemand;
            }
        }

        return (0, commercial, industrial);
    }

    /// <summary>
    /// What the deals in force do to the plants' capacity: what is bought added, what is sold taken
    /// away. A deal whose power line to its neighbour is gone carries no power until it is back.
    /// </summary>
    public int PowerTraded()
    {
        var traded = 0;
        foreach (var deal in _deals)
        {
            if (ConnectionsTo(deal.Side).HasFlag(Connections.Power))
            {
                traded += deal.Kind == DealKind.CityBuys ? deal.Power : -deal.Power;
            }
        }

        return traded;
    }

    /// <summary>
    /// What the deals in force earn and cost in a year, the cost as a positive number. A deal whose
    /// power line is gone is neither paid nor paid for while it is.
    /// </summary>
    public (int Income, int Cost) YearlyMoney()
    {
        var (income, cost) = (0L, 0L);
        foreach (var deal in _deals)
        {
            if (!ConnectionsTo(deal.Side).HasFlag(Connections.Power))
            {
                continue;
            }

            if (deal.Kind == DealKind.CityBuys)
            {
                cost += deal.Money;
            }
            else
            {
                income += deal.Money;
            }
        }

        return ((int)Math.Min(income, int.MaxValue), (int)Math.Min(cost, int.MaxValue));
    }

    /// <summary>
    /// Ends the deals whose term is over, and lets an offer lapse when its time is up. Returns the
    /// deals that ended, and whether an offer lapsed.
    /// </summary>
    public (List<Deal> Ended, Offer? Lapsed) Expire(long step)
    {
        var ended = _deals.FindAll(deal => step >= deal.EndStep);
        _deals.RemoveAll(deal => step >= deal.EndStep);
        Offer? lapsed = null;
        if (Offer is { } offer && step >= offer.ExpiresStep)
        {
            lapsed = offer;
            Offer = null;
        }

        return (ended, lapsed);
    }

    /// <summary>
    /// The offer a neighbour would make now, or null: to sell power when the city ran short, or to
    /// buy what it has spare, from the first neighbour, north, east, south, then west, with a power
    /// line to it, no deal with it already, and no grudge. A city too small, or with an offer
    /// standing, gets none.
    /// </summary>
    public Offer? Propose(int population, bool brownedOut, int capacity, int used, long step)
    {
        if (Offer is not null || population < DealPopulation)
        {
            return null;
        }

        var spare = capacity - used;
        DealKind kind;
        int power;
        if (brownedOut)
        {
            (kind, power) = (DealKind.CityBuys, PowerSold);
        }
        else if (spare >= LeastSpare)
        {
            (kind, power) = (DealKind.CitySells, Math.Min(MostBought, spare / 2 / 50 * 50));
        }
        else
        {
            return null;
        }

        foreach (var neighbour in _neighbours)
        {
            if (DealWith(neighbour.Side) is null && !HoldsGrudge(neighbour.Side, step) && ConnectionsTo(neighbour.Side).HasFlag(Connections.Power))
            {
                var money = power / 100 * (kind == DealKind.CityBuys ? BuyPricePer100 : SellPricePer100);
                Offer = new Offer(neighbour.Side, kind, power, money, TermYears, step + ((long)OfferCounts * CityCalendar.StepsPerCount));
                return Offer;
            }
        }

        return null;
    }

    /// <summary>Takes the offer standing, as a deal from now for its term. Returns it, or null when there was none.</summary>
    public Deal? Accept(long step)
    {
        if (Offer is not { } offer)
        {
            return null;
        }

        var deal = new Deal(offer.Side, offer.Kind, offer.Power, offer.Money, step + ((long)offer.Years * CityCalendar.StepsPerYear));
        _deals.Add(deal);
        Offer = null;
        return deal;
    }

    /// <summary>Turns the offer standing down. Returns it, or null when there was none.</summary>
    public Offer? Decline()
    {
        var offer = Offer;
        Offer = null;
        return offer;
    }

    /// <summary>Breaks the deal with a neighbour, which then won't deal for <see cref="GrudgeYears"/>. Returns it, or null when there was none.</summary>
    public Deal? Break(Side side, long step)
    {
        if (DealWith(side) is not { } deal)
        {
            return null;
        }

        _deals.Remove(deal);
        _grudgeUntil[(int)side] = step + ((long)GrudgeYears * CityCalendar.StepsPerYear);
        return deal;
    }

    internal ConnectionsSave Capture() => new()
    {
        Deals = [.. _deals.Select(deal => new DealSave { Side = SideName(deal.Side), Kind = KindName(deal.Kind), Power = deal.Power, Money = deal.Money, EndStep = deal.EndStep })],
        Offer = Offer is { } offer
            ? new OfferSave { Side = SideName(offer.Side), Kind = KindName(offer.Kind), Power = offer.Power, Money = offer.Money, Years = offer.Years, ExpiresStep = offer.ExpiresStep }
            : null,
        GrudgeUntil = [.. _grudgeUntil],
    };

    internal void Restore(ConnectionsSave save)
    {
        _deals.Clear();
        var deals = save.Deals ?? [];
        for (var i = 0; i < deals.Length; i++)
        {
            var at = $"connections.deals[{i}]";
            var deal = deals[i] ?? throw new InvalidSave($"{at}: needs a deal, not null.");
            var side = SideOf(deal.Side, $"{at}.side");
            if (_deals.Exists(given => given.Side == side))
            {
                throw new InvalidSave($"{at}.side: '{deal.Side}' has two deals.");
            }

            _deals.Add(new Deal(side, KindOf(deal.Kind, $"{at}.kind"), SaveChecks.InRange(deal.Power, 1, int.MaxValue, $"{at}.power"), SaveChecks.InRange(deal.Money, 0, int.MaxValue, $"{at}.money"), deal.EndStep));
        }

        Offer = save.Offer is { } offer
            ? new Offer(SideOf(offer.Side, "connections.offer.side"), KindOf(offer.Kind, "connections.offer.kind"), SaveChecks.InRange(offer.Power, 1, int.MaxValue, "connections.offer.power"), SaveChecks.InRange(offer.Money, 0, int.MaxValue, "connections.offer.money"), SaveChecks.InRange(offer.Years, 1, 100, "connections.offer.years"), offer.ExpiresStep)
            : null;

        var grudges = save.GrudgeUntil ?? new long[4];
        if (grudges.Length != 4)
        {
            throw new InvalidSave("connections.grudgeUntil: needs a step for each of the four sides.");
        }

        Array.Copy(grudges, _grudgeUntil, 4);
    }

    /// <summary>A side's name, as saves write it.</summary>
    public static string SideName(Side side) => side switch
    {
        Side.North => "north",
        Side.East => "east",
        Side.South => "south",
        _ => "west",
    };

    private static string KindName(DealKind kind) => kind == DealKind.CityBuys ? "cityBuys" : "citySells";

    private static Side SideOf(string? name, string at) => name switch
    {
        "north" => Side.North,
        "east" => Side.East,
        "south" => Side.South,
        "west" => Side.West,
        _ => throw new InvalidSave($"{at}: '{name}' is not north, east, south, or west."),
    };

    private static DealKind KindOf(string? name, string at) => name switch
    {
        "cityBuys" => DealKind.CityBuys,
        "citySells" => DealKind.CitySells,
        _ => throw new InvalidSave($"{at}: '{name}' is not cityBuys or citySells."),
    };

    // SplitMix64's finaliser: the same number from the same seed, on every machine.
    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}

/// <summary>What crosses an edge of the map to a neighbour.</summary>
[Flags]
public enum Connections
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>A road.</summary>
    Road = 1,

    /// <summary>Rail.</summary>
    Rail = 2,

    /// <summary>A power line.</summary>
    Power = 4,
}

/// <summary>Which way a deal's power goes.</summary>
public enum DealKind
{
    /// <summary>The city buys power from its neighbour, and pays for it.</summary>
    CityBuys,

    /// <summary>The city sells its spare power to its neighbour, and is paid for it.</summary>
    CitySells,
}

/// <summary>A city beyond an edge of the map.</summary>
/// <param name="Side">Which edge it lies beyond.</param>
/// <param name="Name">Its name.</param>
/// <param name="Start">The people it has to begin with.</param>
/// <param name="Share">How big it grows beside the city, in percent of the city's own people.</param>
public sealed record Neighbour(CityNeighbours.Side Side, string Name, int Start, int Share)
{
    /// <summary>How many people it has, beside a city of the population given.</summary>
    public int PopulationBeside(int population) => (int)Math.Min(int.MaxValue, Start + ((long)Math.Max(0, population) * Share / 100));
}

/// <summary>A deal in force with a neighbour.</summary>
/// <param name="Side">The neighbour it is with.</param>
/// <param name="Kind">Which way its power goes.</param>
/// <param name="Power">How much power, in tiles of capacity.</param>
/// <param name="Money">What changes hands each year.</param>
/// <param name="EndStep">The step it ends at.</param>
public sealed record Deal(CityNeighbours.Side Side, DealKind Kind, int Power, int Money, long EndStep);

/// <summary>A deal a neighbour has offered, standing until the player answers or it lapses.</summary>
/// <param name="Side">The neighbour offering it.</param>
/// <param name="Kind">Which way its power would go.</param>
/// <param name="Power">How much power, in tiles of capacity.</param>
/// <param name="Money">What would change hands each year.</param>
/// <param name="Years">How long it would run.</param>
/// <param name="ExpiresStep">The step the offer lapses at.</param>
public sealed record Offer(CityNeighbours.Side Side, DealKind Kind, int Power, int Money, int Years, long ExpiresStep);

/// <summary>What happened between the city and a neighbour, for the ticker to say.</summary>
public enum NeighbourNewsKind
{
    /// <summary>The neighbour offers a deal.</summary>
    Offer,

    /// <summary>The player took the deal.</summary>
    Accepted,

    /// <summary>The player turned it down.</summary>
    Declined,

    /// <summary>The offer lapsed, unanswered.</summary>
    Lapsed,

    /// <summary>The deal's term is over.</summary>
    Ended,

    /// <summary>The player broke the deal.</summary>
    Broken,
}

/// <summary>News of a neighbour: who, what happened, and the deal it is about.</summary>
public sealed record NeighbourNews(Neighbour Neighbour, NeighbourNewsKind Kind, DealKind Deal, int Power, int Money);
