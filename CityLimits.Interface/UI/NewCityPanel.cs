namespace CityLimits.UI;

/// <summary>
/// The panel New Game opens: the city's name, starting as "CityLimits", and a seed, blank for the
/// game to pick one. Start, or Enter, begins the city; Back, or Escape, returns to the menu. Tab
/// or a click moves between the fields, and a problem with either is shown under them.
/// </summary>
public sealed class NewCityPanel
{
    private const int FontSize = 20;
    private const int LabelSize = 10;
    private const int Padding = 16;
    private const int Width = 420;
    private const int Height = 262;
    private const int ButtonWidth = 100;
    private const int ButtonHeight = 30;
    private const int SeedLength = 11;

    private readonly TextField _name = new(CitySimulation.DefaultName, CitySimulation.MaxNameLength, _ => true);
    private readonly TextField _seed = new(string.Empty, SeedLength, c => char.IsAsciiDigit(c) || c == '-');
    private bool _seedFocused;
    private string? _problem;

    /// <summary>What a frame of the panel came to.</summary>
    public enum Outcome
    {
        /// <summary>The player is still filling it in.</summary>
        Open,

        /// <summary>The player asked to start the city.</summary>
        Start,

        /// <summary>The player went back to the menu.</summary>
        Back,
    }

    /// <summary>The city the player asked for, once the outcome is <see cref="Outcome.Start"/>.</summary>
    public NewCity? City { get; private set; }

    /// <summary>Clears the panel for a new city, with the seed given on the command line, if any.</summary>
    public void Reset(int? seed)
    {
        _name.Text = CitySimulation.DefaultName;
        _seed.Text = seed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _seedFocused = false;
        _problem = null;
        City = null;
    }

    /// <summary>Takes this frame's typing and clicks.</summary>
    public Outcome Update()
    {
        var mouse = Raylib.GetMousePosition();
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Outcome.Back;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _seedFocused = !_seedFocused;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mouse, NameBounds()))
            {
                _seedFocused = false;
            }
            else if (Raylib.CheckCollisionPointRec(mouse, SeedBounds()))
            {
                _seedFocused = true;
            }
            else if (Raylib.CheckCollisionPointRec(mouse, BackBounds()))
            {
                return Outcome.Back;
            }
            else if (Raylib.CheckCollisionPointRec(mouse, StartBounds()))
            {
                return TryStart();
            }
        }

        // A problem stays until the player changes what it was about.
        if ((_seedFocused ? _seed : _name).Update())
        {
            _problem = null;
        }

        return Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) ? TryStart() : Outcome.Open;
    }

    /// <summary>Draws the panel in the middle of the screen.</summary>
    public void Draw()
    {
        var bounds = Bounds();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        var x = (int)bounds.X + Padding;
        Raylib.DrawText("New city", x, (int)bounds.Y + Padding, FontSize, Theme.Text);

        var name = NameBounds();
        Raylib.DrawText("Name", x, (int)name.Y - LabelSize - 4, LabelSize, Theme.TextDim);
        _name.Draw(name, !_seedFocused);

        var seed = SeedBounds();
        Raylib.DrawText("Seed, the number the map grows from: blank for any", x, (int)seed.Y - LabelSize - 4, LabelSize, Theme.TextDim);
        _seed.Draw(seed, _seedFocused);

        if (_problem is not null)
        {
            Raylib.DrawText(_problem, x, (int)(seed.Y + seed.Height) + 8, LabelSize, Theme.Error);
        }

        var mouse = Raylib.GetMousePosition();
        DrawButton(BackBounds(), "Back", mouse);
        DrawButton(StartBounds(), "Start", mouse);
    }

    private Outcome TryStart()
    {
        if (!NewCity.TryRead(_name.Text, _seed.Text, out var city, out var problem))
        {
            _problem = problem;
            return Outcome.Open;
        }

        City = city;
        return Outcome.Start;
    }

    private static void DrawButton(Rectangle bounds, string label, Vector2 mouse)
    {
        var hovered = Raylib.CheckCollisionPointRec(mouse, bounds);
        Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
        var width = Raylib.MeasureText(label, FontSize);
        Raylib.DrawText(label, (int)(bounds.X + ((bounds.Width - width) / 2)), (int)bounds.Y + 5, FontSize, hovered ? Theme.Accent : Theme.Text);
    }

    private static Rectangle Bounds() => new()
    {
        X = (Raylib.GetScreenWidth() - Width) / 2,
        Y = (Raylib.GetScreenHeight() - Height) / 2,
        Width = Width,
        Height = Height,
    };

    private static Rectangle NameBounds()
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + Padding + FontSize + 28, Width = Width - (Padding * 2), Height = TextField.Height };
    }

    private static Rectangle SeedBounds()
    {
        var name = NameBounds();
        return name with { Y = name.Y + name.Height + 30 };
    }

    private static Rectangle StartBounds()
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Width - Padding - ButtonWidth, Y = bounds.Y + Height - Padding - ButtonHeight, Width = ButtonWidth, Height = ButtonHeight };
    }

    private static Rectangle BackBounds() => StartBounds() with { X = StartBounds().X - ButtonWidth - 10 };
}
