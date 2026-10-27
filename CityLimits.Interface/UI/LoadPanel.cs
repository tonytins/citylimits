namespace CityLimits.UI;

/// <summary>
/// The list of saved cities, newest first, that Load Game and Ctrl+O open: each with its date,
/// population, money, and when it was saved. A click picks one, and under the list its stats show
/// beside a map of it; Open, Enter, or a second click opens it. The mouse wheel scrolls a long
/// list, and Back or Escape leaves. A city that can't be read says why here and stays listed.
/// </summary>
public sealed class LoadPanel : IDisposable
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
    private const int ListTop = Padding + FontSize + 16;

    // The row under the list: the picked city's stats, and its map beside them.
    private const int DetailsTop = ListTop + (Rows * RowHeight) + 30;
    private const int PreviewWidth = 240;
    private const int PreviewHeight = 200;
    private const int StatsLineHeight = 20;
    private const int Height = DetailsTop + PreviewHeight + 12 + ButtonHeight + Padding;

    // The buttons along the bottom right, counted from the right.
    private const int BackButton = 0;
    private const int OpenButton = 1;

    private IReadOnlyList<SavedCitySummary> _cities = [];
    private int _first;
    private string? _problem;

    // The city picked, read once when it is picked and opened from there, and its stats, laid out then.
    private int _selected = -1;
    private CitySimulation? _picked;
    private IReadOnlyList<string> _pickedNotes = [];
    private readonly List<string> _stats = [];
    private readonly MapPreview _preview = new();

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

    /// <summary>What was read but not used, or what an import replaced.</summary>
    public IReadOnlyList<string> Notes { get; private set; } = [];

    /// <summary>Lists the cities in a folder afresh.</summary>
    public void Reset(string folder)
    {
        _cities = CityFiles.List(folder);
        _first = 0;
        _problem = null;
        City = null;
        Path = null;
        Notes = [];
        Unpick();
    }

    /// <summary>Lets go of the map. Call it before the window closes.</summary>
    public void Dispose() => _preview.Dispose();

    /// <summary>Tells whether the folder last listed holds any city.</summary>
    public bool HasCities => _cities.Count > 0;

    /// <summary>Takes this frame's keys, clicks, and scrolling.</summary>
    public Outcome Update()
    {
        // Leaving the panel lets go of the city picked and its map; they are read again when it opens.
        var outcome = Choose();
        if (outcome != Outcome.Open)
        {
            Unpick();
        }

        return outcome;
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
            var lit = Raylib.CheckCollisionPointRec(mouse, rowBounds) || _first + row == _selected;
            if (lit)
            {
                Raylib.DrawRectangleRec(rowBounds, Theme.PanelBorder);
            }

            Raylib.DrawText(city.Name, x + 6, (int)rowBounds.Y + 4, RowSize, lit ? Theme.Accent : Theme.Text);
            var flat = CityFiles.IsImport(city.Path) ? "   Micropolis city, to import"
                : Autosave.IsAutosave(city.Path) ? "   autosave"
                : System.IO.Path.GetExtension(city.Path).Equals(FlatCityFormat.Extension, StringComparison.OrdinalIgnoreCase) ? "   flat file"
                : string.Empty;
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

        var y = (int)bounds.Y + DetailsTop;
        Raylib.DrawLine(x, y - 4, x + Width - (Padding * 2), y - 4, Theme.PanelBorder);
        if (_stats.Count == 0)
        {
            Raylib.DrawText(_cities.Count > 0 ? "Pick a city to see it." : string.Empty, x, y + 4, SmallSize, Theme.TextDim);
        }

        for (var i = 0; i < _stats.Count; i++)
        {
            Raylib.DrawText(_stats[i], x, y + 4 + (i * StatsLineHeight), i == 0 ? FontSize : RowSize, i == 0 ? Theme.Text : Theme.TextDim);
        }

        _preview.Draw(new Rectangle { X = bounds.X + Width - Padding - PreviewWidth, Y = y + 2, Width = PreviewWidth, Height = PreviewHeight - 4 });
        DrawButton(ButtonBounds(OpenButton), "Open", _picked is not null, mouse);
        DrawButton(ButtonBounds(BackButton), "Back", enabled: true, mouse);
    }

    private static void DrawButton(Rectangle bounds, string text, bool enabled, Vector2 mouse)
    {
        Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
        var width = Raylib.MeasureText(text, FontSize);
        var color = !enabled ? Theme.TextDim : Raylib.CheckCollisionPointRec(mouse, bounds) ? Theme.Accent : Theme.Text;
        Raylib.DrawText(text, (int)(bounds.X + ((bounds.Width - width) / 2)), (int)bounds.Y + 5, FontSize, color);
    }

    private Outcome Choose()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Outcome.Back;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            return TryOpen();
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
        if (Raylib.CheckCollisionPointRec(mouse, ButtonBounds(BackButton)))
        {
            return Outcome.Back;
        }

        if (Raylib.CheckCollisionPointRec(mouse, ButtonBounds(OpenButton)))
        {
            return TryOpen();
        }

        for (var row = 0; row < Rows && _first + row < _cities.Count; row++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, RowBounds(row)))
            {
                // A second click on the city picked opens it.
                if (_first + row == _selected)
                {
                    return TryOpen();
                }

                Pick(_first + row);
                return Outcome.Open;
            }
        }

        return Outcome.Open;
    }

    // A city is read when it is picked, for its stats and its map, and opened from what was read.
    private void Pick(int index)
    {
        Unpick();
        _selected = index;
        var chosen = _cities[index];
        if (!CityFiles.TryRead(chosen.Path, out var city, out var problem, out var notes))
        {
            Logger.Warn("{File} couldn't be opened: {Problem}", chosen.Path, problem);
            _problem = $"{System.IO.Path.GetFileName(chosen.Path)}: {problem}";
            _preview.ShowNote("No map: the file can't be read");
            return;
        }

        (_picked, _pickedNotes) = (city, notes);
        _problem = null;
        _preview.Show(city);
        var date = city.Date;
        var age = date.Year - city.FoundedYear;
        _stats.Add(city.Name);
        _stats.Add($"{date.Month:00}/{date.Year:0000}, {(age == 0 ? "founded this year" : age == 1 ? "1 year old" : $"{age} years old")}");
        _stats.Add($"Population {Hud.Count(city.Census.CityPopulation)}");
        _stats.Add($"Funds {Hud.Money(city.Treasury.Funds)}, tax {city.Budget.TaxRate}%");
        _stats.Add($"Saved {chosen.SavedUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        if (city.Challenge is { } challenge)
        {
            _stats.Add($"Challenge: {challenge.Name}");
        }
    }

    private void Unpick()
    {
        _selected = -1;
        _picked = null;
        _pickedNotes = [];
        _stats.Clear();
        _preview.Clear();
    }

    private Outcome TryOpen()
    {
        if (_picked is null || _selected < 0)
        {
            return Outcome.Open;
        }

        var path = _cities[_selected].Path;
        foreach (var note in _pickedNotes)
        {
            Logger.Info("{File}: {Note}", path, note);
        }

        (City, Path, Notes) = (_picked, path, _pickedNotes);
        return Outcome.Opened;
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
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + ListTop + (row * RowHeight), Width = Width - (Padding * 2), Height = RowHeight - 2 };
    }

    // Open, then Back, along the bottom right; a button is counted from the right.
    private static Rectangle ButtonBounds(int button)
    {
        var bounds = Bounds();
        var right = bounds.X + Width - Padding - ButtonWidth;
        return new Rectangle { X = right - (button * (ButtonWidth + 8)), Y = bounds.Y + Height - Padding - ButtonHeight, Width = ButtonWidth, Height = ButtonHeight };
    }
}
