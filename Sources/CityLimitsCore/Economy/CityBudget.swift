/// One year's money, estimated or settled.
public struct BudgetYear: Hashable, Sendable {
    public var taxRate: Int
    public var taxIncome: Int
    public var roadRequest: Int
    public var roadFunded: Int
    public var fireRequest: Int
    public var fireFunded: Int
    public var policeRequest: Int
    public var policeFunded: Int
    /// Whether the city couldn't pay all it funded, so something went unpaid.
    public var cut: Bool
    public var ordinanceIncome = 0
    public var ordinanceCost = 0
    public var dealIncome = 0
    public var dealCost = 0

    public var expenses: Int { roadFunded + fireFunded + policeFunded + ordinanceCost + dealCost }
    public var income: Int { taxIncome + ordinanceIncome + dealIncome }
    public var cashFlow: Int { income - expenses }
    public var isDeficit: Bool { expenses > income }
    public var needsAttention: Bool { isDeficit || cut }
}

/// The city's taxes and upkeep, as Micropolis keeps them at Easy: a 48th of a full-year estimate
/// is set aside each count, and the year settles into the treasury once a year.
public final class CityBudget {
    public static let defaultTaxRate = 7
    public static let maxTaxRate = 20
    public static let fullFunding = 100
    public static let countsPerYear = CityCalendar.countsPerYear
    /// How well funded roads are, at full funding.
    public static let fullRoadEffect = 32
    /// How well funded a service is, at full funding.
    public static let fullServiceEffect = 1000

    // Tax income is population times average land value over this, times the tax rate, times
    // Easy's factor; road upkeep is the road count times Easy's factor. They are Micropolis'.
    private static let landValueDivisor = 120
    private static let easyTaxFactor = 1.4
    private static let easyRoadFactor = 0.7

    private let fireStationUpkeep: Int
    private let policeStationUpkeep: Int

    // The year so far, in full-year estimates added up once a count.
    var taxFund = 0
    var roadFund = 0
    var fireFund = 0
    var policeFund = 0
    var ordinanceIncomeFund = 0
    var ordinanceCostFund = 0
    var dealIncomeFund = 0
    var dealCostFund = 0
    var cutThisYear = false

    public internal(set) var taxRate = CityBudget.defaultTaxRate
    public internal(set) var roadFunding = CityBudget.fullFunding
    public internal(set) var fireFunding = CityBudget.fullFunding
    public internal(set) var policeFunding = CityBudget.fullFunding
    /// The tax rate in force, which demand reads; it takes a change at the next count.
    public internal(set) var taxEffect = CityBudget.defaultTaxRate
    public internal(set) var roadEffect = CityBudget.fullRoadEffect
    public internal(set) var fireEffect = CityBudget.fullServiceEffect
    public internal(set) var policeEffect = CityBudget.fullServiceEffect
    public internal(set) var lastYear: BudgetYear?
    public internal(set) var estimated: BudgetYear?

    public init(fireStationUpkeep: Int = 100, policeStationUpkeep: Int = 100) {
        precondition(fireStationUpkeep >= 0 && policeStationUpkeep >= 0, "upkeep is never negative")
        self.fireStationUpkeep = fireStationUpkeep
        self.policeStationUpkeep = policeStationUpkeep
    }

    /// Sets the tax rate, and says whether it was in range.
    @discardableResult
    public func setTaxRate(_ rate: Int) -> Bool {
        guard (0...Self.maxTaxRate).contains(rate) else { return false }
        taxRate = rate
        return true
    }

    /// Sets the funding of roads, fire, and police, each in percent, and says whether they were
    /// all in range.
    @discardableResult
    public func setFunding(roads: Int, fire: Int, police: Int) -> Bool {
        let range = 0...Self.fullFunding
        guard range.contains(roads), range.contains(fire), range.contains(police) else { return false }
        (roadFunding, fireFunding, policeFunding) = (roads, fire, police)
        return true
    }

    /// What a full year would bring in and cost at today's numbers.
    public func estimate(
        population: Int, landValueAverage: Int, roadTiles: Int, fireStations: Int, policeStations: Int, funds: Int,
        ordinanceIncome: Int = 0, ordinanceCost: Int = 0, dealIncome: Int = 0, dealCost: Int = 0
    ) -> BudgetYear {
        // As in Micropolis, the population and land value are divided as whole numbers first.
        let taxed = population * landValueAverage / Self.landValueDivisor * taxRate
        let taxIncome = Self.round(Double(taxed) * Self.easyTaxFactor)
        let roadRequest = Self.round(Double(roadTiles) * Self.easyRoadFactor)
        let fireRequest = fireStationUpkeep * fireStations
        let policeRequest = policeStationUpkeep * policeStations

        // The money there is pays roads first, then fire, then police; the rest goes unpaid.
        // The ordinances and the deals, CityLimits' own, earn with the taxes and are paid last.
        var available = max(0, funds + taxIncome + ordinanceIncome + dealIncome)
        var cut = false
        func pay(_ wanted: Int) -> Int {
            if cut { return 0 }
            if wanted <= available {
                available -= wanted
                return wanted
            }
            defer { available = 0 }
            cut = true
            return available
        }
        let roadFunded = pay(Self.round(Double(roadRequest * roadFunding) / 100))
        let fireFunded = pay(Self.round(Double(fireRequest * fireFunding) / 100))
        let policeFunded = pay(Self.round(Double(policeRequest * policeFunding) / 100))
        let ordinancesPaid = pay(ordinanceCost)
        let dealsPaid = pay(dealCost)

        return BudgetYear(
            taxRate: taxRate, taxIncome: taxIncome, roadRequest: roadRequest, roadFunded: roadFunded,
            fireRequest: fireRequest, fireFunded: fireFunded, policeRequest: policeRequest, policeFunded: policeFunded,
            cut: cut, ordinanceIncome: ordinanceIncome, ordinanceCost: ordinancesPaid, dealIncome: dealIncome, dealCost: dealsPaid)
    }

    /// Sets a count's share of an estimate aside, and puts its funding into effect.
    public func accrue(_ estimate: BudgetYear) {
        taxFund += estimate.taxIncome
        roadFund += estimate.roadFunded
        fireFund += estimate.fireFunded
        policeFund += estimate.policeFunded
        ordinanceIncomeFund += estimate.ordinanceIncome
        ordinanceCostFund += estimate.ordinanceCost
        dealIncomeFund += estimate.dealIncome
        dealCostFund += estimate.dealCost
        cutThisYear = cutThisYear || estimate.cut
        estimated = estimate

        taxEffect = estimate.taxRate
        roadEffect = Self.effect(estimate.roadFunded, estimate.roadRequest, Self.fullRoadEffect)
        fireEffect = Self.effect(estimate.fireFunded, estimate.fireRequest, Self.fullServiceEffect)
        policeEffect = Self.effect(estimate.policeFunded, estimate.policeRequest, Self.fullServiceEffect)
    }

    /// Settles the year into the treasury and starts the next.
    @discardableResult
    public func settle(into treasury: Treasury) -> BudgetYear {
        let counts = Self.countsPerYear
        let year = BudgetYear(
            taxRate: taxEffect, taxIncome: taxFund / counts, roadRequest: 0, roadFunded: roadFund / counts,
            fireRequest: 0, fireFunded: fireFund / counts, policeRequest: 0, policeFunded: policeFund / counts,
            cut: cutThisYear, ordinanceIncome: ordinanceIncomeFund / counts, ordinanceCost: ordinanceCostFund / counts,
            dealIncome: dealIncomeFund / counts, dealCost: dealCostFund / counts)
        treasury.settle(year.cashFlow)
        (taxFund, roadFund, fireFund, policeFund, cutThisYear) = (0, 0, 0, 0, false)
        (ordinanceIncomeFund, ordinanceCostFund, dealIncomeFund, dealCostFund) = (0, 0, 0, 0)
        lastYear = year
        return year
    }

    private static func effect(_ funded: Int, _ request: Int, _ full: Int) -> Int {
        request == 0 ? full : Int((Double(full) * Double(funded) / Double(request)).rounded(.down))
    }

    // Micropolis rounds halves up.
    private static func round(_ value: Double) -> Int { Int((value + 0.5).rounded(.down)) }
}
