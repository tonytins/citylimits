/// How the city's time is counted, as in Micropolis: a step is one sixteenth of a count, a count
/// is a quarter of a month, and a year is 48 counts.
public enum CityCalendar {
    public static let startYear = 1900
    /// The latest year a new city or scenario can start in.
    public static let latestStartYear = 9999
    public static let countsPerMonth = 4
    public static let countsPerYear = countsPerMonth * 12
    public static let stepsPerCount = 16
    /// The map is scanned in this many bands of columns, one each step after the first of a count.
    public static let scanBands = 8
    public static let stepsPerMonth = stepsPerCount * countsPerMonth
    public static let stepsPerYear = stepsPerCount * countsPerYear

    /// The date at a count.
    public static func date(at cityTime: Int) -> GameDate {
        precondition(cityTime >= 0, "a city's time is never negative")
        return GameDate(year: startYear + cityTime / countsPerYear, month: cityTime % countsPerYear / countsPerMonth + 1)
    }

    /// The columns a band of the scan covers; the last band takes what's left over.
    public static func bandColumns(_ band: Int, mapWidth: Int) -> Range<Int> {
        let width = mapWidth / scanBands
        let first = band * width
        return first..<(band == scanBands - 1 ? mapWidth : first + width)
    }
}

/// A year and a month in the city.
public struct GameDate: Hashable, Comparable, Sendable, CustomStringConvertible {
    public let year: Int
    /// From 1 to 12.
    public let month: Int

    public init(year: Int, month: Int) {
        precondition(year >= 0 && (1...12).contains(month), "\(year)-\(month) isn't a date")
        self.year = year
        self.month = month
    }

    public func adding(months: Int) -> GameDate {
        let total = year * 12 + (month - 1) + months
        let (whole, rest) = total.quotientAndRemainder(dividingBy: 12)
        return rest < 0 ? GameDate(year: whole - 1, month: rest + 13) : GameDate(year: whole, month: rest + 1)
    }

    public static func < (lhs: GameDate, rhs: GameDate) -> Bool {
        (lhs.year, lhs.month) < (rhs.year, rhs.month)
    }

    public var description: String {
        let paddedYear = String(repeating: "0", count: max(0, 4 - String(year).count)) + String(year)
        return paddedYear + (month < 10 ? "-0\(month)" : "-\(month)")
    }
}

/// How fast the city runs: how many of its fixed-size steps a second of play holds. The step
/// never changes size; speed only changes how many run.
public enum GameSpeed: Int, CaseIterable, Sendable {
    case paused
    case normal
    case fast
    case superFast

    public var multiplier: Double {
        switch self {
        case .paused: 0
        case .normal: 1
        case .fast: 5
        case .superFast: 50
        }
    }
}

/// Turns the time between frames into whole steps, carrying the rest over. A frame that would run
/// too many steps, after a stall, runs the most and drops the rest, so the game never spirals.
public struct GameClock: Sendable {
    public static let defaultStepSeconds = 0.25
    public static let defaultMaxStepsPerFrame = 16

    public let stepSeconds: Double
    public let maxStepsPerFrame: Int
    public private(set) var accumulator = 0.0

    public init(stepSeconds: Double = defaultStepSeconds, maxStepsPerFrame: Int = defaultMaxStepsPerFrame) {
        precondition(stepSeconds > 0 && maxStepsPerFrame > 0, "a clock needs a step and a limit")
        self.stepSeconds = stepSeconds
        self.maxStepsPerFrame = maxStepsPerFrame
    }

    /// How many steps are due after `elapsedSeconds` of play.
    public mutating func advance(_ elapsedSeconds: Double) -> Int {
        guard elapsedSeconds > 0, elapsedSeconds.isFinite else { return 0 }
        accumulator += elapsedSeconds
        let steps = accumulator / stepSeconds
        // Checked before converting, since a long enough stall wouldn't fit in an Int.
        if steps > Double(maxStepsPerFrame) {
            accumulator = 0
            return maxStepsPerFrame
        }
        let due = Int(steps)
        guard due > 0 else { return 0 }
        accumulator -= Double(due) * stepSeconds
        return due
    }
}
