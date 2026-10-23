namespace CityLimits.Core.World;

/// <summary>
/// One use of a building tool, as in Micropolis: it starts where the button went down and
/// stretches as the pointer drags. <see cref="CityBuilder.Plan"/> previews it, and
/// <see cref="CityBuilder.Apply(BuildStroke)"/> applies all of it or none.
/// </summary>
public sealed class BuildStroke
{
    /// <summary>Starts a stroke on the tile where the button went down.</summary>
    public BuildStroke(BuildAction action, int x, int y)
    {
        Action = action;
        StartX = x;
        StartY = y;
        EndX = x;
        EndY = y;
    }

    /// <summary>The work the stroke does on each tile.</summary>
    public BuildAction Action { get; }

    /// <summary>The column where the stroke started.</summary>
    public int StartX { get; }

    /// <summary>The row where the stroke started.</summary>
    public int StartY { get; }

    /// <summary>The column the stroke has been dragged to.</summary>
    public int EndX { get; private set; }

    /// <summary>The row the stroke has been dragged to.</summary>
    public int EndY { get; private set; }

    /// <summary>Tells whether the stroke lays zones, which are 3 by 3 and laid as a grid.</summary>
    public bool LaysZones => Action is BuildAction.ZoneResidential or BuildAction.ZoneCommercial or BuildAction.ZoneIndustrial;

    /// <summary>Tells whether the stroke places one building, such as a power plant or a station, which follows the pointer.</summary>
    public bool PlacesBuilding => Action is BuildAction.CoalPowerPlant or BuildAction.NuclearPowerPlant or BuildAction.FireStation or BuildAction.PoliceStation
        or BuildAction.Stadium or BuildAction.Seaport or BuildAction.Airport;

    /// <summary>What the stroke's zones or building are used for, or <see cref="TileUse.None"/> for any other stroke.</summary>
    public TileUse Use => Action switch
    {
        BuildAction.ZoneResidential => TileUse.Residential,
        BuildAction.ZoneCommercial => TileUse.Commercial,
        BuildAction.ZoneIndustrial => TileUse.Industrial,
        BuildAction.CoalPowerPlant => TileUse.CoalPowerPlant,
        BuildAction.NuclearPowerPlant => TileUse.NuclearPowerPlant,
        BuildAction.FireStation => TileUse.FireStation,
        BuildAction.PoliceStation => TileUse.PoliceStation,
        BuildAction.Stadium => TileUse.Stadium,
        BuildAction.Seaport => TileUse.Seaport,
        BuildAction.Airport => TileUse.Airport,
        _ => TileUse.None,
    };

    /// <summary>
    /// The tiles the stroke covers. A road or a power line runs in one straight line from where it started,
    /// across or down, whichever way the drag has gone further; across wins a tie. Zones are
    /// laid as a grid of 3 by 3 zones, the first centred where the stroke started, growing a
    /// whole zone at a time toward the pointer. A building, such as a power plant, is one
    /// footprint centred where the pointer is now, as in Micropolis, so dragging moves it. The
    /// bulldozer covers the rectangle dragged.
    /// </summary>
    public TileRect Bounds
    {
        get
        {
            if (PlacesBuilding)
            {
                var size = Footprint.SizeOf(Use);
                return new TileRect(EndX - 1, EndY - 1, size, size);
            }

            if (LaysZones)
            {
                var (x, width) = ZoneSpan(StartX, EndX);
                var (y, height) = ZoneSpan(StartY, EndY);
                return new TileRect(x, y, width, height);
            }

            if (Action is not (BuildAction.Road or BuildAction.PowerLine or BuildAction.Rail))
            {
                return TileRect.Spanning(StartX, StartY, EndX, EndY);
            }

            return Math.Abs(EndX - StartX) >= Math.Abs(EndY - StartY)
                ? TileRect.Spanning(StartX, StartY, EndX, StartY)
                : TileRect.Spanning(StartX, StartY, StartX, EndY);
        }
    }

    // Along one axis: the first zone's centre is at the start, and each further whole zone's
    // worth of drag adds a zone on that side.
    private static (int First, int Length) ZoneSpan(int start, int end)
    {
        var zones = (Math.Abs(end - start) / ZoneLayout.Size) + 1;
        var length = zones * ZoneLayout.Size;
        var first = end >= start ? start - 1 : start + 2 - length;
        return (first, length);
    }

    /// <summary>Stretches the stroke to the tile under the pointer.</summary>
    public void DragTo(int x, int y)
    {
        EndX = x;
        EndY = y;
    }
}
