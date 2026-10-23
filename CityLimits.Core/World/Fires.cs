namespace CityLimits.Core.World;

/// <summary>
/// The fires burning on the map, as Micropolis has them. A burning tile has lost what stood on it:
/// the map holds rubble there, and this holds that it burns, until it burns out and the rubble is
/// all that is left. Fire spreads to almost anything built, and to trees; not to water, bare
/// ground, or rubble. A building that loses an outer tile keeps standing with a hole in it
/// (<see cref="Hazards"/>).
/// </summary>
/// <remarks>
/// When fire reaches a building's centre, the building is lost, and its other tiles with
/// something on them catch fire: a house, every tile of a zone's building, and every tile of a
/// plant, a station, a hospital, or a church. An empty lot becomes bare ground. Micropolis leaves
/// those tiles standing as loose pieces, which CityLimits can't hold, so Tony Bark chose this.
/// </remarks>
public sealed class Fires
{
    private readonly TileMap _map;
    private readonly bool[] _burning;

    /// <summary>Makes the fires of a map, with none burning.</summary>
    public Fires(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _burning = new bool[map.Width * map.Height];
    }

    /// <summary>How many tiles are burning.</summary>
    public int Count { get; private set; }

    /// <summary>Tells whether a tile is burning; a tile off the map never is.</summary>
    public bool IsBurning(int x, int y) => _map.Contains(x, y) && _burning[(y * _map.Width) + x];

    /// <summary>
    /// Tells whether something on a tile can catch fire: anything built on land, and trees. Water,
    /// and so a bridge, a tunnel, or a line under water, can't; nor bare ground, rubble, or a tile
    /// already burning.
    /// </summary>
    public bool CanBurn(int x, int y)
    {
        if (!_map.Contains(x, y) || IsBurning(x, y))
        {
            return false;
        }

        var state = _map.StateAt(x, y);
        return state.Kind != TileKind.Water && (state.Use != TileUse.None || state.Kind == TileKind.Trees);
    }

    /// <summary>
    /// Sets a tile alight: what stood there is lost, and rubble burns in its place. A building's centre
    /// takes the building with it; whether it explodes is the caller's to decide, from what it was.
    /// </summary>
    /// <returns>The building whose centre caught, or null when the tile wasn't one.</returns>
    public TileState? Ignite(int x, int y)
    {
        if (!_map.Contains(x, y) || IsBurning(x, y))
        {
            return null;
        }

        var state = _map.StateAt(x, y);
        SetBurning(x, y);
        if (!state.IsBuildingCenter)
        {
            return null;
        }

        var size = Footprint.SizeOf(state.Use);
        for (var part = 0; part < Footprint.Parts(size); part++)
        {
            var (dx, dy) = Footprint.OffsetFromCenter(part, size);
            var (px, py) = (x + dx, y + dy);
            var piece = _map.StateAtOrDefault(px, py);
            if ((dx, dy) == (0, 0) || piece.Use != state.Use || piece.ZonePart != part || IsBurning(px, py))
            {
                continue;
            }

            if (HasSomethingOn(state, piece))
            {
                SetBurning(px, py);
            }
            else
            {
                _map.SetState(px, py, new TileState(TileKind.Grass));
            }
        }

        return state;
    }

    /// <summary>Puts a tile's fire out, leaving the rubble.</summary>
    public void PutOut(int x, int y)
    {
        if (IsBurning(x, y))
        {
            _burning[(y * _map.Width) + x] = false;
            Count--;
        }
    }

    /// <summary>Tells whether any tile of the building centred on a tile is burning, which stops it being redrawn.</summary>
    public bool AnyBurningIn(int centerX, int centerY)
    {
        var center = _map.StateAtOrDefault(centerX, centerY);
        var size = Footprint.SizeOf(center.Use);
        for (var part = 0; part < Footprint.Parts(size); part++)
        {
            var (dx, dy) = Footprint.OffsetFromCenter(part, size);
            if (IsBurning(centerX + dx, centerY + dy))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Sets a tile burning as a save had it, without touching what the map holds beneath it.</summary>
    internal void RestoreBurning(int x, int y)
    {
        if (!IsBurning(x, y))
        {
            _burning[(y * _map.Width) + x] = true;
            Count++;
        }
    }

    // A house, a zone's building, or any tile of a plant, a station, a hospital, or a church; not
    // the empty lot of a zone with no building.
    private static bool HasSomethingOn(TileState center, TileState piece) => center.Use switch
    {
        TileUse.Residential => center.Development > 0 || piece.HasHouse,
        TileUse.Commercial or TileUse.Industrial => center.Development > 0,
        _ => true,
    };

    private void SetBurning(int x, int y)
    {
        _map.SetState(x, y, new TileState(TileKind.Rubble));
        _burning[(y * _map.Width) + x] = true;
        Count++;
    }
}
