import Foundation

extension CityMessage {
    /// What the player reads.
    public var text: String {
        switch self {
        case .needPower: "The city needs a power plant"
        case .blackouts: "Blackouts: many zones have no power"
        case .brownouts: "Brownouts: the power plants can't keep up, so build another"
        case .highTaxes: "Taxes are too high: people and businesses are leaving"
        case .roadsNeedFunding: "The roads need funding, or they will crumble"
        case .needFireStation: "The city needs a fire station"
        case .needPoliceStation: "The city needs a police station"
        case .fireNeedsFunding: "The fire department needs funding"
        case .policeNeedsFunding: "The police department needs funding"
        case .trafficJams: "Frequent traffic jams reported"
        case .needRail: "Inadequate rail system"
        case .highPollution: "Pollution very high"
        case .highCrime: "Crime very high"
        case .fireReported: "Fire reported!"
        case .explosionReported: "Explosion reported!"
        case .floodReported: "Flooding reported!"
        case .earthquakeReported: "Major earthquake reported!"
        case .needStadium: "Residents demand a stadium"
        case .needSeaport: "Industry requires a seaport"
        case .needAirport: "Commerce requires an airport"
        case .meltdownReported: "A nuclear meltdown has occurred at your power plant!"
        case .planeCrashReported: "A plane has crashed!"
        case .helicopterCrashReported: "A helicopter crashed!"
        case .shipwreckReported: "Shipwreck reported!"
        case .heavyTrafficReported: "Heavy traffic reported"
        case .tornadoReported: "Tornado reported!"
        case .monsterReported: "A monster has been sighted!"
        }
    }

    /// Whether it reports a disaster, which the news ticker puts first.
    public var isDisaster: Bool {
        switch self {
        case .fireReported, .explosionReported, .floodReported, .earthquakeReported, .meltdownReported, .planeCrashReported,
            .helicopterCrashReported, .shipwreckReported, .tornadoReported, .monsterReported:
            true
        default:
            false
        }
    }
}

/// Holds back a message the city repeats within a year, so the log and the ticker don't fill
/// with the same complaint.
public struct CityMessageFilter: Sendable {
    private var last: CityMessage?
    private var lastTime = 0

    public init() {}

    public mutating func shouldPass(_ message: CityMessage, cityTime: Int) -> Bool {
        if message == last && cityTime - lastTime < CityCalendar.countsPerYear { return false }
        (last, lastTime) = (message, cityTime)
        return true
    }
}
