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

    /// <summary>
    /// The tiles the stroke covers. A road runs in one straight line from where it started,
    /// across or down, whichever way the drag has gone further; across wins a tie. Every
    /// other tool covers the rectangle between where it started and where it has been dragged.
    /// </summary>
    public TileRect Bounds
    {
        get
        {
            if (Action != BuildAction.Road)
            {
                return TileRect.Spanning(StartX, StartY, EndX, EndY);
            }

            return Math.Abs(EndX - StartX) >= Math.Abs(EndY - StartY)
                ? TileRect.Spanning(StartX, StartY, EndX, StartY)
                : TileRect.Spanning(StartX, StartY, StartX, EndY);
        }
    }

    /// <summary>Stretches the stroke to the tile under the pointer.</summary>
    public void DragTo(int x, int y)
    {
        EndX = x;
        EndY = y;
    }
}
