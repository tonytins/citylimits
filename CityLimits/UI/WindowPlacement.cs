namespace CityLimits.UI;

/// <summary>
/// Where a window sits on the screen. The player moves it by dragging its title strip, as windows
/// move anywhere else; it starts where its panel asks, keeps where it was left for the next time it
/// opens, and is always kept on the screen below the top bar.
/// </summary>
public sealed class WindowPlacement
{
    private readonly int _titleHeight;
    private Vector2? _position;
    private Vector2 _grab;

    /// <summary>Creates a placement for a window whose title strip is the given height.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The title strip's height isn't positive.</exception>
    public WindowPlacement(int titleHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(titleHeight);
        _titleHeight = titleHeight;
    }

    /// <summary>Tells whether the player is dragging the window.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>
    /// Where the window of the given size is: where the player left it, or at the start given until
    /// they move it, kept on the screen.
    /// </summary>
    public Rectangle Bounds(Vector2 size, Vector2 start)
    {
        var position = Keep(_position ?? start, size);
        return new Rectangle { X = position.X, Y = position.Y, Width = size.X, Height = size.Y };
    }

    /// <summary>Starts a drag when a press lands on the window's title strip. Tells whether it did.</summary>
    public bool BeginDrag(Vector2 point, Rectangle bounds)
    {
        var title = bounds with { Height = Math.Min(_titleHeight, bounds.Height) };
        if (!Raylib.CheckCollisionPointRec(point, title))
        {
            return false;
        }

        _position = new Vector2(bounds.X, bounds.Y);
        _grab = point - _position.Value;
        IsDragging = true;
        return true;
    }

    /// <summary>Moves the window with the pointer while the button is held, and lets it go when it isn't.</summary>
    public void Drag(Vector2 point, bool held, Vector2 size)
    {
        if (!IsDragging)
        {
            return;
        }

        _position = Keep(point - _grab, size);
        IsDragging = held;
    }

    // A window bigger than the screen keeps its top-left corner, and so its title, in view.
    private static Vector2 Keep(Vector2 position, Vector2 size)
    {
        var right = Math.Max(0f, Raylib.GetScreenWidth() - size.X);
        var bottom = Math.Max(Hud.BarHeight, Raylib.GetScreenHeight() - size.Y);
        return new Vector2(Math.Clamp(position.X, 0f, right), Math.Clamp(position.Y, Hud.BarHeight, bottom));
    }
}
