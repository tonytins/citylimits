/// What kind of line the ticker shows, which decides how soon it goes out.
public enum NewsKind: Sendable {
    case headline
    case advisory
    case disaster
    case ordinance
    case neighbour

    // Disasters go first, then the city's own business, then headlines.
    var rank: Int {
        switch self {
        case .disaster: 2
        case .advisory, .ordinance, .neighbour: 1
        case .headline: 0
        }
    }
}

/// A line for the ticker, and what clicking it opens, if anything.
public struct NewsItem: Hashable, Sendable {
    public var text: String
    public var kind: NewsKind
    public var link: String?

    public init(_ text: String, kind: NewsKind, link: String? = nil) {
        self.text = text
        self.kind = kind
        self.link = link
    }
}

/// The lines waiting for the ticker, most urgent first, at most 16. When it's full, a headline
/// makes way, or failing one the oldest line.
public struct NewsQueue: Sendable {
    public static let capacity = 16

    private var waiting: [NewsItem] = []

    public init() {}

    public var count: Int { waiting.count }

    /// Queues a line behind every line as urgent or more. A blank line, or one already waiting,
    /// is left out.
    public mutating func post(_ item: NewsItem) {
        guard !item.text.trimmingWhitespace.isEmpty, !waiting.contains(item) else { return }
        if waiting.count == Self.capacity {
            waiting.remove(at: waiting.firstIndex { $0.kind == .headline } ?? 0)
        }
        let at = waiting.firstIndex { $0.kind.rank < item.kind.rank } ?? waiting.count
        waiting.insert(item, at: at)
    }

    public mutating func next() -> NewsItem? {
        waiting.isEmpty ? nil : waiting.removeFirst()
    }
}

/// Writes the ticker's headlines from what the city is up to: a population milestone, the new
/// year, a line the city's state has earned, or flavour. It uses the game's own random numbers,
/// never the city's, since headlines are for show.
public struct NewsDesk {
    // A reactive line, once shown, rests for this many headlines before it can be news again.
    private static let reactiveRest = 12

    private var resting = Array(repeating: 0, count: Headlines.reactive.count)
    private var flavourBag = Array(Headlines.flavour.indices)
    private var flavourNext = Headlines.flavour.count
    private var milestonesPassed: Int
    private var newYearShown: Int

    public init(city: CitySimulation) {
        milestonesPassed = Self.milestonesPassed(city.census.cityPopulation)
        newYearShown = city.date.year
    }

    public mutating func next<Random: RandomNumberGenerator>(for city: CitySimulation, random: inout Random) -> NewsItem {
        for index in resting.indices { resting[index] = max(0, resting[index] - 1) }

        let passed = Self.milestonesPassed(city.census.cityPopulation)
        if passed > milestonesPassed {
            milestonesPassed = passed
            let count = Self.grouped(Headlines.populationMilestones[passed - 1])
            return headline(Headlines.populationMilestone.replacing("{count}", with: count), city)
        }

        let date = city.date
        if date.month == 1 && date.year > newYearShown {
            newYearShown = date.year
            return headline(Headlines.newYear, city)
        }

        if Bool.random(using: &random), let reactive = reactive(city, &random) { return reactive }
        return headline(nextFlavour(&random), city)
    }

    // One of the lines whose state the city is in, and which hasn't been news lately.
    private mutating func reactive<Random: RandomNumberGenerator>(_ city: CitySimulation, _ random: inout Random) -> NewsItem? {
        let eligible = Headlines.reactive.indices.filter { resting[$0] == 0 && Headlines.reactive[$0].when(city) }
        guard let pick = eligible.randomElement(using: &random), let line = Headlines.reactive[pick].lines.randomElement(using: &random) else {
            return nil
        }
        resting[pick] = Self.reactiveRest
        return headline(line, city)
    }

    // Every flavour line once, in a shuffled order, before any comes round again.
    private mutating func nextFlavour<Random: RandomNumberGenerator>(_ random: inout Random) -> String {
        if flavourNext >= flavourBag.count {
            flavourBag.shuffle(using: &random)
            flavourNext = 0
        }
        defer { flavourNext += 1 }
        return Headlines.flavour[flavourBag[flavourNext]]
    }

    private func headline(_ line: String, _ city: CitySimulation) -> NewsItem {
        NewsItem(line.replacing("{city}", with: city.name).replacing("{year}", with: String(city.date.year)), kind: .headline)
    }

    private static func milestonesPassed(_ population: Int) -> Int {
        Headlines.populationMilestones.prefix { population >= $0 }.count
    }

    // A whole number with commas between the thousands, as 250,000.
    public static func grouped(_ value: Int) -> String {
        let digits = String(value.magnitude)
        var result = ""
        for (index, digit) in digits.enumerated() {
            if index > 0 && (digits.count - index) % 3 == 0 { result.append(",") }
            result.append(digit)
        }
        return value < 0 ? "-" + result : result
    }
}
