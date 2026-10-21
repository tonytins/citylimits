namespace CityLimits.UI;

/// <summary>
/// Draws the palette of tools as Micropolis lays it out: the tool's name and price at the top,
/// then rows of icon buttons, each row centred. The rows keep Micropolis' order, leaving out the
/// tools CityLimits doesn't have yet. The number keys pick tools too.
/// </summary>
public sealed class ToolPaletteView
{
    /// <summary>The keys that pick a tool.</summary>
    public static readonly KeyboardKey[] ToolKeys =
    [
        KeyboardKey.One,
        KeyboardKey.Two,
        KeyboardKey.Three,
        KeyboardKey.Four,
        KeyboardKey.Five,
        KeyboardKey.Six,
        KeyboardKey.Seven,
        KeyboardKey.Eight,
        KeyboardKey.Nine,
    ];

    // Micropolis' rows, with its park, rail, police, fire, stadium, seaport, and airport left out
    // until CityLimits has them.
    private static readonly Tool[][] Rows =
    [
        [Tool.Bulldozer, Tool.PowerLine],
        [Tool.Road],
        [Tool.Residential, Tool.Commercial, Tool.Industrial],
        [Tool.Inspect],
        [Tool.CoalPowerPlant, Tool.NuclearPowerPlant],
    ];

    private const int FontSize = 14;
    private const int LineHeight = 18;
    private const int Padding = 8;
    private const int Gap = 2;
    private const int Top = 46;
    private const int MissingIconSize = 34;
    private const int MissingIconFontSize = 10;

    private readonly GameAssets? _assets;
    private readonly CityDefinitions _definitions;
    private readonly (Tool Tool, Rectangle Bounds)[] _buttons;
    private readonly Rectangle _bounds;

    /// <summary>
    /// Creates a palette that draws the icons it has and the prices the city's definitions set.
    /// It lays itself out from the icons' sizes, so create it after the assets are loaded.
    /// </summary>
    public ToolPaletteView(GameAssets? assets, CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _assets = assets;
        _definitions = definitions;

        // The panel is as wide as its widest row of buttons, or its widest header.
        var widest = 0;
        foreach (var row in Rows)
        {
            widest = Math.Max(widest, RowWidth(row));
            foreach (var tool in row)
            {
                widest = Math.Max(widest, Raylib.MeasureText(Name(tool), FontSize));
                widest = Math.Max(widest, Raylib.MeasureText(PriceText(tool), FontSize));
            }
        }

        var buttons = new List<(Tool, Rectangle)>();
        var y = Top + Padding + (2 * LineHeight) + Padding;
        foreach (var row in Rows)
        {
            var x = Padding + Padding + ((widest - RowWidth(row)) / 2);
            var height = 0;
            foreach (var tool in row)
            {
                var (width, iconHeight) = IconSize(tool);
                buttons.Add((tool, new Rectangle { X = x, Y = y, Width = width, Height = iconHeight }));
                x += width + Gap;
                height = Math.Max(height, iconHeight);
            }

            y += height + Gap;
        }

        _buttons = [.. buttons];
        _bounds = new Rectangle { X = Padding, Y = Top, Width = widest + (Padding * 2), Height = y - Top + Padding - Gap };
    }

    /// <summary>Returns the tool a key picks, or null when the key is not a tool key.</summary>
    public static Tool? ToolForKey(KeyboardKey key) => key switch
    {
        KeyboardKey.One => Tool.Inspect,
        KeyboardKey.Two => Tool.Road,
        KeyboardKey.Three => Tool.Residential,
        KeyboardKey.Four => Tool.Commercial,
        KeyboardKey.Five => Tool.Industrial,
        KeyboardKey.Six => Tool.Bulldozer,
        KeyboardKey.Seven => Tool.CoalPowerPlant,
        KeyboardKey.Eight => Tool.NuclearPowerPlant,
        KeyboardKey.Nine => Tool.PowerLine,
        _ => null,
    };

    /// <summary>Tells whether a point on the screen is over the palette, so a click there isn't a click on the map.</summary>
    public bool Contains(Vector2 point) => Raylib.CheckCollisionPointRec(point, _bounds);

    /// <summary>The tool whose button is under a point on the screen, or null when there is none.</summary>
    public Tool? ToolAt(Vector2 point)
    {
        foreach (var (tool, bounds) in _buttons)
        {
            if (Raylib.CheckCollisionPointRec(point, bounds))
            {
                return tool;
            }
        }

        return null;
    }

    /// <summary>Where the palette ends on the screen, so the panels below it can sit under it.</summary>
    public int Bottom() => (int)(_bounds.Y + _bounds.Height);

    /// <summary>
    /// Draws the palette down the left edge of the screen. The header names the tool under the
    /// pointer, as Micropolis' tooltip does, or the held tool when the pointer is elsewhere.
    /// </summary>
    public void Draw(ToolPalette palette, Vector2 mouse)
    {
        Raylib.DrawRectangleRec(_bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(_bounds, 1f, Theme.PanelBorder);

        var shown = ToolAt(mouse) ?? palette.Selected;
        var headerX = (int)_bounds.X + Padding;
        var headerY = (int)_bounds.Y + Padding;
        Raylib.DrawText(Name(shown), headerX, headerY, FontSize, Theme.Text);
        Raylib.DrawText(PriceText(shown), headerX, headerY + LineHeight, FontSize, Theme.TextDim);

        foreach (var (tool, bounds) in _buttons)
        {
            // The held tool shows its highlighted icon, as Micropolis' pressed button does.
            var selected = tool == palette.Selected;
            var icon = selected ? _assets?.SelectedIconFor(tool) ?? _assets?.IconFor(tool) : _assets?.IconFor(tool);
            if (icon is not null)
            {
                Raylib.DrawTexture(icon.Value, (int)bounds.X, (int)bounds.Y, Color.White);
            }
            else
            {
                // A missing icon keeps the tool's label, so the button still says what it is.
                Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.TextDim);
                Raylib.DrawText(ShortName(tool), (int)bounds.X + 3, (int)bounds.Y + 3, MissingIconFontSize, Theme.TextDim);
            }

            if (selected && (icon is null || _assets?.SelectedIconFor(tool) is null))
            {
                Raylib.DrawRectangleLinesEx(bounds, 2f, Theme.Accent);
            }
        }
    }

    private (int Width, int Height) IconSize(Tool tool) =>
        _assets?.IconFor(tool) is { } icon ? (icon.Width, icon.Height) : (MissingIconSize, MissingIconSize);

    private int RowWidth(Tool[] row)
    {
        var width = -Gap;
        foreach (var tool in row)
        {
            width += IconSize(tool).Width + Gap;
        }

        return width;
    }

    private string PriceText(Tool tool)
    {
        var action = ToolActions.ActionFor(tool);
        return action is null ? "free" : $"${_definitions.CostOf(action.Value)}";
    }

    private static string Name(Tool tool) => tool switch
    {
        Tool.Road => "Road",
        Tool.Residential => "Residential",
        Tool.Commercial => "Commercial",
        Tool.Industrial => "Industrial",
        Tool.Bulldozer => "Bulldozer",
        Tool.CoalPowerPlant => "Coal plant",
        Tool.NuclearPowerPlant => "Nuclear plant",
        Tool.PowerLine => "Power line",
        _ => "Inspect",
    };

    private static string ShortName(Tool tool) => tool switch
    {
        Tool.Road => "Rd",
        Tool.Residential => "R",
        Tool.Commercial => "C",
        Tool.Industrial => "I",
        Tool.Bulldozer => "Doze",
        Tool.CoalPowerPlant => "Coal",
        Tool.NuclearPowerPlant => "Nuc",
        Tool.PowerLine => "Line",
        _ => "?",
    };
}
