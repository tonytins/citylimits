namespace CityLimits.Rendering;

/// <summary>The colors the game draws with, in one place.</summary>
public static class Theme
{
    /// <summary>The background of the splash screen.</summary>
    public static readonly Color SplashBackground = new() { R = 78, G = 95, B = 187, A = 255 };

    /// <summary>The color of open land.</summary>
    public static readonly Color Grass = new() { R = 92, G = 132, B = 74, A = 255 };

    /// <summary>The color of water.</summary>
    public static readonly Color Water = new() { R = 48, G = 96, B = 148, A = 255 };

    /// <summary>The color of trees.</summary>
    public static readonly Color Trees = new() { R = 44, G = 96, B = 56, A = 255 };

    /// <summary>The color of rubble.</summary>
    public static readonly Color Rubble = new() { R = 128, G = 126, B = 120, A = 255 };

    /// <summary>The color of the tile grid.</summary>
    public static readonly Color Grid = new() { R = 255, G = 255, B = 255, A = 28 };

    /// <summary>The color of the tile under the mouse.</summary>
    public static readonly Color Hover = new() { R = 255, G = 235, B = 120, A = 255 };

    /// <summary>The bar's background, and the news ticker's.</summary>
    public static readonly Color Bar = new() { R = 0, G = 0, B = 0, A = 255 };

    /// <summary>A lit speed button, up to the speed the game runs at.</summary>
    public static readonly Color SpeedLit = new() { R = 62, G = 110, B = 190, A = 255 };

    /// <summary>A speed button lit, dimmed, up to the speed play brings back while the game is paused.</summary>
    public static readonly Color SpeedResume = new() { R = 46, G = 70, B = 110, A = 255 };

    /// <summary>A speed button above the speed.</summary>
    public static readonly Color SpeedUnlit = new() { R = 217, G = 217, B = 217, A = 255 };

    /// <summary>A zone or building with power, on the overview's power map.</summary>
    public static readonly Color OverviewPowered = new() { R = 240, G = 70, B = 50, A = 255 };

    /// <summary>A zone or building without power, on the overview's power map.</summary>
    public static readonly Color OverviewUnpowered = new() { R = 110, G = 120, B = 230, A = 255 };

    /// <summary>A power line, on the overview's power map.</summary>
    public static readonly Color OverviewLine = new() { R = 190, G = 190, B = 190, A = 255 };

    /// <summary>The overview's four bands for a map's values, from low to high.</summary>
    public static readonly Color[] OverlayBands =
    [
        new() { R = 80, G = 140, B = 200, A = 255 },
        new() { R = 90, G = 200, B = 110, A = 255 },
        new() { R = 240, G = 210, B = 60, A = 255 },
        new() { R = 235, G = 80, B = 50, A = 255 },
    ];

    /// <summary>The veil drawn over the map while a panel such as the budget holds the game.</summary>
    public static readonly Color Dim = new() { R = 0, G = 0, B = 0, A = 140 };

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

    /// <summary>The ticker's headlines from the city's world, after Tony Bark's mock-up: white.</summary>
    public static readonly Color NewsHeadline = new() { R = 240, G = 240, B = 240, A = 255 };

    /// <summary>The ticker's advisories, the game's notices and the city's messages: yellow.</summary>
    public static readonly Color NewsAdvisory = new() { R = 250, G = 210, B = 60, A = 255 };

    /// <summary>The ticker's disasters: red.</summary>
    public static readonly Color NewsDisaster = new() { R = 235, G = 70, B = 60, A = 255 };

    /// <summary>The background of a hint beside the pointer, as Tony Bark chose: yellow.</summary>
    public static readonly Color HintBackground = new() { R = 250, G = 215, B = 80, A = 255 };

    /// <summary>The text of a hint beside the pointer, and of the help button's mark: black.</summary>
    public static readonly Color HintText = new() { R = 0, G = 0, B = 0, A = 255 };

    /// <summary>The help button in the bar, after Tony Bark's mock-up: green.</summary>
    public static readonly Color HelpButton = new() { R = 74, G = 138, B = 66, A = 255 };

    /// <summary>The color of a road, when the tile sheet could not be loaded.</summary>
    public static readonly Color Road = new() { R = 66, G = 66, B = 70, A = 255 };

    /// <summary>The color of a residential zone.</summary>
    public static readonly Color Residential = new() { R = 74, G = 138, B = 66, A = 255 };

    /// <summary>The color of a commercial zone.</summary>
    public static readonly Color Commercial = new() { R = 62, G = 110, B = 190, A = 255 };

    /// <summary>The color of an industrial zone.</summary>
    public static readonly Color Industrial = new() { R = 198, G = 156, B = 58, A = 255 };

    /// <summary>The color that marks a zone with no power, when the tile sheet could not be loaded.</summary>
    public static readonly Color Unpowered = new() { R = 250, G = 220, B = 40, A = 255 };

    /// <summary>The color of a power line, when the tile sheet could not be loaded.</summary>
    public static readonly Color PowerLine = new() { R = 210, G = 190, B = 80, A = 255 };

    /// <summary>The color of a hospital or a church, when the tile sheet could not be loaded.</summary>
    public static readonly Color CommunityBuilding = new() { R = 220, G = 220, B = 220, A = 255 };

    /// <summary>The color of a park, when the tile sheet could not be loaded.</summary>
    public static readonly Color Park = new() { R = 70, G = 160, B = 70, A = 255 };

    /// <summary>The color of a fire station, when the tile sheet could not be loaded.</summary>
    public static readonly Color FireStation = new() { R = 200, G = 60, B = 50, A = 255 };

    /// <summary>The color of a police station, when the tile sheet could not be loaded.</summary>
    public static readonly Color PoliceStation = new() { R = 60, G = 80, B = 190, A = 255 };

    /// <summary>The color of a power plant, when the tile sheet could not be loaded.</summary>
    public static readonly Color PowerPlant = new() { R = 150, G = 150, B = 160, A = 255 };

    /// <summary>Returns the color of a kind of ground.</summary>
    public static Color For(TileKind kind) => kind switch
    {
        TileKind.Water => Water,
        TileKind.Trees => Trees,
        TileKind.Rubble => Rubble,
        _ => Grass,
    };

    /// <summary>Returns the color of a use.</summary>
    public static Color For(TileUse use) => use switch
    {
        TileUse.Road or TileUse.RoadAcrossPowerLine or TileUse.RoadDownPowerLine => Road,
        TileUse.PowerLine => PowerLine,
        TileUse.Park => Park,
        TileUse.Hospital or TileUse.Church => CommunityBuilding,
        TileUse.FireStation => FireStation,
        TileUse.PoliceStation => PoliceStation,
        TileUse.Residential => Residential,
        TileUse.Commercial => Commercial,
        TileUse.Industrial => Industrial,
        TileUse.CoalPowerPlant or TileUse.NuclearPowerPlant => PowerPlant,
        _ => Grass,
    };
}
