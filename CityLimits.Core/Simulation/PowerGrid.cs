namespace CityLimits.Core.Simulation;

/// <summary>
/// Which tiles have power, worked out as Micropolis does, with CityLimits' own ways to carry it.
/// The scan counts the plants it passes and gives each zone it reaches the power the last trace
/// left it; then, once a cycle, power is traced from the plants through everything that carries
/// it, one tile at a time, until the plants' pooled capacity runs out.
/// </summary>
/// <remarks>
/// As in Micropolis, lines and every zone and building carry power to what touches them, the
/// capacity is every plant's on the map, connected to the grid or not, and a plant is always
/// powered itself. Where the definitions say so (<see cref="InfrastructureSpec"/>), a road or rail
/// network joins every zone and building along it: once
/// one of them is powered, so are the rest, without the network itself being powered or using
/// capacity; the buildings nearest along it are powered first, each whole or not at all. The
/// trace is otherwise a plain flood fill, so when capacity runs out the tiles left dark are
/// not exactly the ones Micropolis' walk would leave.
/// </remarks>
public sealed class PowerGrid
{
    private readonly List<(int X, int Y)> _plants = [];
    private bool[] _powered = [];
    private int[] _stack = [];
    private bool[] _joined = [];
    private int[] _networkStack = [];
    private int _width;
    private InfrastructureSpec _carriers = BaseGame.Infrastructure;

    /// <summary>The coal plants the scan has passed this cycle.</summary>
    public int CoalPlants { get; private set; }

    /// <summary>The nuclear plants the scan has passed this cycle.</summary>
    public int NuclearPlants { get; private set; }

    /// <summary>The zones the scan has passed this cycle, the three kinds together.</summary>
    public int Zones { get; private set; }

    /// <summary>The zones and plants the scan found with power this cycle.</summary>
    public int PoweredBuildings { get; private set; }

    /// <summary>The zones the scan found without power this cycle.</summary>
    public int UnpoweredBuildings { get; private set; }

    /// <summary>How many tiles the last trace could power, from the plants it was given.</summary>
    public int Capacity { get; private set; }

    /// <summary>How many tiles the last trace powered.</summary>
    public int PoweredTiles { get; private set; }

    /// <summary>Whether the last trace ran out of capacity before it reached everything connected.</summary>
    public bool IsBrownedOut { get; private set; }

    /// <summary>Tells whether the last trace powered a tile; a tile off the map has none.</summary>
    public bool HasPower(int x, int y) =>
        x >= 0 && y >= 0 && x < _width && (y * _width) + x < _powered.Length && _powered[(y * _width) + x];

    /// <summary>Starts a new cycle's count of plants and zones, as the census does.</summary>
    public void StartCount()
    {
        _plants.Clear();
        CoalPlants = 0;
        NuclearPlants = 0;
        Zones = 0;
        PoweredBuildings = 0;
        UnpoweredBuildings = 0;
    }

    /// <summary>
    /// Counts the zone or plant whose centre the scan has reached, and records on the centre
    /// whether it has power: a plant always does, and a zone does when the last trace reached it.
    /// </summary>
    /// <returns>Whether the zone or plant has power.</returns>
    public bool Visit(TileMap map, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(map);

        var center = map.StateAt(x, y);
        var powered = TileRules.IsPowerPlant(center.Use) || HasPower(x, y);
        switch (center.Use)
        {
            case TileUse.CoalPowerPlant:
                CoalPlants++;
                _plants.Add((x, y));
                break;
            case TileUse.NuclearPowerPlant:
                NuclearPlants++;
                _plants.Add((x, y));
                break;
            default:
                Zones += center.IsZoned ? 1 : 0;
                break;
        }

        if (powered)
        {
            PoweredBuildings++;
        }
        else
        {
            UnpoweredBuildings++;
        }

        if (center.Powered != powered)
        {
            map.SetState(x, y, new TileState(center.Kind, center.Use, center.Development, center.ZonePart, center.ValueClass, center.House, powered));
        }

        return powered;
    }

    /// <summary>
    /// Traces power from the plants the scan counted, through every tile that carries it toward
    /// the next, until everything connected is powered or the plants' capacity runs out.
    /// </summary>
    /// <param name="map">The map to trace.</param>
    /// <param name="definitions">The definitions, for the plants' capacity and what carries power.</param>
    /// <param name="capacityPercent">The plants' capacity in percent, 100 unless the city's ordinances say otherwise.</param>
    /// <param name="traded">The capacity the city's deals with its neighbours add, or take away when below zero.</param>
    public void Trace(TileMap map, CityDefinitions definitions, int capacityPercent = 100, int traded = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(definitions);

        var tiles = map.Width * map.Height;
        if (_powered.Length != tiles)
        {
            _powered = new bool[tiles];
            _stack = new int[tiles];
        }
        else
        {
            Array.Clear(_powered);
        }

        if (_joined.Length != tiles)
        {
            _joined = new bool[tiles];
            _networkStack = new int[tiles];
        }
        else
        {
            Array.Clear(_joined);
        }

        _width = map.Width;
        _carriers = definitions.Infrastructure;
        var capacity = ((long)CoalPlants * definitions.Plant(TileUse.CoalPowerPlant).Capacity)
            + ((long)NuclearPlants * definitions.Plant(TileUse.NuclearPowerPlant).Capacity);
        Capacity = (int)Math.Clamp((capacity * Math.Max(0, capacityPercent) / 100) + traded, 0, int.MaxValue);
        PoweredTiles = 0;
        IsBrownedOut = false;

        // The plant found last is traced from first, as Micropolis takes them from a stack.
        for (var i = _plants.Count - 1; i >= 0 && !IsBrownedOut; i--)
        {
            var (plantX, plantY) = _plants[i];
            if (map.StateAt(plantX, plantY).IsBuildingCenter && TileRules.IsPowerPlant(map.StateAt(plantX, plantY).Use))
            {
                TraceFrom(map, plantX, plantY);
            }
        }
    }

    // A tile is marked as it is reached, so each is powered, and counted, once, and the stack
    // never holds more tiles than the map has.
    private void TraceFrom(TileMap map, int startX, int startY)
    {
        var depth = 0;
        if (!TryPower((startY * _width) + startX, ref depth))
        {
            return;
        }

        while (depth > 0)
        {
            var index = _stack[--depth];
            var (x, y) = (index % _width, index / _width);
            var state = map.StateAt(x, y);
            foreach (var (side, dx, dy) in Sides.All)
            {
                var (nextX, nextY) = (x + dx, y + dy);
                if (!map.Contains(nextX, nextY))
                {
                    continue;
                }

                var next = (nextY * _width) + nextX;
                var nextState = map.StateAt(nextX, nextY);

                // A zone or building on a road or rail joins every other one along it.
                if (state.HasFootprint && IsNetwork(nextState) && !_joined[next] && !JoinNetwork(map, next, ref depth))
                {
                    return;
                }

                if (!_powered[next] && PowerLines.RunsToward(state, side) && PowerLines.RunsToward(nextState, Sides.Opposite(side)) && !TryPower(next, ref depth))
                {
                    return;
                }
            }
        }
    }

    private bool IsNetwork(TileState state) =>
        (_carriers.RoadCarriesPower && state.HasRoad) || (_carriers.RailCarriesPower && state.IsRail);

    private bool Joins(TileState from, TileState to, RoadConnections side) =>
        (_carriers.RoadCarriesPower && RoadNetwork.RunsToward(from, side) && RoadNetwork.RunsToward(to, Sides.Opposite(side)))
        || (_carriers.RailCarriesPower && Rails.RunsToward(from, side) && Rails.RunsToward(to, Sides.Opposite(side)));

    // Walks the road or rail network from one of its tiles, nearest first, each tile once, and
    // powers each zone and building beside it whole, in the order the walk reaches them, so a
    // plant short of capacity lights the buildings nearest it along the road, not a strip of
    // each. The trace then carries on from them. The network itself isn't powered and uses no
    // capacity. Returns false when the capacity runs out.
    private bool JoinNetwork(TileMap map, int start, ref int depth)
    {
        var (head, tail) = (0, 0);
        _joined[start] = true;
        _networkStack[tail++] = start;
        while (head < tail)
        {
            var index = _networkStack[head++];
            var (x, y) = (index % _width, index / _width);
            var state = map.StateAt(x, y);
            foreach (var (side, dx, dy) in Sides.All)
            {
                var (nextX, nextY) = (x + dx, y + dy);
                if (!map.Contains(nextX, nextY))
                {
                    continue;
                }

                var next = (nextY * _width) + nextX;
                var nextState = map.StateAt(nextX, nextY);
                if (nextState.HasFootprint)
                {
                    if (!_powered[next] && !PowerWhole(map, nextX, nextY, nextState, ref depth))
                    {
                        return false;
                    }
                }
                else if (!_joined[next] && IsNetwork(nextState) && Joins(state, nextState, side))
                {
                    _joined[next] = true;
                    _networkStack[tail++] = next;
                }
            }
        }

        return true;
    }

    // Powers every tile of the building a tile is part of, or, when the capacity left can't
    // power them all, none of them.
    private bool PowerWhole(TileMap map, int x, int y, TileState part, ref int depth)
    {
        var size = Footprint.SizeOf(part.Use);
        var (left, top) = (x - (part.ZonePart % size), y - (part.ZonePart / size));
        var needed = 0;
        for (var row = top; row < top + size; row++)
        {
            for (var column = left; column < left + size; column++)
            {
                needed += map.Contains(column, row) && !_powered[(row * _width) + column] ? 1 : 0;
            }
        }

        if (PoweredTiles + needed > Capacity)
        {
            IsBrownedOut = true;
            return false;
        }

        for (var row = top; row < top + size; row++)
        {
            for (var column = left; column < left + size; column++)
            {
                if (map.Contains(column, row))
                {
                    TryPower((row * _width) + column, ref depth);
                }
            }
        }

        return true;
    }

    private bool TryPower(int index, ref int depth)
    {
        if (_powered[index])
        {
            return true;
        }

        if (PoweredTiles >= Capacity)
        {
            IsBrownedOut = true;
            return false;
        }

        _powered[index] = true;
        PoweredTiles++;
        _stack[depth++] = index;
        return true;
    }

    internal PowerSave Capture() => new()
    {
        Plants = [.. _plants.SelectMany(plant => new[] { plant.X, plant.Y })],
        Powered = PoweredRows(),
        CoalPlants = CoalPlants,
        NuclearPlants = NuclearPlants,
        Zones = Zones,
        PoweredBuildings = PoweredBuildings,
        UnpoweredBuildings = UnpoweredBuildings,
        Capacity = Capacity,
        PoweredTiles = PoweredTiles,
        BrownedOut = IsBrownedOut,
    };

    // A map that hasn't been traced yet has no rows.
    internal void Restore(PowerSave save, TileMap map)
    {
        if (save.Plants is null || save.Plants.Length % 2 != 0)
        {
            throw new InvalidSave("power.plants: needs x and y pairs.");
        }

        _plants.Clear();
        for (var i = 0; i < save.Plants.Length; i += 2)
        {
            var (x, y) = (save.Plants[i], save.Plants[i + 1]);
            _plants.Add(map.Contains(x, y) ? (x, y) : throw new InvalidSave($"power.plants: {x}, {y} is off the map."));
        }

        var rows = save.Powered ?? [];
        if (rows.Length == 0)
        {
            (_powered, _width) = ([], 0);
        }
        else
        {
            if (rows.Length != map.Height)
            {
                throw new InvalidSave($"power.powered: needs {map.Height} rows, not {rows.Length}.");
            }

            _width = map.Width;
            _powered = new bool[map.Width * map.Height];
            _stack = new int[map.Width * map.Height];
            for (var y = 0; y < rows.Length; y++)
            {
                var row = rows[y];
                if (row is null || row.Length != map.Width || row.Any(c => c is not ('0' or '1')))
                {
                    throw new InvalidSave($"power.powered[{y}]: needs {map.Width} characters, each 0 or 1.");
                }

                for (var x = 0; x < row.Length; x++)
                {
                    _powered[(y * _width) + x] = row[x] == '1';
                }
            }
        }

        CoalPlants = SaveChecks.InRange(save.CoalPlants, 0, int.MaxValue, "power.coalPlants");
        NuclearPlants = SaveChecks.InRange(save.NuclearPlants, 0, int.MaxValue, "power.nuclearPlants");
        Zones = SaveChecks.InRange(save.Zones, 0, int.MaxValue, "power.zones");
        PoweredBuildings = SaveChecks.InRange(save.PoweredBuildings, 0, int.MaxValue, "power.poweredBuildings");
        UnpoweredBuildings = SaveChecks.InRange(save.UnpoweredBuildings, 0, int.MaxValue, "power.unpoweredBuildings");
        Capacity = SaveChecks.InRange(save.Capacity, 0, int.MaxValue, "power.capacity");
        PoweredTiles = SaveChecks.InRange(save.PoweredTiles, 0, int.MaxValue, "power.poweredTiles");
        IsBrownedOut = save.BrownedOut;
    }

    private string[] PoweredRows()
    {
        if (_width == 0)
        {
            return [];
        }

        var rows = new string[_powered.Length / _width];
        for (var y = 0; y < rows.Length; y++)
        {
            rows[y] = string.Create(_width, (this, y), static (span, state) =>
            {
                for (var x = 0; x < span.Length; x++)
                {
                    span[x] = state.Item1._powered[(state.y * state.Item1._width) + x] ? '1' : '0';
                }
            });
        }

        return rows;
    }
}
