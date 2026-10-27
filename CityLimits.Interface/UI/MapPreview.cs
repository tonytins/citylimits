namespace CityLimits.UI;

/// <summary>
/// A small picture of a city's map, as the Scenarios and Load windows show beside what is picked:
/// the overview's City colours, a pixel a tile, drawn into a texture once when a city is shown and
/// let go when another is, so a frame only draws it. A map that can't be shown says why instead.
/// </summary>
public sealed class MapPreview : IDisposable
{
    private const int TextSize = 10;

    private Texture2D? _texture;
    private string? _note;

    /// <summary>
    /// Shows a city's map, in place of whatever was shown. Call it after the window is open, when
    /// the player picks something, never every frame.
    /// </summary>
    public void Show(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        Paint(city.Map, (x, y) => OverviewMap.CityColorAt(city, x, y));
    }

    /// <summary>Shows a map with no city on it yet, such as one just grown from a seed, as <see cref="Show(CitySimulation)"/> does.</summary>
    public void Show(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Paint(map, (x, y) => OverviewMap.MapColorAt(map, x, y));
    }

    private void Paint(TileMap map, Func<int, int, Color> colorAt)
    {
        Clear();
        var image = Raylib.GenImageColor(map.Width, map.Height, Color.Blank);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Raylib.ImageDrawPixel(ref image, x, y, colorAt(x, y));
            }
        }

        var texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        if (Raylib.IsTextureValid(texture))
        {
            _texture = texture;
        }
        else
        {
            _note = "The map couldn't be drawn";
        }
    }

    /// <summary>Shows a line saying why there is no map, in place of whatever was shown.</summary>
    public void ShowNote(string note)
    {
        Clear();
        _note = note;
    }

    /// <summary>Shows nothing, and lets go of the picture.</summary>
    public void Clear()
    {
        if (_texture is { } texture)
        {
            Raylib.UnloadTexture(texture);
        }

        _texture = null;
        _note = null;
    }

    /// <summary>Draws the map in an area, as large as fits and in its own shape, framed; or the note.</summary>
    public void Draw(Rectangle area)
    {
        Raylib.DrawRectangleRec(area, Theme.Dim);
        if (_texture is { } texture)
        {
            var scale = MathF.Min(area.Width / texture.Width, area.Height / texture.Height);
            var (width, height) = (texture.Width * scale, texture.Height * scale);
            var target = new Rectangle { X = area.X + ((area.Width - width) / 2), Y = area.Y + ((area.Height - height) / 2), Width = width, Height = height };
            Raylib.DrawTexturePro(texture, new Rectangle { Width = texture.Width, Height = texture.Height }, target, Vector2.Zero, 0f, Color.White);
        }
        else if (_note is not null)
        {
            Raylib.DrawText(_note, (int)area.X + 8, (int)(area.Y + (area.Height / 2) - (TextSize / 2)), TextSize, Theme.TextDim);
        }

        Raylib.DrawRectangleLinesEx(area, 1f, Theme.PanelBorder);
    }

    /// <summary>Lets go of the picture. Call it before the window closes.</summary>
    public void Dispose() => Clear();
}
