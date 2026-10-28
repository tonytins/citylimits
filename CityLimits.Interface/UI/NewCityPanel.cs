namespace CityLimits.UI;

/// <summary>
/// The window New Game, and the Scenarios list's New, open: a name, and the seed the map grows
/// from, beside a map of what that seed grows, as the OLPC port's terrain editor shows it. It opens
/// on a seed of its own; typing another shows its map, and Generate picks another at random. A
/// blank seed leaves the game to pick one when it starts. Start, or Enter, begins; Back, or Escape,
/// returns, as its close button does. Tab or a click moves between the fields, and a problem with
/// either is shown under them. The player moves it by its title bar. The map is grown again only
/// when the seed changes, never in a frame.
/// </summary>
public sealed class NewCityPanel : IDisposable
{
    private const int FontSize = 20;
    private const int LabelSize = 10;
    private const int Padding = 16;
    private const int Width = 620;
    private const int Height = 320;
    private const int FieldWidth = 320;
    private const int PreviewWidth = 240;
    private const int PreviewHeight = 200;
    private const int GenerateWidth = 120;
    private const int SeedLength = 11;

    private static readonly string[] Buttons = ["Back", "Start"];

    private readonly TextField _name = new(CitySimulation.DefaultName, CitySimulation.MaxNameLength, _ => true);
    private readonly TextField _seed = new(string.Empty, SeedLength, c => char.IsAsciiDigit(c) || c == '-');
    private readonly MapPreview _preview = new();
    private readonly WindowFrame _frame = new("New city");
    private bool _seedFocused;
    private string? _problem;

    // The seed the map shown grew from, so typing the same one again grows nothing.
    private string? _shownSeed;

    /// <summary>What a frame of the panel came to.</summary>
    public enum Outcome
    {
        /// <summary>The player is still filling it in.</summary>
        Open,

        /// <summary>The player asked to start.</summary>
        Start,

        /// <summary>The player went back.</summary>
        Back,
    }

    /// <summary>The name and seed the player asked for, once the outcome is <see cref="Outcome.Start"/>.</summary>
    public NewCity? City { get; private set; }

    /// <summary>
    /// Clears the panel, titled as given, with the name given and the seed given on the command
    /// line, or one picked at random so there is a map to show. Call it after the window is open.
    /// </summary>
    public void Reset(int? seed, string title = "New city", string name = CitySimulation.DefaultName)
    {
        _frame.Title = title;
        _name.Text = name;
        _seed.Text = (seed ?? PickSeed()).ToString(CultureInfo.InvariantCulture);
        _seedFocused = false;
        _problem = null;
        _shownSeed = null;
        City = null;
        ShowSeed();
    }

    /// <summary>Takes this frame's typing and clicks.</summary>
    public Outcome Update()
    {
        // Leaving lets go of the map; it is grown again when the panel opens.
        var outcome = Choose();
        if (outcome != Outcome.Open)
        {
            _preview.Clear();
            _shownSeed = null;
        }

        return outcome;
    }

    /// <summary>Draws the panel in the middle of the screen.</summary>
    public void Draw()
    {
        var bounds = Bounds();
        _frame.Draw(bounds);
        var x = (int)bounds.X + Padding;

        var name = NameBounds();
        Raylib.DrawText("Name", x, (int)name.Y - LabelSize - 4, LabelSize, Theme.TextDim);
        _name.Draw(name, !_seedFocused);

        var seed = SeedBounds();
        Raylib.DrawText("Seed, the number the map grows from: blank for any", x, (int)seed.Y - LabelSize - 4, LabelSize, Theme.TextDim);
        _seed.Draw(seed, _seedFocused);

        var mouse = Raylib.GetMousePosition();
        var generate = GenerateBounds();
        Raylib.DrawRectangleRec(generate, Theme.PanelBorder);
        Raylib.DrawText("Generate", (int)generate.X + 14, (int)generate.Y + 5, FontSize, Raylib.CheckCollisionPointRec(mouse, generate) ? Theme.Accent : Theme.Text);

        if (_problem is not null)
        {
            Raylib.DrawText(_problem, x, (int)(generate.Y + generate.Height) + 10, LabelSize, Theme.Error);
        }

        _preview.Draw(PreviewBounds());
        ButtonRow.Draw(Corner(), Buttons, mouse);
    }

    /// <summary>Lets go of the map. Call it before the window closes.</summary>
    public void Dispose() => _preview.Dispose();

    private Outcome Choose()
    {
        var mouse = Raylib.GetMousePosition();
        if (_frame.FollowPointer(Bounds()))
        {
            return Outcome.Open;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || _frame.Update(Bounds()) == WindowFrame.Action.Close)
        {
            return Outcome.Back;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _seedFocused = !_seedFocused;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            switch (ButtonRow.ButtonAt(mouse, Corner(), Buttons))
            {
                case 0:
                    return Outcome.Back;
                case 1:
                    return TryStart();
            }

            if (Raylib.CheckCollisionPointRec(mouse, NameBounds()))
            {
                _seedFocused = false;
            }
            else if (Raylib.CheckCollisionPointRec(mouse, SeedBounds()))
            {
                _seedFocused = true;
            }
            else if (Raylib.CheckCollisionPointRec(mouse, GenerateBounds()))
            {
                _seed.Text = PickSeed().ToString(CultureInfo.InvariantCulture);
                _problem = null;
                ShowSeed();
            }
        }

        // A problem stays until the player changes what it was about.
        if ((_seedFocused ? _seed : _name).Update())
        {
            _problem = null;
            if (_seedFocused)
            {
                ShowSeed();
            }
        }

        return Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) ? TryStart() : Outcome.Open;
    }

    // The map the seed in the field grows, at the size a new city has; a blank or unreadable seed
    // shows why there is no map.
    private void ShowSeed()
    {
        var text = _seed.Text.Trim();
        if (text == _shownSeed)
        {
            return;
        }

        _shownSeed = text;
        if (text.Length == 0)
        {
            _preview.ShowNote("A seed is picked when you start");
        }
        else if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed))
        {
            _preview.Show(TileMapGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight));
        }
        else
        {
            _preview.ShowNote("No map: the seed is a whole number");
        }
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

    // Choosing a seed is outside any city, so the shared generator may pick it; the city logs it.
    private static int PickSeed() => Random.Shared.Next();

    private Rectangle Bounds() => _frame.Place(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Raylib.GetScreenHeight() - Height) / 2));

    private Vector2 Corner()
    {
        var bounds = Bounds();
        return new Vector2(bounds.X + Width - Padding, bounds.Y + Height - Padding);
    }

    private Rectangle NameBounds()
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + Padding + FontSize + 28, Width = FieldWidth, Height = TextField.Height };
    }

    private Rectangle SeedBounds()
    {
        var name = NameBounds();
        return name with { Y = name.Y + name.Height + 30 };
    }

    private Rectangle GenerateBounds()
    {
        var seed = SeedBounds();
        return new Rectangle { X = seed.X, Y = seed.Y + seed.Height + 10, Width = GenerateWidth, Height = 30 };
    }

    private Rectangle PreviewBounds()
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Width - Padding - PreviewWidth, Y = bounds.Y + Padding + FontSize + 14, Width = PreviewWidth, Height = PreviewHeight };
    }
}
