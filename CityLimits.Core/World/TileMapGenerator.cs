using CityLimits.Core.Randomness;

namespace CityLimits.Core.World;

/// <summary>
/// Builds maps from a seed, so the same seed always yields the same world. The land is laid out
/// as Micropolis lays it out: now and then an island, otherwise clear land crossed by two wide
/// rivers and a narrow one, with a few lakes; then woods scattered over it and thinned until
/// every tree stands in a wood.
/// </summary>
public static class TileMapGenerator
{
    // One map in ten is an island.
    private const int IslandChancePercent = 10;

    // The island's land starts this far in from the edge, and the sea eats up to this far into it.
    private const int IslandInset = 5;
    private const int IslandReach = 18;

    // The rivers start at least this far from the left and right edges, and from the top and bottom.
    private const int RiverMarginX = 40;
    private const int RiverMarginY = 33;

    // A river goes back to its usual heading about one step in ten; otherwise a roll of 0 to 200
    // above 90 turns it one way, and another the other way.
    private const int RiverStraightenChance = 10;
    private const int RiverVeerAbove = 90;

    // Lakes sit at least this far in from the edge, and their splashes spread this far around.
    private const int LakeMargin = 10;
    private const int LakeSpread = 6;

    // How far a splash reaches from its centre, in half tiles across plus half tiles down.
    private const int WideSplashSize = 9;
    private const int WideSplashReach = 10;
    private const int NarrowSplashSize = 6;
    private const int NarrowSplashReach = 6;
    private const int MostLakes = 10;

    // How many woods are scattered, and how far each one wanders as it is planted.
    private const int FewestWoods = 50;
    private const int MoreWoods = 100;
    private const int ShortestWood = 50;
    private const int LongerWood = 150;

    // The eight headings, clockwise from north; a heading is taken modulo 8.
    private static readonly (int Dx, int Dy)[] Headings =
    [
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1),
    ];

    /// <summary>Generates a map of the given size from the given seed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The width or the height is not positive.</exception>
    public static TileMap Generate(int seed, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var land = new Land(width, height);
        var random = new DeterministicRandom(seed);

        if (random.NextInt(0, 100) < IslandChancePercent)
        {
            MakeIsland(land, random);
        }
        else
        {
            var startX = Between(random, RiverMarginX, width - RiverMarginX, width);
            var startY = Between(random, RiverMarginY, height - RiverMarginY - 1, height);
            LayRivers(land, random, startX, startY);
            LayLakes(land, random);
        }

        PlantWoods(land, random);
        return TileMap.FromTerrain(width, height, land.Tiles);
    }

    // A place from low to high, or the middle of the side when the map is too small for the range.
    private static int Between(DeterministicRandom random, int low, int high, int size) =>
        high >= low ? random.NextInt(low, high + 1) : size / 2;

    // The smaller of two rolls, so a splash lands near the edge more often than far from it.
    private static int NearEdge(DeterministicRandom random) =>
        Math.Min(random.NextInt(0, IslandReach + 1), random.NextInt(0, IslandReach + 1));

    private static void MakeIsland(Land land, DeterministicRandom random)
    {
        var (width, height) = (land.Width, land.Height);
        Array.Fill(land.Tiles, TileKind.Water);
        for (var y = IslandInset; y < height - IslandInset; y++)
        {
            for (var x = IslandInset; x < width - IslandInset; x++)
            {
                land[x, y] = TileKind.Grass;
            }
        }

        // The sea bites into the coast at random depths, so it isn't a straight line.
        for (var x = 0; x < width - IslandInset; x += 2)
        {
            land.SplashWide(x, NearEdge(random));
            land.SplashWide(x, height - 10 - NearEdge(random));
            land.SplashNarrow(x, 0);
            land.SplashNarrow(x, height - 6);
        }

        for (var y = 0; y < height - IslandInset; y += 2)
        {
            land.SplashWide(NearEdge(random), y);
            land.SplashWide(width - 10 - NearEdge(random), y);
            land.SplashNarrow(0, y);
            land.SplashNarrow(width - 6, y);
        }
    }

    // Two wide rivers run out from the start in opposite directions, then a narrow one wanders off.
    private static void LayRivers(Land land, DeterministicRandom random, int startX, int startY)
    {
        var first = random.NextInt(0, 4);
        var heading = LayRiver(land, random, startX, startY, first, first, wide: true);
        var opposite = (first + 4) % Headings.Length;
        heading = LayRiver(land, random, startX, startY, opposite, opposite, wide: true);
        LayRiver(land, random, startX, startY, heading, random.NextInt(0, 4), wide: false);
    }

    // Splashes water along a winding path until it runs off the map, and returns its last heading.
    private static int LayRiver(Land land, DeterministicRandom random, int x, int y, int heading, int usualHeading, bool wide)
    {
        var reach = wide ? 4 : 3;

        // A wander that never left the map would never end, so a river is cut off after this many steps.
        var steps = land.Width * land.Height;
        while (land.Contains(x + reach, y + reach) && steps-- > 0)
        {
            if (wide)
            {
                land.SplashWide(x, y);
            }
            else
            {
                land.SplashNarrow(x, y);
            }

            if (random.NextInt(0, 101) < RiverStraightenChance)
            {
                heading = usualHeading;
            }
            else
            {
                if (random.NextInt(0, 201) > RiverVeerAbove)
                {
                    heading++;
                }

                if (random.NextInt(0, 201) > RiverVeerAbove)
                {
                    heading--;
                }
            }

            var (dx, dy) = Headings[heading & 7];
            x += dx;
            y += dy;
        }

        return heading;
    }

    private static void LayLakes(Land land, DeterministicRandom random)
    {
        var lakes = random.NextInt(0, MostLakes + 1);
        for (var lake = 0; lake < lakes; lake++)
        {
            var x = Between(random, LakeMargin, land.Width - LakeMargin - 1, land.Width);
            var y = Between(random, LakeMargin, land.Height - LakeMargin, land.Height);
            var splashes = random.NextInt(2, 15);
            for (var splash = 0; splash < splashes; splash++)
            {
                var splashX = x + random.NextInt(-LakeSpread, LakeSpread + 1);
                var splashY = y + random.NextInt(-LakeSpread, LakeSpread + 1);

                // Most of a lake is small splashes; one in five is wide.
                if (random.NextInt(0, 5) != 0)
                {
                    land.SplashNarrow(splashX, splashY);
                }
                else
                {
                    land.SplashWide(splashX, splashY);
                }
            }
        }
    }

    // Each wood is a random walk that plants trees on the clear land it crosses.
    private static void PlantWoods(Land land, DeterministicRandom random)
    {
        var woods = random.NextInt(FewestWoods, FewestWoods + MoreWoods + 1);
        for (var wood = 0; wood < woods; wood++)
        {
            var x = random.NextInt(0, land.Width);
            var y = random.NextInt(0, land.Height);
            var length = random.NextInt(ShortestWood, ShortestWood + LongerWood + 1);
            for (var step = 0; step < length; step++)
            {
                var (dx, dy) = Headings[random.NextInt(0, Headings.Length)];
                x += dx;
                y += dy;
                if (!land.Contains(x, y))
                {
                    break;
                }

                if (land[x, y] == TileKind.Grass)
                {
                    land[x, y] = TileKind.Trees;
                }
            }
        }

        ThinWoods(land);
        ThinWoods(land);
    }

    // A tree the sheet can't draw as part of a wood, one with trees on fewer than two sides that
    // meet at a corner, is cleared. The pass works in place, so a cleared tree can clear the next.
    private static void ThinWoods(Land land)
    {
        for (var y = 0; y < land.Height; y++)
        {
            for (var x = 0; x < land.Width; x++)
            {
                if (land[x, y] == TileKind.Trees && !TerrainEdges.IsWood(TreeSides(land, x, y)))
                {
                    land[x, y] = TileKind.Grass;
                }
            }
        }
    }

    // The map's edge counts as no tree, so the woods thin out towards it.
    private static TerrainSides TreeSides(Land land, int x, int y)
    {
        var sides = TerrainSides.None;
        if (land.Contains(x, y - 1) && land[x, y - 1] == TileKind.Trees)
        {
            sides |= TerrainSides.North;
        }

        if (land.Contains(x + 1, y) && land[x + 1, y] == TileKind.Trees)
        {
            sides |= TerrainSides.East;
        }

        if (land.Contains(x, y + 1) && land[x, y + 1] == TileKind.Trees)
        {
            sides |= TerrainSides.South;
        }

        if (land.Contains(x - 1, y) && land[x - 1, y] == TileKind.Trees)
        {
            sides |= TerrainSides.West;
        }

        return sides;
    }

    // The ground being laid out, before it becomes a map.
    private sealed class Land(int width, int height)
    {
        public int Width { get; } = width;

        public int Height { get; } = height;

        public TileKind[] Tiles { get; } = new TileKind[width * height];

        public TileKind this[int x, int y]
        {
            get => Tiles[(y * Width) + x];
            set => Tiles[(y * Width) + x] = value;
        }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        // A rounded splash of water 9 tiles across, its top-left corner at the point.
        public void SplashWide(int left, int top) => Splash(left, top, WideSplashSize, WideSplashReach);

        // A rounded splash of water 6 tiles across, its top-left corner at the point.
        public void SplashNarrow(int left, int top) => Splash(left, top, NarrowSplashSize, NarrowSplashReach);

        // Fills the tiles of a square that are within the reach of its centre, which cuts the
        // square's corners off. Distances are in half tiles, so an even square has a centre too.
        private void Splash(int left, int top, int size, int reach)
        {
            for (var dy = 0; dy < size; dy++)
            {
                for (var dx = 0; dx < size; dx++)
                {
                    var distance = Math.Abs((2 * dx) - (size - 1)) + Math.Abs((2 * dy) - (size - 1));
                    if (distance <= reach && Contains(left + dx, top + dy))
                    {
                        this[left + dx, top + dy] = TileKind.Water;
                    }
                }
            }
        }
    }
}
