import CityLimitsCore
import Raylib

/// Which drawing of the tile sheet each tile is: every sheet index the game uses lives here, and
/// `Resources/Assets/README.md` lists them, so new art is a change to this table, not the renderer.
/// The sheet is Micropolis', 16 tiles across, each 16 pixels square.
enum TileAtlas {
    static let cellSize = 16
    static let columns = 16

    private static let land = 0
    private static let water = 2
    private static let woods = 37
    private static let shoreVariantOffset = 1
    private static let woodEdgeVariantOffset = 8
    private static let firstRubble = 44
    private static let rubbleTiles = 4
    private static let bridgeAcross = 64
    private static let bridgeDown = 65
    private static let roadAcrossPowerLine = 77
    private static let roadDownPowerLine = 78
    private static let tunnelAcross = 224
    private static let tunnelDown = 225
    private static let railAcrossPowerLine = 221
    private static let railDownPowerLine = 222
    private static let railAcrossRoad = 237
    private static let railDownRoad = 238
    private static let firstWoodedPark = 40
    private static let fountain = 840
    private static let underwaterLineDown = 208
    private static let underwaterLineAcross = 209
    private static let firstHouse = 249

    static let lightningBolt = 827
    static let radiation = 52
    static let fireFrames = 8
    static let explosionFrames = 8
    private static let fire = 56
    private static let floodTile = 48
    private static let floodDrawings = 3
    private static let explosion = 860

    // Each building's first tile; a tile's drawing is that plus its part number.
    private static let buildings: [TileUse: Int] = [
        .coalPowerPlant: 745, .nuclearPowerPlant: 811, .fireStation: 761, .policeStation: 770,
        .hospital: 405, .church: 414, .stadium: 779, .seaport: 693, .airport: 709,
    ]

    // A stadium with a game on, and the game's two eight-frame tiles, a tile right of the centre
    // and the tile below that.
    private static let fullStadium = 795
    private static let footballGame = 932
    private static let footballGameLower = 940
    static let footballGamePart = 6
    static let footballGameLowerPart = 10

    // A zone's drawings: its empty zone, and its buildings by land-value class and level.
    private struct ZoneDrawings {
        var emptyZone: Int
        var firstBuilding: Int
        var levels: Int
        var valueClasses: Int

        func firstTile(development: Int, valueClass: Int) -> Int {
            guard development > 0 else { return emptyZone }
            let level = min(development, levels) - 1
            let valueClassIndex = min(max(valueClass, 0), valueClasses - 1)
            return firstBuilding + ZoneLayout.parts * (valueClassIndex * levels + level)
        }
    }

    private static let zones: [TileUse: ZoneDrawings] = [
        .residential: ZoneDrawings(emptyZone: 240, firstBuilding: 261, levels: 4, valueClasses: 4),
        .commercial: ZoneDrawings(emptyZone: 423, firstBuilding: 432, levels: 5, valueClasses: 4),
        .industrial: ZoneDrawings(emptyZone: 612, firstBuilding: 621, levels: 4, valueClasses: 2),
    ]

    // Each of these is indexed by the sides a tile joins: north 1, east 2, south 4, west 8.
    private static let powerLines = [210, 211, 210, 212, 211, 211, 213, 217, 210, 215, 210, 216, 214, 219, 218, 220]
    private static let roads = [66, 67, 66, 68, 67, 67, 69, 73, 66, 71, 66, 72, 70, 75, 74, 76]
    private static let rails = [226, 227, 226, 228, 227, 227, 229, 233, 226, 231, 226, 232, 230, 235, 234, 236]
    // Indexed by the sides with water: the shore drawing for the land on the other sides.
    private static let shores = [water, water, water, 15, water, water, 19, 17, water, 11, water, 13, 7, 9, 5, water]
    // Indexed by the sides with trees.
    private static let woodEdges = [woods, woods, woods, 26, woods, woods, 28, 27, woods, 24, woods, 25, 22, 23, 21, woods]

    // Traffic: each frame holds a drawing for every road drawing from 64 on, in the same order,
    // played in this order as Micropolis plays them.
    private static let firstRoad = 64
    private static let lastRoad = roadDownPowerLine
    static let trafficFrames = 4
    private static let lightTraffic = [80, 128, 112, 96]
    private static let heavyTraffic = [144, 192, 176, 160]

    /// The drawing for a tile, from its state, what it joins, and its zone's centre.
    static func index(
        _ state: TileState, roads joinedRoads: Sides, lines: Sides, rails joinedRails: Sides, ground: Sides,
        center: TileState, x: Int, y: Int
    ) -> Int {
        // A bridge, a tunnel, or a line under water runs down only when it joins nothing across.
        func runsDown(_ sides: Sides) -> Bool { !sides.isDisjoint(with: [.north, .south]) && sides.isDisjoint(with: [.east, .west]) }
        switch state.use {
        case .railAcrossRoad: return railAcrossRoad
        case .railDownRoad: return railDownRoad
        case .railAcrossPowerLine: return railAcrossPowerLine
        case .railDownPowerLine: return railDownPowerLine
        case .rail: return state.kind == .water ? (runsDown(joinedRails) ? tunnelDown : tunnelAcross) : rails[Int(joinedRails.rawValue & 15)]
        case .roadAcrossPowerLine: return roadAcrossPowerLine
        case .roadDownPowerLine: return roadDownPowerLine
        case .powerLine:
            return state.kind == .water ? (runsDown(lines) ? underwaterLineDown : underwaterLineAcross) : powerLines[Int(lines.rawValue & 15)]
        case .park:
            let look = Parks.look(x, y)
            return look == Parks.fountainLook ? fountain : firstWoodedPark + look
        case .road:
            return state.kind == .water ? (runsDown(joinedRoads) ? bridgeDown : bridgeAcross) : roads[Int(joinedRoads.rawValue & 15)]
        default:
            break
        }
        if let first = buildings[state.use] { return first + state.zonePart }
        if let drawings = zones[state.use] {
            if center.development == 0 && state.hasHouse { return firstHouse + state.house - 1 }
            return drawings.firstTile(development: center.development, valueClass: center.valueClass) + state.zonePart
        }
        // The same tile always picks the same drawing, so the ground doesn't flicker as the view moves.
        let variant = x * 7 + y * 13
        switch state.kind {
        case .water: return varied(shores[Int(ground.rawValue & 15)], open: water, offset: shoreVariantOffset, variant)
        case .trees: return varied(woodEdges[Int(ground.rawValue & 15)], open: woods, offset: woodEdgeVariantOffset, variant)
        case .rubble: return firstRubble + variant % rubbleTiles
        case .grass: return land
        }
    }

    // An edge drawing, or its second drawing on every other tile; the open drawing has no second.
    private static func varied(_ tile: Int, open: Int, offset: Int, _ variant: Int) -> Int {
        tile == open ? tile : tile + offset * (variant % 2)
    }

    /// A road's drawing with its traffic, at an animation frame.
    static func traffic(road: Int, _ level: TrafficLevel, frame: Int) -> Int {
        guard (firstRoad...lastRoad).contains(road), level != .none else { return road }
        let frames = level == .heavy ? heavyTraffic : lightTraffic
        return frames[(frame % trafficFrames + trafficFrames) % trafficFrames] + road - firstRoad
    }

    static func fire(_ x: Int, _ y: Int, frame: Int) -> Int {
        fire + ((frame + x * 3 + y * 5) % fireFrames + fireFrames) % fireFrames
    }

    static func flood(_ x: Int, _ y: Int) -> Int {
        floodTile + Int((UInt32(truncatingIfNeeded: x * 7) &+ UInt32(truncatingIfNeeded: y * 13)) % UInt32(floodDrawings))
    }

    static func explosion(frame: Int) -> Int { explosion + min(max(frame, 0), explosionFrames - 1) }

    static func fullStadium(part: Int) -> Int { fullStadium + part }

    static func footballGame(lower: Bool, frame: Int) -> Int {
        (lower ? footballGameLower : footballGame) + (frame % 8 + 8) % 8
    }

    /// Where a drawing is on the sheet.
    static func source(_ index: Int) -> Rectangle {
        Rectangle(x: Float(index % columns * cellSize), y: Float(index / columns * cellSize), width: Float(cellSize), height: Float(cellSize))
    }
}
