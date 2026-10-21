namespace CityLimits.UI;

/// <summary>
/// The demand panel, opened from the meter in the top bar: each demand against its limit, what
/// moved it at the last update (the economy and the tax), and the census behind it. Micropolis
/// has only the meter; the panel is a liberty the owner chose, after later city builders. It
/// shows what the simulation already works out, and the game keeps running while it is open.
/// </summary>
public sealed class DemandPanel
{
    private const int FontSize = 14;
    private const int SmallSize = 10;
    private const int TitleSize = 20;
    private const int Padding = 12;
    private const int Width = 360;
    private const int Height = 278;
    private const int BarHeight = 8;
    private const int CloseSize = 20;

    /// <summary>Where the panel is on the screen, with its top at the given height.</summary>
    public static Rectangle Bounds(int top) => new() { X = 8, Y = top, Width = Width, Height = Height };

    /// <summary>Tells whether a point on the screen is on the panel's close button.</summary>
    public static bool IsOnClose(Vector2 point, int top) => Raylib.CheckCollisionPointRec(point, CloseBounds(top));

    /// <summary>Draws the panel with its top at the given height.</summary>
    public static void Draw(CitySimulation city, int top)
    {
        ArgumentNullException.ThrowIfNull(city);

        var bounds = Bounds(top);
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        var x = (int)bounds.X + Padding;
        var y = top + Padding;
        Raylib.DrawText("Demand", x, y, TitleSize, Theme.Text);
        var close = CloseBounds(top);
        Raylib.DrawText("x", (int)close.X + 6, (int)close.Y + 2, FontSize, Theme.TextDim);
        y += TitleSize + 10;

        var demand = city.Demand;
        y = DrawDemand("Residential", demand.Residential, CityDemand.ResidentialLimit, demand.ResidentialPull, demand.TaxPushApplied, Theme.Residential, x, y);
        y = DrawDemand("Commercial", demand.Commercial, CityDemand.BusinessLimit, demand.CommercialPull, demand.TaxPushApplied, Theme.Commercial, x, y);
        y = DrawDemand("Industrial", demand.Industrial, CityDemand.BusinessLimit, demand.IndustrialPull, demand.TaxPushApplied, Theme.Industrial, x, y);

        y += 4;
        Raylib.DrawText($"Jobs per resident {demand.JobsPerResident:0.00}   Workers per job {demand.WorkersPerJob:0.00}", x, y, SmallSize, Theme.TextDim);
        y += SmallSize + 6;
        Raylib.DrawText($"Taxes at {city.Budget.TaxRate}% push every demand by {Signed(demand.TaxPushApplied)}", x, y, SmallSize, Theme.TextDim);
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

    // One demand: its name and level, a bar from the middle out to its limit either way, and what
    // moved it at the last update. Returns where the next one starts.
    private static int DrawDemand(string name, int value, int limit, int pull, int taxPush, Color color, int x, int y)
    {
        var level = $"{Signed(value)} of {Hud.Count(limit)}";
        Raylib.DrawText(name, x, y, FontSize, Theme.Text);
        Raylib.DrawText(level, x + Width - (Padding * 2) - Raylib.MeasureText(level, FontSize), y, FontSize, value < 0 ? Theme.Error : Theme.Accent);
        y += FontSize + 4;

        var barWidth = Width - (Padding * 2);
        var middle = x + (barWidth / 2);
        Raylib.DrawRectangle(x, y, barWidth, BarHeight, Theme.Bar);
        var length = (int)Math.Round((double)Math.Clamp(value, -limit, limit) / limit * (barWidth / 2));
        Raylib.DrawRectangle(length >= 0 ? middle : middle + length, y, Math.Abs(length), BarHeight, color);
        Raylib.DrawLine(middle, y - 2, middle, y + BarHeight + 2, Theme.TextDim);
        y += BarHeight + 4;

        Raylib.DrawText($"Last move: economy {Signed(pull)}, taxes {Signed(taxPush)}", x, y, SmallSize, Theme.TextDim);
        return y + SmallSize + 10;
    }

    private static string Signed(int value) => value > 0 ? $"+{Hud.Count(value)}" : Hud.Count(value);

    private static Rectangle CloseBounds(int top) => new() { X = 8 + Width - CloseSize - 6, Y = top + 6, Width = CloseSize, Height = CloseSize };
}
