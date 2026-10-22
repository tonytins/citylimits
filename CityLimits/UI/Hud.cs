namespace CityLimits.UI;

/// <summary>Draws the bar along the top and the hints along the bottom.</summary>
public sealed class Hud
{
    /// <summary>The height of the top bar, in pixels.</summary>
    public const int BarHeight = 30;

    private const int FontSize = 14;
    private const int Padding = 10;
    private const int ReadoutHeight = 46;
    private const string Hints = "WASD pan   wheel zoom   space pause   , . speed   1-9 tools   drag to build   B budget   esc cancel or menu";

    // The pieces of the top bar, from left to right, as wide as each needs.
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

    /// <summary>A button in the top bar.</summary>
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
    }

    /// <summary>Tells which button of the top bar, as last drawn, is under a point on the screen.</summary>
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
    /// Draws the top bar: the pause or play button, the month, the speed, the city's name, the
    /// demand meter, the funds, and the population, with the step count and the frame rate on
    /// the right. The speed buttons light up to the speed, so two lit is Fast; while paused they
    /// show, dimmed, the speed play will bring back.
    /// </summary>
    public void DrawStatus(string cityName, GameDate date, long tickCount, int speed, int resumeSpeed, int population, CityDemand demand, int funds, int framesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(demand);

        var width = Raylib.GetScreenWidth();
        Raylib.DrawRectangle(0, 0, width, BarHeight, Theme.Bar);
        var textY = (BarHeight - FontSize) / 2;
        var x = Padding;

        _pauseBounds = new Rectangle { X = x, Y = (BarHeight - ButtonSize) / 2, Width = ButtonSize, Height = ButtonSize };
        DrawPausePlay(_pauseBounds, paused: speed == GameSpeed.Paused);
        x += ButtonSize + 10;

        var dateText = $"{date.Month:00}/{date.Year:0000}";
        Raylib.DrawText(dateText, x, textY, FontSize, Theme.Text);
        x += Raylib.MeasureText(dateText, FontSize) + 10;

        var shown = speed == GameSpeed.Paused ? resumeSpeed : speed;
        for (var i = 0; i < Speeds.Length; i++)
        {
            _speedBounds[i] = new Rectangle { X = x, Y = (BarHeight - ButtonSize) / 2, Width = SpeedWidth, Height = ButtonSize };
            var lit = Speeds[i] <= shown ? (speed == GameSpeed.Paused ? Theme.SpeedResume : Theme.SpeedLit) : Theme.SpeedUnlit;
            DrawArrow(_speedBounds[i], lit);
            x += SpeedWidth;
        }

        x += Gap;
        var nameWidth = Raylib.MeasureText(cityName, FontSize);
        _nameBounds = new Rectangle { X = x - 4, Y = 0, Width = nameWidth + 8, Height = BarHeight };
        Raylib.DrawText(cityName, x, textY, FontSize, Theme.Text);
        x += nameWidth + Gap;

        _demandBounds = new Rectangle { X = x - 3, Y = (BarHeight - MeterHeight) / 2, Width = (MeterBarWidth * 3) + 4 + 6, Height = MeterHeight };
        DrawDemandMeter(demand, x, (BarHeight - MeterHeight) / 2);
        x += (MeterBarWidth * 3) + 4 + Gap;

        // The funds are drawn on their own, so they can show red below zero and be clicked.
        var fundsText = Money(funds);
        var fundsWidth = Raylib.MeasureText(fundsText, FontSize);
        _fundsBounds = new Rectangle { X = x - 4, Y = 0, Width = fundsWidth + 8, Height = BarHeight };
        Raylib.DrawText(fundsText, x, textY, FontSize, funds < 0 ? Theme.Error : Theme.Accent);
        x += fundsWidth + Gap;

        Raylib.DrawText($"{Count(population)} Pop", x, textY, FontSize, Theme.Text);

        var right = $"{Count(tickCount)} Ticks   {framesPerSecond} FPS";
        Raylib.DrawText(right, width - Padding - Raylib.MeasureText(right, FontSize), textY, FontSize, Theme.TextDim);
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

    /// <summary>Draws the readout of the tile under the mouse, naming zones as the city's definitions do.</summary>
    public void DrawTileReadout(TileMap map, TileState? state, int tileX, int tileY, CityDefinitions definitions)
    {
        var heading = state is null ? "Outside the map" : $"Tile {tileX}, {tileY}";
        var detail = state is null ? "Move the pointer over the map" : TileInfo.Describe(map, tileX, tileY, definitions);

        var width = Math.Max(Raylib.MeasureText(heading, FontSize), Raylib.MeasureText(detail, FontSize)) + (Padding * 2);
        var x = Raylib.GetScreenWidth() - width - 8;
        var y = Raylib.GetScreenHeight() - 26 - ReadoutHeight - 6;

        Raylib.DrawRectangle(x, y, width, ReadoutHeight, Theme.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle { X = x, Y = y, Width = width, Height = ReadoutHeight }, 1f, Theme.PanelBorder);
        Raylib.DrawText(heading, x + Padding, y + 6, FontSize, Theme.Text);
        Raylib.DrawText(detail, x + Padding, y + 24, FontSize, AdviceColor(state));
    }

    /// <summary>Draws the line of hints that tells the player what the keys do.</summary>
    public void DrawHints()
    {
        var width = Raylib.MeasureText(Hints, FontSize);
        var y = Raylib.GetScreenHeight() - 26;

        Raylib.DrawRectangle(0, y - 6, width + (Padding * 2), 26, Theme.Panel);
        Raylib.DrawText(Hints, Padding, y, FontSize, Theme.TextDim);
    }

    /// <summary>Draws what the stroke being dragged would cost, beside the pointer, or why it can't be built.</summary>
    public void DrawStrokeCost(Vector2 mouse, BuildPlan plan)
    {
        var (text, color) = plan switch
        {
            { Refusal: BuildRefusal.OffTheMap } => ("Runs off the map", Theme.Error),
            { Refusal: BuildRefusal.NotEnoughFunds } => ($"${plan.Cost} - not enough money", Theme.Error),
            { Tiles.Count: 0 } => ("Nothing to do here", Theme.TextDim),
            _ => ($"${plan.Cost}", Theme.Accent),
        };

        var width = Raylib.MeasureText(text, FontSize) + (Padding * 2);
        var x = (int)mouse.X + 16;
        var y = (int)mouse.Y + 16;

        Raylib.DrawRectangle(x, y, width, 22, Theme.Panel);
        Raylib.DrawText(text, x + Padding, y + 4, FontSize, color);
    }

    /// <summary>Draws a message the player needs to know about, or nothing when there is none.</summary>
    public void DrawMessage(string? message, bool isError)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var width = Raylib.MeasureText(message, FontSize);
        Raylib.DrawRectangle(Raylib.GetScreenWidth() - width - (Padding * 2), BarHeight + 8, width + (Padding * 2), 24, Theme.Panel);
        Raylib.DrawText(message, Raylib.GetScreenWidth() - width - Padding, BarHeight + 12, FontSize, isError ? Theme.Error : Theme.Accent);
    }

    private static Color AdviceColor(TileState? state)
    {
        if (state is null)
        {
            return Theme.TextDim;
        }

        if (state.Value.IsRoad)
        {
            return Theme.TextDim;
        }

        if (state.Value.HasFootprint)
        {
            return Theme.Accent;
        }

        if (!TileRules.CanBuildOn(state.Value.Kind))
        {
            return Theme.Error;
        }

        return TileRules.NeedsClearing(state.Value.Kind) ? Theme.Accent : Theme.TextDim;
    }
}
