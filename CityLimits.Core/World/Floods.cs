namespace CityLimits.Core.World;

/// <summary>
/// The water flooding the map, as Micropolis has it. A flooded tile has lost what stood there: the
/// map holds bare ground beneath, which is what the water leaves when it drains. Water spreads over
/// almost any land: anything built on it, trees, rubble, and bare ground, but not a fire, and not
/// water itself. A flood spreads for <see cref="Duration"/> cycles from when it starts, and then
/// drains away a tile at a time. A building that loses an outer tile keeps standing with a hole in
/// it (<see cref="Hazards"/>); one whose centre floods is lost, and all of it floods, as Tony Bark chose.
/// </summary>
public sealed class Floods
{
    /// <summary>How many cycles a flood spreads for, as in Micropolis.</summary>
    public const int Duration = 30;

    private readonly TileMap _map;
    private readonly Fires _fires;
    private readonly bool[] _flooded;

    /// <summary>Makes the floods of a map, with none, beside the fires that water can't put out.</summary>
    public Floods(TileMap map, Fires fires)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fires);
        _map = map;
        _fires = fires;
        _flooded = new bool[map.Width * map.Height];
    }

    /// <summary>How many tiles are under water from a flood.</summary>
    public int Count { get; private set; }

    /// <summary>How many more cycles the water spreads for; 0 when it is draining, or there is no flood.</summary>
    public int CyclesLeft { get; private set; }

    /// <summary>Tells whether a tile is flooded; a tile off the map never is.</summary>
    public bool IsFlooded(int x, int y) => _map.Contains(x, y) && _flooded[(y * _map.Width) + x];

    /// <summary>Tells whether water can spread onto a tile: any land not already flooded or burning.</summary>
    public bool CanFlood(int x, int y) =>
        _map.Contains(x, y) && !IsFlooded(x, y) && !_fires.IsBurning(x, y) && _map.StateAt(x, y).Kind != TileKind.Water;

    /// <summary>Starts a flood's spreading again, for <see cref="Duration"/> cycles.</summary>
    public void Rise() => CyclesLeft = Duration;

    /// <summary>Counts a cycle off the time the water has left to spread.</summary>
    public void Recede() => CyclesLeft = Math.Max(CyclesLeft - 1, 0);

    /// <summary>
    /// Puts a tile under water: what stood there is lost. A building's centre takes the whole
    /// building with it, every tile but those burning going under too.
    /// </summary>
    public void Flood(int x, int y)
    {
        if (!CanFlood(x, y))
        {
            return;
        }

        var state = _map.StateAt(x, y);
        SetFlooded(x, y);
        if (!state.IsBuildingCenter)
        {
            return;
        }

        var size = Footprint.SizeOf(state.Use);
        for (var part = 0; part < Footprint.Parts(size); part++)
        {
            var (dx, dy) = Footprint.OffsetFromCenter(part, size);
            var (px, py) = (x + dx, y + dy);
            var piece = _map.StateAtOrDefault(px, py);
            if ((dx, dy) != (0, 0) && piece.Use == state.Use && piece.ZonePart == part && CanFlood(px, py))
            {
                SetFlooded(px, py);
            }
        }
    }

    /// <summary>Lets the water off a tile, leaving the bare ground.</summary>
    public void Drain(int x, int y)
    {
        if (IsFlooded(x, y))
        {
            _flooded[(y * _map.Width) + x] = false;
            Count--;
        }
    }

    /// <summary>Tells whether any tile of the building centred on a tile is flooded, which stops it being redrawn.</summary>
    public bool AnyFloodedIn(int centerX, int centerY)
    {
        var center = _map.StateAtOrDefault(centerX, centerY);
        var size = Footprint.SizeOf(center.Use);
        for (var part = 0; part < Footprint.Parts(size); part++)
        {
            var (dx, dy) = Footprint.OffsetFromCenter(part, size);
            if (IsFlooded(centerX + dx, centerY + dy))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Sets a tile flooded as a save had it, without touching what the map holds beneath it.</summary>
    internal void RestoreFlooded(int x, int y)
    {
        if (!IsFlooded(x, y))
        {
            _flooded[(y * _map.Width) + x] = true;
            Count++;
        }
    }

    /// <summary>Sets how long the water spreads for, as a save had it.</summary>
    internal void RestoreCyclesLeft(int cycles) => CyclesLeft = Math.Clamp(cycles, 0, Duration);

    private void SetFlooded(int x, int y)
    {
        _map.SetState(x, y, new TileState(TileKind.Grass));
        _flooded[(y * _map.Width) + x] = true;
        Count++;
    }
}
