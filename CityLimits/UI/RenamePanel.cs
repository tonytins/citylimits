namespace CityLimits.UI;

/// <summary>
/// The panel a click on the city's name in the top bar opens: the name, ready to edit, with Rename
/// and Cancel. Enter renames and Escape cancels. The game is paused while it is open, so typing
/// never reaches the city's keys.
/// </summary>
public sealed class RenamePanel
{
    private const int FontSize = 20;
    private const int LabelSize = 10;
    private const int Padding = 16;
    private const int Width = 420;
    private const int Height = 150;
    private const int ButtonWidth = 100;
    private const int ButtonHeight = 30;

    private readonly TextField _name = new(CitySimulation.DefaultName, CitySimulation.MaxNameLength, _ => true);
    private string? _problem;

    /// <summary>What a frame of the panel came to.</summary>
    public enum Outcome
    {
        /// <summary>The player is still typing.</summary>
        Open,

        /// <summary>The player renamed the city to <see cref="Name"/>.</summary>
        Renamed,

        /// <summary>The player kept the old name.</summary>
        Cancelled,
    }

    /// <summary>The new name, once the outcome is <see cref="Outcome.Renamed"/>, trimmed.</summary>
    public string Name { get; private set; } = CitySimulation.DefaultName;

    /// <summary>Opens the panel on the city's current name.</summary>
    public void Reset(string name)
    {
        _name.Text = name;
        _problem = null;
    }

    /// <summary>Takes this frame's typing and clicks.</summary>
    public Outcome Update()
    {
        var mouse = Raylib.GetMousePosition();
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse, CancelBounds())))
        {
            return Outcome.Cancelled;
        }

        if (_name.Update())
        {
            _problem = null;
        }

        var confirmed = Raylib.IsKeyPressed(KeyboardKey.Enter)
            || Raylib.IsKeyPressed(KeyboardKey.KpEnter)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse, RenameBounds()));
        if (!confirmed)
        {
            return Outcome.Open;
        }

        if (!CitySimulation.IsValidName(_name.Text))
        {
            _problem = $"A city's name needs 1 to {CitySimulation.MaxNameLength} characters";
            return Outcome.Open;
        }

        Name = _name.Text.Trim();
        return Outcome.Renamed;
    }

    /// <summary>Draws the panel in the middle of the screen, over a dimmed city.</summary>
    public void Draw()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        Raylib.DrawText("Rename the city", (int)bounds.X + Padding, (int)bounds.Y + Padding, FontSize, Theme.Text);

        var field = FieldBounds();
        _name.Draw(field, focused: true);
        if (_problem is not null)
        {
            Raylib.DrawText(_problem, (int)field.X, (int)(field.Y + field.Height) + 6, LabelSize, Theme.Error);
        }

        var mouse = Raylib.GetMousePosition();
        DrawButton(CancelBounds(), "Cancel", mouse);
        DrawButton(RenameBounds(), "Rename", mouse);
    }

    private static void DrawButton(Rectangle bounds, string label, Vector2 mouse)
    {
        Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
        var width = Raylib.MeasureText(label, FontSize);
        var color = Raylib.CheckCollisionPointRec(mouse, bounds) ? Theme.Accent : Theme.Text;
        Raylib.DrawText(label, (int)(bounds.X + ((bounds.Width - width) / 2)), (int)bounds.Y + 5, FontSize, color);
    }

    private static Rectangle Bounds() => new()
    {
        X = (Raylib.GetScreenWidth() - Width) / 2,
        Y = (Raylib.GetScreenHeight() - Height) / 2,
        Width = Width,
        Height = Height,
    };

    private static Rectangle FieldBounds()
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + Padding + FontSize + 10, Width = Width - (Padding * 2), Height = TextField.Height };
    }

    private static Rectangle RenameBounds()
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Width - Padding - ButtonWidth, Y = bounds.Y + Height - Padding - ButtonHeight, Width = ButtonWidth, Height = ButtonHeight };
    }

    private static Rectangle CancelBounds() => RenameBounds() with { X = RenameBounds().X - ButtonWidth - 10 };
}
