/// Which demands a missing stadium, seaport, or airport holds back, as in Micropolis.
public struct DemandCaps: OptionSet, Hashable, Sendable {
    public let rawValue: UInt8
    public init(rawValue: UInt8) { self.rawValue = rawValue }

    public static let residential = DemandCaps(rawValue: 1)
    public static let commercial = DemandCaps(rawValue: 2)
    public static let industrial = DemandCaps(rawValue: 4)
}

/// How much the city wants more residents, commerce, and industry: the RCI demand, Micropolis'
/// model at Easy difficulty. It moves every other count, pulled by the jobs and labour the census
/// found and pushed by the tax rate.
public final class CityDemand {
    public static let residentialLimit = 2000
    public static let businessLimit = 1500
    public static let historyLength = 240

    // A year's births, as a share of the residents.
    private static let birthRate = 0.02
    // The most the labour available can tilt commerce and industry.
    private static let mostLabour = 1.3
    // How many people support one unit of commerce.
    private static let peoplePerCommerce = 3.7
    // How much industry the economy wants at Easy, and the least it always wants.
    private static let industryAppetite = 1.2
    private static let leastIndustry = 5.0
    // A population this many times what it should be, or more, counts as the most.
    private static let mostRatio = 2.0
    // How strongly a ratio moves its demand.
    private static let pull = 600.0
    // With no residents yet, how much the city wants them.
    private static let emptyCityResidentialRatio = 1.3
    // How hard each tax rate, from 0% to 20%, pushes every demand: Micropolis' table.
    static let taxPush = [200, 150, 120, 100, 80, 50, 30, 0, -10, -40, -100, -150, -200, -250, -300, -350, -400, -450, -500, -550, -600]

    public internal(set) var residential = 0
    public internal(set) var commercial = 0
    public internal(set) var industrial = 0
    /// The people the budget taxes, in the census' units.
    public internal(set) var taxablePopulation = 0
    /// What the economy alone does to each demand, for showing the player.
    public internal(set) var residentialPull = 0
    public internal(set) var commercialPull = 0
    public internal(set) var industrialPull = 0
    public internal(set) var taxPushApplied = 0
    public internal(set) var jobsPerResident = 1.0
    public internal(set) var workersPerJob = 1.0

    /// Each update's demand, the latest first; only the first `historyCount` are real.
    public internal(set) var residentialHistory = Array(repeating: 0, count: historyLength)
    public internal(set) var commercialHistory = Array(repeating: 0, count: historyLength)
    public internal(set) var industrialHistory = Array(repeating: 0, count: historyLength)
    public internal(set) var historyCount = 0

    public init() {}

    /// Moves the demand once, from the census, the tax rate in force, the facilities missing,
    /// and a nudge from ordinances and neighbours.
    public func update(
        census: CityCensus, taxRate: Int = CityBudget.defaultTaxRate, caps: DemandCaps = [],
        nudge: (residential: Int, commercial: Int, industrial: Int) = (0, 0, 0)
    ) {
        precondition(Self.taxPush.indices.contains(taxRate), "tax rate \(taxRate) is out of range")

        // Residents are counted in eighths, as Micropolis counts them against jobs.
        let residents = Double(census.residential) / 8
        let commerce = Double(census.commercial)
        let industry = Double(census.industrial)
        let lastMonthsJobs = Double(census.commercialHistory[1] + census.industrialHistory[1])

        // People move in when there are more jobs than residents, and out when there are fewer.
        let employment = residents > 0 ? lastMonthsJobs / residents : 1
        let projectedResidents = residents + residents * (employment - 1) + residents * Self.birthRate

        var labour = lastMonthsJobs > 0 ? Double(census.residentialHistory[1]) / lastMonthsJobs : 1
        labour = min(max(labour, 0), Self.mostLabour)
        let projectedCommerce = (residents + commerce + industry) / Self.peoplePerCommerce * labour
        let projectedIndustry = max(industry * labour * Self.industryAppetite, Self.leastIndustry)

        let residentialRatio = residents > 0 ? projectedResidents / residents : Self.emptyCityResidentialRatio
        let commercialRatio = commerce > 0 ? projectedCommerce / commerce : projectedCommerce
        let industrialRatio = industry > 0 ? projectedIndustry / industry : projectedIndustry

        taxablePopulation = Int(residents + commerce + industry)
        jobsPerResident = employment
        workersPerJob = labour
        residentialPull = Int(Self.economy(residentialRatio).rounded(.toNearestOrEven))
        commercialPull = Int(Self.economy(commercialRatio).rounded(.toNearestOrEven))
        industrialPull = Int(Self.economy(industrialRatio).rounded(.toNearestOrEven))
        let push = Self.taxPush[taxRate]
        taxPushApplied = push
        residential = Self.move(residential, residentialRatio, push + nudge.residential, Self.residentialLimit)
        commercial = Self.move(commercial, commercialRatio, push + nudge.commercial, Self.businessLimit)
        industrial = Self.move(industrial, industrialRatio, push + nudge.industrial, Self.businessLimit)

        // As Micropolis does, once it has moved: a demand held back stays where it is if it is
        // falling, and comes down to zero if it would rise.
        if caps.contains(.residential) { residential = min(residential, 0) }
        if caps.contains(.commercial) { commercial = min(commercial, 0) }
        if caps.contains(.industrial) { industrial = min(industrial, 0) }

        CityDemand.push(residential, onto: &residentialHistory)
        CityDemand.push(commercial, onto: &commercialHistory)
        CityDemand.push(industrial, onto: &industrialHistory)
        historyCount = min(historyCount + 1, Self.historyLength)
    }

    private static func push(_ value: Int, onto history: inout [Int]) {
        history.removeLast()
        history.insert(value, at: 0)
    }

    // A ratio above 1 raises demand, below 1 lowers it, and the tax pushes it as well.
    private static func move(_ demand: Int, _ ratio: Double, _ push: Int, _ limit: Int) -> Int {
        let change = Int((economy(ratio) + Double(push)).rounded(.towardZero))
        return min(max(demand + change, -limit), limit)
    }

    private static func economy(_ ratio: Double) -> Double { (min(ratio, mostRatio) - 1) * pull }
}
