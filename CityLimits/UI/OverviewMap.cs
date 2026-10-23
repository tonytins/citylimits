namespace CityLimits.UI;

/// <summary>What the overview map shows: the city itself, or one of the simulation's maps over it.</summary>
public enum OverviewMode
{
    /// <summary>The city as it stands: ground, roads, and what is built.</summary>
    City,

    /// <summary>Which zones and buildings have power, and the lines that carry it.</summary>
    Power,

    /// <summary>How well fire stations cover the city.</summary>
    FireCoverage,

    /// <summary>How well police stations cover the city.</summary>
    PoliceCoverage,

    /// <summary>What the land is worth.</summary>
    LandValue,

    /// <summary>Where people are.</summary>
    PopulationDensity,

    /// <summary>How busy the roads are.</summary>
    Traffic,

    /// <summary>What industry, coal plants, and busy roads give off.</summary>
    Pollution,

    /// <summary>Where crime is.</summary>
    Crime,
}

/// <summary>
/// The overview map, as Micropolis has it: the whole city small, in a corner, with buttons that
/// switch it between the city and the simulation's maps. It outlines what the main view shows,
/// and a click on it moves the view there. It is drawn into its own texture and redrawn only when
/// the map changes, the mode changes, or the simulation starts a new count, since its maps change
/// once a cycle; when the texture can't be made it is drawn straight to the screen.
/// </summary>
public sealed class OverviewMap : IDisposable
{
    // The texture holds two pixels a tile; the panel shows it at most this big.
    private const int PixelsPerTile = 2;
    private const int MostWidth = 240;
    private const int MostHeight = 200;
    private const int Padding = 6;
    private const int ButtonHeight = 18;
    private const int ButtonGap = 2;

    // The buttons are laid out in rows of this many, so each label fits its button.
    private const int ButtonsPerRow = 4;
    private const int FontSize = 10;
    private const int Top = 8;
    private const int Margin = 8;

    // A value at or above this is shown as the highest band; the coverage figure is a fully
    // funded, powered, served station on its own block.
    private const int HighCoverage = 218;
    private const int HighDensity = 200;

    private static readonly OverviewMode[] Modes = Enum.GetValues<OverviewMode>();
    private static readonly string[] Labels = ["City", "Power", "Fire", "Police", "Value", "People", "Traffic", "Pollution", "Crime"];

    private readonly CitySimulation _city;
    private readonly RenderTexture2D? _texture;
    private bool _stale = true;
    private long _drawnAt = -1;

    /// <summary>Creates the overview of a city. Call it after the window is open.</summary>
    public OverviewMap(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        _city = city;

        var texture = Raylib.LoadRenderTexture(city.Map.Width * PixelsPerTile, city.Map.Height * PixelsPerTile);
        _texture = Raylib.IsRenderTextureValid(texture) ? texture : null;
        _city.Map.TileChanged += OnTileChanged;
    }

    /// <summary>What the overview shows now.</summary>
    public OverviewMode Mode { get; private set; } = OverviewMode.City;

    /// <summary>Tells whether a point on the screen is on the overview, so a click there isn't a click on the map.</summary>
    public bool Contains(Vector2 point) => Raylib.CheckCollisionPointRec(point, Bounds());

    /// <summary>
    /// Acts on a press or a drag on the overview: a button picks a mode, and the map moves the
    /// camera to the place under the pointer. Tells whether the overview took the pointer.
    /// </summary>
    public bool HandlePointer(Vector2 point, Camera camera, bool pressed)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var map = MapBounds();
        if (Raylib.CheckCollisionPointRec(point, map))
        {
            var tileX = (point.X - map.X) / map.Width * _city.Map.Width;
            var tileY = (point.Y - map.Y) / map.Height * _city.Map.Height;
            camera.CenterOn(new Vector2(tileX * MapRenderer.TileSize, tileY * MapRenderer.TileSize));
            return true;
        }

        if (!pressed)
        {
            return false;
        }

        for (var i = 0; i < Modes.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, ButtonBounds(i)))
            {
                Mode = Modes[i];
                _stale = true;
                return true;
            }
        }

        return Contains(point);
    }

    /// <summary>Redraws the overview's texture when something it shows has changed. Call it before the frame begins.</summary>
    public void Refresh()
    {
        if (_texture is null || (!_stale && _drawnAt == _city.CityTime))
        {
            return;
        }

        Raylib.BeginTextureMode(_texture.Value);
        Raylib.ClearBackground(Color.Blank);
        DrawTiles(0, 0, PixelsPerTile);
        Raylib.EndTextureMode();
        _stale = false;
        _drawnAt = _city.CityTime;
    }

    /// <summary>Draws the overview, its outline of the main view, and its buttons. Call it outside the camera.</summary>
    public void Draw(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var bounds = Bounds();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        var map = MapBounds();
        if (_texture is { } texture)
        {
            // A render texture is stored upside down, so it is drawn with a negative source height.
            var source = new Rectangle { X = 0, Y = 0, Width = texture.Texture.Width, Height = -texture.Texture.Height };
            Raylib.DrawTexturePro(texture.Texture, source, map, Vector2.Zero, 0f, Color.White);
        }
        else
        {
            DrawTiles((int)map.X, (int)map.Y, map.Width / _city.Map.Width);
        }

        DrawView(camera, map);

        for (var i = 0; i < Modes.Length; i++)
        {
            var button = ButtonBounds(i);
            var selected = Modes[i] == Mode;
            Raylib.DrawRectangleRec(button, selected ? Theme.PanelBorder : Theme.Panel);
            var width = Raylib.MeasureText(Labels[i], FontSize);
            Raylib.DrawText(Labels[i], (int)(button.X + ((button.Width - width) / 2)), (int)button.Y + 4, FontSize, selected ? Theme.Accent : Theme.TextDim);
        }
    }

    /// <summary>Stops listening to the map and releases the texture. Call it before the window closes.</summary>
    public void Dispose()
    {
        _city.Map.TileChanged -= OnTileChanged;
        if (_texture is { } texture)
        {
            Raylib.UnloadRenderTexture(texture);
        }
    }

    private void OnTileChanged(int x, int y) => _stale = true;

    private void DrawTiles(int left, int top, float pixels)
    {
        var map = _city.Map;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new Rectangle { X = left + (x * pixels), Y = top + (y * pixels), Width = pixels, Height = pixels };
                Raylib.DrawRectangleRec(cell, ColorAt(x, y));
            }
        }
    }

    // The city in flat colours; an overlay shows its values over the city dimmed.
    private Color ColorAt(int x, int y)
    {
        // A fire shows in every mode, so it can be found whatever the map shows.
        if (_city.Fires.IsBurning(x, y))
        {
            return Theme.Fire;
        }

        var state = _city.Map.StateAt(x, y);
        var city = state.Use == TileUse.None ? Theme.For(state.Kind) : Theme.For(state.Use);
        return Mode switch
        {
            OverviewMode.Power => PowerColor(x, y, state, city),
            OverviewMode.FireCoverage => Band(_city.Services.FireCoverageAt(x, y), HighCoverage, city),
            OverviewMode.PoliceCoverage => Band(_city.Services.PoliceCoverageAt(x, y), HighCoverage, city),
            OverviewMode.LandValue => Band(_city.Maps.LandValueAt(x, y), CityMaps.MaxLandValue, city),
            OverviewMode.PopulationDensity => Band(_city.Maps.DensityAt(x, y), HighDensity, city),
            OverviewMode.Traffic => Band(_city.Traffic.DensityAt(x, y), TrafficMap.Most, city),
            OverviewMode.Pollution => Band(_city.Maps.PollutionAt(x, y), CityMaps.MostPollution, city),
            OverviewMode.Crime => Band(_city.Maps.CrimeAt(x, y), CityMaps.MostCrime, city),
            _ => city,
        };
    }

    // As Micropolis shows power: a zone or building lit or dark by what its centre last had, the
    // lines that carry it, and everything else dimmed.
    private Color PowerColor(int x, int y, TileState state, Color city)
    {
        if (state.HasFootprint)
        {
            return _city.Map.ZoneStateAt(x, y).Powered ? Theme.OverviewPowered : Theme.OverviewUnpowered;
        }

        return state.HasPowerLine ? Theme.OverviewLine : Dim(city);
    }

    // A value in one of four bands, from a quarter of the high mark up; nothing at all shows the
    // city dimmed beneath.
    private static Color Band(int value, int high, Color city)
    {
        if (value <= 0)
        {
            return Dim(city);
        }

        var band = Math.Min(value * Theme.OverlayBands.Length / high, Theme.OverlayBands.Length - 1);
        return Theme.OverlayBands[band];
    }

    private static Color Dim(Color color) => new() { R = (byte)(color.R / 3), G = (byte)(color.G / 3), B = (byte)(color.B / 3), A = 255 };

    private void DrawView(Camera camera, Rectangle map)
    {
        var topLeft = camera.ScreenToWorld(Vector2.Zero);
        var bottomRight = camera.ScreenToWorld(camera.ScreenSize);
        var scaleX = map.Width / (_city.Map.Width * MapRenderer.TileSize);
        var scaleY = map.Height / (_city.Map.Height * MapRenderer.TileSize);
        var left = Math.Clamp(map.X + (topLeft.X * scaleX), map.X, map.X + map.Width);
        var top = Math.Clamp(map.Y + (topLeft.Y * scaleY), map.Y, map.Y + map.Height);
        var right = Math.Clamp(map.X + (bottomRight.X * scaleX), map.X, map.X + map.Width);
        var bottom = Math.Clamp(map.Y + (bottomRight.Y * scaleY), map.Y, map.Y + map.Height);
        Raylib.DrawRectangleLinesEx(new Rectangle { X = left, Y = top, Width = right - left, Height = bottom - top }, 1f, Theme.Hover);
    }

    // The map keeps its shape, scaled down to fit the panel when it is bigger than the standard one.
    private Rectangle MapBounds()
    {
        var width = (float)_city.Map.Width * PixelsPerTile;
        var height = (float)_city.Map.Height * PixelsPerTile;
        var scale = Math.Min(1f, Math.Min(MostWidth / width, MostHeight / height));
        width *= scale;
        height *= scale;
        var panel = PanelSize();
        return new Rectangle
        {
            X = Raylib.GetScreenWidth() - Margin - panel.X + Padding + ((panel.X - (Padding * 2) - width) / 2),
            Y = Top + Padding,
            Width = width,
            Height = height,
        };
    }

    private Vector2 PanelSize()
    {
        var width = (float)_city.Map.Width * PixelsPerTile;
        var height = (float)_city.Map.Height * PixelsPerTile;
        var scale = Math.Min(1f, Math.Min(MostWidth / width, MostHeight / height));
        return new Vector2(Math.Max(width * scale, MostWidth) + (Padding * 2), (height * scale) + ButtonRowsHeight() + (Padding * 3));
    }

    private Rectangle Bounds()
    {
        var size = PanelSize();
        return new Rectangle { X = Raylib.GetScreenWidth() - Margin - size.X, Y = Top, Width = size.X, Height = size.Y };
    }

    private static int ButtonRows() => (Modes.Length + ButtonsPerRow - 1) / ButtonsPerRow;

    private static int ButtonRowsHeight() => (ButtonRows() * ButtonHeight) + ((ButtonRows() - 1) * ButtonGap);

    private Rectangle ButtonBounds(int index)
    {
        var bounds = Bounds();
        var width = (bounds.Width - (Padding * 2)) / ButtonsPerRow;
        var row = index / ButtonsPerRow;
        return new Rectangle
        {
            X = bounds.X + Padding + (index % ButtonsPerRow * width),
            Y = bounds.Y + bounds.Height - Padding - ButtonRowsHeight() + (row * (ButtonHeight + ButtonGap)),
            Width = width - ButtonGap,
            Height = ButtonHeight,
        };
    }
}
