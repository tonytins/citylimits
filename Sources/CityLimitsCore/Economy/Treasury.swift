/// The city's money. Building spends it up front, and the budget settles the year's taxes and
/// upkeep into it, which can leave it below zero.
public final class Treasury {
    public private(set) var funds: Int

    public init(funds: Int) {
        precondition(funds >= 0, "a city starts with money, not debt")
        self.funds = funds
    }

    public func canAfford(_ cost: Int) -> Bool { cost <= funds }

    /// Spends the money if the city has it, and says whether it did.
    public func spend(_ amount: Int) -> Bool {
        precondition(amount >= 0, "spending is never negative")
        guard canAfford(amount) else { return false }
        funds -= amount
        return true
    }

    /// Adds a year's cash flow, which may be below zero.
    public func settle(_ cashFlow: Int) { funds += cashFlow }

    public func refund(_ amount: Int) {
        precondition(amount >= 0, "a refund is never negative")
        funds += amount
    }

    /// A saved city's money, which may be below zero.
    func restore(_ funds: Int) { self.funds = funds }
}
