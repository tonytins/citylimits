/// One kind of hazard on the map: which tiles it is on. Fires, floods, and radiation each keep one,
/// beside the map, since a burning tile's state is rubble and a flooded one's grass.
public struct HazardLayer: Sendable {
    let width: Int
    private var marked: [Bool]
    public private(set) var count = 0

    init(width: Int, height: Int) {
        self.width = width
        marked = Array(repeating: false, count: width * height)
    }

    func contains(_ x: Int, _ y: Int) -> Bool {
        x >= 0 && y >= 0 && x < width && y * width + x < marked.count
    }

    public func isOn(_ x: Int, _ y: Int) -> Bool {
        contains(x, y) && marked[y * width + x]
    }

    /// Marks a tile, and says whether it wasn't marked before.
    @discardableResult
    mutating func mark(_ x: Int, _ y: Int) -> Bool {
        guard contains(x, y), !marked[y * width + x] else { return false }
        marked[y * width + x] = true
        count += 1
        return true
    }

    mutating func clear(_ x: Int, _ y: Int) {
        guard isOn(x, y) else { return }
        marked[y * width + x] = false
        count -= 1
    }

    /// Whether it's on any tile of the zone or building with this centre.
    func anyIn(_ map: TileMap, centerX: Int, centerY: Int) -> Bool {
        guard count > 0 else { return false }
        let size = map.state(atOrDefault: centerX, centerY).use.footprint
        for part in 0..<Footprint.parts(size) {
            let offset = Footprint.offsetFromCenter(part: part, size: size)
            if isOn(centerX + offset.dx, centerY + offset.dy) { return true }
        }
        return false
    }
}

/// The fires, floods, and radiation on the map, and the holes they leave in buildings.
public final class Hazards {
    /// How many cycles a flood lasts before it starts to drain.
    public static let floodDuration = 30
    /// The chance of a radioactive tile decaying in a cycle is one in this.
    public static let radiationDecayChance = 4096

    private let map: TileMap
    public private(set) var fires: HazardLayer
    public private(set) var floods: HazardLayer
    public private(set) var radiation: HazardLayer
    /// How many cycles the water keeps rising; 0 once it drains.
    public private(set) var floodCyclesLeft = 0

    public init(map: TileMap) {
        self.map = map
        fires = HazardLayer(width: map.width, height: map.height)
        floods = HazardLayer(width: map.width, height: map.height)
        radiation = HazardLayer(width: map.width, height: map.height)
    }

    public func isBurning(_ x: Int, _ y: Int) -> Bool { fires.isOn(x, y) }
    public func isFlooded(_ x: Int, _ y: Int) -> Bool { floods.isOn(x, y) }
    public func isRadioactive(_ x: Int, _ y: Int) -> Bool { radiation.isOn(x, y) }

    /// Whether a fire, a flood, or radiation is on a tile.
    public func isActive(_ x: Int, _ y: Int) -> Bool {
        isBurning(x, y) || isFlooded(x, y) || isRadioactive(x, y)
    }

    /// Whether a hazard is on any tile of the zone or building with this centre.
    public func anyActiveIn(centerX: Int, centerY: Int) -> Bool {
        fires.anyIn(map, centerX: centerX, centerY: centerY)
            || floods.anyIn(map, centerX: centerX, centerY: centerY)
            || radiation.anyIn(map, centerX: centerX, centerY: centerY)
    }

    /// Why a hazard on a tile refuses work there, or nil when there's none.
    public func refusal(_ x: Int, _ y: Int) -> BuildRefusal? {
        if isBurning(x, y) { return .onFire }
        if isFlooded(x, y) { return .flooded }
        if isRadioactive(x, y) { return .radioactive }
        return nil
    }

    // MARK: Fires

    /// Whether a fire can start on a tile: anything built, or trees, but never water.
    public func canBurn(_ x: Int, _ y: Int) -> Bool {
        guard map.contains(x, y), !isBurning(x, y) else { return false }
        let state = map[x, y]
        return state.kind != .water && (state.use != .none || state.kind == .trees)
    }

    /// Sets a tile alight. A building's centre takes the rest of it with it: the tiles with
    /// something on them burn, and the empty lots of a zone clear. Returns the building that
    /// burned, or nil for a tile on its own.
    @discardableResult
    public func ignite(_ x: Int, _ y: Int) -> TileState? {
        guard map.contains(x, y), !isBurning(x, y) else { return nil }
        let state = map[x, y]
        setBurning(x, y)
        guard state.isBuildingCenter else { return nil }

        let size = state.use.footprint
        for part in 0..<Footprint.parts(size) {
            let offset = Footprint.offsetFromCenter(part: part, size: size)
            let (px, py) = (x + offset.dx, y + offset.dy)
            let piece = map.state(atOrDefault: px, py)
            if (offset.dx == 0 && offset.dy == 0) || piece.use != state.use || piece.zonePart != part || isBurning(px, py) {
                continue
            }
            if Self.hasSomethingOn(center: state, piece: piece) {
                setBurning(px, py)
            } else {
                map.setState(px, py, .grass)
            }
        }
        return state
    }

    public func putOut(_ x: Int, _ y: Int) { fires.clear(x, y) }

    func restoreBurning(_ x: Int, _ y: Int) { fires.mark(x, y) }

    // A house, a zone's building, or any tile of a plant, station, hospital, or church; not the
    // empty lot of a zone with no building.
    private static func hasSomethingOn(center: TileState, piece: TileState) -> Bool {
        switch center.use {
        case .residential: center.development > 0 || piece.hasHouse
        case .commercial, .industrial: center.development > 0
        default: true
        }
    }

    private func setBurning(_ x: Int, _ y: Int) {
        map.setState(x, y, TileState(.rubble))
        fires.mark(x, y)
    }

    // MARK: Floods

    /// Whether water can spread onto a tile: dry land that isn't burning or radioactive.
    public func canFlood(_ x: Int, _ y: Int) -> Bool {
        map.contains(x, y) && !isFlooded(x, y) && !isBurning(x, y) && !isRadioactive(x, y) && map[x, y].kind != .water
    }

    /// Starts a flood's clock.
    public func riseFlood() { floodCyclesLeft = Self.floodDuration }

    public func recedeFlood() { floodCyclesLeft = max(floodCyclesLeft - 1, 0) }

    /// Floods a tile, washing what was on it away. A building's centre floods the rest of it.
    public func flood(_ x: Int, _ y: Int) {
        guard canFlood(x, y) else { return }
        let state = map[x, y]
        setFlooded(x, y)
        guard state.isBuildingCenter else { return }

        let size = state.use.footprint
        for part in 0..<Footprint.parts(size) {
            let offset = Footprint.offsetFromCenter(part: part, size: size)
            let (px, py) = (x + offset.dx, y + offset.dy)
            let piece = map.state(atOrDefault: px, py)
            if !(offset.dx == 0 && offset.dy == 0) && piece.use == state.use && piece.zonePart == part && canFlood(px, py) {
                setFlooded(px, py)
            }
        }
    }

    public func drain(_ x: Int, _ y: Int) { floods.clear(x, y) }

    func restoreFlooded(_ x: Int, _ y: Int) { floods.mark(x, y) }

    func restoreFloodCyclesLeft(_ cycles: Int) { floodCyclesLeft = min(max(cycles, 0), Self.floodDuration) }

    private func setFlooded(_ x: Int, _ y: Int) {
        map.setState(x, y, .grass)
        floods.mark(x, y)
    }

    // MARK: Radiation

    /// Makes a tile radioactive, clearing it to bare ground.
    public func contaminate(_ x: Int, _ y: Int) {
        guard map.contains(x, y), !isRadioactive(x, y) else { return }
        map.setState(x, y, .grass)
        radiation.mark(x, y)
    }

    public func decay(_ x: Int, _ y: Int) { radiation.clear(x, y) }

    func restoreRadioactive(_ x: Int, _ y: Int) { radiation.mark(x, y) }

    // MARK: Holes

    /// Whether a tile is a hole a hazard left in a building, cleared ground inside its footprint.
    public func isHole(_ x: Int, _ y: Int) -> Bool { holeOwner(x, y) != nil }

    /// The centre of the building a hole belongs to, or nil when the tile isn't one.
    public func holeOwner(_ x: Int, _ y: Int) -> (x: Int, y: Int)? {
        guard map.contains(x, y), !map[x, y].hasFootprint else { return nil }

        // A building's centre is one tile in from its top-left corner, so it is at most one tile
        // up or left of any of its tiles, and as far down or right as the largest is wide less
        // two; buildings never overlap.
        for cy in (y - (Footprint.largest - 2))...(y + 1) {
            for cx in (x - (Footprint.largest - 2))...(x + 1) {
                let center = map.state(atOrDefault: cx, cy)
                guard center.isBuildingCenter else { continue }
                let size = center.use.footprint
                if x >= cx - 1 && x < cx - 1 + size && y >= cy - 1 && y < cy - 1 + size {
                    return (cx, cy)
                }
            }
        }
        return nil
    }

    /// Puts back the holes in a building once nothing is burning, flooded, or radioactive there.
    public func repairHoles(centerX: Int, centerY: Int) { redraw(centerX, centerY, onlyBareGround: false) }

    /// Puts back only the holes that have gone back to bare ground.
    public func repairBareHoles(centerX: Int, centerY: Int) { redraw(centerX, centerY, onlyBareGround: true) }

    /// A tile as it would be with its building whole, for drawing a hole's outline.
    public func wholeState(_ x: Int, _ y: Int) -> TileState {
        guard let owner = holeOwner(x, y) else { return map[x, y] }
        let center = map[owner.x, owner.y]
        let size = center.use.footprint
        return TileState(.grass, center.use, zonePart: Footprint.part(column: x - owner.x + 1, row: y - owner.y + 1, size: size))
    }

    private func redraw(_ centerX: Int, _ centerY: Int, onlyBareGround: Bool) {
        let center = map.state(atOrDefault: centerX, centerY)
        guard center.isBuildingCenter else { return }
        let size = center.use.footprint
        for part in 0..<Footprint.parts(size) {
            let offset = Footprint.offsetFromCenter(part: part, size: size)
            let (px, py) = (centerX + offset.dx, centerY + offset.dy)
            guard map.contains(px, py), !isActive(px, py),
                let owner = holeOwner(px, py), owner.x == centerX, owner.y == centerY
            else { continue }
            if onlyBareGround && map[px, py].kind != .grass { continue }
            map.setState(px, py, TileState(.grass, center.use, zonePart: part))
        }
    }
}
