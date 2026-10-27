namespace CityLimits.UI;

/// <summary>
/// A question with two answers, such as replacing a saved city or leaving one with unsaved changes.
/// Enter gives the first answer and Escape the second, the safe one. A long message is wrapped to
/// the panel's width when it is asked, and the panel grows to hold it.
/// </summary>
public sealed class ConfirmPanel
{
    private const int FontSize = 20;
    private const int TextSize = 14;
    private const int Padding = 16;
    private const int Width = 480;
    private const int LeastHeight = 150;
    private const int LineHeight = TextSize + 4;
    private const int ButtonHeight = 30;
    private const int ButtonPadding = 14;

    private string _title = string.Empty;
    private IReadOnlyList<string> _lines = [];
    private string _yes = "Yes";
    private string _no = "No";

    /// <summary>The player's answer.</summary>
    public enum Answer
    {
        /// <summary>Not yet.</summary>
        None,

        /// <summary>The first answer, which goes ahead.</summary>
        Yes,

        /// <summary>The second answer, which leaves things as they are.</summary>
        No,
    }

    /// <summary>Sets the question and its two answers, the one that goes ahead first.</summary>
    public void Ask(string title, string message, string yes, string no)
    {
        (_title, _yes, _no) = (title, yes, no);
        _lines = TextWrap.Lines(message, TextSize, Width - (Padding * 2));
    }

    /// <summary>Takes this frame's keys and clicks.</summary>
    public Answer Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Answer.No;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            return Answer.Yes;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return Answer.None;
        }

        var mouse = Raylib.GetMousePosition();
        return Raylib.CheckCollisionPointRec(mouse, YesBounds()) ? Answer.Yes
            : Raylib.CheckCollisionPointRec(mouse, NoBounds()) ? Answer.No
            : Answer.None;
    }

    /// <summary>Draws the question in the middle of the screen, over a dimmed view.</summary>
    public void Draw()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        Raylib.DrawText(_title, (int)bounds.X + Padding, (int)bounds.Y + Padding, FontSize, Theme.Text);
        var y = (int)bounds.Y + Padding + FontSize + 12;
        foreach (var line in _lines)
        {
            Raylib.DrawText(line, (int)bounds.X + Padding, y, TextSize, Theme.TextDim);
            y += LineHeight;
        }

        var mouse = Raylib.GetMousePosition();
        DrawButton(YesBounds(), _yes, mouse);
        DrawButton(NoBounds(), _no, mouse);
    }

    private static void DrawButton(Rectangle bounds, string label, Vector2 mouse)
    {
        Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
        var width = Raylib.MeasureText(label, FontSize);
        var color = Raylib.CheckCollisionPointRec(mouse, bounds) ? Theme.Accent : Theme.Text;
        Raylib.DrawText(label, (int)(bounds.X + ((bounds.Width - width) / 2)), (int)bounds.Y + 5, FontSize, color);
    }

    private int Height => Math.Max(LeastHeight, Padding + FontSize + 12 + (_lines.Count * LineHeight) + 16 + ButtonHeight + Padding);

    private Rectangle Bounds() => new()
    {
        X = (Raylib.GetScreenWidth() - Width) / 2,
        Y = (Raylib.GetScreenHeight() - Height) / 2,
        Width = Width,
        Height = Height,
    };

    // The safe answer sits on the right, by the panel's corner; the one that goes ahead to its left.
    private Rectangle NoBounds()
    {
        var bounds = Bounds();
        var width = Raylib.MeasureText(_no, FontSize) + (ButtonPadding * 2);
        return new Rectangle { X = bounds.X + Width - Padding - width, Y = bounds.Y + Height - Padding - ButtonHeight, Width = width, Height = ButtonHeight };
    }

    private Rectangle YesBounds()
    {
        var no = NoBounds();
        var width = Raylib.MeasureText(_yes, FontSize) + (ButtonPadding * 2);
        return no with { X = no.X - width - 10, Width = width };
    }
}
