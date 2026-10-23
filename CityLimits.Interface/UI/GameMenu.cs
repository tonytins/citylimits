namespace CityLimits.UI;

/// <summary>
/// The menu Escape opens in a city when nothing else is open: a column of buttons to resume, save,
/// load, open the settings, leave for the main menu, or quit. The game is paused while it is open,
/// and Escape again resumes.
/// </summary>
public sealed class GameMenu
{
    private const int FontSize = 20;
    private const int Padding = 20;
    private const int Width = 260;
    private const int ButtonHeight = 32;
    private const int ButtonGap = 8;
    private const int TitleGap = 16;

    private static readonly string[] Items = ["Resume", "Save", "Load", "Settings", "Main menu", "Quit"];

    /// <summary>What the player chose.</summary>
    public enum Choice
    {
        /// <summary>Nothing yet.</summary>
        None,

        /// <summary>Back to the city.</summary>
        Resume,

        /// <summary>Save the city, as Ctrl+S does.</summary>
        Save,

        /// <summary>Open a saved city, as Ctrl+O does.</summary>
        Load,

        /// <summary>Open the settings.</summary>
        Settings,

        /// <summary>Leave the city for the start menu.</summary>
        MainMenu,

        /// <summary>Leave the game.</summary>
        Quit,
    }

    /// <summary>Takes this frame's input.</summary>
    public static Choice Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Choice.Resume;
        }

        return Raylib.IsMouseButtonPressed(MouseButton.Left)
            ? ItemAt(Raylib.GetMousePosition()) switch
            {
                0 => Choice.Resume,
                1 => Choice.Save,
                2 => Choice.Load,
                3 => Choice.Settings,
                4 => Choice.MainMenu,
                5 => Choice.Quit,
                _ => Choice.None,
            }
            : Choice.None;
    }

    /// <summary>Draws the menu in the middle of the screen, over a dimmed city.</summary>
    public static void Draw()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        Raylib.DrawText("Paused", (int)bounds.X + Padding, (int)bounds.Y + Padding, FontSize, Theme.TextDim);

        var hovered = ItemAt(Raylib.GetMousePosition());
        for (var i = 0; i < Items.Length; i++)
        {
            var item = ItemBounds(i);
            Raylib.DrawRectangleRec(item, i == hovered ? Theme.PanelBorder : Theme.Panel);
            Raylib.DrawRectangleLinesEx(item, 1f, i == hovered ? Theme.Accent : Theme.PanelBorder);
            var x = (int)(item.X + ((item.Width - Raylib.MeasureText(Items[i], FontSize)) / 2));
            Raylib.DrawText(Items[i], x, (int)item.Y + ((ButtonHeight - FontSize) / 2), FontSize, i == hovered ? Theme.Accent : Theme.Text);
        }
    }

    private static int ItemAt(Vector2 point)
    {
        for (var i = 0; i < Items.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, ItemBounds(i)))
            {
                return i;
            }
        }

        return -1;
    }

    private static Rectangle Bounds()
    {
        var height = (Padding * 2) + FontSize + TitleGap + (Items.Length * (ButtonHeight + ButtonGap)) - ButtonGap;
        return new Rectangle { X = (Raylib.GetScreenWidth() - Width) / 2, Y = (Raylib.GetScreenHeight() - height) / 2, Width = Width, Height = height };
    }

    // Each button is as wide as the panel, one under another.
    private static Rectangle ItemBounds(int item)
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + Padding + FontSize + TitleGap + (item * (ButtonHeight + ButtonGap)), Width = Width - (Padding * 2), Height = ButtonHeight };
    }
}
