/// What a tool does to the map.
public enum BuildAction: Int, CaseIterable, Sendable {
    case road
    case zoneResidential
    case zoneCommercial
    case zoneIndustrial
    case bulldoze
    case coalPowerPlant
    case nuclearPowerPlant
    case powerLine
    case fireStation
    case policeStation
    case park
    case rail
    case stadium
    case seaport
    case airport

    /// What the tool puts down, for one that lays zones or places a building.
    public var use: TileUse {
        switch self {
        case .zoneResidential: .residential
        case .zoneCommercial: .commercial
        case .zoneIndustrial: .industrial
        case .coalPowerPlant: .coalPowerPlant
        case .nuclearPowerPlant: .nuclearPowerPlant
        case .fireStation: .fireStation
        case .policeStation: .policeStation
        case .stadium: .stadium
        case .seaport: .seaport
        case .airport: .airport
        default: .none
        }
    }

    public var laysZones: Bool { use.isZone }

    public var placesBuilding: Bool { use != .none && !use.isZone }

    /// Whether a drag lays one straight line, as roads, rail, and power lines do.
    public var laysLine: Bool { self == .road || self == .powerLine || self == .rail }
}

/// Why a tool couldn't do some or all of its work.
public enum BuildRefusal: Int, CaseIterable, Sendable {
    case none
    case offTheMap
    case cannotBuildHere
    case alreadyDone
    case notEnoughFunds
    case nothingToClear
    case partOfAZone
    case onFire
    case flooded
    case radioactive
}

/// What happened when a tool was used.
public struct BuildResult: Hashable, Sendable {
    public enum Status: Sendable { case built, unchanged, refused }

    public var status: Status
    public var reason: BuildRefusal
    public var cost = 0
    public var tilesChanged = 0

    public var changed: Bool { status == .built }
}

/// One use of a tool, from where the drag started to where it is now.
public struct BuildStroke: Hashable, Sendable {
    public let action: BuildAction
    public let startX: Int
    public let startY: Int
    public var endX: Int
    public var endY: Int

    public init(_ action: BuildAction, _ x: Int, _ y: Int) {
        self.action = action
        startX = x
        startY = y
        endX = x
        endY = y
    }

    public mutating func drag(to x: Int, _ y: Int) {
        endX = x
        endY = y
    }

    /// The tiles the stroke covers. A building sits with its centre under the pointer; zones fill
    /// whole 3 by 3 squares, the first centred on the start; lines run straight along the longer
    /// way of the drag; the bulldozer clears the whole rectangle.
    public var bounds: TileRect {
        if action.placesBuilding {
            let size = action.use.footprint
            return TileRect(x: endX - 1, y: endY - 1, width: size, height: size)
        }
        if action.laysZones {
            let across = Self.zoneSpan(startX, endX)
            let down = Self.zoneSpan(startY, endY)
            return TileRect(x: across.first, y: down.first, width: across.length, height: down.length)
        }
        guard action.laysLine else { return .spanning(startX, startY, endX, endY) }
        return abs(endX - startX) >= abs(endY - startY)
            ? .spanning(startX, startY, endX, startY)
            : .spanning(startX, startY, startX, endY)
    }

    // Along one axis: the first zone's centre is at the start, and each further whole zone's
    // worth of drag adds a zone on that side.
    private static func zoneSpan(_ start: Int, _ end: Int) -> (first: Int, length: Int) {
        let length = (abs(end - start) / ZoneLayout.size + 1) * ZoneLayout.size
        return (end >= start ? start - 1 : start + 2 - length, length)
    }
}

/// What a stroke would do, tile by tile, worked out before it is applied so the player can see
/// it. It's reused from frame to frame, so planning doesn't allocate once it has grown.
public final class BuildPlan {
    public struct Tile: Hashable, Sendable {
        public var x: Int
        public var y: Int
        public var state: TileState
    }

    public private(set) var tiles: [Tile] = []
    public private(set) var bounds = TileRect(x: 0, y: 0, width: 0, height: 0)
    public private(set) var cost = 0
    /// Whether the cost was waived, as the scenario editor waives it.
    public private(set) var isFree = false
    /// Why the whole stroke is refused, or `.none`.
    public private(set) var refusal = BuildRefusal.none
    private var indexByTile: [Int: Int] = [:]
    private var skipped = Array(repeating: 0, count: BuildRefusal.allCases.count)
    private var map: TileMap?

    public init() {}

    public var willChange: Bool { refusal == .none && !tiles.isEmpty }

    /// How many tiles were left alone for a reason.
    public func skipped(because reason: BuildRefusal) -> Int { skipped[reason.rawValue] }

    /// What a tile would be after the stroke.
    public func state(_ x: Int, _ y: Int) -> TileState {
        guard let map else { return .grass }
        return planned(x, y) ?? map.state(atOrDefault: x, y)
    }

    /// What the stroke puts on a tile, or nil when it leaves the tile alone.
    public func planned(_ x: Int, _ y: Int) -> TileState? {
        guard let map, map.contains(x, y), let index = indexByTile[y * map.width + x] else { return nil }
        return tiles[index].state
    }

    /// Which neighbours a network would join a tile to after the stroke.
    public func connections(_ network: Network, _ x: Int, _ y: Int) -> Sides {
        network.connections(x, y) { self.state($0, $1) }
    }

    func start(_ map: TileMap, _ bounds: TileRect) {
        self.map = map
        tiles.removeAll(keepingCapacity: true)
        indexByTile.removeAll(keepingCapacity: true)
        for index in skipped.indices { skipped[index] = 0 }
        self.bounds = bounds
        cost = 0
        isFree = false
        refusal = .none
    }

    func change(_ x: Int, _ y: Int, _ state: TileState, cost: Int) {
        guard let map else { return }
        indexByTile[y * map.width + x] = tiles.count
        tiles.append(Tile(x: x, y: y, state: state))
        self.cost += cost
    }

    func addCost(_ cost: Int) { self.cost += cost }

    func waive() {
        cost = 0
        isFree = true
    }

    func skip(_ reason: BuildRefusal) { skipped[reason.rawValue] += 1 }

    func refuse(_ reason: BuildRefusal) { refusal = reason }
}

/// Lays roads, rail, power lines, parks, zones, and buildings, and bulldozes, at Micropolis'
/// prices, paying from the treasury.
public final class CityBuilder {
    private let map: TileMap
    private let treasury: Treasury
    private let roads: RoadNetwork
    private let definitions: CityDefinitions
    private let hazards: Hazards?
    // Apply works the stroke out again here, so a preview the player saw earlier can't go stale.
    private let applying = BuildPlan()
    // The water a stroke has still to cross; kept so planning every frame doesn't allocate.
    private var water: [(x: Int, y: Int)] = []

    /// Whether building costs nothing, as in the scenario editor.
    public var buildsForFree = false

    public init(map: TileMap, treasury: Treasury, roads: RoadNetwork, definitions: CityDefinitions = .base, hazards: Hazards? = nil) {
        self.map = map
        self.treasury = treasury
        self.roads = roads
        self.definitions = definitions
        self.hazards = hazards
    }

    /// Works out what a stroke would do, without doing it.
    public func plan(_ stroke: BuildStroke, into plan: BuildPlan) {
        let bounds = stroke.bounds
        plan.start(map, bounds)
        guard bounds.isInside(map) else {
            plan.refuse(.offTheMap)
            return
        }

        let infrastructure = definitions.infrastructure
        switch stroke.action {
        case .road:
            planLine(bounds, plan, network: .road, laid: TileState(.grass, .road), price: infrastructure.road,
                     crossing: TileState(.water, .road), crossingPrice: infrastructure.bridge)
        case .powerLine:
            planLine(bounds, plan, network: .power, laid: TileState(.grass, .powerLine), price: infrastructure.powerLine,
                     crossing: TileState(.water, .powerLine), crossingPrice: infrastructure.underwaterPowerLine)
        case .rail:
            planLine(bounds, plan, network: .rail, laid: TileState(.grass, .rail), price: infrastructure.rail,
                     crossing: TileState(.water, .rail), crossingPrice: infrastructure.tunnel)
        case .bulldoze:
            planBulldozer(bounds, plan)
        case .park:
            planParks(bounds, plan)
        case .zoneResidential, .zoneCommercial, .zoneIndustrial:
            planZones(bounds, stroke.action.use, plan)
        case .coalPowerPlant, .nuclearPowerPlant, .fireStation, .policeStation, .stadium, .seaport, .airport:
            planFootprint(bounds.x, bounds.y, stroke.action.use, price: definitions.cost(of: stroke.action), plan)
        }

        if buildsForFree { plan.waive() }
        if plan.cost > treasury.funds { plan.refuse(.notEnoughFunds) }
    }

    /// Does a stroke's work and pays for it, or says why not.
    @discardableResult
    public func apply(_ stroke: BuildStroke) -> BuildResult {
        plan(stroke, into: applying)
        let plan = applying
        if plan.refusal != .none {
            return BuildResult(status: .refused, reason: plan.refusal, cost: plan.cost)
        }
        if plan.tiles.isEmpty {
            return Self.nothingChanged(plan)
        }
        guard treasury.spend(plan.cost) else {
            return BuildResult(status: .refused, reason: .notEnoughFunds, cost: plan.cost)
        }

        var roadsChanged = false
        for tile in plan.tiles {
            roadsChanged = roadsChanged || tile.state.carriesTrips || map[tile.x, tile.y].carriesTrips
            map.setState(tile.x, tile.y, tile.state)
        }
        if roadsChanged { roads.markDirty() }
        return BuildResult(status: .built, reason: .none, cost: plan.cost, tilesChanged: plan.tiles.count)
    }

    @discardableResult
    public func apply(_ action: BuildAction, _ x: Int, _ y: Int) -> BuildResult {
        apply(BuildStroke(action, x, y))
    }

    // When nothing would change, the result names the reason that matters most to the player.
    private static func nothingChanged(_ plan: BuildPlan) -> BuildResult {
        for reason in [BuildRefusal.cannotBuildHere, .onFire, .flooded, .radioactive, .partOfAZone]
        where plan.skipped(because: reason) > 0 {
            return BuildResult(status: .refused, reason: reason)
        }
        let reason: BuildRefusal = plan.skipped(because: .alreadyDone) > 0 ? .alreadyDone : .nothingToClear
        return BuildResult(status: .unchanged, reason: reason)
    }

    // Roads, rail, and power lines are laid alike, as in Micropolis: in one straight line,
    // clearing trees and rubble. Over another line that runs straight, they make a crossing that
    // carries both; a corner or junction can't take one. On water they make a bridge, tunnel, or
    // underwater line, which needs its own kind beside it to start from, so a stroke can start on
    // either bank.
    private func planLine(
        _ bounds: TileRect, _ plan: BuildPlan, network: Network, laid: TileState, price: Int,
        crossing: TileState, crossingPrice: Int
    ) {
        water.removeAll(keepingCapacity: true)
        for y in bounds.y..<(bounds.y + bounds.height) {
            for x in bounds.x..<(bounds.x + bounds.width) {
                let state = map[x, y]
                if blocked(x, y, plan) { continue }

                if network.isOn(state) {
                    plan.skip(.alreadyDone)
                } else if state.kind == .water && state.use == .none {
                    water.append((x, y))
                } else if state.kind == .grass, let over = Self.crossing(laying: network, over: state.use) {
                    let under = over.network.connections(x, y, on: map)
                    if let use = under.runsAcross ? over.whenAcross : under.runsDown ? over.whenDown : nil {
                        plan.change(x, y, TileState(.grass, use), cost: price)
                    } else {
                        plan.skip(.cannotBuildHere)
                    }
                } else if state.use != .none || !state.kind.canBuildOn {
                    plan.skip(.cannotBuildHere)
                } else {
                    plan.change(x, y, laid, cost: price + clearingCost(state))
                }
            }
        }
        crossWater(crossing, cost: crossingPrice, network: network, plan)
    }

    // What laying one network over a straight tile of another makes, by which way the one under
    // runs. Something joined to nothing is drawn across, as in Micropolis.
    private static func crossing(laying network: Network, over use: TileUse)
        -> (network: Network, whenAcross: TileUse, whenDown: TileUse)?
    {
        switch (network, use) {
        case (.road, .powerLine): (.power, .roadDownPowerLine, .roadAcrossPowerLine)
        case (.road, .rail): (.rail, .railAcrossRoad, .railDownRoad)
        case (.power, .road): (.road, .roadAcrossPowerLine, .roadDownPowerLine)
        case (.power, .rail): (.rail, .railAcrossPowerLine, .railDownPowerLine)
        case (.rail, .road): (.road, .railDownRoad, .railAcrossRoad)
        case (.rail, .powerLine): (.power, .railDownPowerLine, .railAcrossPowerLine)
        default: nil
        }
    }

    // Water is crossed in passes until no more of it can be: a tile is crossed once something
    // beside it, planned or already there, runs toward it.
    private func crossWater(_ crossing: TileState, cost: Int, network: Network, _ plan: BuildPlan) {
        var crossed = true
        while crossed {
            crossed = false
            for index in water.indices.reversed() {
                let (x, y) = water[index]
                let reached = Sides.steps.contains { step in
                    network.runs(plan.state(x + step.dx, y + step.dy), toward: step.side.opposite)
                }
                guard reached else { continue }
                plan.change(x, y, crossing, cost: cost)
                water.remove(at: index)
                crossed = true
            }
        }
        for _ in water { plan.skip(.cannotBuildHere) }
    }

    // As in Micropolis, a park goes only on clear ground or rubble, which it clears for $1 more.
    private func planParks(_ bounds: TileRect, _ plan: BuildPlan) {
        for y in bounds.y..<(bounds.y + bounds.height) {
            for x in bounds.x..<(bounds.x + bounds.width) {
                let state = map[x, y]
                if blocked(x, y, plan) { continue }
                if state.use == .park {
                    plan.skip(.alreadyDone)
                } else if state.use != .none || !(state.kind == .grass || state.kind == .rubble) {
                    plan.skip(.cannotBuildHere)
                } else {
                    plan.change(x, y, TileState(.grass, .park), cost: definitions.infrastructure.park + clearingCost(state))
                }
            }
        }
    }

    // Zones are laid as a grid, each one going down whole or not at all.
    private func planZones(_ bounds: TileRect, _ use: TileUse, _ plan: BuildPlan) {
        for top in stride(from: bounds.y, to: bounds.y + bounds.height, by: ZoneLayout.size) {
            for left in stride(from: bounds.x, to: bounds.x + bounds.width, by: ZoneLayout.size) {
                planFootprint(left, top, use, price: definitions.zone(use).price, plan)
            }
        }
    }

    // A zone or building goes down whole or not at all, on ground with nothing on it; trees and
    // rubble are cleared first, at a price. Its centre is one tile in from its top-left corner.
    private func planFootprint(_ left: Int, _ top: Int, _ use: TileUse, price: Int, _ plan: BuildPlan) {
        let size = use.footprint
        let center = map[left + 1, top + 1]
        let centerOf = map.centerOf(left, top)
        if center.isBuildingCenter && center.use == use && centerOf.x == left + 1 && centerOf.y == top + 1 {
            plan.skip(.alreadyDone)
            return
        }

        var cost = price
        for row in 0..<size {
            for column in 0..<size {
                let (x, y) = (left + column, top + row)
                if let hazard = hazards?.refusal(x, y) {
                    plan.skip(hazard)
                    return
                }
                let state = map[x, y]
                if state.use != .none || !state.kind.canBuildOn || hazards?.isHole(x, y) == true {
                    plan.skip(.cannotBuildHere)
                    return
                }
                cost += clearingCost(state)
            }
        }

        for row in 0..<size {
            for column in 0..<size {
                // A plant has power from the moment it is placed, as in Micropolis; the rest wait
                // for the scan.
                let part = Footprint.part(column: column, row: row, size: size)
                plan.change(left + column, top + row, TileState(.grass, use, zonePart: part, powered: use.isPowerPlant), cost: 0)
            }
        }
        plan.addCost(cost)
    }

    // The bulldozer clears lines, trees, and rubble tile by tile, for $1 each. A zone or building
    // comes down only when its centre is in the stroke: all its tiles turn to rubble, for $1.
    private func planBulldozer(_ bounds: TileRect, _ plan: BuildPlan) {
        for y in bounds.y..<(bounds.y + bounds.height) {
            for x in bounds.x..<(bounds.x + bounds.width) {
                let state = map[x, y]
                if let hazard = hazards?.refusal(x, y) {
                    plan.skip(hazard)
                    continue
                }
                if state.hasFootprint {
                    let center = map.centerOf(x, y)
                    if !bounds.contains(center.x, center.y) { plan.skip(.partOfAZone) }
                    continue
                }
                guard state.hasSomethingToClear else {
                    plan.skip(.nothingToClear)
                    continue
                }
                plan.change(x, y, TileState(state.kind == .water ? .water : .grass), cost: definitions.infrastructure.bulldozer)
            }
        }

        for y in bounds.y..<(bounds.y + bounds.height) {
            for x in bounds.x..<(bounds.x + bounds.width) where map[x, y].isBuildingCenter {
                // A tile burning, flooded, or radioactive stays so: the bulldozer can't clear
                // fire, water, or radiation, as in Micropolis.
                let size = map[x, y].use.footprint
                for dy in -1..<(size - 1) {
                    for dx in -1..<(size - 1) where hazards?.isActive(x + dx, y + dy) != true {
                        plan.change(x + dx, y + dy, TileState(.rubble), cost: 0)
                    }
                }
                plan.addCost(definitions.infrastructure.bulldozer)
            }
        }
    }

    // Nothing is built on a burning, flooded, or radioactive tile, or in the hole a hazard left
    // in a building.
    private func blocked(_ x: Int, _ y: Int, _ plan: BuildPlan) -> Bool {
        if let hazard = hazards?.refusal(x, y) {
            plan.skip(hazard)
            return true
        }
        if hazards?.isHole(x, y) == true {
            plan.skip(.cannotBuildHere)
            return true
        }
        return false
    }

    private func clearingCost(_ state: TileState) -> Int {
        state.kind.needsClearing ? definitions.infrastructure.clearing : 0
    }
}
