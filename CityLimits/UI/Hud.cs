namespace CityLimits.UI;

/// <summary>Draws the bar along the top and the hints along the bottom.</summary>
public sealed class Hud
{
    /// <summary>The height of the top bar, in pixels.</summary>
    public const int BarHeight = 30;

    private const int FontSize = 14;
    private const int Padding = 10;
    private const int ReadoutHeight = 46;
    private const string Hints = "WASD pan   wheel zoom   space pause   , . speed   1-6 tools   drag to build   esc cancel or quit";

    /// <summary>Draws the top bar with the state of the city.</summary>
    public void DrawStatus(GameDate date, long tickCount, int speed, Tool tool, int funds, int toolCost, Vector2 mouseWorld, int framesPerSecond)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), BarHeight, Theme.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle { X = 0, Y = 0, Width = Raylib.GetScreenWidth(), Height = BarHeight }, 1f, Theme.PanelBorder);

        var speedText = speed == 0 ? "paused" : $"{speed}x";
        var costText = toolCost > 0 ? $" ${toolCost}" : string.Empty;
        var status = $"CityLimits   {date}   tick {tickCount}   {speedText}   ${funds}   {ToolName(tool)}{costText}   {mouseWorld.X:0}, {mouseWorld.Y:0}   {framesPerSecond} fps";
        Raylib.DrawText(status, Padding, 8, FontSize, Theme.Text);
    }

    /// <summary>Draws the readout of the tile under the mouse.</summary>
    public void DrawTileReadout(TileState? state, int tileX, int tileY)
    {
        var heading = state is null ? "Outside the map" : $"Tile {tileX}, {tileY}";
        var detail = state is null ? "Move the pointer over the map" : TileInfo.Describe(state.Value);

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
            { Refund: > 0 } => ($"${plan.Refund} back", Theme.Accent),
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

        if (state.Value.IsZoned)
        {
            return Theme.Accent;
        }

        if (!TileRules.CanBuildOn(state.Value.Kind))
        {
            return Theme.Error;
        }

        return TileRules.NeedsClearing(state.Value.Kind) ? Theme.Accent : Theme.TextDim;
    }

    private static string ToolName(Tool tool) => tool switch
    {
        Tool.Road => "road",
        Tool.Residential => "residential",
        Tool.Commercial => "commercial",
        Tool.Industrial => "industrial",
        Tool.Bulldozer => "bulldozer",
        _ => "inspect",
    };
}
