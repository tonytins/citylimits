namespace CityLimits.UI;

/// <summary>
/// A neighbour's offer, in a window of its own, as SimCity 3000's neighbours make theirs: who
/// offers, what power would go which way, what it would earn or cost a year, for how long, and
/// until when the offer stands. Accept, or Enter, takes it; Decline turns it down. Its close
/// button, or Escape, leaves it standing until it lapses. The player moves it by its title bar.
/// </summary>
public sealed class DealWindow : IMovableWindow
{
    private const int FontSize = 14;
    private const int Padding = 16;
    private const int Width = 440;
    private const int LineHeight = FontSize + 6;

    private static readonly string[] Buttons = ["Decline", "Accept"];

    private readonly WindowFrame _frame = new("A deal");
    private List<string> _lines = [];

    /// <summary>What a frame of the window came to.</summary>
    public enum Outcome
    {
        /// <summary>The window stays open.</summary>
        Open,

        /// <summary>The player took the deal.</summary>
        Accept,

        /// <summary>The player turned it down.</summary>
        Decline,

        /// <summary>The player closed the window without answering; the offer stands.</summary>
        Later,
    }

    /// <summary>Where the window is: in the middle of the screen until the player moves it.</summary>
    public Rectangle Bounds => _frame.Place(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Hud.BarTop() - Height) / 2));

    /// <inheritdoc />
    public bool IsDragging => _frame.IsDragging;

    private int Height => WindowFrame.TitleHeight + Padding + (_lines.Count * LineHeight) + Padding + ButtonRow.RowHeight + Padding;

    /// <inheritdoc />
    public bool BeginDrag(Vector2 point) => _frame.BeginDrag(point, Bounds);

    /// <inheritdoc />
    public void Drag(Vector2 point, bool held) => _frame.Drag(point, held, new Vector2(Width, Height));

    /// <summary>Shows an offer to the city, worded for it. Call it when the window opens, not every frame.</summary>
    public void Show(Offer offer, Neighbour neighbour, CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(neighbour);
        ArgumentNullException.ThrowIfNull(city);

        _frame.Title = $"A deal from {neighbour.Name}";
        var people = Hud.Count(neighbour.PopulationBeside(city.Census.CityPopulation));
        var side = CityNeighbours.SideName(neighbour.Side);
        var what = offer.Kind == DealKind.CityBuys
            ? $"{neighbour.Name} offers to sell {city.Name} {Hud.Count(offer.Power)} tiles of power, for {Hud.Money(offer.Money)} a year."
            : $"{neighbour.Name} offers to buy {Hud.Count(offer.Power)} tiles of {city.Name}'s spare power, for {Hud.Money(offer.Money)} a year.";
        var text = $"{neighbour.Name}, a city of {people} to the {side}, sends word. {what} "
            + $"The deal would run {offer.Years} years, while the power line to {neighbour.Name} stands. "
            + $"Breaking it would cost a year's money, and {neighbour.Name} wouldn't deal again for {CityNeighbours.GrudgeYears} years. "
            + $"The offer stands until {CityCalendar.DateAt(offer.ExpiresStep / CityCalendar.StepsPerCount)}.";
        _lines = TextWrap.Lines(text, FontSize, Width - (Padding * 2));
    }

    /// <summary>Takes this frame's keys and clicks, other than a drag by the title bar.</summary>
    public Outcome Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || _frame.Update(Bounds) == WindowFrame.Action.Close)
        {
            return Outcome.Later;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            return Outcome.Accept;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return Outcome.Open;
        }

        return ButtonRow.ButtonAt(Raylib.GetMousePosition(), Corner(), Buttons) switch
        {
            0 => Outcome.Decline,
            1 => Outcome.Accept,
            _ => Outcome.Open,
        };
    }

    /// <summary>Draws the window over a dimmed city.</summary>
    public void Draw()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds;
        _frame.Draw(bounds);
        var y = (int)bounds.Y + WindowFrame.TitleHeight + Padding;
        foreach (var line in _lines)
        {
            Raylib.DrawText(line, (int)bounds.X + Padding, y, FontSize, Theme.Text);
            y += LineHeight;
        }

        ButtonRow.Draw(Corner(), Buttons, Raylib.GetMousePosition());
    }

    private Vector2 Corner()
    {
        var bounds = Bounds;
        return new Vector2(bounds.X + Width - Padding, bounds.Y + Height - Padding);
    }
}
