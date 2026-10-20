namespace CityLimits.Rendering;

/// <summary>The colors the game draws with, in one place.</summary>
public static class Theme
{
    /// <summary>The color of open land.</summary>
    public static readonly Color Grass = new() { R = 92, G = 132, B = 74, A = 255 };

    /// <summary>The color of water.</summary>
    public static readonly Color Water = new() { R = 48, G = 96, B = 148, A = 255 };

    /// <summary>The color of trees.</summary>
    public static readonly Color Trees = new() { R = 44, G = 96, B = 56, A = 255 };

    /// <summary>The color of bare rock.</summary>
    public static readonly Color Rock = new() { R = 128, G = 126, B = 120, A = 255 };

    /// <summary>The color of the tile grid.</summary>
    public static readonly Color Grid = new() { R = 255, G = 255, B = 255, A = 28 };

    /// <summary>The color of the tile under the mouse.</summary>
    public static readonly Color Hover = new() { R = 255, G = 235, B = 120, A = 255 };

    /// <summary>The background of the panels.</summary>
    public static readonly Color Panel = new() { R = 22, G = 26, B = 32, A = 225 };

    /// <summary>The border of the panels.</summary>
    public static readonly Color PanelBorder = new() { R = 72, G = 80, B = 92, A = 255 };

    /// <summary>The color of the panel text.</summary>
    public static readonly Color Text = new() { R = 228, G = 232, B = 238, A = 255 };

    /// <summary>The color of text that matters less.</summary>
    public static readonly Color TextDim = new() { R = 150, G = 158, B = 170, A = 255 };

    /// <summary>The color that marks the current selection.</summary>
    public static readonly Color Accent = new() { R = 240, G = 190, B = 90, A = 255 };

    /// <summary>The color of a failure the player needs to know about.</summary>
    public static readonly Color Error = new() { R = 232, G = 106, B = 96, A = 255 };

    /// <summary>Returns the color of a kind of ground.</summary>
    public static Color For(TileKind kind) => kind switch
    {
        TileKind.Water => Water,
        TileKind.Trees => Trees,
        TileKind.Rock => Rock,
        _ => Grass,
    };
}
