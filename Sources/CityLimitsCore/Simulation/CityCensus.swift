/// What the scan counted this count, and the monthly history the graphs read, as in Micropolis.
public final class CityCensus {
    public static let historyMonths = 120

    /// This count's tally of residents, shops, and industry, in the scan's units.
    public internal(set) var residential = 0
    public internal(set) var commercial = 0
    public internal(set) var industrial = 0

    /// The people as of the last month, as the player sees them.
    public internal(set) var residentPopulation = 0
    public internal(set) var commercialPopulation = 0
    public internal(set) var industrialPopulation = 0
    public internal(set) var cityPopulation = 0

    /// Month by month, the latest first.
    public internal(set) var residentialHistory = Array(repeating: 0, count: historyMonths)
    public internal(set) var commercialHistory = Array(repeating: 0, count: historyMonths)
    public internal(set) var industrialHistory = Array(repeating: 0, count: historyMonths)

    public init() {}

    public func add(_ use: TileUse, population: Int) {
        switch use {
        case .residential: residential += population
        case .commercial: commercial += population
        case .industrial: industrial += population
        default: break
        }
    }

    public func clear() {
        residential = 0
        commercial = 0
        industrial = 0
    }

    /// Pushes this count's tally onto the history, and works out the people from it.
    public func recordMonth() {
        Self.push(residential / 8, onto: &residentialHistory)
        Self.push(commercial, onto: &commercialHistory)
        Self.push(industrial, onto: &industrialHistory)
        residentPopulation = residential * 20
        commercialPopulation = commercial * 8 * 20
        industrialPopulation = industrial * 8 * 20
        cityPopulation = residentPopulation + commercialPopulation + industrialPopulation
    }

    private static func push(_ value: Int, onto history: inout [Int]) {
        history.removeLast()
        history.insert(value, at: 0)
    }
}
