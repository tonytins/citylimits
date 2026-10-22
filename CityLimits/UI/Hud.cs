namespace CityLimits.UI;

/// <summary>
/// Draws the bar along the bottom, over the news ticker, after Tony Bark's mock-up in
/// <c>docs/design/</c>, and the hints beside the pointer.
/// </summary>
public sealed class Hud
{
    /// <summary>The height of the bar, in pixels.</summary>
    public const int BarHeight = 30;

    /// <summary>The height of everything along the bottom of the screen: the bar and the news ticker under it.</summary>
    public const int BottomHeight = BarHeight + NewsTicker.Height;

    private const int FontSize = 14;
    private const int Padding = 10;
    private const int HelpSize = 18;
    private const int HintLine = 18;
    private const int HintMargin = 4;

    // The pieces of the bar, from left to right, as wide as each needs.
    private const int ButtonSize = 22;
    private const int SpeedWidth = 16;
    private const int Gap = 16;
    private const int MeterBarWidth = 6;
    private const int MeterHeight = 22;

    private static readonly int[] Speeds = [GameSpeed.Normal, GameSpeed.Fast, GameSpeed.SuperFast];

    // Where each button was last drawn, so a click can be matched to it in the next frame.
    private Rectangle _pauseBounds;
    private readonly Rectangle[] _speedBounds = new Rectangle[3];
    private Rectangle _nameBounds;
    private Rectangle _demandBounds;
    private Rectangle _fundsBounds;
    private Rectangle _helpBounds;

    /// <summary>A button in the bar.</summary>
    public enum BarButton
    {
        /// <summary>No button.</summary>
        None,

        /// <summary>Pauses the game, or plays it at the speed it last ran.</summary>
        PausePlay,

        /// <summary>Plays at Normal speed.</summary>
        Normal,

        /// <summary>Plays at Fast speed.</summary>
        Fast,

        /// <summary>Plays at Super fast speed.</summary>
        SuperFast,

        /// <summary>The city's name, which opens the panel to rename it.</summary>
        CityName,

        /// <summary>The demand meter, which opens the demand panel.</summary>
        Demand,

        /// <summary>The funds, which open the budget.</summary>
        Funds,

        /// <summary>The green question mark, which opens and closes the help window.</summary>
        Help,
    }

    /// <summary>Where the bar's top edge is, on a screen of the current size; the news ticker sits under the bar.</summary>
    public static int BarTop() => Raylib.GetScreenHeight() - BottomHeight;

    /// <summary>Tells whether a point on the screen is on the bar or the ticker, where a press never reaches the map.</summary>
    public static bool IsOnBottom(Vector2 point) => point.Y >= BarTop();

    /// <summary>Tells which button of the bar, as last drawn, is under a point on the screen.</summary>
    public BarButton ButtonAt(Vector2 point)
    {
        if (Raylib.CheckCollisionPointRec(point, _pauseBounds))
        {
            return BarButton.PausePlay;
        }

        for (var i = 0; i < _speedBounds.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, _speedBounds[i]))
            {
                return BarButton.Normal + i;
            }
        }

        if (Raylib.CheckCollisionPointRec(point, _nameBounds))
        {
            return BarButton.CityName;
        }

        if (Raylib.CheckCollisionPointRec(point, _demandBounds))
        {
            return BarButton.Demand;
        }

        if (Raylib.CheckCollisionPointRec(point, _helpBounds))
        {
            return BarButton.Help;
        }

        return Raylib.CheckCollisionPointRec(point, _fundsBounds) ? BarButton.Funds : BarButton.None;
    }

    /// <summary>The speed a speed button plays at; nothing for any other button.</summary>
    public static int? SpeedOf(BarButton button) => button switch
    {
        BarButton.Normal => GameSpeed.Normal,
        BarButton.Fast => GameSpeed.Fast,
        BarButton.SuperFast => GameSpeed.SuperFast,
        _ => null,
    };

    /// <summary>
    /// Draws the bar: the pause or play button, the month, the speed, the city's name, the
    /// demand meter, the funds, and the population, with the step count, the frame rate, and the
    /// help button on the right. The speed buttons light up to the speed, so two lit is Fast; while paused they
    /// show, dimmed, the speed play will bring back.
    /// </summary>
    public void DrawStatus(string cityName, GameDate date, long tickCount, int speed, int resumeSpeed, int population, CityDemand demand, int funds, int framesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(demand);

        var width = Raylib.GetScreenWidth();
        var top = BarTop();
        Raylib.DrawRectangle(0, top, width, BarHeight, Theme.Bar);
        var textY = top + ((BarHeight - FontSize) / 2);
        var x = Padding;

        _pauseBounds = new Rectangle { X = x, Y = top + ((BarHeight - ButtonSize) / 2), Width = ButtonSize, Height = ButtonSize };
        DrawPausePlay(_pauseBounds, paused: speed == GameSpeed.Paused);
        x += ButtonSize + 10;

        var dateText = $"{date.Month:00}/{date.Year:0000}";
        Raylib.DrawText(dateText, x, textY, FontSize, Theme.Text);
        x += Raylib.MeasureText(dateText, FontSize) + 10;

        var shown = speed == GameSpeed.Paused ? resumeSpeed : speed;
        for (var i = 0; i < Speeds.Length; i++)
        {
            _speedBounds[i] = new Rectangle { X = x, Y = top + ((BarHeight - ButtonSize) / 2), Width = SpeedWidth, Height = ButtonSize };
            var lit = Speeds[i] <= shown ? (speed == GameSpeed.Paused ? Theme.SpeedResume : Theme.SpeedLit) : Theme.SpeedUnlit;
            DrawArrow(_speedBounds[i], lit);
            x += SpeedWidth;
        }

        x += Gap;
        var nameWidth = Raylib.MeasureText(cityName, FontSize);
        _nameBounds = new Rectangle { X = x - 4, Y = top, Width = nameWidth + 8, Height = BarHeight };
        Raylib.DrawText(cityName, x, textY, FontSize, Theme.Text);
        x += nameWidth + Gap;

        _demandBounds = new Rectangle { X = x - 3, Y = top + ((BarHeight - MeterHeight) / 2), Width = (MeterBarWidth * 3) + 4 + 6, Height = MeterHeight };
        DrawDemandMeter(demand, x, top + ((BarHeight - MeterHeight) / 2));
        x += (MeterBarWidth * 3) + 4 + Gap;

        // The funds are drawn on their own, so they can show red below zero and be clicked.
        var fundsText = Money(funds);
        var fundsWidth = Raylib.MeasureText(fundsText, FontSize);
        _fundsBounds = new Rectangle { X = x - 4, Y = top, Width = fundsWidth + 8, Height = BarHeight };
        Raylib.DrawText(fundsText, x, textY, FontSize, funds < 0 ? Theme.Error : Theme.Accent);
        x += fundsWidth + Gap;

        Raylib.DrawText($"{Count(population)} Pop", x, textY, FontSize, Theme.Text);

        // The help button sits at the far right, after the frame rate, as the mock-up has it.
        _helpBounds = new Rectangle { X = width - Padding - HelpSize, Y = top + ((BarHeight - HelpSize) / 2), Width = HelpSize, Height = HelpSize };
        DrawHelpButton(_helpBounds);

        var right = $"{Count(tickCount)} Ticks   {framesPerSecond} FPS";
        Raylib.DrawText(right, (int)_helpBounds.X - 10 - Raylib.MeasureText(right, FontSize), textY, FontSize, Theme.TextDim);
    }

    // A green circle with a dark question mark.
    private static void DrawHelpButton(Rectangle bounds)
    {
        var radius = bounds.Width / 2;
        Raylib.DrawCircleV(new Vector2(bounds.X + radius, bounds.Y + radius), radius, Theme.HelpButton);
        var mark = Raylib.MeasureText("?", FontSize);
        Raylib.DrawText("?", (int)(bounds.X + radius - (mark / 2f)), (int)(bounds.Y + ((bounds.Height - FontSize) / 2) + 1), FontSize, Theme.HintText);
    }

    /// <summary>Writes an amount of money as the bar and the panels show it, such as $20,000 or -$1,500.</summary>
    public static string Money(long amount) => amount < 0 ? $"-${Count(-amount)}" : $"${Count(amount)}";

    /// <summary>Writes a count with its thousands separated, such as 12,340, whatever the machine's language.</summary>
    public static string Count(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

    // Blue, as a lit speed is, so the button doubles as a sign the city is running.
    private static void DrawPausePlay(Rectangle bounds, bool paused)
    {
        var cx = bounds.X + (bounds.Width / 2);
        var cy = bounds.Y + (bounds.Height / 2);
        if (paused)
        {
            // Play: a triangle pointing right.
            Raylib.DrawTriangle(new Vector2(cx - 5, cy - 7), new Vector2(cx - 5, cy + 7), new Vector2(cx + 7, cy), Theme.SpeedLit);
            return;
        }

        Raylib.DrawRectangleRec(new Rectangle { X = cx - 6, Y = cy - 7, Width = 4, Height = 14 }, Theme.SpeedLit);
        Raylib.DrawRectangleRec(new Rectangle { X = cx + 2, Y = cy - 7, Width = 4, Height = 14 }, Theme.SpeedLit);
    }

    // A triangle pointing right, as the speed buttons are drawn; raylib wants its corners anticlockwise.
    private static void DrawArrow(Rectangle bounds, Color color)
    {
        var cy = bounds.Y + (bounds.Height / 2);
        Raylib.DrawTriangle(new Vector2(bounds.X + 2, cy - 6), new Vector2(bounds.X + 2, cy + 6), new Vector2(bounds.X + bounds.Width - 3, cy), color);
    }

    // As Micropolis shows demand: each bar rises from the middle when the city wants more of the
    // kind and falls below it when it wants less, as far as its limit allows.
    private static void DrawDemandMeter(CityDemand demand, int left, int top)
    {
        var middle = top + (MeterHeight / 2);
        var reach = MeterHeight / 2;
        Raylib.DrawLine(left - 2, middle, left + (MeterBarWidth * 3) + 6, middle, Theme.TextDim);
        DrawMeterBar(demand.Residential, CityDemand.ResidentialLimit, left, middle, reach, Theme.Residential);
        DrawMeterBar(demand.Commercial, CityDemand.BusinessLimit, left + MeterBarWidth + 2, middle, reach, Theme.Commercial);
        DrawMeterBar(demand.Industrial, CityDemand.BusinessLimit, left + ((MeterBarWidth + 2) * 2), middle, reach, Theme.Industrial);
    }

    private static void DrawMeterBar(int value, int limit, int left, int middle, int reach, Color color)
    {
        var length = (int)Math.Round((double)Math.Clamp(value, -limit, limit) / limit * reach);
        var top = length >= 0 ? middle - length : middle;
        Raylib.DrawRectangle(left, top, MeterBarWidth, Math.Abs(length), color);
    }

    /// <summary>What the stroke being dragged would cost, or why it can't be built, as the pointer's hint shows it.</summary>
    public static string StrokeHint(BuildPlan plan) => plan switch
    {
        { Refusal: BuildRefusal.OffTheMap } => "Runs off the map",
        { Refusal: BuildRefusal.NotEnoughFunds } => $"${plan.Cost} - not enough money",
        { Tiles.Count: 0 } => "Nothing to do here",
        _ => $"${plan.Cost}",
    };

    /// <summary>
    /// Draws a hint beside the pointer, in black on yellow, of one line or two: what a stroke would
    /// cost while it is dragged, what the work came to, or why it was refused, once it is let go,
    /// and, with the Inspect tool, what the tile under the pointer is. It stays on the screen,
    /// moving left or up from the pointer when the edge is near.
    /// </summary>
    public static void DrawPointerHint(Vector2 mouse, string hint, string? second = null)
    {
        if (string.IsNullOrEmpty(hint))
        {
            return;
        }

        var lines = string.IsNullOrEmpty(second) ? 1 : 2;
        var textWidth = Raylib.MeasureText(hint, FontSize);
        if (lines == 2)
        {
            textWidth = Math.Max(textWidth, Raylib.MeasureText(second, FontSize));
        }

        var width = textWidth + (Padding * 2);
        var height = (lines * HintLine) + (HintMargin * 2);
        var x = (int)mouse.X + 16;
        var y = (int)mouse.Y + 16;
        if (x + width > Raylib.GetScreenWidth())
        {
            x = (int)mouse.X - 8 - width;
        }

        if (y + height > BarTop())
        {
            y = (int)mouse.Y - 8 - height;
        }

        Raylib.DrawRectangle(x, y, width, height, Theme.HintBackground);
        var textY = y + HintMargin + ((HintLine - FontSize) / 2);
        Raylib.DrawText(hint, x + Padding, textY, FontSize, Theme.HintText);
        if (lines == 2)
        {
            Raylib.DrawText(second!, x + Padding, textY + HintLine, FontSize, Theme.HintText);
        }
    }
}
