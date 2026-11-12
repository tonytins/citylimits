import Testing

@testable import CityLimitsCore

/// A small city of bare ground to build on, with a builder that pays from its treasury.
struct TestCity {
    let map: TileMap
    let treasury: Treasury
    let roads = RoadNetwork()
    let hazards: Hazards
    let builder: CityBuilder

    init(funds: Int = 1000, size: Int = 9) {
        map = TileMap(width: size, height: size, terrain: Array(repeating: .grass, count: size * size))
        treasury = Treasury(funds: funds)
        hazards = Hazards(map: map)
        builder = CityBuilder(map: map, treasury: treasury, roads: roads, hazards: hazards)
    }

    @discardableResult
    func drag(_ action: BuildAction, from: (Int, Int), to: (Int, Int)) -> BuildResult {
        var stroke = BuildStroke(action, from.0, from.1)
        stroke.drag(to: to.0, to.1)
        return builder.apply(stroke)
    }
}

struct BuildStrokeTests {
    @Test(arguments: [
        ((7, 5), TileRect(x: 3, y: 4, width: 5, height: 1)),  // mostly across: a line across
        ((0, 3), TileRect(x: 0, y: 4, width: 4, height: 1)),  // across to the left
        ((4, 9), TileRect(x: 3, y: 4, width: 1, height: 6)),  // mostly down: a line down
        ((2, 1), TileRect(x: 3, y: 1, width: 1, height: 4)),  // up
        ((5, 6), TileRect(x: 3, y: 4, width: 3, height: 1)),  // as far across as down: across wins
    ])
    func aRoadRunsInOneStraightLine(to: (Int, Int), expected: TileRect) {
        var stroke = BuildStroke(.road, 3, 4)
        stroke.drag(to: to.0, to.1)
        #expect(stroke.bounds == expected)
    }

    @Test(arguments: [
        ((10, 10), TileRect(x: 9, y: 9, width: 3, height: 3)),  // a click: one zone centred there
        ((12, 10), TileRect(x: 9, y: 9, width: 3, height: 3)),  // two right: not yet a second zone
        ((13, 10), TileRect(x: 9, y: 9, width: 6, height: 3)),  // three right: a second zone
        ((16, 13), TileRect(x: 9, y: 9, width: 9, height: 6)),  // right and down: 3 by 2 zones
        ((7, 10), TileRect(x: 6, y: 9, width: 6, height: 3)),  // three left: the second on the left
        ((10, 4), TileRect(x: 9, y: 3, width: 3, height: 9)),  // six up: three zones up
    ])
    func zonesAreLaidAsAGridCentredWhereTheStrokeStarted(to: (Int, Int), expected: TileRect) {
        var stroke = BuildStroke(.zoneResidential, 10, 10)
        stroke.drag(to: to.0, to.1)
        #expect(stroke.bounds == expected)
    }

    @Test func aPlanChangesNothingUntilApplied() {
        let city = TestCity()
        let plan = BuildPlan()
        var stroke = BuildStroke(.road, 1, 1)
        stroke.drag(to: 5, 1)
        city.builder.plan(stroke, into: plan)
        #expect(plan.tiles.count == 5)
        #expect(plan.cost == 50)
        #expect(city.map[3, 1].use == .none)
        #expect(city.treasury.funds == 1000)
        #expect(plan.connections(.road, 3, 1) == [.east, .west])
    }

    @Test func aStrokeTheCityCantPayForIsRefusedWhole() {
        let city = TestCity(funds: 30)
        let result = city.drag(.road, from: (1, 1), to: (5, 1))
        #expect(result.status == .refused && result.reason == .notEnoughFunds)
        #expect(city.map[1, 1].use == .none)
        #expect(city.treasury.funds == 30)
    }
}

struct CityBuilderTests {
    @Test func aRoadIsLaidOnClearGround() {
        let city = TestCity()
        let result = city.builder.apply(.road, 2, 2)
        #expect(result.status == .built)
        #expect(city.map[2, 2].use == .road)
        #expect(city.treasury.funds == 990)
        #expect(city.roads.isDirty)
    }

    @Test(arguments: [TileKind.trees, .rubble])
    func aRoadClearsTreesAndRubbleForADollarMore(kind: TileKind) {
        let city = TestCity()
        city.map.setState(2, 2, TileState(kind))
        #expect(city.builder.apply(.road, 2, 2).status == .built)
        #expect(city.map[2, 2].kind == .grass)
        #expect(city.treasury.funds == 989)
    }

    @Test func layingTheSameRoadTwiceChangesNothing() {
        let city = TestCity()
        city.builder.apply(.road, 2, 2)
        let result = city.builder.apply(.road, 2, 2)
        #expect(result.status == .unchanged && result.reason == .alreadyDone)
        #expect(city.treasury.funds == 990)
    }

    @Test func aRoadCannotBeLaidOnAZone() {
        let city = TestCity()
        city.builder.apply(.zoneResidential, 4, 4)
        #expect(city.builder.apply(.road, 3, 3).reason == .cannotBuildHere)
        #expect(city.map[3, 3].use == .residential)
    }

    @Test func aRoadAcrossWaterBecomesABridgeFromTheBank() {
        let city = TestCity()
        city.map.setState(3, 2, TileState(.water))
        city.map.setState(4, 2, TileState(.water))
        let result = city.drag(.road, from: (1, 2), to: (5, 2))
        #expect(result.tilesChanged == 5)
        #expect(city.map[3, 2].isBridge && city.map[4, 2].isBridge)
        #expect(city.treasury.funds == 1000 - 3 * 10 - 2 * 50)
    }

    @Test func aBridgeCanStartFromTheFarBank() {
        let city = TestCity()
        city.map.setState(2, 2, TileState(.water))
        city.map.setState(3, 2, TileState(.water))
        #expect(city.drag(.road, from: (2, 2), to: (4, 2)).tilesChanged == 3)
        #expect(city.map[2, 2].isBridge)
    }

    @Test func waterWithNoRoadToStartFromIsNotBridged() {
        let city = TestCity()
        city.map.setState(2, 2, TileState(.water))
        #expect(city.builder.apply(.road, 2, 2).reason == .cannotBuildHere)
        #expect(city.map[2, 2].use == .none)
    }

    @Test(arguments: [(BuildAction.zoneResidential, TileUse.residential), (.zoneCommercial, .commercial), (.zoneIndustrial, .industrial)])
    func aZoneIsThreeByThreeAroundWhereItIsPlaced(action: BuildAction, use: TileUse) {
        let city = TestCity()
        #expect(city.builder.apply(action, 4, 4).tilesChanged == 9)
        for y in 3...5 {
            for x in 3...5 {
                #expect(city.map[x, y].use == use)
                #expect(city.map[x, y].zonePart == (y - 3) * 3 + (x - 3))
            }
        }
        #expect(city.map[4, 4].isZoneCenter)
        #expect(city.treasury.funds == 900)
    }

    @Test func aZoneGoesDownWholeOrNotAtAll() {
        let city = TestCity()
        city.map.setState(5, 5, TileState(.water))
        #expect(city.builder.apply(.zoneResidential, 4, 4).reason == .cannotBuildHere)
        #expect(city.map[4, 4].use == .none)
        #expect(city.treasury.funds == 1000)
    }

    @Test func aZoneThatWouldRunOffTheMapIsRefused() {
        let city = TestCity()
        #expect(city.builder.apply(.zoneResidential, 0, 0).reason == .offTheMap)
    }

    @Test func theBulldozerKnocksDownAZoneThroughItsCentreAndLeavesRubble() {
        let city = TestCity()
        city.builder.apply(.zoneIndustrial, 4, 4)
        let result = city.builder.apply(.bulldoze, 4, 4)
        #expect(result.tilesChanged == 9)
        #expect(city.treasury.funds == 1000 - 100 - 1)
        for y in 3...5 {
            for x in 3...5 {
                #expect(city.map[x, y] == TileState(.rubble))
            }
        }
    }

    @Test func theBulldozerWontClearAZoneTileByTile() {
        let city = TestCity()
        city.builder.apply(.zoneIndustrial, 4, 4)
        #expect(city.builder.apply(.bulldoze, 3, 3).reason == .partOfAZone)
        #expect(city.map[3, 3].use == .industrial)
    }

    @Test func theBulldozerTurnsABridgeBackIntoWater() {
        let city = TestCity()
        city.map.setState(2, 2, TileState(.water, .road))
        city.builder.apply(.bulldoze, 2, 2)
        #expect(city.map[2, 2] == TileState(.water))
    }

    @Test func theBulldozerHasNothingToClearOnBareGround() {
        let city = TestCity()
        let result = city.builder.apply(.bulldoze, 2, 2)
        #expect(result.status == .unchanged && result.reason == .nothingToClear)
    }

    @Test func aPlantHasPowerFromTheMomentItIsPlaced() {
        let city = TestCity(funds: 5000)
        #expect(city.builder.apply(.coalPowerPlant, 3, 3).tilesChanged == 16)
        #expect(city.map[3, 3].powered && city.map[3, 3].isBuildingCenter)
        #expect(city.treasury.funds == 2000)
    }

    // Crossings, after the C# PowerLineTests and RailTests: one network laid over another that
    // runs straight makes a tile that carries both, each its own way.
    @Test func aLineOverAStraightRoadMakesACrossing() {
        let city = TestCity()
        city.drag(.road, from: (0, 2), to: (8, 2))
        city.drag(.powerLine, from: (4, 0), to: (4, 4))
        #expect(city.map[4, 2].use == .roadAcrossPowerLine)
        #expect(Network.road.connections(4, 2, on: city.map) == [.east, .west])
        #expect(Network.power.connections(4, 2, on: city.map) == [.north, .south])
    }

    @Test func aRoadOverAStraightLineMakesACrossing() {
        let city = TestCity()
        city.drag(.powerLine, from: (0, 2), to: (8, 2))
        city.drag(.road, from: (4, 0), to: (4, 4))
        #expect(city.map[4, 2].use == .roadDownPowerLine)
    }

    @Test func railOverARoadRunsTheOtherWay() {
        let city = TestCity()
        city.drag(.road, from: (0, 2), to: (8, 2))
        city.drag(.rail, from: (4, 0), to: (4, 4))
        #expect(city.map[4, 2].use == .railDownRoad)
        #expect(Network.rail.connections(4, 2, on: city.map) == [.north, .south])
    }

    @Test func aCornerTakesNoCrossing() {
        let city = TestCity()
        city.drag(.road, from: (0, 2), to: (4, 2))
        city.drag(.road, from: (4, 2), to: (4, 6))
        let result = city.drag(.powerLine, from: (4, 0), to: (4, 2))
        #expect(city.map[4, 2].use == .road)
        #expect(result.tilesChanged == 2)
    }

    @Test func nothingIsBuiltOnAFire() {
        let city = TestCity()
        city.map.setState(2, 2, TileState(.trees))
        city.hazards.ignite(2, 2)
        #expect(city.builder.apply(.road, 2, 2).reason == .onFire)
        #expect(city.builder.apply(.bulldoze, 2, 2).reason == .onFire)
    }
}

struct TileInfoTests {
    @Test func describesWhatIsOnATile() {
        let city = TestCity()
        city.builder.apply(.zoneResidential, 4, 4)
        let definitions = CityDefinitions.base
        #expect(city.map.describe(0, 0, definitions: definitions) == "Grass - ready to build on")
        #expect(city.map.describe(3, 3, definitions: definitions) == "Residential zone - not developed yet, no power")
        #expect(TileUse.coalPowerPlant.shortName == "coal plant")
        #expect(TileUse.residential.shortName == "residential")
    }
}
