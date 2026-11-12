/// A seeded random number generator with a fixed algorithm (SplitMix64), so the same seed gives
/// the same numbers on every machine.
///
/// The simulation owns one and passes it to whatever needs chance. Never use
/// `SystemRandomNumberGenerator` or the clock in the simulation: a city must replay the same way.
public struct SeededGenerator: RandomNumberGenerator, Sendable, Equatable {
    private static let goldenGamma: UInt64 = 0x9E37_79B9_7F4A_7C15

    /// The whole state, which a save keeps so a loaded city draws the same numbers.
    public var state: UInt64

    /// Starts from a seed. Seeds are mixed the same way the C# build mixed them.
    public init(seed: Int32) {
        state = UInt64(bitPattern: Int64(seed)) &+ Self.goldenGamma
    }

    /// Picks up from a saved state.
    public init(state: UInt64) {
        self.state = state
    }

    public mutating func next() -> UInt64 {
        state &+= Self.goldenGamma
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }

    /// A whole number from `range`, by plain modulo, as the C# build drew them.
    ///
    /// Swift's own `Int.random(in:using:)` draws differently, so use this where a number should
    /// match the C# build's sequence. The range must not be empty.
    public mutating func next(in range: Range<Int>) -> Int {
        precondition(!range.isEmpty, "next(in:) needs a range that isn't empty")
        let width = UInt64(range.upperBound - range.lowerBound)
        return range.lowerBound + Int(next() % width)
    }

    /// A number in 0..<1 with 53 bits of precision.
    public mutating func nextUnit() -> Double {
        Double(next() >> 11) * (1.0 / 9_007_199_254_740_992.0)
    }
}
