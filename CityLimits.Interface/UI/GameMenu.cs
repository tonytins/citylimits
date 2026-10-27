namespace CityLimits.UI;

/// <summary>
/// The menu Escape opens in a city when nothing else is open: a column of buttons to resume, save,
/// load, open the settings, leave for the main menu, or quit. Editing a scenario, save saves the
/// scenario, which is dimmed while it is tested, and load makes way for going back to before the
/// test was played, which is dimmed with no test under way, and for starting over. The game is paused while it is open, and Escape again resumes.
/// </summary>
public sealed class GameMenu
{
    private const int FontSize = 20;
    private const int Padding = 20;
    private const int Width = 300;
    private const int ButtonHeight = 32;
    private const int ButtonGap = 8;
    private const int TitleGap = 16;

    private static readonly (string Text, Choice Choice)[] PlayItems =
    [
        ("Resume", Choice.Resume),
        ("Save", Choice.Save),
        ("Load", Choice.Load),
        ("Settings", Choice.Settings),
        ("Main menu", Choice.MainMenu),
        ("Quit", Choice.Quit),
    ];

    private static readonly (string Text, Choice Choice)[] EditItems =
    [
        ("Resume", Choice.Resume),
        ("Save", Choice.Save),
        ("Back to before play", Choice.BackToBeforePlay),
        ("Start over", Choice.StartOver),
        ("Settings", Choice.Settings),
        ("Main menu", Choice.MainMenu),
        ("Quit", Choice.Quit),
    ];

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

        /// <summary>Put the scenario's city back as it was when its test was played.</summary>
        BackToBeforePlay,

        /// <summary>Open the scenario's city afresh, dropping every change.</summary>
        StartOver,
    }

    /// <summary>Takes this frame's input: the editor's menu while a scenario is edited, and its test, if any, under way.</summary>
    public static Choice Update(bool editing = false, bool testing = false)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Choice.Resume;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return Choice.None;
        }

        var items = editing ? EditItems : PlayItems;
        var item = ItemAt(Raylib.GetMousePosition(), items.Length);
        return item < 0 || !IsEnabled(items[item].Choice, testing) ? Choice.None : items[item].Choice;
    }

    /// <summary>Draws the menu in the middle of the screen, over a dimmed city.</summary>
    public static void Draw(bool editing = false, bool testing = false)
    {
        var items = editing ? EditItems : PlayItems;
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds(items.Length);
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        Raylib.DrawText("Paused", (int)bounds.X + Padding, (int)bounds.Y + Padding, FontSize, Theme.TextDim);

        var hovered = ItemAt(Raylib.GetMousePosition(), items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            var item = ItemBounds(i, items.Length);
            var enabled = IsEnabled(items[i].Choice, testing);
            var lit = i == hovered && enabled;
            Raylib.DrawRectangleRec(item, lit ? Theme.PanelBorder : Theme.Panel);
            Raylib.DrawRectangleLinesEx(item, 1f, lit ? Theme.Accent : Theme.PanelBorder);
            var x = (int)(item.X + ((item.Width - Raylib.MeasureText(items[i].Text, FontSize)) / 2));
            Raylib.DrawText(items[i].Text, x, (int)item.Y + ((ButtonHeight - FontSize) / 2), FontSize, !enabled ? Theme.TextDim : lit ? Theme.Accent : Theme.Text);
        }
    }

    // Going back to before play needs a test to go back from; saving a scenario needs none under way.
    private static bool IsEnabled(Choice choice, bool testing) => choice switch
    {
        Choice.BackToBeforePlay => testing,
        Choice.Save => !testing,
        _ => true,
    };

    private static int ItemAt(Vector2 point, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, ItemBounds(i, count)))
            {
                return i;
            }
        }

        return -1;
    }

    private static Rectangle Bounds(int count)
    {
        var height = (Padding * 2) + FontSize + TitleGap + (count * (ButtonHeight + ButtonGap)) - ButtonGap;
        return new Rectangle { X = (Raylib.GetScreenWidth() - Width) / 2, Y = (Raylib.GetScreenHeight() - height) / 2, Width = Width, Height = height };
    }

    // Each button is as wide as the panel, one under another.
    private static Rectangle ItemBounds(int item, int count)
    {
        var bounds = Bounds(count);
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + Padding + FontSize + TitleGap + (item * (ButtonHeight + ButtonGap)), Width = Width - (Padding * 2), Height = ButtonHeight };
    }
}
