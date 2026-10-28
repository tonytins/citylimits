namespace CityLimits.Core.World;

/// <summary>
/// The fires, floods, and radiation on the map, and the holes they leave in buildings. A building that loses an
/// outer tile keeps standing with a hole in it, which nothing can be built in; it can't be redrawn
/// while any of it burns or is flooded, and when it is, as a zone grows or declines, the holes are
/// redrawn too, as Micropolis' zones are. Plants, stations, hospitals, and churches redraw the holes
/// that are bare ground now and then, as Micropolis repairs them. A hole is worked out from the map,
/// not kept, so it can't drift from it.
/// </summary>
public sealed class Hazards
{
    private readonly TileMap _map;

    /// <summary>Makes the hazards of a map, with none.</summary>
    public Hazards(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        Fires = new Fires(map);
        Radiation = new Radiation(map);
        Floods = new Floods(map, Fires, Radiation);
    }

    /// <summary>The fires burning.</summary>
    public Fires Fires { get; }

    /// <summary>The floods.</summary>
    public Floods Floods { get; }

    /// <summary>The radioactive ground a meltdown leaves.</summary>
    public Radiation Radiation { get; }

    /// <summary>Tells whether a tile is burning, flooded, or radioactive.</summary>
    public bool IsActive(int x, int y) => Fires.IsBurning(x, y) || Floods.IsFlooded(x, y) || Radiation.IsRadioactive(x, y);

    /// <summary>Tells whether any tile of the building centred on a tile is burning, flooded, or radioactive, which stops it being redrawn.</summary>
    public bool AnyActiveIn(int centerX, int centerY) => Fires.AnyBurningIn(centerX, centerY) || Floods.AnyFloodedIn(centerX, centerY) || AnyRadioactiveIn(centerX, centerY);

    private bool AnyRadioactiveIn(int centerX, int centerY)
    {
        if (Radiation.Count == 0)
        {
            return false;
        }

        var size = Footprint.SizeOf(_map.StateAtOrDefault(centerX, centerY).Use);
        for (var part = 0; part < Footprint.Parts(size); part++)
        {
            var (dx, dy) = Footprint.OffsetFromCenter(part, size);
            if (Radiation.IsRadioactive(centerX + dx, centerY + dy))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether a tile is a hole in a standing building: inside its footprint, but not one of
    /// its tiles any more. Nothing can be built in one.
    /// </summary>
    public bool IsHole(int x, int y) => HoleOwner(x, y) is not null;

    /// <summary>The centre of the standing building a tile is a hole in, or null when it isn't one.</summary>
    public (int X, int Y)? HoleOwner(int x, int y)
    {
        if (!_map.Contains(x, y) || _map.StateAt(x, y).HasFootprint)
        {
            return null;
        }

        // A building's centre is one tile in from its top-left corner, so it is at most one tile up or
        // left of any of its tiles, and as far down or right as the largest is wide less two; buildings never overlap.
        for (var cy = y - (Footprint.Largest - 2); cy <= y + 1; cy++)
        {
            for (var cx = x - (Footprint.Largest - 2); cx <= x + 1; cx++)
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
    /// Redraws the building centred on a tile whole, as a Micropolis zone does when it grows or
    /// declines: each hole in it becomes its tile again. Call it only when none of it is burning or flooded.
    /// </summary>
    public void RepairHoles(int centerX, int centerY) => Redraw(centerX, centerY, onlyBareGround: false);

    /// <summary>
    /// Redraws the holes in the building centred on a tile that are bare ground, as Micropolis repairs
    /// a plant, a station, a hospital, or a church: not rubble, and not a tile burning or flooded.
    /// </summary>
    public void RepairBareHoles(int centerX, int centerY) => Redraw(centerX, centerY, onlyBareGround: true);

    /// <summary>
    /// The building's tile a hole stands in for, as it would be whole: its use and its part, with
    /// nothing on it. A save writes it there, so an older version opens the building whole.
    /// </summary>
    public TileState WholeStateAt(int x, int y)
    {
        if (HoleOwner(x, y) is not var (cx, cy))
        {
            return _map.StateAt(x, y);
        }

        var center = _map.StateAt(cx, cy);
        var size = Footprint.SizeOf(center.Use);
        return new TileState(TileKind.Grass, center.Use, zonePart: Footprint.PartAt(x - cx + 1, y - cy + 1, size));
    }

    private void Redraw(int centerX, int centerY, bool onlyBareGround)
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
            if (!_map.Contains(px, py) || IsActive(px, py) || HoleOwner(px, py) != (centerX, centerY))
            {
                continue;
            }

            if (onlyBareGround && _map.StateAt(px, py).Kind != TileKind.Grass)
            {
                continue;
            }

            _map.SetState(px, py, new TileState(TileKind.Grass, center.Use, zonePart: part));
        }
    }
}
