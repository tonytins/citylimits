namespace CityLimits.Core.World;

/// <summary>A tile a stroke will change, and what it will become.</summary>
public readonly record struct PlannedTile(int X, int Y, TileState State);

/// <summary>
/// What a stroke would do if it were applied now: the tiles it would change, what it would cost,
/// the tiles it would leave alone and why, and whether it would be refused as a
/// whole. <see cref="CityBuilder.Plan"/> fills it in; it changes nothing on the map.
/// </summary>
/// <remarks>
/// A plan is meant to be kept and filled again every frame while the player drags, so it reuses
/// its buffers instead of allocating new ones.
/// </remarks>
public sealed class BuildPlan
{
    private static readonly int RefusalCount = Enum.GetValues<BuildRefusal>().Length;

    private readonly List<PlannedTile> _tiles = [];
    private readonly Dictionary<int, int> _indexByTile = [];
    private readonly int[] _skipped = new int[RefusalCount];
    private TileMap? _map;

    /// <summary>The tiles the stroke would change, in the order it would change them.</summary>
    public IReadOnlyList<PlannedTile> Tiles => _tiles;

    /// <summary>The tiles the stroke covers, whether it changes them or not.</summary>
    public TileRect Bounds { get; private set; }

    /// <summary>What the stroke would cost.</summary>
    public int Cost { get; private set; }

    /// <summary>
    /// Why the stroke would be refused as a whole: <see cref="BuildRefusal.OffTheMap"/> when it
    /// runs off the map, <see cref="BuildRefusal.NotEnoughFunds"/> when the city can't pay for all
    /// of it, and <see cref="BuildRefusal.None"/> when it can go ahead.
    /// </summary>
    public BuildRefusal Refusal { get; private set; }

    /// <summary>Tells whether applying the stroke now would change the map.</summary>
    public bool WillChange => Refusal == BuildRefusal.None && _tiles.Count > 0;

    /// <summary>How many tiles the stroke would leave alone for the given reason.</summary>
    public int SkippedBecause(BuildRefusal reason) => _skipped[(int)reason];

    /// <summary>The state a tile would have once the stroke is applied: its planned state, or its state today.</summary>
    /// <exception cref="InvalidOperationException">The plan has not been filled in.</exception>
    public TileState StateAt(int x, int y)
    {
        var map = _map ?? throw new InvalidOperationException("The plan has not been filled in.");
        return TryGetPlanned(x, y, out var planned) ? planned : map.StateAtOrDefault(x, y);
    }

    /// <summary>Tells which sides of a tile would join a road once the stroke is applied.</summary>
    /// <exception cref="InvalidOperationException">The plan has not been filled in.</exception>
    public RoadConnections RoadConnectionsAt(int x, int y)
    {
        if (!StateAt(x, y).IsRoad)
        {
            return RoadConnections.None;
        }

        var connections = RoadConnections.None;
        if (IsRoad(x, y - 1))
        {
            connections |= RoadConnections.North;
        }

        if (IsRoad(x + 1, y))
        {
            connections |= RoadConnections.East;
        }

        if (IsRoad(x, y + 1))
        {
            connections |= RoadConnections.South;
        }

        if (IsRoad(x - 1, y))
        {
            connections |= RoadConnections.West;
        }

        return connections;
    }

    /// <summary>Tells whether the stroke would change the given tile, and to what.</summary>
    public bool TryGetPlanned(int x, int y, out TileState state)
    {
        if (_map is not null && _map.Contains(x, y) && _indexByTile.TryGetValue((y * _map.Width) + x, out var index))
        {
            state = _tiles[index].State;
            return true;
        }

        state = default;
        return false;
    }

    internal void Start(TileMap map, TileRect bounds)
    {
        _map = map;
        _tiles.Clear();
        _indexByTile.Clear();
        Array.Clear(_skipped);
        Bounds = bounds;
        Cost = 0;
        Refusal = BuildRefusal.None;
    }

    internal void Change(int x, int y, TileState state, int cost)
    {
        _indexByTile[(y * _map!.Width) + x] = _tiles.Count;
        _tiles.Add(new PlannedTile(x, y, state));
        Cost += cost;
    }

    internal void AddCost(int cost) => Cost += cost;

    internal void Skip(BuildRefusal reason) => _skipped[(int)reason]++;

    internal void Refuse(BuildRefusal reason) => Refusal = reason;

    private bool IsRoad(int x, int y) => StateAt(x, y).IsRoad;
}
