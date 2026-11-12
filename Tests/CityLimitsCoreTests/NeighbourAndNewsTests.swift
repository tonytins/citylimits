import Testing

@testable import CityLimitsCore

struct NeighbourTests {
    private static func grass(_ size: Int = 16) -> TileMap {
        TileMap(width: size, height: size, terrain: Array(repeating: .grass, count: size * size))
    }

    // From the C# build's CityNeighbours for the same seeds.
    @Test(arguments: [
        (Int32(2000), ["Redfield 9364 74", "Westbrook 7800 127", "Clearwater 4661 93", "Harborview 2642 135"]),
        (Int32(-7), ["Lakemont 9428 127", "Hillcrest 3326 97", "Southvale 6862 56", "Clearwater 7329 139"]),
    ])
    func theSeedNamesTheNeighboursAsTheCSharpBuildDid(seed: Int32, expected: [String]) {
        let neighbours = CityNeighbours(map: Self.grass(), seed: seed)
        #expect(neighbours.all.map { "\($0.name) \($0.start) \($0.share)" } == expected)
    }

    @Test func roadsAndRailOffTheMapLiftDemand() {
        let city = TestCity(size: 16)
        var road = BuildStroke(.road, 10, 5)
        road.drag(to: 15, 5)
        city.builder.apply(road)
        let neighbours = CityNeighbours(map: city.map, seed: 1)
        #expect(neighbours.connections(to: .east) == [.road])
        #expect(neighbours.connections(to: .west) == [])
        #expect(neighbours.demandNudge() == (0, CityNeighbours.roadDemand, CityNeighbours.roadDemand))
    }

    @Test func aDealIsOfferedOverAPowerLineTakenAndBroken() {
        let city = TestCity(size: 16)
        var line = BuildStroke(.powerLine, 10, 5)
        line.drag(to: 15, 5)
        city.builder.apply(line)
        let neighbours = CityNeighbours(map: city.map, seed: 1)

        #expect(neighbours.propose(population: 100, brownedOut: true, capacity: 0, used: 0, at: 0) == nil)
        let offer = neighbours.propose(population: 5000, brownedOut: true, capacity: 700, used: 700, at: 0)
        #expect(offer?.side == .east && offer?.kind == .cityBuys && offer?.power == CityNeighbours.powerSold)
        #expect(neighbours.propose(population: 5000, brownedOut: true, capacity: 700, used: 700, at: 0) == nil)

        let deal = neighbours.accept(at: 10)
        #expect(deal?.money == 3 * CityNeighbours.buyPricePer100)
        #expect(neighbours.powerTraded() == CityNeighbours.powerSold)
        #expect(neighbours.yearlyMoney() == (0, 180))

        #expect(neighbours.breakDeal(with: .east, at: 20) != nil)
        #expect(neighbours.holdsGrudge(.east, at: 21))
        #expect(neighbours.propose(population: 5000, brownedOut: true, capacity: 700, used: 700, at: 21) == nil)
    }

    @Test func anOfferLapsesAndADealEnds() {
        let city = TestCity(size: 16)
        var line = BuildStroke(.powerLine, 0, 5)
        line.drag(to: 4, 5)
        city.builder.apply(line)
        let neighbours = CityNeighbours(map: city.map, seed: 1)
        let offer = neighbours.propose(population: 5000, brownedOut: false, capacity: 2000, used: 100, at: 0)
        #expect(offer?.kind == .citySells && offer?.power == 500)
        #expect(neighbours.expire(at: offer!.expiresStep).lapsed == offer)

        _ = neighbours.propose(population: 5000, brownedOut: false, capacity: 2000, used: 100, at: 0)
        let deal = neighbours.accept(at: 0)!
        #expect(neighbours.expire(at: deal.endStep - 1).ended.isEmpty)
        #expect(neighbours.expire(at: deal.endStep).ended == [deal])
        #expect(neighbours.deals.isEmpty)
    }
}

struct NewsTests {
    @Test func urgentNewsGoesFirstAndHeadlinesMakeWay() {
        var queue = NewsQueue()
        queue.post(NewsItem("Flavour", kind: .headline))
        queue.post(NewsItem("Advice", kind: .advisory))
        queue.post(NewsItem("Fire!", kind: .disaster))
        queue.post(NewsItem("Fire!", kind: .disaster))
        queue.post(NewsItem("   ", kind: .disaster))
        #expect(queue.count == 3)
        #expect(queue.next()?.text == "Fire!")
        #expect(queue.next()?.text == "Advice")
        #expect(queue.next()?.text == "Flavour")
        #expect(queue.next() == nil)

        for index in 0..<NewsQueue.capacity { queue.post(NewsItem("Line \(index)", kind: index == 3 ? .headline : .advisory)) }
        queue.post(NewsItem("One more", kind: .advisory))
        #expect(queue.count == NewsQueue.capacity)
        var texts: [String] = []
        while let item = queue.next() { texts.append(item.text) }
        #expect(!texts.contains("Line 3"))
        #expect(texts.last == "One more")
    }

    @Test func theDeskMarksMilestonesAndTheNewYear() {
        let city = SimulationParityTests.scriptedTown()
        city.rename("Riverton")
        var desk = NewsDesk(city: city)
        var random = SeededGenerator(seed: 5)
        city.census.cityPopulation = 1200
        #expect(desk.next(for: city, random: &random).text == "Riverton Tops 1,000 People; Welcome Signs Updated")
        city.run(steps: CityCalendar.stepsPerYear)
        #expect(desk.next(for: city, random: &random).text == "Happy New Year From Riverton: Welcome To 1901")
        for _ in 0..<200 {
            let text = desk.next(for: city, random: &random).text
            #expect(!text.contains("{city}") && !text.contains("{year}"))
        }
    }

    @Test func numbersAreGroupedByThousands() {
        #expect(NewsDesk.grouped(500) == "500")
        #expect(NewsDesk.grouped(250_000) == "250,000")
        #expect(NewsDesk.grouped(1_000_000) == "1,000,000")
    }
}

struct TrainTests {
    @Test func aTrainRunsAlongTheRailAndNeverLeavesIt() {
        let city = TestCity(size: 16)
        var rail = BuildStroke(.rail, 1, 5)
        rail.drag(to: 14, 5)
        city.builder.apply(rail)
        var train = Train()
        var random = SeededGenerator(seed: 3)
        for _ in 0..<2000 {
            train.step(map: city.map, population: 1000, random: &random)
            if train.isRunning {
                #expect(city.map[train.x / 16, train.y / 16].isRail)
            }
        }
    }
}
