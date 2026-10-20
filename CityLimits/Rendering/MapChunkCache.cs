namespace CityLimits.Rendering;

/// <summary>
/// The map drawn ahead of time into render textures of <see cref="ChunkTiles"/> by
/// <see cref="ChunkTiles"/> tiles, so a frame draws a few textures instead of every tile.
/// A chunk is redrawn only when a tile in it, or next to it, changes.
/// </summary>
public sealed class MapChunkCache : IDisposable
{
    /// <summary>The width and height of a chunk, in tiles.</summary>
    public const int ChunkTiles = 32;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly TileMap _map;
    private readonly Texture2D? _sheet;
    private readonly RenderTexture2D[] _chunks;
    private readonly bool[] _stale;
    private readonly int _columns;
    private bool _anyStale;

    private MapChunkCache(TileMap map, Texture2D? sheet, RenderTexture2D[] chunks, int columns)
    {
        _map = map;
        _sheet = sheet;
        _chunks = chunks;
        _columns = columns;
        _stale = new bool[chunks.Length];
        Array.Fill(_stale, true);
        _anyStale = true;
        _map.TileChanged += OnTileChanged;
    }

    /// <summary>
    /// Creates the chunks for a map, or returns null when the graphics driver can't make
    /// them, in which case the map is drawn tile by tile instead. Call it after the window is open.
    /// </summary>
    public static MapChunkCache? TryCreate(TileMap map, Texture2D? sheet)
    {
        ArgumentNullException.ThrowIfNull(map);

        var columns = (map.Width + ChunkTiles - 1) / ChunkTiles;
        var rows = (map.Height + ChunkTiles - 1) / ChunkTiles;
        var chunks = new RenderTexture2D[columns * rows];

        for (var i = 0; i < chunks.Length; i++)
        {
            var (firstX, firstY, width, height) = TilesOf(map, columns, i);
            var chunk = Raylib.LoadRenderTexture(width * MapRenderer.TileSize, height * MapRenderer.TileSize);
            if (!Raylib.IsRenderTextureValid(chunk))
            {
                for (var made = 0; made < i; made++)
                {
                    Raylib.UnloadRenderTexture(chunks[made]);
                }

                Logger.Warn(
                    "The map can't be cached in textures (chunk at {X}, {Y} failed), so it is drawn tile by tile",
                    firstX,
                    firstY);
                return null;
            }

            // Pixel art stays sharp when the camera zooms the cached map.
            Raylib.SetTextureFilter(chunk.Texture, TextureFilter.Point);
            chunks[i] = chunk;
        }

        return new MapChunkCache(map, sheet, chunks, columns);
    }

    /// <summary>Redraws the chunks that changed since the last frame. Call it before the frame begins.</summary>
    public void Refresh()
    {
        if (!_anyStale)
        {
            return;
        }

        for (var i = 0; i < _chunks.Length; i++)
        {
            if (_stale[i])
            {
                Redraw(i);
            }
        }

        _anyStale = false;
    }

    /// <summary>Draws the chunks that hold the given range of tiles, in world space.</summary>
    public void Draw(int minX, int minY, int maxX, int maxY)
    {
        for (var row = minY / ChunkTiles; row <= maxY / ChunkTiles; row++)
        {
            for (var column = minX / ChunkTiles; column <= maxX / ChunkTiles; column++)
            {
                var texture = _chunks[(row * _columns) + column].Texture;

                // A render texture is stored upside down, so it is drawn with a negative source height.
                var source = new Rectangle { X = 0, Y = 0, Width = texture.Width, Height = -texture.Height };
                var destination = new Rectangle
                {
                    X = column * ChunkTiles * MapRenderer.TileSize,
                    Y = row * ChunkTiles * MapRenderer.TileSize,
                    Width = texture.Width,
                    Height = texture.Height,
                };

                Raylib.DrawTexturePro(texture, source, destination, Vector2.Zero, 0f, Color.White);
            }
        }
    }

    /// <summary>Stops listening to the map and releases the textures. Call it before the window closes.</summary>
    public void Dispose()
    {
        _map.TileChanged -= OnTileChanged;

        foreach (var chunk in _chunks)
        {
            Raylib.UnloadRenderTexture(chunk);
        }
    }

    // A road's drawing depends on the roads beside it, so a change also stales its neighbours' chunks.
    private void OnTileChanged(int x, int y)
    {
        MarkStale(x, y);
        MarkStale(x - 1, y);
        MarkStale(x + 1, y);
        MarkStale(x, y - 1);
        MarkStale(x, y + 1);
    }

    private void MarkStale(int x, int y)
    {
        if (!_map.Contains(x, y))
        {
            return;
        }

        _stale[((y / ChunkTiles) * _columns) + (x / ChunkTiles)] = true;
        _anyStale = true;
    }

    private void Redraw(int index)
    {
        var (firstX, firstY, width, height) = TilesOf(_map, _columns, index);

        Raylib.BeginTextureMode(_chunks[index]);
        Raylib.ClearBackground(Color.Blank);

        for (var y = firstY; y < firstY + height; y++)
        {
            for (var x = firstX; x < firstX + width; x++)
            {
                MapRenderer.DrawTile(_map, _sheet, x, y, (x - firstX) * MapRenderer.TileSize, (y - firstY) * MapRenderer.TileSize);
            }
        }

        Raylib.EndTextureMode();
        _stale[index] = false;
    }

    // The chunks on the right and bottom edges are smaller when the map isn't a whole number of chunks.
    private static (int FirstX, int FirstY, int Width, int Height) TilesOf(TileMap map, int columns, int index)
    {
        var firstX = index % columns * ChunkTiles;
        var firstY = index / columns * ChunkTiles;
        return (firstX, firstY, Math.Min(ChunkTiles, map.Width - firstX), Math.Min(ChunkTiles, map.Height - firstY));
    }
}
