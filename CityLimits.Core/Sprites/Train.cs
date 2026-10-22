using CityLimits.Core.World;

namespace CityLimits.Core.Sprites;

/// <summary>Which of the train's drawings shows, in the order of its frames on disk.</summary>
public enum TrainFrame
{
    /// <summary>Running north or south.</summary>
    Down,

    /// <summary>Running east or west.</summary>
    Across,

    /// <summary>Turning between north and west, or south and east.</summary>
    TurningNorthwest,

    /// <summary>Turning between north and east, or south and west.</summary>
    TurningNortheast,

    /// <summary>In a tunnel, where nothing of it shows.</summary>
    Tunnel,
}

/// <summary>
/// The commuter train, as Micropolis runs it: once a city has more than 20 people and some rail,
/// a train now and then sets out from a rail tile and wanders the track, a quarter of a tile each
/// animation step, picking a way at random at each tile and never turning straight back until it
/// is stuck; with nowhere at all to go, it retires. It only reads the map and picks with the
/// generator it is given, never the city's, so it can't change what the city does and isn't
/// saved. In Micropolis the city's own generator decides when it sets out; that is the only difference.
/// </summary>
public sealed class Train
{
    /// <summary>The people a city needs before a train sets out, as in Micropolis.</summary>
    public const int PeopleBeforeTrains = 20;

    /// <summary>The chance, one in this many, that a train sets out on an animation step while none runs.</summary>
    public const int SetOutChance = 26;

    private const int TileSize = 16;
    private const int StepsPerTile = 4;
    private const int StepPixels = TileSize / StepsPerTile;
    private const int NoDirection = -1;

    // North, east, south, and west; a direction's opposite is two along.
    private static readonly (int Dx, int Dy)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private int _direction = NoDirection;
    private int _step;

    /// <summary>Whether a train is out on the track.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Where the middle of the train is, in pixels across the map.</summary>
    public int X { get; private set; }

    /// <summary>Where the middle of the train is, in pixels down the map.</summary>
    public int Y { get; private set; }

    /// <summary>Which drawing of the train shows.</summary>
    public TrainFrame Frame { get; private set; } = TrainFrame.Across;

    /// <summary>
    /// Runs one animation step: sets a train out when none runs and the city is ready for one, or
    /// moves the running train on a quarter of a tile, choosing its way when it reaches a tile's middle.
    /// </summary>
    public void Step(TileMap map, int population, Random random)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);

        if (!IsRunning)
        {
            TrySetOut(map, population, random);
            return;
        }

        // After the turn, the train runs straight again; stopped, it shows as Micropolis' does.
        if (Frame is TrainFrame.TurningNorthwest or TrainFrame.TurningNortheast)
        {
            Frame = _direction switch
            {
                0 or 2 => TrainFrame.Down,
                1 or 3 => TrainFrame.Across,
                _ => TrainFrame.Tunnel,
            };
        }

        if (_direction != NoDirection)
        {
            X += Directions[_direction].Dx * StepPixels;
            Y += Directions[_direction].Dy * StepPixels;
        }

        if (_step++ % StepsPerTile == 0)
        {
            ChooseWay(map, random);
        }
    }

    private void TrySetOut(TileMap map, int population, Random random)
    {
        if (population <= PeopleBeforeTrains || random.Next(SetOutChance) != 0)
        {
            return;
        }

        var rail = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                rail += map.StateAt(x, y).IsRail ? 1 : 0;
            }
        }

        if (rail == 0)
        {
            return;
        }

        var pick = random.Next(rail);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (!map.StateAt(x, y).IsRail || pick-- > 0)
                {
                    continue;
                }

                (X, Y) = ((x * TileSize) + (TileSize / 2), (y * TileSize) + (TileSize / 2));
                (_direction, _step, Frame, IsRunning) = (NoDirection, 0, map.StateAt(x, y).IsTunnel ? TrainFrame.Tunnel : TrainFrame.Across, true);
                return;
            }
        }
    }

    // At a tile's middle the train tries the four ways from a random one round, never straight
    // back while it is moving, and takes the first with rail; stuck, it stops, and stopped with
    // nowhere to go, it retires.
    private void ChooseWay(TileMap map, Random random)
    {
        var (tileX, tileY) = (X / TileSize, Y / TileSize);
        (X, Y) = ((tileX * TileSize) + (TileSize / 2), (tileY * TileSize) + (TileSize / 2));

        var first = random.Next(Directions.Length);
        for (var turn = 0; turn < Directions.Length; turn++)
        {
            var way = (first + turn) % Directions.Length;
            if (_direction != NoDirection && way == (_direction + 2) % Directions.Length)
            {
                continue;
            }

            var next = map.StateAtOrDefault(tileX + Directions[way].Dx, tileY + Directions[way].Dy);
            if (!next.IsRail)
            {
                continue;
            }

            // Ways that add up to 3 are north and west, or east and south.
            Frame = next.IsTunnel ? TrainFrame.Tunnel
                : _direction == NoDirection || _direction == way ? (way % 2 == 0 ? TrainFrame.Down : TrainFrame.Across)
                : _direction + way == 3 ? TrainFrame.TurningNorthwest
                : TrainFrame.TurningNortheast;
            _direction = way;
            return;
        }

        if (_direction == NoDirection)
        {
            IsRunning = false;
            return;
        }

        _direction = NoDirection;
    }
}
