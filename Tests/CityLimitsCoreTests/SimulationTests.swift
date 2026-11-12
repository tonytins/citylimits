import Testing

@testable import CityLimitsCore

struct CalendarTests {
    @Test func aYearIs48CountsOf16Steps() {
        #expect(CityCalendar.stepsPerYear == 768)
        #expect(CityCalendar.date(at: 0) == GameDate(year: 1900, month: 1))
        #expect(CityCalendar.date(at: 47) == GameDate(year: 1900, month: 12))
        #expect(CityCalendar.date(at: 48) == GameDate(year: 1901, month: 1))
    }

    @Test func theLastBandTakesTheColumnsLeftOver() {
        #expect(CityCalendar.bandColumns(0, mapWidth: 120) == 0..<15)
        #expect(CityCalendar.bandColumns(7, mapWidth: 123) == 105..<123)
    }

    @Test func datesAddMonthsBothWays() {
        let date = GameDate(year: 1950, month: 3)
        #expect(date.adding(months: 10) == GameDate(year: 1951, month: 1))
        #expect(date.adding(months: -3) == GameDate(year: 1949, month: 12))
        #expect(date.description == "1950-03")
    }

    @Test func theClockCarriesTheRestAndCapsAStall() {
        var clock = GameClock()
        #expect(clock.advance(0.625) == 2)
        #expect(clock.advance(0.125) == 1)
        #expect(clock.advance(10) == GameClock.defaultMaxStepsPerFrame)
        #expect(clock.advance(1e300) == GameClock.defaultMaxStepsPerFrame)
        #expect(clock.advance(.nan) == 0)
        #expect(clock.advance(-1) == 0)
    }
}

struct CitySimulationTests {
    @Test(arguments: [("Springfield", true), ("  Padded  ", true), ("", false), ("   ", false),
                      (String(repeating: "x", count: 33), false), ("Tab\there", false)])
    func namesAreChecked(name: String, valid: Bool) {
        #expect(CitySimulation.isValidName(name) == valid)
    }

    @Test func aRenameTrimsTheName() {
        let city = CitySimulation(map: TileMap(width: 16, height: 16, terrain: Array(repeating: .grass, count: 256)), seed: 1)
        #expect(city.rename("  Riverton "))
        #expect(city.name == "Riverton")
        #expect(!city.rename(""))
        #expect(city.name == "Riverton")
    }

    @Test func aCityCanMoveItsFoundingOnlyBeforeItStarts() {
        let city = CitySimulation(map: TileMap(width: 16, height: 16, terrain: Array(repeating: .grass, count: 256)), seed: 1)
        #expect(city.moveFounding(to: 1950))
        #expect(city.date == GameDate(year: 1950, month: 1))
        city.tick()
        #expect(!city.moveFounding(to: 1960))
    }

    @Test func anOrdinanceChangesWhatTheCityReads() {
        let city = CitySimulation(map: TileMap(width: 16, height: 16, terrain: Array(repeating: .grass, count: 256)), seed: 1)
        var news: [OrdinanceNews] = []
        city.onEvent = { if case .ordinance(let item) = $0 { news.append(item) } }
        #expect(city.setOrdinance("parkingFines", enacted: true))
        #expect(!city.setOrdinance("parkingFines", enacted: true))
        #expect(!city.setOrdinance("noSuchThing", enacted: true))
        #expect(city.traffic.tripPercent == 90)
        #expect(news.map(\.kind) == [.enacted])
        #expect(city.ordinances.yearlyMoney(population: 10_000) == (income: 20, cost: 0))
    }

    @Test func taxAndFundingStayInRange() {
        let budget = CityBudget()
        #expect(!budget.setTaxRate(21))
        #expect(!budget.setFunding(roads: 101, fire: 100, police: 100))
        #expect(budget.setTaxRate(9))
        #expect(budget.taxRate == 9)
    }

    @Test func roadsArePaidFirstWhenMoneyRunsShort() {
        let budget = CityBudget()
        let year = budget.estimate(population: 0, landValueAverage: 0, roadTiles: 100, fireStations: 1, policeStations: 1, funds: 120)
        #expect(year.roadFunded == 70)
        #expect(year.fireFunded == 50)
        #expect(year.policeFunded == 0)
        #expect(year.cut)
    }
}
