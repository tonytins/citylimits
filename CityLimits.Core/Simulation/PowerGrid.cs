namespace CityLimits.Core.Simulation;

/// <summary>
/// Which tiles have power, worked out as Micropolis does. The scan counts the plants it passes
/// and gives each zone it reaches the power the last trace left it; then, once a cycle, power is
/// traced from the plants through everything that carries it, one tile at a time, until the
/// plants' pooled capacity runs out.
/// </summary>
/// <remarks>
/// As in Micropolis, the capacity is every plant's on the map, connected to the grid or not,
/// and a plant is always powered itself. The trace is a plain flood fill, so when capacity runs
/// out the tiles left dark are not exactly the ones Micropolis' walk would leave.
/// </remarks>
public sealed class PowerGrid
{
    private readonly List<(int X, int Y)> _plants = [];
    private bool[] _powered = [];
    private int[] _stack = [];
    private int _width;

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
    public void Trace(TileMap map, CityDefinitions definitions)
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

        _width = map.Width;
        Capacity = (CoalPlants * definitions.Plant(TileUse.CoalPowerPlant).Capacity)
            + (NuclearPlants * definitions.Plant(TileUse.NuclearPowerPlant).Capacity);
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
                if (!map.Contains(nextX, nextY) || _powered[(nextY * _width) + nextX])
                {
                    continue;
                }

                if (PowerLines.RunsToward(state, side) && PowerLines.RunsToward(map.StateAt(nextX, nextY), Sides.Opposite(side))
                    && !TryPower((nextY * _width) + nextX, ref depth))
                {
                    return;
                }
            }
        }
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
}
