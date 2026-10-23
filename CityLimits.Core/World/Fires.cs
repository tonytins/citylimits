namespace CityLimits.Core.World;

/// <summary>
/// The fires burning on the map, as Micropolis has them. A burning tile has lost what stood on it:
/// the map holds rubble there, and this holds that it burns, until it burns out and the rubble is
/// all that is left. Fire spreads to almost anything built, and to trees; not to water, bare
/// ground, or rubble. A building that loses an outer tile keeps standing with a hole in it, which
/// nothing can be built in, until the building redraws itself as it grows or declines, as
/// Micropolis' zones do; a hole is worked out from the map, not kept, so it can't drift from it.
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
    /// Tells whether a tile is a hole in a standing building: inside its footprint, but not one of
    /// its tiles any more, because it burned. Nothing can be built in one.
    /// </summary>
    public bool IsHole(int x, int y) => HoleOwner(x, y) is not null;

    /// <summary>Tells whether the player can't build on a tile, or clear it, because it is burning or a hole in a building.</summary>
    public bool IsOffLimits(int x, int y) => IsBurning(x, y) || IsHole(x, y);

    /// <summary>The centre of the standing building a tile is a hole in, or null when it isn't one.</summary>
    public (int X, int Y)? HoleOwner(int x, int y)
    {
        if (!_map.Contains(x, y) || _map.StateAt(x, y).HasFootprint)
        {
            return null;
        }

        // A building's centre is at most two tiles from any of its tiles, and buildings never overlap.
        for (var cy = y - 2; cy <= y + 2; cy++)
        {
            for (var cx = x - 2; cx <= x + 2; cx++)
            {
                var center = _map.StateAtOrDefault(cx, cy);
                if (!center.IsBuildingCenter)
                {
                    continue;
                }

                var size = Footprint.SizeOf(center.Use);
                if (x >= cx - 1 && x < cx - 1 + size && y >= cy - 1 && y < cy - 1 + size)
                {
                    return (cx, cy);
                }
            }
        }

        return null;
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

    /// <summary>
    /// Redraws the building centred on a tile whole, as a Micropolis zone does when it grows or
    /// declines: each hole in it becomes its tile again. Call it only when none of it is burning.
    /// </summary>
    public void RepairHoles(int centerX, int centerY)
    {
        var center = _map.StateAtOrDefault(centerX, centerY);
        if (!center.IsBuildingCenter)
        {
            return;
        }

        var size = Footprint.SizeOf(center.Use);
        for (var part = 0; part < Footprint.Parts(size); part++)
        {
            var (dx, dy) = Footprint.OffsetFromCenter(part, size);
            var (px, py) = (centerX + dx, centerY + dy);
            if (_map.Contains(px, py) && HoleOwner(px, py) == (centerX, centerY) && !IsBurning(px, py))
            {
                _map.SetState(px, py, new TileState(TileKind.Grass, center.Use, zonePart: part));
            }
        }
    }

    /// <summary>
    /// The building's tile a hole stands in for, as it would be whole: its use and its part, with
    /// nothing on it. A save writes it there, so an older version that doesn't know fire opens the
    /// building whole.
    /// </summary>
    public TileState WholeStateAt(int x, int y)
    {
        if (HoleOwner(x, y) is not var (cx, cy))
        {
            return _map.StateAt(x, y);
        }

        var center = _map.StateAt(cx, cy);
        var size = Footprint.SizeOf(center.Use);
        var part = Footprint.PartAt(x - cx + 1, y - cy + 1, size);
        return new TileState(TileKind.Grass, center.Use, zonePart: part);
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
