namespace CityLimits.UI;

/// <summary>Draws the palette of tools and the keys that pick them.</summary>
public sealed class ToolPaletteView
{
    /// <summary>The keys that pick a tool, in palette order.</summary>
    public static readonly KeyboardKey[] ToolKeys =
    [
        KeyboardKey.One,
        KeyboardKey.Two,
        KeyboardKey.Three,
        KeyboardKey.Four,
        KeyboardKey.Five,
        KeyboardKey.Six,
    ];

    private const int FontSize = 14;
    private const int RowHeight = 28;
    private const int IconSize = 24;
    private const int Padding = 8;
    private const int Top = 46;
    private const int LabelLeft = 38;
    private const int CostGap = 16;

    private readonly GameAssets? _assets;

    /// <summary>Creates a palette that draws the icons it has.</summary>
    public ToolPaletteView(GameAssets? assets) => _assets = assets;

    /// <summary>Returns the tool a key picks, or null when the key is not a tool key.</summary>
    public static Tool? ToolForKey(KeyboardKey key) => key switch
    {
        KeyboardKey.One => Tool.Inspect,
        KeyboardKey.Two => Tool.Road,
        KeyboardKey.Three => Tool.Residential,
        KeyboardKey.Four => Tool.Commercial,
        KeyboardKey.Five => Tool.Industrial,
        KeyboardKey.Six => Tool.Bulldozer,
        _ => null,
    };

    /// <summary>Tells whether a point on the screen is over the palette, so a click there isn't a click on the map.</summary>
    public static bool Contains(ToolPalette palette, Vector2 point) => Raylib.CheckCollisionPointRec(point, Bounds(palette));

    /// <summary>The tool under a point on the screen, or null when the point isn't on one of the palette's rows.</summary>
    public static Tool? ToolAt(ToolPalette palette, Vector2 point)
    {
        if (!Contains(palette, point))
        {
            return null;
        }

        var row = (int)((point.Y - Top - Padding) / RowHeight);
        return row >= 0 && row < palette.Tools.Count ? palette.Tools[row] : null;
    }

    /// <summary>Draws the palette down the left edge of the screen.</summary>
    public void Draw(ToolPalette palette)
    {
        var bounds = Bounds(palette);
        var width = (int)bounds.Width;

        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        for (var i = 0; i < palette.Tools.Count; i++)
        {
            var tool = palette.Tools[i];
            var selected = tool == palette.Selected;
            var y = Top + Padding + (i * RowHeight);

            if (selected)
            {
                Raylib.DrawRectangle(Padding + 2, y - 2, width - 4, RowHeight, Theme.PanelBorder);
            }

            var icon = _assets?.IconFor(tool);
            if (icon is not null)
            {
                DrawIcon(icon.Value, Padding + 6, y + 2, IconSize);
            }

            Raylib.DrawText(Label(i, tool), Padding + LabelLeft, y + 7, FontSize, selected ? Theme.Accent : Theme.TextDim);

            var cost = CostOf(tool);
            if (cost > 0)
            {
                var text = $"${cost}";
                var x = Padding + width - Padding - Raylib.MeasureText(text, FontSize);
                Raylib.DrawText(text, x, y + 7, FontSize, selected ? Theme.Accent : Theme.TextDim);
            }
        }
    }

    // The panel is as wide as its longest label and its widest price need, so they never overlap.
    private static Rectangle Bounds(ToolPalette palette)
    {
        var widest = 0;
        for (var i = 0; i < palette.Tools.Count; i++)
        {
            var tool = palette.Tools[i];
            var cost = CostOf(tool);
            var costWidth = cost > 0 ? CostGap + Raylib.MeasureText($"${cost}", FontSize) : 0;
            widest = Math.Max(widest, Raylib.MeasureText(Label(i, tool), FontSize) + costWidth);
        }

        return new Rectangle
        {
            X = Padding,
            Y = Top,
            Width = LabelLeft + widest + Padding,
            Height = (RowHeight * palette.Tools.Count) + (Padding * 2),
        };
    }

    private static string Label(int index, Tool tool) => $"{index + 1}  {Name(tool)}";

    private static int CostOf(Tool tool)
    {
        var action = ToolActions.ActionFor(tool);
        return action is null ? 0 : TileCosts.CostFor(action.Value);
    }

    private static void DrawIcon(Texture2D texture, int x, int y, int box)
    {
        if (texture.Width <= 0 || texture.Height <= 0)
        {
            return;
        }

        var scale = Math.Min((float)box / texture.Width, (float)box / texture.Height);
        var width = texture.Width * scale;
        var height = texture.Height * scale;

        Raylib.DrawTexturePro(
            texture,
            new Rectangle { X = 0, Y = 0, Width = texture.Width, Height = texture.Height },
            new Rectangle
            {
                X = x + ((box - width) / 2f),
                Y = y + ((box - height) / 2f),
                Width = width,
                Height = height,
            },
            Vector2.Zero,
            0f,
            Color.White);
    }

    private static string Name(Tool tool) => tool switch
    {
        Tool.Road => "Road",
        Tool.Residential => "Residential",
        Tool.Commercial => "Commercial",
        Tool.Industrial => "Industrial",
        Tool.Bulldozer => "Bulldozer",
        _ => "Inspect",
    };
}
