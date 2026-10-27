namespace CityLimits.UI;

/// <summary>
/// A row of buttons along a window's bottom right, such as Apply and Cancel: the last one given
/// sits in the corner, the others to its left in the order given, each as wide as its label needs.
/// Any window can use it, so they look and answer alike.
/// </summary>
public static class ButtonRow
{
    private const int FontSize = 20;
    private const int Height = 30;
    private const int LabelPadding = 14;
    private const int Gap = 10;

    /// <summary>How tall the row is.</summary>
    public const int RowHeight = Height;

    /// <summary>The label of the button under a point, of those given in a row ending at the corner given, or -1.</summary>
    public static int ButtonAt(Vector2 point, Vector2 corner, IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        for (var i = 0; i < labels.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, Bounds(corner, labels, i)))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Draws the row ending at the corner given; a button whose place in <paramref name="enabled"/>
    /// is false is dimmed. The flags are the caller's own, kept between frames, so drawing allocates nothing.
    /// </summary>
    public static void Draw(Vector2 corner, IReadOnlyList<string> labels, Vector2 mouse, IReadOnlyList<bool>? enabled = null)
    {
        ArgumentNullException.ThrowIfNull(labels);
        for (var i = 0; i < labels.Count; i++)
        {
            var bounds = Bounds(corner, labels, i);
            var on = enabled is null || i >= enabled.Count || enabled[i];
            Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
            var color = !on ? Theme.TextDim : Raylib.CheckCollisionPointRec(mouse, bounds) ? Theme.Accent : Theme.Text;
            Raylib.DrawText(labels[i], (int)bounds.X + LabelPadding, (int)bounds.Y + 5, FontSize, color);
        }
    }

    // A button's bounds: the row is laid out from the corner leftward, the last label first.
    private static Rectangle Bounds(Vector2 corner, IReadOnlyList<string> labels, int index)
    {
        var right = corner.X;
        for (var i = labels.Count - 1; i >= 0; i--)
        {
            var width = Raylib.MeasureText(labels[i], FontSize) + (LabelPadding * 2);
            if (i == index)
            {
                return new Rectangle { X = right - width, Y = corner.Y - Height, Width = width, Height = Height };
            }

            right -= width + Gap;
        }

        return default;
    }
}
