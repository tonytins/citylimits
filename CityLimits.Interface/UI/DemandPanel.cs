namespace CityLimits.UI;

/// <summary>
/// The demand panel, opened from the meter in the bar. It charts residential, commercial, and
/// industrial demand over the history the city keeps, each line rising and falling against its own
/// limit. Under the chart are the demand now, what moved it at the last update, and the census
/// behind it. Micropolis has only the meter; the panel is a liberty the owner chose, after later city
/// builders. It shows what the simulation already works out, the game keeps running while it is
/// open, and the player moves it by its title.
/// </summary>
public sealed class DemandPanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int SmallSize = 10;
    private const int TitleSize = 20;
    private const int Padding = 12;
    private const int Width = 360;
    private const int Height = 312;
    private const int ChartHeight = 120;
    private const int CloseSize = 20;
    private const int TitleStrip = Padding + TitleSize + 6;

    private readonly WindowPlacement _placement = new(TitleStrip);

    /// <summary>Tells whether the player is dragging the panel by its title.</summary>
    public bool IsDragging => _placement.IsDragging;

    /// <summary>Where the panel's top sits until the player moves it: under the palette, as the game lays it out.</summary>
    public int DefaultTop { get; set; }

    /// <summary>Where the panel is on the screen.</summary>
    public Rectangle Bounds => _placement.Bounds(new Vector2(Width, Height), new Vector2(8, DefaultTop));

    /// <summary>Tells whether a point on the screen is on the panel's close button.</summary>
    public bool IsOnClose(Vector2 point) => Raylib.CheckCollisionPointRec(point, CloseBounds(Bounds));

    /// <summary>Starts moving the panel when a press lands on its title. Tells whether it did.</summary>
    public bool BeginDrag(Vector2 point) => _placement.BeginDrag(point, Bounds);

    /// <summary>Moves the panel with the pointer while the button is held.</summary>
    public void Drag(Vector2 point, bool held) => _placement.Drag(point, held, new Vector2(Width, Height));

    /// <summary>Draws the panel where it is.</summary>
    public void Draw(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);

        var bounds = Bounds;
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + Padding;
        Raylib.DrawText("Demand", x, y, TitleSize, Theme.Text);
        var close = CloseBounds(bounds);
        Raylib.DrawText("x", (int)close.X + 6, (int)close.Y + 2, FontSize, Theme.TextDim);
        y = (int)bounds.Y + TitleStrip + 4;

        var demand = city.Demand;
        var chart = new Rectangle { X = x, Y = y, Width = Width - (Padding * 2), Height = ChartHeight };
        DrawChart(demand, chart);
        y += ChartHeight + 4;
        Raylib.DrawText(Span(demand.HistoryCount), x, y, SmallSize, Theme.TextDim);
        y += SmallSize + 10;

        x = DrawLevel("R", demand.Residential, Theme.Residential, x, y);
        x = DrawLevel("C", demand.Commercial, Theme.Commercial, x, y);
        DrawLevel("I", demand.Industrial, Theme.Industrial, x, y);
        x = (int)bounds.X + Padding;
        y += FontSize + 8;

        Raylib.DrawText(
            $"Last move from the economy: R {Signed(demand.ResidentialPull)}, C {Signed(demand.CommercialPull)}, I {Signed(demand.IndustrialPull)}",
            x,
            y,
            SmallSize,
            Theme.TextDim);
        y += SmallSize + 6;
        Raylib.DrawText($"Taxes at {city.Budget.TaxRate}% push every demand by {Signed(demand.TaxPushApplied)}", x, y, SmallSize, Theme.TextDim);
        y += SmallSize + 6;
        Raylib.DrawText($"Jobs per resident {demand.JobsPerResident:0.00}   Workers per job {demand.WorkersPerJob:0.00}", x, y, SmallSize, Theme.TextDim);
        y += SmallSize + 10;

        var census = city.Census;
        Raylib.DrawText($"Population {Hud.Count(census.CityPopulation)}", x, y, FontSize, Theme.Text);
        y += FontSize + 6;
        Raylib.DrawText(
            $"Residents {Hud.Count(census.ResidentPopulation)}   Commerce {Hud.Count(census.CommercialPopulation)}   Industry {Hud.Count(census.IndustrialPopulation)}",
            x,
            y,
            SmallSize,
            Theme.TextDim);
    }

    // Three plain lines, oldest on the left and newest on the right, each against its own limit,
    // so the middle line is no demand either way.
    private static void DrawChart(CityDemand demand, Rectangle chart)
    {
        Raylib.DrawRectangleRec(chart, Theme.Bar);
        var middle = chart.Y + (chart.Height / 2);
        Raylib.DrawLineV(new Vector2(chart.X, middle), new Vector2(chart.X + chart.Width, middle), Theme.PanelBorder);

        var count = demand.HistoryCount;
        if (count < 2)
        {
            const string Waiting = "The chart starts after the next update";
            Raylib.DrawText(Waiting, (int)(chart.X + ((chart.Width - Raylib.MeasureText(Waiting, SmallSize)) / 2)), (int)middle - SmallSize - 4, SmallSize, Theme.TextDim);
            return;
        }

        DrawLine(chart, demand, TileUse.Residential, CityDemand.ResidentialLimit, Theme.Residential);
        DrawLine(chart, demand, TileUse.Commercial, CityDemand.BusinessLimit, Theme.Commercial);
        DrawLine(chart, demand, TileUse.Industrial, CityDemand.BusinessLimit, Theme.Industrial);
    }

    private static void DrawLine(Rectangle chart, CityDemand demand, TileUse kind, int limit, Color color)
    {
        var count = demand.HistoryCount;
        var step = chart.Width / (count - 1);
        var previous = Point(chart, 0, History(demand, kind, count - 1), limit);
        for (var ago = count - 2; ago >= 0; ago--)
        {
            var next = Point(chart, (count - 1 - ago) * step, History(demand, kind, ago), limit);
            Raylib.DrawLineEx(previous, next, 2f, color);
            previous = next;
        }
    }

    // Read straight from the demand rather than through a delegate, so drawing allocates nothing.
    private static int History(CityDemand demand, TileUse kind, int ago) => kind switch
    {
        TileUse.Residential => demand.ResidentialHistory(ago),
        TileUse.Commercial => demand.CommercialHistory(ago),
        _ => demand.IndustrialHistory(ago),
    };

    private static Vector2 Point(Rectangle chart, float across, int value, int limit)
    {
        var share = (float)Math.Clamp(value, -limit, limit) / limit;
        return new Vector2(chart.X + across, chart.Y + (chart.Height / 2) - (share * ((chart.Height / 2) - 2)));
    }

    // How far back the chart goes: two updates a month.
    private static string Span(int updates)
    {
        var months = Math.Max(updates - 1, 0) / 2;
        return months switch
        {
            < 1 => "Under a month of history",
            1 => "The last month",
            < 24 => $"The last {months} months",
            _ => $"The last {months / 12} years",
        };
    }

    // A demand's letter and level in its colour. Returns where the next one starts.
    private static int DrawLevel(string letter, int value, Color color, int x, int y)
    {
        var text = $"{letter} {Signed(value)}";
        Raylib.DrawText(text, x, y, FontSize, color);
        return x + Raylib.MeasureText(text, FontSize) + 24;
    }

    private static string Signed(int value) => value > 0 ? $"+{Hud.Count(value)}" : Hud.Count(value);

    private static Rectangle CloseBounds(Rectangle bounds) => new() { X = bounds.X + Width - CloseSize - 6, Y = bounds.Y + 6, Width = CloseSize, Height = CloseSize };
}
