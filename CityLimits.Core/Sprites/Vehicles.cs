using CityLimits.Core.Randomness;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Core.Sprites;

/// <summary>
/// The city's aircraft and ships, as Micropolis flies and sails them: a plane that takes off from
/// a powered airport and wanders the sky, a helicopter that sets out from one, watches the traffic,
/// and flies home, and a ship that comes in from a channel at the map's edge for a powered seaport
/// and wanders the water. There is one of each at most. They move twice each step, as Micropolis'
/// sprites move twice for each of its simulation steps, on the city's own dice, so they are part of
/// what the city does and are saved. When disasters allow, a plane that comes within 30 pixels of
/// the helicopter brings both down, and a ship that runs aground is wrecked; the city is told where
/// (<see cref="VehicleEvent"/>) and sets the fires.
/// </summary>
public sealed class Vehicles
{
    /// <summary>The length of Micropolis' animation cycle, which the vehicles' turns count.</summary>
    public const int CycleLength = 960;

    /// <summary>How close, in pixels, two aircraft come before they collide, as in Micropolis.</summary>
    public const int CollisionDistance = 30;

    /// <summary>How many moves the helicopter flies before it heads home, as in Micropolis.</summary>
    public const int HelicopterFlight = 1500;

    /// <summary>The traffic over which the helicopter sometimes reports it, as in Micropolis.</summary>
    public const int HeavyTraffic = 170;

    private const int TileSize = 16;

    // Micropolis' directions are 1 to 8, north and on clockwise; 9 to 11 are the plane taking off.
    private static readonly int[] PlaneDx = [0, 0, 6, 8, 6, 0, -6, -8, -6, 8, 8, 8];
    private static readonly int[] PlaneDy = [0, -8, -6, 0, 6, 8, 6, 0, -6, 0, 0, 0];
    private static readonly int[] HelicopterDx = [0, 0, 3, 5, 3, 0, -3, -5, -3];
    private static readonly int[] HelicopterDy = [0, -5, -3, 0, 3, 5, 3, 0, -3];
    private static readonly int[] ShipTileDx = [0, 0, 1, 1, 1, 0, -1, -1, -1];
    private static readonly int[] ShipTileDy = [0, -1, -1, 0, 1, 1, 1, 0, -1];
    private static readonly int[] ShipDx = [0, 0, 2, 2, 2, 0, -2, -2, -2];
    private static readonly int[] ShipDy = [0, -2, -2, 0, 2, 2, 2, 0, -2];
    private static readonly int[] Directions = [0, 3, 2, 1, 3, 4, 5, 7, 6, 5, 7, 8, 1];

    // The direction a ship faces as it comes in from each edge: south from the north, and so on.
    private const int FromNorth = 5;
    private const int FromEast = 7;
    private const int FromSouth = 1;
    private const int FromWest = 3;

    // A ship searching for a way has none to come back by.
    private const int NoWayBack = 10;

    /// <summary>The plane, or null when none is flying.</summary>
    public Plane? Plane { get; private set; }

    /// <summary>The helicopter, or null when none is flying.</summary>
    public Helicopter? Helicopter { get; private set; }

    /// <summary>The ship, or null when none is sailing.</summary>
    public Ship? Ship { get; private set; }

    /// <summary>
    /// Where Micropolis' animation cycle is, from 0 to <see cref="CycleLength"/> less one: two moves a
    /// step, so it is worked out from the city's steps and never saved.
    /// </summary>
    public int Cycle { get; private set; }

    /// <summary>
    /// The scan passing a powered airport: one time in six a plane takes off, unless one is flying,
    /// and one time in thirteen a helicopter, as in Micropolis; the dice are rolled either way.
    /// </summary>
    public void VisitAirport(int x, int y, TileMap map, DeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);
        if (random.NextInt(0, 6) == 0 && Plane is null)
        {
            var (px, py) = Centre(x, y);
            var east = x <= map.Width - 20;
            Plane = new Plane { X = px, Y = py, Frame = east ? 11 : 7, DestinationX = east ? px + 200 : px - 200, DestinationY = py };
        }

        if (random.NextInt(0, 13) == 0 && Helicopter is null)
        {
            var (hx, hy) = Centre(x, y);
            Helicopter = new Helicopter
            {
                X = hx,
                Y = hy,
                Frame = 5,
                DestinationX = (random.NextInt(0, map.Width) * TileSize) + 8,
                DestinationY = (random.NextInt(0, map.Height) * TileSize) + 8,
                HomeX = hx,
                HomeY = hy,
                MovesLeft = HelicopterFlight,
            };
        }
    }

    /// <summary>
    /// The scan passing a powered seaport: with no ship sailing, one comes in from a channel at an
    /// edge picked at random, when that edge has one, as in Micropolis.
    /// </summary>
    public void VisitSeaport(TileMap map, DeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);
        if (Ship is not null)
        {
            return;
        }

        switch (random.NextInt(0, 4))
        {
            case 0:
                for (var x = 4; x < map.Width - 2; x++)
                {
                    if (IsChannel(map, x, 0))
                    {
                        Ship = NewShip(x, 0, FromNorth);
                        return;
                    }
                }

                break;
            case 1:
                for (var y = 1; y < map.Height - 2; y++)
                {
                    if (IsChannel(map, 0, y))
                    {
                        Ship = NewShip(0, y, FromWest);
                        return;
                    }
                }

                break;
            case 2:
                for (var x = 4; x < map.Width - 2; x++)
                {
                    if (IsChannel(map, x, map.Height - 1))
                    {
                        Ship = NewShip(x, map.Height - 1, FromSouth);
                        return;
                    }
                }

                break;
            default:
                for (var y = 1; y < map.Height - 2; y++)
                {
                    if (IsChannel(map, map.Width - 1, y))
                    {
                        Ship = NewShip(map.Width - 1, y, FromEast);
                        return;
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Moves each vehicle once, as one of Micropolis' animation steps does, and adds what came of
    /// it to <paramref name="events"/>: a crash, a wreck, or the helicopter's traffic report.
    /// </summary>
    /// <param name="step">The city's step count, which sets where the animation cycle is.</param>
    /// <param name="move">Which of the step's two moves this is, 0 or 1.</param>
    /// <param name="map">The map the ship sails and runs aground on.</param>
    /// <param name="traffic">The traffic the helicopter watches.</param>
    /// <param name="random">The city's dice.</param>
    /// <param name="airCrashes">Whether aircraft can collide.</param>
    /// <param name="shipwrecks">Whether a ship can run aground.</param>
    /// <param name="events">Where what happened is added.</param>
    public void Move(long step, int move, TileMap map, TrafficMap traffic, DeterministicRandom random, bool airCrashes, bool shipwrecks, List<VehicleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(traffic);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(events);
        Cycle = (int)(((step * 2) + move) % CycleLength);
        MovePlane(map, random, airCrashes, events);
        MoveHelicopter(traffic, random, events);
        MoveShip(map, random, shipwrecks, events);
    }

    private void MovePlane(TileMap map, DeterministicRandom random, bool airCrashes, List<VehicleEvent> events)
    {
        if (Plane is not { } plane)
        {
            return;
        }

        var z = plane.Frame;
        if (Cycle % 5 == 0)
        {
            if (z > 8)
            {
                // Still taking off: the last take-off frame turns it east.
                z--;
                if (z < 9)
                {
                    z = 3;
                }
            }
            else
            {
                z = TurnTo(z, DirectionTo(plane.X, plane.Y, plane.DestinationX, plane.DestinationY));
            }

            plane.Frame = z;
        }

        if (Distance(plane.X, plane.Y, plane.DestinationX, plane.DestinationY) < 50)
        {
            plane.DestinationX = (random.NextInt(0, map.Width) * TileSize) + 8;
            plane.DestinationY = (random.NextInt(0, map.Height) * TileSize) + 8;
        }

        if (airCrashes && Helicopter is { } helicopter && Distance(plane.X, plane.Y, helicopter.X, helicopter.Y) < CollisionDistance)
        {
            events.Add(new VehicleEvent(VehicleEventKind.HelicopterCrash, helicopter.X / TileSize, helicopter.Y / TileSize));
            events.Add(new VehicleEvent(VehicleEventKind.PlaneCrash, plane.X / TileSize, plane.Y / TileSize));
            Helicopter = null;
            Plane = null;
            return;
        }

        plane.X += PlaneDx[z];
        plane.Y += PlaneDy[z];
    }

    private void MoveHelicopter(TrafficMap traffic, DeterministicRandom random, List<VehicleEvent> events)
    {
        if (Helicopter is not { } helicopter)
        {
            return;
        }

        if (helicopter.MovesLeft > 0)
        {
            helicopter.MovesLeft--;
        }

        // Its flight over, it heads home and lands there. N6d sends it after a monster or a tornado.
        if (helicopter.MovesLeft == 0)
        {
            (helicopter.DestinationX, helicopter.DestinationY) = (helicopter.HomeX, helicopter.HomeY);
            if (Distance(helicopter.X, helicopter.Y, helicopter.HomeX, helicopter.HomeY) < CollisionDistance)
            {
                Helicopter = null;
                return;
            }
        }

        if (Cycle % 200 == 0)
        {
            var (x, y) = (helicopter.X / TileSize, helicopter.Y / TileSize);
            if (traffic.DensityAt(x, y) > HeavyTraffic && random.NextInt(0, 8) == 0)
            {
                events.Add(new VehicleEvent(VehicleEventKind.HeavyTraffic, x, y));
            }
        }

        var z = helicopter.Frame;
        if (Cycle % 3 == 0)
        {
            z = TurnTo(z, DirectionTo(helicopter.X, helicopter.Y, helicopter.DestinationX, helicopter.DestinationY));
            helicopter.Frame = z;
        }

        helicopter.X += HelicopterDx[z];
        helicopter.Y += HelicopterDy[z];
    }

    // As Micropolis' ship: every ninth move it turns toward its way, or, facing it, looks for the
    // next, at random among the eight, never straight back; between, it sails on two pixels. A ship
    // that looked and found only land runs aground, and one that leaves the map is gone.
    private void MoveShip(TileMap map, DeterministicRandom random, bool shipwrecks, List<VehicleEvent> events)
    {
        if (Ship is not { } ship)
        {
            return;
        }

        var aground = false;
        ship.Count--;
        if (ship.Count <= 0)
        {
            ship.Count = 9;
            if (ship.NewDirection != ship.Frame)
            {
                ship.Frame = TurnTo(ship.Frame, ship.NewDirection);
                return;
            }

            var first = random.NextInt(0, 8);
            var found = false;
            for (var tried = first; tried < first + 8; tried++)
            {
                var z = (tried % 8) + 1;
                if (z == ship.WayBack)
                {
                    continue;
                }

                var (x, y) = ((ship.X / TileSize) + ShipTileDx[z], (ship.Y / TileSize) + ShipTileDy[z]);
                if (!map.Contains(x, y))
                {
                    continue;
                }

                // The last tile looked at decides whether it runs aground, as in Micropolis.
                aground = map.StateAt(x, y).Kind != TileKind.Water;
                if (IsChannel(map, x, y) || IsBridge(map.StateAt(x, y)) || IsStraightThrough(map.StateAt(x, y), ship.WayBack, z))
                {
                    ship.NewDirection = z;
                    ship.Frame = TurnTo(ship.Frame, z);
                    ship.WayBack = z + 4 > 8 ? z - 4 : z + 4;
                    found = true;
                    aground = false;
                    break;
                }
            }

            if (!found)
            {
                ship.WayBack = NoWayBack;
                ship.NewDirection = random.NextInt(0, 8) + 1;
            }
        }
        else if (ship.Frame == ship.NewDirection)
        {
            ship.X += ShipDx[ship.Frame];
            ship.Y += ShipDy[ship.Frame];
        }

        if (!map.Contains(ship.X / TileSize, ship.Y / TileSize))
        {
            Ship = null;
            return;
        }

        if (aground && shipwrecks)
        {
            events.Add(new VehicleEvent(VehicleEventKind.Shipwreck, ship.X / TileSize, ship.Y / TileSize));
            Ship = null;
        }
    }

    /// <summary>Takes the plane out of the air for a scenario's crash, saying where it was; null with none flying.</summary>
    internal (int X, int Y)? DownPlane()
    {
        if (Plane is not { } plane)
        {
            return null;
        }

        Plane = null;
        return (plane.X / TileSize, plane.Y / TileSize);
    }

    /// <summary>Takes the ship off the water for a scenario's wreck, saying where it was; null with none sailing.</summary>
    internal (int X, int Y)? SinkShip()
    {
        if (Ship is not { } ship)
        {
            return null;
        }

        Ship = null;
        return (ship.X / TileSize, ship.Y / TileSize);
    }

    internal VehiclesSave Capture() => new()
    {
        Plane = Plane is { } plane ? [plane.X, plane.Y, plane.Frame, plane.DestinationX, plane.DestinationY] : null,
        Helicopter = Helicopter is { } copter ? [copter.X, copter.Y, copter.Frame, copter.DestinationX, copter.DestinationY, copter.HomeX, copter.HomeY, copter.MovesLeft] : null,
        Ship = Ship is { } ship ? [ship.X, ship.Y, ship.Frame, ship.NewDirection, ship.WayBack, ship.Count] : null,
    };

    // A vehicle that isn't about is written as no numbers at all, which the save's arrays read back as empty.
    internal void Restore(VehiclesSave save)
    {
        Plane = save.Plane is { Length: > 0 } plane
            ? new Plane { X = plane.Length == 5 ? plane[0] : throw Wrong("plane", 5), Y = plane[1], Frame = SaveChecks.InRange(plane[2], 1, 11, "vehicles.plane frame"), DestinationX = plane[3], DestinationY = plane[4] }
            : null;
        Helicopter = save.Helicopter is { Length: > 0 } copter
            ? new Helicopter
            {
                X = copter.Length == 8 ? copter[0] : throw Wrong("helicopter", 8),
                Y = copter[1],
                Frame = SaveChecks.InRange(copter[2], 1, 8, "vehicles.helicopter frame"),
                DestinationX = copter[3],
                DestinationY = copter[4],
                HomeX = copter[5],
                HomeY = copter[6],
                MovesLeft = SaveChecks.InRange(copter[7], 0, HelicopterFlight, "vehicles.helicopter moves left"),
            }
            : null;
        Ship = save.Ship is { Length: > 0 } ship
            ? new Ship
            {
                X = ship.Length == 6 ? ship[0] : throw Wrong("ship", 6),
                Y = ship[1],
                Frame = SaveChecks.InRange(ship[2], 1, 8, "vehicles.ship frame"),
                NewDirection = SaveChecks.InRange(ship[3], 1, 8, "vehicles.ship new direction"),
                WayBack = SaveChecks.InRange(ship[4], 1, NoWayBack, "vehicles.ship way back"),
                Count = SaveChecks.InRange(ship[5], 0, 9, "vehicles.ship count"),
            }
            : null;
    }

    private static InvalidSave Wrong(string vehicle, int count) => new($"vehicles.{vehicle}: needs {count} numbers.");

    private static Ship NewShip(int x, int y, int facing)
    {
        var (sx, sy) = Centre(x, y);
        return new Ship { X = sx, Y = sy, Frame = facing, NewDirection = facing, WayBack = NoWayBack, Count = 1 };
    }

    private static (int X, int Y) Centre(int x, int y) => ((x * TileSize) + 8, (y * TileSize) + 8);

    // Micropolis' channel is deep water; here, water with water on all four sides, and nothing built on it.
    private static bool IsChannel(TileMap map, int x, int y)
    {
        var state = map.StateAtOrDefault(x, y);
        if (state.Kind != TileKind.Water || state.Use != TileUse.None)
        {
            return false;
        }

        foreach (var (_, dx, dy) in Sides.All)
        {
            if (map.StateAtOrDefault(x + dx, y + dy, new TileState(TileKind.Water)).Kind != TileKind.Water)
            {
                return false;
            }
        }

        return true;
    }

    // A ship passes under a bridge, as under Micropolis' drawbridges.
    private static bool IsBridge(TileState state) => state.Kind == TileKind.Water && state.HasRoad;

    // It passes over a power line or a tunnel under the water only going straight on, as in Micropolis.
    private static bool IsStraightThrough(TileState state, int wayBack, int direction)
    {
        var ahead = wayBack + 4 > 8 ? wayBack - 4 : wayBack + 4;
        return direction == ahead && state.Kind == TileKind.Water && (state.HasPowerLine || state.IsRail);
    }

    // Micropolis' getDir: which of the eight ways points most nearly from one place to another.
    private static int DirectionTo(int fromX, int fromY, int toX, int toY)
    {
        var (dx, dy) = (toX - fromX, toY - fromY);
        var z = dx < 0 ? (dy < 0 ? 11 : 8) : (dy < 0 ? 2 : 5);
        (dx, dy) = (Math.Abs(dx), Math.Abs(dy));
        if (dx * 2 < dy)
        {
            z++;
        }
        else if (dy * 2 < dx)
        {
            z--;
        }

        return z is >= 1 and <= 12 ? Directions[z] : 0;
    }

    // Micropolis' turnTo: an eighth of a turn toward a way, the shorter way round.
    private static int TurnTo(int facing, int toward)
    {
        if (facing == toward || toward == 0)
        {
            return facing;
        }

        facing += facing < toward ? (toward - facing < 4 ? 1 : -1) : (facing - toward < 4 ? -1 : 1);
        return facing > 8 ? 1 : facing < 1 ? 8 : facing;
    }

    private static int Distance(int x0, int y0, int x1, int y1) => Math.Abs(x0 - x1) + Math.Abs(y0 - y1);
}

/// <summary>The plane: where it is, in pixels, its frame, 1 to 8 its heading and 9 to 11 taking off, and where it heads.</summary>
public sealed class Plane
{
    /// <summary>Where its middle is across the map, in pixels.</summary>
    public int X { get; internal set; }

    /// <summary>Where its middle is down the map, in pixels.</summary>
    public int Y { get; internal set; }

    /// <summary>Its frame: 1 to 8, north and on clockwise, or 9 to 11 as it takes off.</summary>
    public int Frame { get; internal set; }

    /// <summary>Where it heads across the map, in pixels.</summary>
    public int DestinationX { get; internal set; }

    /// <summary>Where it heads down the map, in pixels.</summary>
    public int DestinationY { get; internal set; }
}

/// <summary>The helicopter: where it is, its heading, where it heads, the airport it came from, and the moves left of its flight.</summary>
public sealed class Helicopter
{
    /// <summary>Where its middle is across the map, in pixels.</summary>
    public int X { get; internal set; }

    /// <summary>Where its middle is down the map, in pixels.</summary>
    public int Y { get; internal set; }

    /// <summary>Its heading, 1 to 8, north and on clockwise.</summary>
    public int Frame { get; internal set; }

    /// <summary>Where it heads across the map, in pixels.</summary>
    public int DestinationX { get; internal set; }

    /// <summary>Where it heads down the map, in pixels.</summary>
    public int DestinationY { get; internal set; }

    /// <summary>Where it came from and lands, across the map, in pixels.</summary>
    public int HomeX { get; internal set; }

    /// <summary>Where it came from and lands, down the map, in pixels.</summary>
    public int HomeY { get; internal set; }

    /// <summary>How many moves its flight has left before it heads home.</summary>
    public int MovesLeft { get; internal set; }
}

/// <summary>The ship: where it is, its heading, the way it is turning to, the way it came, and the moves until it next looks.</summary>
public sealed class Ship
{
    /// <summary>Where its middle is across the map, in pixels.</summary>
    public int X { get; internal set; }

    /// <summary>Where its middle is down the map, in pixels.</summary>
    public int Y { get; internal set; }

    /// <summary>Its heading, 1 to 8, north and on clockwise.</summary>
    public int Frame { get; internal set; }

    /// <summary>The way it is turning to, 1 to 8.</summary>
    public int NewDirection { get; internal set; }

    /// <summary>The way it came, which it won't take, 1 to 8, or 10 for none.</summary>
    public int WayBack { get; internal set; }

    /// <summary>How many moves until it next turns or looks for its way.</summary>
    public int Count { get; internal set; }
}

/// <summary>What happened to a vehicle as it moved, and on which tile.</summary>
public enum VehicleEventKind
{
    /// <summary>The plane crashed.</summary>
    PlaneCrash,

    /// <summary>The helicopter crashed.</summary>
    HelicopterCrash,

    /// <summary>The ship ran aground and was wrecked.</summary>
    Shipwreck,

    /// <summary>The helicopter saw heavy traffic below it.</summary>
    HeavyTraffic,
}

/// <summary>What happened to a vehicle, and the tile it happened over.</summary>
public readonly record struct VehicleEvent(VehicleEventKind Kind, int X, int Y);
