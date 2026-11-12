/// What the city's ordinances news is about.
public enum OrdinanceNewsKind: Sendable {
    /// The city asks for an ordinance, as SimCity 3000's petitions do.
    case petition
    case enacted
    case repealed
}

public struct OrdinanceNews: Hashable, Sendable {
    public var ordinance: OrdinanceSpec
    public var kind: OrdinanceNewsKind
}

/// Which ordinances are in force, and what they add up to.
public final class CityOrdinances {
    /// A city asks for ordinances once it has this many people.
    public static let petitionPopulation = 2000

    // The revenue ordinances a city in the red is asked for, the least painful first.
    private static let revenue = ["salesTax", "incomeTax", "parkingFines", "legalizedGambling"]
    private static let slackDemand = -500
    private static let highCrime = 100
    private static let highPollution = 60

    /// The ordinances, in the order the player is shown them.
    public let all: [OrdinanceSpec]
    var enactedFlags: [Bool]
    private var effects: [OrdinanceEffect: Int] = [:]

    public init(definitions: CityDefinitions) {
        all = definitions.ordinances
        enactedFlags = Array(repeating: false, count: all.count)
    }

    /// The ids of the ordinances in force, in order.
    public var enacted: [String] { all.indices.filter { enactedFlags[$0] }.map { all[$0].id } }

    public func isEnacted(_ id: String) -> Bool { index(of: id).map { enactedFlags[$0] } ?? false }

    /// What the ordinances in force add up to for one effect.
    public func effect(_ effect: OrdinanceEffect) -> Int { effects[effect] ?? 0 }

    /// A value scaled by a percentage effect: 100 with energy conservation's 15 is 115.
    public func scaled(_ effect: OrdinanceEffect, _ value: Int) -> Int {
        max(0, value * (100 + self.effect(effect)) / 100)
    }

    /// What the ordinances in force earn and cost in a year, for a city of this size.
    public func yearlyMoney(population: Int) -> (income: Int, cost: Int) {
        var (income, cost) = (0, 0)
        for index in all.indices where enactedFlags[index] {
            let money = all[index].yearlyMoney(population: population)
            if money > 0 { income += money } else { cost -= money }
        }
        return (income, cost)
    }

    /// Enacts or repeals an ordinance. Returns false when it was already so, or isn't one.
    @discardableResult
    public func set(_ id: String, enacted: Bool) -> Bool {
        guard let index = index(of: id), enactedFlags[index] != enacted else { return false }
        enactedFlags[index] = enacted
        sum()
        return true
    }

    /// What a city asks for, given how it's doing, as SimCity 3000's petitions do: power when it
    /// browns out, then crime, pollution, fires, money when the budget needs it, then commerce and
    /// industry when their demand is slack. Only ordinances not yet in force are asked for.
    func mostWanted(
        population: Int, brownedOut: Bool, crimeAverage: Int, pollutionAverage: Int, burning: Int,
        budgetNeedsAttention: Bool, commercialDemand: Int, industrialDemand: Int
    ) -> OrdinanceSpec? {
        guard population >= Self.petitionPopulation else { return nil }
        if brownedOut, let wanted = wanted("energyConservation") { return wanted }
        if crimeAverage > Self.highCrime, let wanted = wanted("neighbourhoodWatch") { return wanted }
        if pollutionAverage > Self.highPollution, let wanted = wanted("pollutionControls") { return wanted }
        if burning > 0, let wanted = wanted("smokeDetectors") { return wanted }
        if budgetNeedsAttention, let wanted = Self.revenue.lazy.compactMap(wanted).first { return wanted }
        if commercialDemand < Self.slackDemand, let wanted = wanted("tourismPromotion") { return wanted }
        return industrialDemand < Self.slackDemand ? wanted("industrialPromotion") : nil
    }

    private func wanted(_ id: String) -> OrdinanceSpec? {
        guard let index = index(of: id), !enactedFlags[index] else { return nil }
        return all[index]
    }

    private func index(of id: String) -> Int? { all.firstIndex { $0.id == id } }

    func sum() {
        effects.removeAll()
        for index in all.indices where enactedFlags[index] {
            for (effect, amount) in all[index].effects { effects[effect, default: 0] += amount }
        }
    }
}
