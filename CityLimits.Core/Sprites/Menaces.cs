using CityLimits.Core.Randomness;
using CityLimits.Core.Saving;
using CityLimits.Core.World;

namespace CityLimits.Core.Sprites;

/// <summary>
/// The disasters that roam the city, as Micropolis' sprites do: a tornado that wanders at random
/// for a while, and a monster that comes up out of the water, makes for the most polluted place,
/// and goes back where it came from. There is one of each at most. They move twice each step, as
/// the vehicles do, on the city's own dice, so they are part of what the city does and are saved.
/// Each wrecks the tile it passes, which the city does (<see cref="Move"/>'s trampled tiles), and
/// brings down any plane, helicopter, or ship that comes within 30 pixels, when disasters allow.
/// </summary>
public sealed class Menaces
{
    /// <summary>How many moves a tornado lasts before it may blow itself out, as in Micropolis.</summary>
    public const int TornadoMoves = 200;

    /// <summary>How close, in pixels, the monster comes to where it heads before it turns for home, as in Micropolis.</summary>
    public const int MonsterArrival = 60;

    private const int TileSize = Vehicles.TileSize;
    private const int MonsterTries = 300;
    private const int TornadoEndChance = 501;
    private const int MonsterTurnChance = 11;
    private const int MonsterFrames = 16;
    private const int TornadoFrames = 3;

    // The tornado's moves, one picked at random each time, as Micropolis' CDx and CDy.
    private static readonly int[] TornadoDx = [2, 3, 2, 0, -2, -3];
    private static readonly int[] TornadoDy = [-2, 0, 2, 3, 2, 0];

    // The monster walks diagonally, 0 to 3 northeast and on clockwise; 4 is standing to turn.
    private static readonly int[] MonsterDx = [2, 2, -2, -2, 0];
    private static readonly int[] MonsterDy = [-2, 2, 2, -2, 0];

    // Micropolis' ND1, ND2, nn1, and nn2: the frames it turns through, a quarter turn either way.
    private static readonly int[] TurnLeft = [0, 1, 2, 3];
    private static readonly int[] TurnRight = [1, 2, 3, 0];
    private static readonly int[] FromTurnLeft = [2, 5, 8, 11];
    private static readonly int[] FromTurnRight = [11, 2, 5, 8];

    /// <summary>The tornado, or null when none is blowing.</summary>
    public Tornado? Tornado { get; private set; }

    /// <summary>The monster, or null when none is about.</summary>
    public Monster? Monster { get; private set; }

    /// <summary>Where the helicopter chases: the monster, else the tornado, in pixels; null with neither about.</summary>
    public (int X, int Y)? Quarry => Monster is { } monster ? (monster.X, monster.Y) : Tornado is { } tornado ? (tornado.X, tornado.Y) : null;

    /// <summary>
    /// Calls up a tornado, as Micropolis' makeTornado: one already blowing lasts its full time
    /// again; otherwise one touches down somewhere at least ten tiles from each edge, or in the
    /// middle of a map too small for that. The place is rolled for either way.
    /// </summary>
    /// <param name="map">The map it touches down on.</param>
    /// <param name="random">The city's dice.</param>
    /// <param name="allowed">Whether it touches down at all, or only the dice are rolled.</param>
    /// <returns>Whether a new tornado touched down.</returns>
    public bool CallTornado(TileMap map, DeterministicRandom random, bool allowed = true)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);
        if (Tornado is { } blowing)
        {
            if (allowed)
            {
                blowing.MovesLeft = TornadoMoves;
            }

            return false;
        }

        var (x, y) = map.Width >= 20 && map.Height >= 20
            ? (random.NextInt(0, map.Width - 19) + 10, random.NextInt(0, map.Height - 19) + 10)
            : (map.Width / 2, map.Height / 2);
        if (!allowed)
        {
            return false;
        }

        var (px, py) = Vehicles.Centre(x, y);
        Tornado = new Tornado { X = px, Y = py, Frame = 1, MovesLeft = TornadoMoves };
        return true;
    }

    /// <summary>
    /// Calls up the monster, as Micropolis' makeMonster: one already about makes for the most
    /// polluted place again; otherwise one comes up out of open water, tried for 300 times
    /// away from the edges, or in the middle of the map, and makes for it. The place is rolled
    /// for either way.
    /// </summary>
    /// <param name="map">The map it walks.</param>
    /// <param name="random">The city's dice.</param>
    /// <param name="mostPolluted">The tile it makes for.</param>
    /// <param name="allowed">Whether it comes at all, or only the dice are rolled.</param>
    /// <returns>Whether a new monster came.</returns>
    public bool CallMonster(TileMap map, DeterministicRandom random, (int X, int Y) mostPolluted, bool allowed = true)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);
        var (destinationX, destinationY) = Vehicles.Centre(mostPolluted.X, mostPolluted.Y);
        if (Monster is { } about)
        {
            // Micropolis sends it to the place's tile number rather than its pixels here, a slip
            // that sends it to the map's corner; CityLimits sends it to the place.
            if (allowed)
            {
                (about.DestinationX, about.DestinationY, about.HeadingHome) = (destinationX, destinationY, false);
            }

            return false;
        }

        var (x, y) = (map.Width / 2, map.Height / 2);
        if (map.Width >= 20 && map.Height >= 10)
        {
            for (var tries = 0; tries < MonsterTries; tries++)
            {
                var (tx, ty) = (random.NextInt(0, map.Width - 19) + 10, random.NextInt(0, map.Height - 9) + 5);
                if (Vehicles.IsChannel(map, tx, ty))
                {
                    (x, y) = (tx, ty);
                    break;
                }
            }
        }

        if (!allowed)
        {
            return false;
        }

        var (px, py) = Vehicles.Centre(x, y);
        var east = x > map.Width / 2;
        var south = y > map.Height / 2;
        Monster = new Monster
        {
            X = px,
            Y = py,
            Frame = east ? (south ? 10 : 7) : (south ? 1 : 4),
            DestinationX = destinationX,
            DestinationY = destinationY,
            HomeX = px,
            HomeY = py,
            Step = 1,
        };
        return true;
    }

    /// <summary>
    /// Moves the monster and the tornado once, as one of Micropolis' animation steps does. Each adds
    /// the tile it ends on to <paramref name="trampled"/>, for the city to wreck, and brings down
    /// any vehicle within 30 pixels, when disasters allow, adding the crash to <paramref name="events"/>.
    /// </summary>
    /// <param name="map">The map they roam.</param>
    /// <param name="random">The city's dice.</param>
    /// <param name="vehicles">The vehicles they can bring down.</param>
    /// <param name="airCrashes">Whether aircraft can come down.</param>
    /// <param name="shipwrecks">Whether the ship can be wrecked.</param>
    /// <param name="events">Where the vehicles brought down are added.</param>
    /// <param name="trampled">Where the tiles to wreck are added.</param>
    public void Move(TileMap map, DeterministicRandom random, Vehicles vehicles, bool airCrashes, bool shipwrecks, List<VehicleEvent> events, List<(int X, int Y)> trampled)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(trampled);
        MoveMonster(map, random, vehicles, airCrashes, shipwrecks, events, trampled);
        MoveTornado(map, random, vehicles, airCrashes, shipwrecks, events, trampled);
    }

    // As Micropolis' monster: it walks diagonally, two pixels a move, its legs going back and forth
    // through three frames, and now and then stops to turn a quarter toward where it heads. Near
    // the most polluted place it turns for home, and there it goes. Off the map, it is gone.
    // Micropolis rolls a die for when it next roars here; CityLimits has no sound, and doesn't.
    private void MoveMonster(TileMap map, DeterministicRandom random, Vehicles vehicles, bool airCrashes, bool shipwrecks, List<VehicleEvent> events, List<(int X, int Y)> trampled)
    {
        if (Monster is not { } monster)
        {
            return;
        }

        var d = (monster.Frame - 1) / 3;
        var z = (monster.Frame - 1) % 3;
        if (d < 4)
        {
            if (z == 2)
            {
                monster.Step = -1;
            }

            if (z == 0)
            {
                monster.Step = 1;
            }

            z += monster.Step;
            if (Vehicles.Distance(monster.X, monster.Y, monster.DestinationX, monster.DestinationY) < MonsterArrival)
            {
                if (monster.HeadingHome)
                {
                    Monster = null;
                    return;
                }

                (monster.DestinationX, monster.DestinationY, monster.HeadingHome) = (monster.HomeX, monster.HomeY, true);
            }

            var heading = (Vehicles.DirectionTo(monster.X, monster.Y, monster.DestinationX, monster.DestinationY) - 1) / 2;
            if (heading != d && random.NextInt(0, MonsterTurnChance) == 0)
            {
                z = random.NextInt(0, 2) == 0 ? TurnLeft[d] : TurnRight[d];
                d = 4;
            }
        }
        else if (random.NextInt(0, 4) == 0)
        {
            var turned = (monster.Frame - 13) % 4;
            var frame = random.NextInt(0, 2) == 0 ? FromTurnLeft[turned] : FromTurnRight[turned];
            (d, z) = ((frame - 1) / 3, (frame - 1) % 3);
        }
        else
        {
            d = 4;
        }

        monster.Frame = (d * 3) + z + 1;
        monster.X += MonsterDx[d];
        monster.Y += MonsterDy[d];
        if (!map.Contains(monster.X / TileSize, monster.Y / TileSize))
        {
            Monster = null;
            return;
        }

        BringDown(monster.X, monster.Y, vehicles, airCrashes, shipwrecks, events);
        trampled.Add((monster.X / TileSize, monster.Y / TileSize));
    }

    // As Micropolis' tornado: it spins through its three frames and lurches one of six ways at
    // random. Off the map it is gone, and once its time is up it blows itself out one move in 501.
    private void MoveTornado(TileMap map, DeterministicRandom random, Vehicles vehicles, bool airCrashes, bool shipwrecks, List<VehicleEvent> events, List<(int X, int Y)> trampled)
    {
        if (Tornado is not { } tornado)
        {
            return;
        }

        if (tornado.Frame == 2)
        {
            tornado.Frame = tornado.Rising ? 3 : 1;
        }
        else
        {
            tornado.Rising = tornado.Frame == 1;
            tornado.Frame = 2;
        }

        if (tornado.MovesLeft > 0)
        {
            tornado.MovesLeft--;
        }

        BringDown(tornado.X, tornado.Y, vehicles, airCrashes, shipwrecks, events);
        var way = random.NextInt(0, TornadoDx.Length);
        tornado.X += TornadoDx[way];
        tornado.Y += TornadoDy[way];
        if (!map.Contains(tornado.X / TileSize, tornado.Y / TileSize))
        {
            Tornado = null;
            return;
        }

        if (tornado.MovesLeft == 0 && random.NextInt(0, TornadoEndChance) == 0)
        {
            Tornado = null;
            return;
        }

        trampled.Add((tornado.X / TileSize, tornado.Y / TileSize));
    }

    // Micropolis' collision: a vehicle within 30 pixels explodes. With the switch for its kind off,
    // it passes unharmed.
    private static void BringDown(int x, int y, Vehicles vehicles, bool airCrashes, bool shipwrecks, List<VehicleEvent> events)
    {
        if (airCrashes && vehicles.Plane is { } plane && Vehicles.Distance(x, y, plane.X, plane.Y) < Vehicles.CollisionDistance && vehicles.DownPlane() is { } planeAt)
        {
            events.Add(new VehicleEvent(VehicleEventKind.PlaneCrash, planeAt.X, planeAt.Y));
        }

        if (airCrashes && vehicles.Helicopter is { } copter && Vehicles.Distance(x, y, copter.X, copter.Y) < Vehicles.CollisionDistance && vehicles.DownHelicopter() is { } copterAt)
        {
            events.Add(new VehicleEvent(VehicleEventKind.HelicopterCrash, copterAt.X, copterAt.Y));
        }

        if (shipwrecks && vehicles.Ship is { } ship && Vehicles.Distance(x, y, ship.X, ship.Y) < Vehicles.CollisionDistance && vehicles.SinkShip() is { } shipAt)
        {
            events.Add(new VehicleEvent(VehicleEventKind.Shipwreck, shipAt.X, shipAt.Y));
        }
    }

    internal MenacesSave Capture() => new()
    {
        Tornado = Tornado is { } tornado ? [tornado.X, tornado.Y, tornado.Frame, tornado.MovesLeft, tornado.Rising ? 1 : 0] : null,
        Monster = Monster is { } monster ? [monster.X, monster.Y, monster.Frame, monster.DestinationX, monster.DestinationY, monster.HomeX, monster.HomeY, monster.Step, monster.HeadingHome ? 1 : 0] : null,
    };

    // A menace that isn't about is written as no numbers at all, which the save's arrays read back as empty.
    internal void Restore(MenacesSave save)
    {
        Tornado = save.Tornado is { Length: > 0 } tornado
            ? new Tornado
            {
                X = tornado.Length == 5 ? tornado[0] : throw Wrong("tornado", 5),
                Y = tornado[1],
                Frame = SaveChecks.InRange(tornado[2], 1, TornadoFrames, "menaces.tornado frame"),
                MovesLeft = SaveChecks.InRange(tornado[3], 0, TornadoMoves, "menaces.tornado moves left"),
                Rising = SaveChecks.InRange(tornado[4], 0, 1, "menaces.tornado rising") == 1,
            }
            : null;
        Monster = save.Monster is { Length: > 0 } monster
            ? new Monster
            {
                X = monster.Length == 9 ? monster[0] : throw Wrong("monster", 9),
                Y = monster[1],
                Frame = SaveChecks.InRange(monster[2], 1, MonsterFrames, "menaces.monster frame"),
                DestinationX = monster[3],
                DestinationY = monster[4],
                HomeX = monster[5],
                HomeY = monster[6],
                Step = monster[7] is -1 or 1 ? monster[7] : throw new InvalidSave($"menaces.monster step: needs -1 or 1, not {monster[7]}."),
                HeadingHome = SaveChecks.InRange(monster[8], 0, 1, "menaces.monster heading home") == 1,
            }
            : null;
    }

    private static InvalidSave Wrong(string menace, int count) => new($"menaces.{menace}: needs {count} numbers.");
}

/// <summary>The tornado: where it is, in pixels, its frame, the moves left of its time, and which way its spin is going.</summary>
public sealed class Tornado
{
    /// <summary>Where its foot is across the map, in pixels.</summary>
    public int X { get; internal set; }

    /// <summary>Where its foot is down the map, in pixels.</summary>
    public int Y { get; internal set; }

    /// <summary>Its frame, 1 to 3.</summary>
    public int Frame { get; internal set; }

    /// <summary>The moves left before it may blow itself out.</summary>
    public int MovesLeft { get; internal set; }

    /// <summary>Whether its frames are counting up, from 1 toward 3, rather than down.</summary>
    public bool Rising { get; internal set; }
}

/// <summary>The monster: where it is, its frame, where it heads, where it came from, and its stride.</summary>
public sealed class Monster
{
    /// <summary>Where its middle is across the map, in pixels.</summary>
    public int X { get; internal set; }

    /// <summary>Where its middle is down the map, in pixels.</summary>
    public int Y { get; internal set; }

    /// <summary>
    /// Its frame: 1 to 3 walking northeast, 4 to 6 southeast, 7 to 9 southwest, and 10 to 12
    /// northwest; 13 to 16 standing to turn, north and on clockwise.
    /// </summary>
    public int Frame { get; internal set; }

    /// <summary>Where it heads across the map, in pixels.</summary>
    public int DestinationX { get; internal set; }

    /// <summary>Where it heads down the map, in pixels.</summary>
    public int DestinationY { get; internal set; }

    /// <summary>Where it came up, across the map, in pixels.</summary>
    public int HomeX { get; internal set; }

    /// <summary>Where it came up, down the map, in pixels.</summary>
    public int HomeY { get; internal set; }

    /// <summary>Which way its legs go through their frames, 1 or -1.</summary>
    public int Step { get; internal set; }

    /// <summary>Whether it has been to the polluted place and is going home.</summary>
    public bool HeadingHome { get; internal set; }
}
