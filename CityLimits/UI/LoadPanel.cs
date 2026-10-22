namespace CityLimits.UI;

/// <summary>
/// The list of saved cities, newest first, that Load Game and Ctrl+O open: each with its date,
/// population, money, and when it was saved. A click opens one, the mouse wheel scrolls a long
/// list, and Back or Escape leaves. A city that can't be read says why here and stays listed.
/// </summary>
public sealed class LoadPanel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const int FontSize = 20;
    private const int RowSize = 14;
    private const int SmallSize = 10;
    private const int Padding = 16;
    private const int Width = 620;
    private const int Rows = 8;
    private const int RowHeight = 34;
    private const int ButtonWidth = 100;
    private const int ButtonHeight = 30;
    private const int Height = Padding + FontSize + 16 + (Rows * RowHeight) + 30 + ButtonHeight + Padding;

    private IReadOnlyList<SavedCitySummary> _cities = [];
    private int _first;
    private string? _problem;

    /// <summary>What a frame of the panel came to.</summary>
    public enum Outcome
    {
        /// <summary>The player is still choosing.</summary>
        Open,

        /// <summary>The player opened <see cref="City"/>, read from <see cref="Path"/>.</summary>
        Opened,

        /// <summary>The player went back.</summary>
        Back,
    }

    /// <summary>The city opened, once the outcome is <see cref="Outcome.Opened"/>.</summary>
    public CitySimulation? City { get; private set; }

    /// <summary>The file the city was opened from.</summary>
    public string? Path { get; private set; }

    /// <summary>Lists the cities in a folder afresh.</summary>
    public void Reset(string folder)
    {
        _cities = CityFiles.List(folder);
        _first = 0;
        _problem = null;
        City = null;
        Path = null;
    }

    /// <summary>Tells whether the folder last listed holds any city.</summary>
    public bool HasCities => _cities.Count > 0;

    /// <summary>Takes this frame's keys, clicks, and scrolling.</summary>
    public Outcome Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Outcome.Back;
        }

        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            _first = Math.Clamp(_first - Math.Sign(wheel), 0, Math.Max(0, _cities.Count - Rows));
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return Outcome.Open;
        }

        var mouse = Raylib.GetMousePosition();
        if (Raylib.CheckCollisionPointRec(mouse, BackBounds()))
        {
            return Outcome.Back;
        }

        for (var row = 0; row < Rows && _first + row < _cities.Count; row++)
        {
            if (!Raylib.CheckCollisionPointRec(mouse, RowBounds(row)))
            {
                continue;
            }

            var chosen = _cities[_first + row];
            if (!CityFiles.TryRead(chosen.Path, out var city, out var problem, out var notes))
            {
                Logger.Warn("{File} couldn't be opened: {Problem}", chosen.Path, problem);
                _problem = $"{System.IO.Path.GetFileName(chosen.Path)}: {problem}";
                return Outcome.Open;
            }

            foreach (var note in notes)
            {
                Logger.Info("{File}: {Note}", chosen.Path, note);
            }

            (City, Path) = (city, chosen.Path);
            return Outcome.Opened;
        }

        return Outcome.Open;
    }

    /// <summary>Draws the list in the middle of the screen, over a dimmed view.</summary>
    public void Draw()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        var x = (int)bounds.X + Padding;
        Raylib.DrawText("Load a city", x, (int)bounds.Y + Padding, FontSize, Theme.Text);

        var mouse = Raylib.GetMousePosition();
        if (_cities.Count == 0)
        {
            Raylib.DrawText("No saved cities yet: press Ctrl+S in a city to save it.", x, (int)RowBounds(0).Y + 8, RowSize, Theme.TextDim);
        }

        for (var row = 0; row < Rows && _first + row < _cities.Count; row++)
        {
            var city = _cities[_first + row];
            var rowBounds = RowBounds(row);
            var hovered = Raylib.CheckCollisionPointRec(mouse, rowBounds);
            if (hovered)
            {
                Raylib.DrawRectangleRec(rowBounds, Theme.PanelBorder);
            }

            Raylib.DrawText(city.Name, x + 6, (int)rowBounds.Y + 4, RowSize, hovered ? Theme.Accent : Theme.Text);
            var flat = System.IO.Path.GetExtension(city.Path).Equals(FlatCityFormat.Extension, StringComparison.OrdinalIgnoreCase) ? "   flat file" : string.Empty;
            var details = $"{city.Date.Month:00}/{city.Date.Year:0000}   {Hud.Count(city.Population)} Pop   {Hud.Money(city.Funds)}   saved {city.SavedUtc.ToLocalTime():yyyy-MM-dd HH:mm}{flat}";
            Raylib.DrawText(details, x + 6, (int)rowBounds.Y + 4 + RowSize + 2, SmallSize, Theme.TextDim);
        }

        if (_cities.Count > Rows)
        {
            var range = $"{_first + 1}-{Math.Min(_first + Rows, _cities.Count)} of {_cities.Count}; scroll for more";
            Raylib.DrawText(range, x, (int)RowBounds(Rows).Y + 4, SmallSize, Theme.TextDim);
        }

        if (_problem is not null)
        {
            Raylib.DrawText(_problem, x, (int)RowBounds(Rows).Y + 16, SmallSize, Theme.Error);
        }

        var back = BackBounds();
        Raylib.DrawRectangleRec(back, Theme.PanelBorder);
        var width = Raylib.MeasureText("Back", FontSize);
        var color = Raylib.CheckCollisionPointRec(mouse, back) ? Theme.Accent : Theme.Text;
        Raylib.DrawText("Back", (int)(back.X + ((back.Width - width) / 2)), (int)back.Y + 5, FontSize, color);
    }

    private static Rectangle Bounds() => new()
    {
        X = (Raylib.GetScreenWidth() - Width) / 2,
        Y = (Raylib.GetScreenHeight() - Height) / 2,
        Width = Width,
        Height = Height,
    };

    private static Rectangle RowBounds(int row)
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + Padding + FontSize + 16 + (row * RowHeight), Width = Width - (Padding * 2), Height = RowHeight - 2 };
    }

    private static Rectangle BackBounds()
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Width - Padding - ButtonWidth, Y = bounds.Y + Height - Padding - ButtonHeight, Width = ButtonWidth, Height = ButtonHeight };
    }
}
