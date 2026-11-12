/// What the ground of a tile is, before anything is built on it.
public enum TileKind: Int, CaseIterable, Sendable {
    case grass
    case water
    case trees
    case rubble

    /// Whether anything can be built here (trees and rubble are cleared first).
    public var canBuildOn: Bool { self != .water }

    /// Whether building here first pays to clear the ground.
    public var needsClearing: Bool { self == .trees || self == .rubble }
}

/// What has been built on a tile. The order is the order specs and saves list them in.
public enum TileUse: Int, CaseIterable, Comparable, Sendable {
    case none
    case road
    case residential
    case commercial
    case industrial
    case coalPowerPlant
    case nuclearPowerPlant
    case powerLine
    /// A road running east to west over a power line running north to south.
    case roadAcrossPowerLine
    /// A road running north to south under a power line running east to west.
    case roadDownPowerLine
    case fireStation
    case policeStation
    case park
    case hospital
    case church
    case rail
    /// Rail running east to west over a road running north to south.
    case railAcrossRoad
    /// Rail running north to south over a road running east to west.
    case railDownRoad
    /// Rail running east to west under a power line running north to south.
    case railAcrossPowerLine
    /// Rail running north to south under a power line running east to west.
    case railDownPowerLine
    case stadium
    case seaport
    case airport

    public static func < (lhs: TileUse, rhs: TileUse) -> Bool { lhs.rawValue < rhs.rawValue }

    /// Residential, commercial, or industrial: land that grows on its own.
    public var isZone: Bool { self == .residential || self == .commercial || self == .industrial }

    public var isPowerPlant: Bool { self == .coalPowerPlant || self == .nuclearPowerPlant }

    /// A building the city puts up by itself on a residential zone.
    public var isCommunityBuilding: Bool { self == .hospital || self == .church }

    public var isService: Bool { self == .fireStation || self == .policeStation }

    public var isFacility: Bool { self == .stadium || self == .seaport || self == .airport }

    /// How many tiles a side the use covers: 1 for a tile on its own.
    public var footprint: Int {
        switch self {
        case .residential, .commercial, .industrial, .fireStation, .policeStation, .hospital, .church: ZoneLayout.size
        case .coalPowerPlant, .nuclearPowerPlant, .stadium, .seaport: 4
        case .airport: 6
        default: 1
        }
    }
}

/// The four sides of a tile, as a set: which neighbours a road, rail, power line, water, or wood
/// joins.
public struct Sides: OptionSet, Hashable, Sendable {
    public let rawValue: UInt8
    public init(rawValue: UInt8) { self.rawValue = rawValue }

    public static let north = Sides(rawValue: 1)
    public static let east = Sides(rawValue: 2)
    public static let south = Sides(rawValue: 4)
    public static let west = Sides(rawValue: 8)
    public static let all: Sides = [.north, .east, .south, .west]

    /// Each side with the step toward its neighbour, clockwise from north.
    static let steps: [(side: Sides, dx: Int, dy: Int)] = [
        (.north, 0, -1), (.east, 1, 0), (.south, 0, 1), (.west, -1, 0),
    ]

    /// The side facing this one, for a single side.
    var opposite: Sides {
        switch self {
        case .north: .south
        case .east: .west
        case .south: .north
        default: .east
        }
    }

    /// Whether it runs across, east to west, and not down.
    var runsAcross: Bool { isDisjoint(with: [.north, .south]) }

    /// Whether it runs down, north to south, and not across.
    var runsDown: Bool { isDisjoint(with: [.east, .west]) }
}

/// How a zone's 3 by 3 tiles are numbered: parts 0 to 8, row by row, with the centre at 4.
public enum ZoneLayout {
    public static let size = 3
    public static let parts = size * size
    public static let centerPart = parts / 2
}

/// How a building's tiles are numbered, for any size: row by row from its top-left corner, with
/// its centre one tile in from that corner.
public enum Footprint {
    /// The widest building, the airport.
    public static let largest = 6

    public static func parts(_ size: Int) -> Int { size * size }

    public static func centerPart(_ size: Int) -> Int { size == 1 ? 0 : size + 1 }

    public static func part(column: Int, row: Int, size: Int) -> Int { row * size + column }

    public static func offsetFromCenter(part: Int, size: Int) -> (dx: Int, dy: Int) {
        size == 1 ? (0, 0) : (part % size - 1, part / size - 1)
    }
}

/// Everything a tile holds: its ground, what is built on it, and how far that has grown. Tiles
/// that look the same compare equal, so fields that don't apply to a use are kept at zero.
public struct TileState: Hashable, Sendable, CustomStringConvertible {
    /// The highest level a zone's building grows to.
    public static let maxDevelopment = 5
    /// The highest land-value class a zone's building is drawn in.
    public static let maxValueClass = 3
    /// How many house drawings a residential zone's outer tiles pick from.
    public static let houseDrawings = 12

    public let kind: TileKind
    public let use: TileUse
    /// A zone's building level, kept on its centre; 0 for no building.
    public let development: Int
    /// Which part of a zone or building this tile is.
    public let zonePart: Int
    public let valueClass: Int
    /// Which house stands on a residential zone's outer tile; 0 for none.
    public let house: Int
    /// Whether a zone or building has power, kept on its centre as in Micropolis.
    public let powered: Bool

    /// Makes a tile. Values out of range are a programmer error; anything read from a file is
    /// checked before it gets here.
    public init(
        _ kind: TileKind,
        _ use: TileUse = .none,
        development: Int = 0,
        zonePart: Int? = nil,
        valueClass: Int = 0,
        house: Int = 0,
        powered: Bool = false
    ) {
        let size = use.footprint
        let part = zonePart ?? Footprint.centerPart(size)
        precondition((0...Self.maxDevelopment).contains(development), "development \(development) is out of range")
        precondition(part >= 0 && part < Footprint.parts(max(size, ZoneLayout.size)), "zone part \(part) is out of range")
        precondition((0...Self.maxValueClass).contains(valueClass), "value class \(valueClass) is out of range")
        precondition((0...Self.houseDrawings).contains(house), "house \(house) is out of range")

        self.kind = kind
        self.use = use
        let zoned = use.isZone
        self.zonePart = size > 1 ? part : 0
        self.development = zoned ? development : 0
        self.valueClass = zoned ? valueClass : 0
        self.house = zoned && part != ZoneLayout.centerPart ? house : 0
        self.powered = powered && size > 1 && part == Footprint.centerPart(size)
    }

    /// Plain grass with nothing on it.
    public static let grass = TileState(.grass)

    public var isZoned: Bool { use.isZone }
    public var hasFootprint: Bool { use.footprint > 1 }
    public var isBuildingCenter: Bool { hasFootprint && zonePart == Footprint.centerPart(use.footprint) }
    public var isZoneCenter: Bool { isZoned && zonePart == ZoneLayout.centerPart }
    public var hasHouse: Bool { house > 0 }

    public var isRoad: Bool { use == .road || use == .roadAcrossPowerLine || use == .roadDownPowerLine }
    public var isBridge: Bool { use == .road && kind == .water }
    public var hasRoad: Bool { isRoad || use == .railAcrossRoad || use == .railDownRoad }

    public var isRail: Bool {
        switch use {
        case .rail, .railAcrossRoad, .railDownRoad, .railAcrossPowerLine, .railDownPowerLine: true
        default: false
        }
    }

    public var isTunnel: Bool { use == .rail && kind == .water }

    /// Roads and rail both carry trips.
    public var carriesTrips: Bool { isRoad || isRail }

    public var hasPowerLine: Bool {
        switch use {
        case .powerLine, .roadAcrossPowerLine, .roadDownPowerLine, .railAcrossPowerLine, .railDownPowerLine: true
        default: false
        }
    }

    /// Whether the bulldozer has anything to clear here, one tile at a time.
    public var hasSomethingToClear: Bool {
        carriesTrips || hasPowerLine || use == .park || (use == .none && kind.needsClearing)
    }

    public var description: String {
        let power = powered ? " powered" : ""
        if isZoned {
            return "\(kind) \(use) \(development) part \(zonePart) class \(valueClass) house \(house)\(power)"
        }
        return hasFootprint ? "\(kind) \(use) part \(zonePart)\(power)" : "\(kind) \(use)"
    }
}

/// A rectangle of tiles.
public struct TileRect: Hashable, Sendable {
    public var x: Int
    public var y: Int
    public var width: Int
    public var height: Int

    public init(x: Int, y: Int, width: Int, height: Int) {
        self.x = x
        self.y = y
        self.width = width
        self.height = height
    }

    /// The rectangle with two tiles at opposite corners.
    public static func spanning(_ x1: Int, _ y1: Int, _ x2: Int, _ y2: Int) -> TileRect {
        TileRect(x: min(x1, x2), y: min(y1, y2), width: abs(x2 - x1) + 1, height: abs(y2 - y1) + 1)
    }

    public func contains(_ px: Int, _ py: Int) -> Bool {
        px >= x && px < x + width && py >= y && py < y + height
    }

    public func isInside(_ map: TileMap) -> Bool {
        x >= 0 && y >= 0 && x + width <= map.width && y + height <= map.height
    }
}
