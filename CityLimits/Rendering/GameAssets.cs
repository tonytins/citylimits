namespace CityLimits.Rendering;

/// <summary>The textures the game loads once, at startup, and unloads at shutdown.</summary>
public sealed class GameAssets : IDisposable
{
    /// <summary>The name of the folder, inside the game, that holds the assets.</summary>
    public const string FolderName = "Assets";

    /// <summary>The name of the file that holds every tile of ground.</summary>
    public const string TileSheetFileName = "tiles.png";

    /// <summary>The name of the file that holds the splash-screen logo.</summary>
    public const string LogoFileName = "logo.png";

    private const string ToolFolderName = "tools";

    private static readonly (Tool Tool, string FileName)[] ToolIconFiles =
    [
        (Tool.Inspect, "icqry.png"),
        (Tool.Road, "icroad.png"),
        (Tool.Residential, "icres.png"),
        (Tool.Commercial, "iccom.png"),
        (Tool.Industrial, "icind.png"),
        (Tool.Bulldozer, "icbulldozer.png"),
        (Tool.CoalPowerPlant, "iccoal.png"),
        (Tool.NuclearPowerPlant, "icnuc.png"),
        (Tool.PowerLine, "icwire.png"),
        (Tool.FireStation, "icfire.png"),
        (Tool.PoliceStation, "icpol.png"),
    ];

    // Each icon's highlighted version, drawn while its tool is held, is beside it with "hi" on its name.
    private const string SelectedIconSuffix = "hi";

    private readonly Dictionary<Tool, Texture2D> _icons = new();
    private readonly Dictionary<Tool, Texture2D> _selectedIcons = new();
    private readonly List<Texture2D> _textures = [];

    /// <summary>The sheet that holds every tile of ground, or null when it is missing.</summary>
    public Texture2D? TileSheet { get; private set; }

    /// <summary>The splash-screen logo, or null when it is missing.</summary>
    public Texture2D? Logo { get; private set; }

    /// <summary>The icon for a tool, or null when the tool has none.</summary>
    public Texture2D? IconFor(Tool tool) => _icons.TryGetValue(tool, out var icon) ? icon : null;

    /// <summary>The highlighted icon drawn while a tool is held, or null when it has none.</summary>
    public Texture2D? SelectedIconFor(Tool tool) => _selectedIcons.TryGetValue(tool, out var icon) ? icon : null;

    /// <summary>Loads the assets from a folder, and names the ones that are missing.</summary>
    public static GameAssets Load(string directory, out IReadOnlyList<string> problems)
    {
        var assets = new GameAssets();
        var missing = new List<string>();

        var sheetPath = Path.Combine(directory, TileSheetFileName);
        var sheet = LoadTexture(sheetPath);
        if (sheet.HasValue)
        {
            assets.TileSheet = sheet.Value;
            assets._textures.Add(sheet.Value);
        }
        else
        {
            missing.Add($"The tile sheet is missing: {sheetPath}");
        }

        var logoPath = Path.Combine(directory, LogoFileName);
        var logo = LoadTexture(logoPath);
        if (logo.HasValue)
        {
            assets.Logo = logo.Value;
            assets._textures.Add(logo.Value);
        }
        else
        {
            missing.Add($"The splash-screen logo is missing: {logoPath}");
        }

        var toolDirectory = Path.Combine(directory, ToolFolderName);
        foreach (var (tool, fileName) in ToolIconFiles)
        {
            var path = Path.Combine(toolDirectory, fileName);
            var icon = LoadTexture(path);
            if (!icon.HasValue)
            {
                missing.Add($"The icon for the {tool} tool is missing: {path}");
                continue;
            }

            assets._icons[tool] = icon.Value;
            assets._textures.Add(icon.Value);

            var selectedPath = Path.Combine(toolDirectory, Path.GetFileNameWithoutExtension(fileName) + SelectedIconSuffix + Path.GetExtension(fileName));
            var selected = LoadTexture(selectedPath);
            if (!selected.HasValue)
            {
                missing.Add($"The highlighted icon for the {tool} tool is missing: {selectedPath}");
                continue;
            }

            assets._selectedIcons[tool] = selected.Value;
            assets._textures.Add(selected.Value);
        }

        problems = missing;
        return assets;
    }

    /// <summary>Unloads every texture the game loaded.</summary>
    public void Dispose()
    {
        foreach (var texture in _textures)
        {
            Raylib.UnloadTexture(texture);
        }

        _textures.Clear();
        _icons.Clear();
        _selectedIcons.Clear();
        TileSheet = null;
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var image = Raylib.LoadImage(path);
        if (image.Width <= 0 || image.Height <= 0)
        {
            Raylib.UnloadImage(image);
            return null;
        }

        var texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);

        if (!Raylib.IsTextureValid(texture))
        {
            return null;
        }

        Raylib.SetTextureFilter(texture, TextureFilter.Point);
        return texture;
    }
}
