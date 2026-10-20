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
    ];

    private const int FontSize = 14;
    private const int RowHeight = 28;
    private const int IconSize = 24;
    private const int Width = 176;
    private const int Padding = 8;

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
        _ => null,
    };

    /// <summary>Draws the palette down the left edge of the screen.</summary>
    public void Draw(ToolPalette palette)
    {
        var top = 46;
        var height = (RowHeight * palette.Tools.Count) + (Padding * 2);

        Raylib.DrawRectangle(Padding, top, Width, height, Theme.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle { X = Padding, Y = top, Width = Width, Height = height }, 1f, Theme.PanelBorder);

        for (var i = 0; i < palette.Tools.Count; i++)
        {
            var tool = palette.Tools[i];
            var selected = tool == palette.Selected;
            var y = top + Padding + (i * RowHeight);

            if (selected)
            {
                Raylib.DrawRectangle(Padding + 2, y - 2, Width - 4, RowHeight, Theme.PanelBorder);
            }

            var icon = _assets?.IconFor(tool);
            if (icon is not null)
            {
                DrawIcon(icon.Value, Padding + 6, y + 2, IconSize);
            }

            var label = $"{i + 1}  {Name(tool)}";
            Raylib.DrawText(label, Padding + 38, y + 7, FontSize, selected ? Theme.Accent : Theme.TextDim);
        }
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
        _ => "Inspect",
    };
}
