namespace CityLimits.UI;

/// <summary>Draws the bar along the top and the hints along the bottom.</summary>
public sealed class Hud
{
    /// <summary>The height of the top bar, in pixels.</summary>
    public const int BarHeight = 30;

    private const int FontSize = 14;
    private const int Padding = 10;
    private const int ReadoutHeight = 46;
    private const string Hints = "WASD pan   wheel zoom   space pause   , . speed   1-5 tools   esc quit";

    /// <summary>Draws the top bar with the state of the city.</summary>
    public void DrawStatus(GameDate date, long tickCount, int speed, Tool tool, Vector2 mouseWorld, int framesPerSecond)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), BarHeight, Theme.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle { X = 0, Y = 0, Width = Raylib.GetScreenWidth(), Height = BarHeight }, 1f, Theme.PanelBorder);

        var speedText = speed == 0 ? "paused" : $"{speed}x";
        var status = $"CityLimits   {date}   tick {tickCount}   {speedText}   tool {ToolName(tool)}   {mouseWorld.X:0}, {mouseWorld.Y:0}   {framesPerSecond} fps";
        Raylib.DrawText(status, Padding, 8, FontSize, Theme.Text);
    }

    /// <summary>Draws the readout of the tile under the mouse.</summary>
    public void DrawTileReadout(TileKind? kind, int tileX, int tileY)
    {
        var heading = kind is null ? "Outside the map" : $"Tile {tileX}, {tileY}";
        var detail = kind is null ? "Move the pointer over the map" : TileInfo.Describe(kind.Value);

        var width = Math.Max(Raylib.MeasureText(heading, FontSize), Raylib.MeasureText(detail, FontSize)) + (Padding * 2);
        var x = Raylib.GetScreenWidth() - width - 8;
        var y = Raylib.GetScreenHeight() - 26 - ReadoutHeight - 6;

        Raylib.DrawRectangle(x, y, width, ReadoutHeight, Theme.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle { X = x, Y = y, Width = width, Height = ReadoutHeight }, 1f, Theme.PanelBorder);
        Raylib.DrawText(heading, x + Padding, y + 6, FontSize, Theme.Text);
        Raylib.DrawText(detail, x + Padding, y + 24, FontSize, AdviceColor(kind));
    }

    /// <summary>Draws the line of hints that tells the player what the keys do.</summary>
    public void DrawHints()
    {
        var width = Raylib.MeasureText(Hints, FontSize);
        var y = Raylib.GetScreenHeight() - 26;

        Raylib.DrawRectangle(0, y - 6, width + (Padding * 2), 26, Theme.Panel);
        Raylib.DrawText(Hints, Padding, y, FontSize, Theme.TextDim);
    }

    /// <summary>Draws a failure the player needs to know about.</summary>
    public void DrawError(string message)
    {
        var width = Raylib.MeasureText(message, FontSize);
        Raylib.DrawRectangle(Raylib.GetScreenWidth() - width - (Padding * 2), BarHeight + 8, width + (Padding * 2), 24, Theme.Panel);
        Raylib.DrawText(message, Raylib.GetScreenWidth() - width - Padding, BarHeight + 12, FontSize, Theme.Error);
    }

    private static Color AdviceColor(TileKind? kind)
    {
        if (kind is null)
        {
            return Theme.TextDim;
        }

        if (!TileRules.CanBuildOn(kind.Value))
        {
            return Theme.Error;
        }

        return TileRules.NeedsClearing(kind.Value) ? Theme.Accent : Theme.TextDim;
    }

    private static string ToolName(Tool tool) => tool switch
    {
        Tool.Road => "road",
        Tool.Residential => "residential",
        Tool.Commercial => "commercial",
        Tool.Industrial => "industrial",
        _ => "inspect",
    };
}
