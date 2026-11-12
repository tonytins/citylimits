import Testing

@testable import CityLimitsCore

struct SeededGeneratorTests {
    @Test func sameSeedGivesSameNumbers() {
        var a = SeededGenerator(seed: 1234)
        var b = SeededGenerator(seed: 1234)
        for _ in 0..<100 {
            #expect(a.next() == b.next())
        }
    }

    @Test func differentSeedsDiffer() {
        var a = SeededGenerator(seed: 1)
        var b = SeededGenerator(seed: 2)
        #expect(a.next() != b.next())
    }

    @Test func savedStateResumesTheSequence() {
        var original = SeededGenerator(seed: 42)
        _ = original.next()
        var restored = SeededGenerator(state: original.state)
        #expect(original.next() == restored.next())
    }

    // Taken from the C# build's DeterministicRandom (legacy/csharp): NextUInt64, NextInt(-5, 5),
    // then NextDouble, so the two builds draw the same numbers from the same seed.
    @Test(arguments: [
        (Int32(0), UInt64(0x6E78_9E6A_A1B9_65F4), 4, 0.9708819781538285),
        (1234, 0x97C7_A136_4DF0_6524, 1, 0.3061867920503709),
        (-1, 0xE99F_F867_DBF6_82C9, -4, 0.4262344494451664),
    ])
    func matchesTheCSharpBuild(seed: Int32, first: UInt64, ranged: Int, unit: Double) {
        var generator = SeededGenerator(seed: seed)
        #expect(generator.next() == first)
        #expect(generator.next(in: -5..<5) == ranged)
        #expect(generator.nextUnit() == unit)
    }

    @Test func negativeSeedsUseTheirBitPattern() {
        let generator = SeededGenerator(seed: -1)
        #expect(generator.state == UInt64.max &+ 0x9E37_79B9_7F4A_7C15)
    }

    @Test(arguments: [0..<1, 0..<6, -5..<5, 100..<101])
    func rangeStaysInside(_ range: Range<Int>) {
        var generator = SeededGenerator(seed: 7)
        for _ in 0..<500 {
            #expect(range.contains(generator.next(in: range)))
        }
    }

    @Test func unitIsBelowOne() {
        var generator = SeededGenerator(seed: 9)
        for _ in 0..<500 {
            let value = generator.nextUnit()
            #expect(value >= 0 && value < 1)
        }
    }
}
