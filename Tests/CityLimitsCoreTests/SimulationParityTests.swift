import Testing

@testable import CityLimitsCore

/// The same scripted city, run in the C# build (legacy/csharp) and here, year by year. It has no
/// airport or seaport; `scriptedPort` adds both, so aircraft and ships fly and sail too.
struct SimulationParityTests {
    /// A 64 by 64 town: a coal plant on a road, two more roads, rail, zones of each kind, two
    /// stations, a park, and a line, on grass with a strip of water and a wood.
    static func scriptedTown(nuclear: Bool = false) -> CitySimulation {
        var terrain = Array(repeating: TileKind.grass, count: 64 * 64)
        for y in 40..<44 { for x in 0..<64 { terrain[y * 64 + x] = .water } }
        for y in 50..<60 { for x in 40..<60 { terrain[y * 64 + x] = .trees } }
        let city = CitySimulation(map: TileMap(width: 64, height: 64, terrain: terrain), seed: 2000, funds: 100_000)
        func drag(_ action: BuildAction, _ x1: Int, _ y1: Int, _ x2: Int, _ y2: Int) {
            var stroke = BuildStroke(action, x1, y1)
            stroke.drag(to: x2, y2)
            city.builder.apply(stroke)
        }
        city.builder.apply(nuclear ? .nuclearPowerPlant : .coalPowerPlant, 3, 7)
        drag(.road, 2, 10, 61, 10)
        drag(.road, 2, 20, 61, 20)
        drag(.road, 30, 2, 30, 62)
        drag(.rail, 2, 30, 61, 30)
        for x in stride(from: 8, through: 56, by: 3) where ![29, 30, 31].contains(x) {
            city.builder.apply(.zoneResidential, x, 8)
            city.builder.apply(.zoneResidential, x, 22)
        }
        for x in stride(from: 8, through: 56, by: 6) where ![29, 30, 31, 32].contains(x) {
            city.builder.apply(.zoneCommercial, x, 12)
            city.builder.apply(.zoneIndustrial, x, 18)
            city.builder.apply(.zoneIndustrial, x, 32)
        }
        city.builder.apply(.fireStation, 33, 12)
        city.builder.apply(.policeStation, 27, 12)
        drag(.park, 33, 25, 40, 26)
        drag(.powerLine, 7, 3, 12, 3)
        return city
    }

    static func hash(_ map: TileMap) -> UInt64 {
        var hash: UInt64 = 1_469_598_103_934_665_603
        for y in 0..<map.height {
            for x in 0..<map.width {
                let s = map[x, y]
                for value in [s.kind.rawValue, s.use.rawValue, s.development, s.zonePart, s.valueClass, s.house, s.powered ? 1 : 0] {
                    hash = (hash ^ UInt64(UInt32(value))) &* 1_099_511_628_211
                }
            }
        }
        return hash
    }

    // Year, funds, population, residential, commercial, and industrial demand, and the map's hash.
    static let expected: [(Int, Int, Int, Int, Int, Int, UInt64)] = [
        (0, 87560, 0, 0, 0, 0, 0xE3A9_C374_0179_FE1D),
        (1, 87554, 1020, 511, -1255, 1500, 0x1BA9_6A6E_CA9B_BB06),
        (2, 87244, 3380, 891, 425, 958, 0xEE6A_0C8B_59A2_EC7C),
        (3, 87025, 7320, 753, 1373, 1500, 0x83D9_F83C_E7AA_47E2),
        (4, 86926, 10000, 387, 1273, 1454, 0xDA44_AB69_0C1D_54A0),
        (5, 86958, 13320, 1919, 685, 1500, 0x8144_B089_750A_8FF8),
        (6, 87058, 16800, 694, 1500, 1500, 0x97D0_0A0A_7F26_E812),
    ]

    @Test func aTownGrowsAsItDoesInTheCSharpBuild() {
        let city = Self.scriptedTown()
        for (year, funds, population, residential, commercial, industrial, hash) in Self.expected {
            if year > 0 { city.run(steps: CityCalendar.stepsPerYear) }
            #expect(city.treasury.funds == funds, "funds in year \(year)")
            #expect(city.census.cityPopulation == population, "population in year \(year)")
            #expect(city.demand.residential == residential, "residential demand in year \(year)")
            #expect(city.demand.commercial == commercial, "commercial demand in year \(year)")
            #expect(city.demand.industrial == industrial, "industrial demand in year \(year)")
            #expect(Self.hash(city.map) == hash, "the map in year \(year)")
        }
    }

    // After three years: each disaster struck in turn (whether it struck, the map, and the fires,
    // floods, and radiation), then a year on.
    @Test(arguments: [false, true])
    func disastersStrikeAsTheyDoInTheCSharpBuild(nuclear: Bool) {
        let city = Self.scriptedTown(nuclear: nuclear)
        city.run(steps: 3 * CityCalendar.stepsPerYear)
        #expect(city.census.cityPopulation == (nuclear ? 6220 : 7320))

        let struck: [(Disaster, Bool, UInt64, Int, Int, Int)] = nuclear
            ? [(.earthquake, true, 0x08DF_CE10_2E5C_9E47, 15, 0, 0), (.fire, true, 0xA88C_0478_5B5F_D0C8, 16, 0, 0),
               (.flood, true, 0xA88C_0478_5B5F_D0C8, 16, 1, 0), (.meltdown, true, 0x2113_650C_970D_31D0, 43, 1, 82)]
            : [(.earthquake, true, 0x0C86_0EFA_8D4E_5194, 22, 0, 0), (.fire, true, 0x3B67_846F_B4D4_0093, 23, 0, 0),
               (.flood, true, 0x3B67_846F_B4D4_0093, 23, 1, 0), (.meltdown, false, 0x3B67_846F_B4D4_0093, 23, 1, 0)]
        for (disaster, strikes, hash, fires, floods, radiation) in struck {
            #expect(city.strike(disaster) == strikes, "\(disaster)")
            #expect(Self.hash(city.map) == hash, "the map after \(disaster)")
            #expect(city.hazards.fires.count == fires, "fires after \(disaster)")
            #expect(city.hazards.floods.count == floods, "floods after \(disaster)")
            #expect(city.hazards.radiation.count == radiation, "radiation after \(disaster)")
        }

        city.run(steps: CityCalendar.stepsPerYear)
        // The C# build's nuclear town has a tornado at step 2751.
        #expect(city.treasury.funds == (nuclear ? 84928 : 86926))
        #expect(city.census.cityPopulation == (nuclear ? 3220 : 6880))
        #expect(Self.hash(city.map) == (nuclear ? 0x6929_F387_C18A_EABA : 0xA0F9_B271_7BA1_BCD9))
        #expect(city.hazards.fires.count == (nuclear ? 13 : 40))
        #expect(city.hazards.floods.count == (nuclear ? 21 : 14))
        #expect(city.hazards.radiation.count == (nuclear ? 80 : 0))
    }

    /// The town with a road along the water, a seaport and an airport on it, a power line to the
    /// east edge, and a road off it, so aircraft, ships, and neighbours all take part.
    static func scriptedPort() -> CitySimulation {
        let city = scriptedTown()
        func drag(_ action: BuildAction, _ x1: Int, _ y1: Int, _ x2: Int, _ y2: Int) {
            var stroke = BuildStroke(action, x1, y1)
            stroke.drag(to: x2, y2)
            city.builder.apply(stroke)
        }
        drag(.road, 2, 35, 61, 35)
        city.builder.apply(.seaport, 10, 37)
        city.builder.apply(.airport, 12, 47)
        drag(.road, 17, 48, 29, 48)
        drag(.powerLine, 58, 12, 63, 12)
        drag(.road, 61, 20, 63, 20)
        return city
    }

    @Test func aPortTownWithAircraftAndShipsRunsAsItDoesInTheCSharpBuild() {
        let city = Self.scriptedPort()
        var crashes = 0
        city.onEvent = { if case .message(.planeCrashReported) = $0 { crashes += 1 } }
        // Year, funds, population, the map's hash, the fires, and whether the plane, the
        // helicopter, and the ship are about.
        let expected: [(Int, Int, Int, UInt64, Int, Bool, Bool, Bool)] = [
            (0, 73790, 0, 0xECF1_E0A4_7A85_0E47, 0, false, false, false),
            (1, 73783, 1940, 0x87A6_2FC9_550F_603B, 10, true, false, true),
            (2, 73444, 4940, 0x2695_CDE9_6B0A_9F03, 13, false, false, true),
            (3, 73212, 5260, 0xADB1_3A3D_B5B7_D03B, 50, false, false, true),
            (4, 73034, 8300, 0x8028_F5A4_C683_CBD3, 23, false, false, true),
            (5, 72926, 7420, 0x7771_0B0A_9BAD_6EC4, 16, false, false, true),
            (6, 72867, 11020, 0x233A_54C9_64E8_00FA, 6, false, false, true),
            (7, 72836, 10560, 0xBA34_D0DB_AE47_0C3B, 2, false, false, true),
            (8, 72864, 11560, 0x3E0C_331C_37F6_B16D, 0, false, false, true),
        ]
        for (year, funds, population, hash, fires, plane, helicopter, ship) in expected {
            if year > 0 { city.run(steps: CityCalendar.stepsPerYear) }
            #expect(city.treasury.funds == funds, "funds in year \(year)")
            #expect(city.census.cityPopulation == population, "population in year \(year)")
            #expect(Self.hash(city.map) == hash, "the map in year \(year)")
            #expect(city.hazards.fires.count == fires, "fires in year \(year)")
            #expect((city.vehicles.plane != nil, city.vehicles.helicopter != nil, city.vehicles.ship != nil) == (plane, helicopter, ship))
        }
        #expect(crashes == 5)
    }
}
