/// Smooths a grid of 8 by 8 tile blocks, as Micropolis spreads a station's coverage: each pass, a
/// block becomes half of itself plus an eighth of its four neighbours.
func spreadBlocks(_ grid: inout [Int], scratch: inout [Int], width: Int, height: Int, passes: Int) {
    for _ in 0..<passes {
        for y in 0..<height {
            for x in 0..<width {
                var edge = 0
                if x > 0 { edge += grid[y * width + x - 1] }
                if x + 1 < width { edge += grid[y * width + x + 1] }
                if y > 0 { edge += grid[(y - 1) * width + x] }
                if y + 1 < height { edge += grid[(y + 1) * width + x] }
                scratch[y * width + x] = (edge / 4 + grid[y * width + x]) / 2
            }
        }
        swap(&grid, &scratch)
    }
}

/// How well fire and police stations cover the city, in 8 by 8 tile blocks, as Micropolis keeps it.
public final class ServiceCoverage {
    public static let blockSize = 8

    private let fireSpec: ServiceSpec
    private let policeSpec: ServiceSpec
    let width: Int
    let height: Int
    var fireRaw: [Int]
    var policeRaw: [Int]
    var fire: [Int]
    var police: [Int]
    private var scratch: [Int]

    public internal(set) var fireStations = 0
    public internal(set) var policeStations = 0

    public init(map: TileMap, definitions: CityDefinitions = .base) {
        fireSpec = definitions.service(.fireStation)
        policeSpec = definitions.service(.policeStation)
        width = (map.width + Self.blockSize - 1) / Self.blockSize
        height = (map.height + Self.blockSize - 1) / Self.blockSize
        let blocks = Array(repeating: 0, count: width * height)
        (fireRaw, policeRaw, fire, police, scratch) = (blocks, blocks, blocks, blocks, blocks)
    }

    public func fireCoverage(_ x: Int, _ y: Int) -> Int { at(fire, x, y) }
    public func policeCoverage(_ x: Int, _ y: Int) -> Int { at(police, x, y) }

    public func startCount() {
        for index in fireRaw.indices {
            fireRaw[index] = 0
            policeRaw[index] = 0
        }
        fireStations = 0
        policeStations = 0
    }

    /// Adds a station's cover to its block: its funding's effect, halved without power and again
    /// without a road beside it.
    public func addStation(_ x: Int, _ y: Int, use: TileUse, fundingEffect: Int, powered: Bool, served: Bool) {
        var strength = powered ? fundingEffect : fundingEffect / 2
        if !served { strength /= 2 }
        let block = (y / Self.blockSize) * width + x / Self.blockSize
        switch use {
        case .fireStation:
            fireStations += 1
            fireRaw[block] += Self.scaled(strength, fireSpec)
        case .policeStation:
            policeStations += 1
            policeRaw[block] += Self.scaled(strength, policeSpec)
        default:
            preconditionFailure("\(use) is not a station")
        }
    }

    public func spreadPolice() {
        police = policeRaw
        spreadBlocks(&police, scratch: &scratch, width: width, height: height, passes: policeSpec.reach)
    }

    public func spreadFire() {
        fire = fireRaw
        spreadBlocks(&fire, scratch: &scratch, width: width, height: height, passes: fireSpec.reach)
    }

    // Micropolis' station is 100; a spec's strength scales it.
    private static func scaled(_ strength: Int, _ spec: ServiceSpec) -> Int {
        spec.strength == ServiceSpec.micropolisStrength ? strength : strength * spec.strength / ServiceSpec.micropolisStrength
    }

    private func at(_ grid: [Int], _ x: Int, _ y: Int) -> Int {
        x >= 0 && y >= 0 && x / Self.blockSize < width && y / Self.blockSize < height
            ? grid[(y / Self.blockSize) * width + x / Self.blockSize] : 0
    }
}

/// What powered stadiums, seaports, and airports do to the land value, crime, and pollution around
/// them, spread over 8 by 8 tile blocks as a station's coverage is.
public final class FacilityInfluences {
    private let map: TileMap
    private let facilities: [FacilitySpec]
    private let width: Int
    private let height: Int
    // For each effect, what every facility together does, as of the last time it was worked out.
    private var totals: [InfluenceEffect: [Int]] = [:]
    // For each kind of facility, its blocks' strength before it spreads.
    private var raw: [[Int]]
    private var scratch: [Int]

    public init(map: TileMap, definitions: CityDefinitions = .base) {
        self.map = map
        facilities = definitions.facilities.values.sorted { $0.use < $1.use }
        width = (map.width + ServiceCoverage.blockSize - 1) / ServiceCoverage.blockSize
        height = (map.height + ServiceCoverage.blockSize - 1) / ServiceCoverage.blockSize
        let blocks = Array(repeating: 0, count: width * height)
        for effect in [InfluenceEffect.landValue, .crime, .pollution] { totals[effect] = blocks }
        raw = facilities.map { _ in blocks }
        scratch = blocks
    }

    public func at(_ effect: InfluenceEffect, _ x: Int, _ y: Int) -> Int {
        let size = ServiceCoverage.blockSize
        guard x >= 0, y >= 0, x / size < width, y / size < height, let total = totals[effect] else { return 0 }
        return total[(y / size) * width + x / size]
    }

    public func update(_ effect: InfluenceEffect) {
        var total = Array(repeating: 0, count: width * height)
        defer { totals[effect] = total }
        guard facilities.contains(where: { $0.influence(on: effect) != nil }) else { return }

        for kind in raw.indices {
            for block in raw[kind].indices { raw[kind][block] = 0 }
        }
        let size = ServiceCoverage.blockSize
        for y in 0..<map.height {
            for x in 0..<map.width {
                let state = map[x, y]
                guard state.isBuildingCenter, state.powered, state.use.isFacility,
                    let kind = facilities.firstIndex(where: { $0.use == state.use }),
                    let influence = facilities[kind].influence(on: effect)
                else { continue }
                raw[kind][(y / size) * width + x / size] += influence.strength
            }
        }

        // Each kind spreads as far as its own reach, then they add up.
        for kind in facilities.indices {
            guard let influence = facilities[kind].influence(on: effect) else { continue }
            spreadBlocks(&raw[kind], scratch: &scratch, width: width, height: height, passes: influence.reach)
            for block in total.indices { total[block] += raw[kind][block] }
        }
    }
}

/// How many stadiums, seaports, and airports the scan found this count, and which demands their
/// absence holds back, as in Micropolis.
public final class Facilities {
    public internal(set) var stadiums = 0
    public internal(set) var seaports = 0
    public internal(set) var airports = 0
    public internal(set) var caps: DemandCaps = []

    public init() {}

    func startCount() { (stadiums, seaports, airports) = (0, 0, 0) }

    func count(_ use: TileUse) {
        switch use {
        case .stadium: stadiums += 1
        case .seaport: seaports += 1
        case .airport: airports += 1
        default: break
        }
    }

    /// Whether the city has outgrown its want of a stadium, and holds residential demand if so.
    func checkStadium(_ census: CityCensus, cap: Int) -> Bool { hold(.residential, census.residential > cap && stadiums == 0) }
    func checkSeaport(_ census: CityCensus, cap: Int) -> Bool { hold(.industrial, census.industrial > cap && seaports == 0) }
    func checkAirport(_ census: CityCensus, cap: Int) -> Bool { hold(.commercial, census.commercial > cap && airports == 0) }

    private func hold(_ cap: DemandCaps, _ held: Bool) -> Bool {
        if held { caps.insert(cap) } else { caps.remove(cap) }
        return held
    }
}
