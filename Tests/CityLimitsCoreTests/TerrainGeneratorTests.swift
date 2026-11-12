import Testing

@testable import CityLimitsCore

struct TerrainGeneratorTests {
    // Fingerprints of the C# build's maps at 120 by 100 (legacy/csharp): an FNV-1a hash of the
    // tile kinds row by row, and how many of each kind. Seed 9 is an island.
    @Test(arguments: [
        (Int32(1), UInt64(0x2189_F2E4_9A1C_7789), [5463, 3576, 2961, 0]),
        (2000, 0x001A_62B6_7EBD_7C76, [7040, 3267, 1693, 0]),
        (9, 0x69D3_EFEF_3C52_F403, [3958, 6756, 1286, 0]),
        (4242, 0x6F19_1326_015C_FAAE, [5789, 2897, 3314, 0]),
    ])
    func matchesTheCSharpBuild(seed: Int32, hash: UInt64, counts: [Int]) {
        let map = TerrainGenerator.generate(seed: seed)
        var fnv: UInt64 = 1_469_598_103_934_665_603
        var kinds = [0, 0, 0, 0]
        for y in 0..<map.height {
            for x in 0..<map.width {
                let kind = map[x, y].kind.rawValue
                kinds[kind] += 1
                fnv = (fnv ^ UInt64(kind)) &* 1_099_511_628_211
            }
        }
        #expect(kinds == counts)
        #expect(fnv == hash)
    }

    @Test func sameSeedGivesSameMap() {
        let first = TerrainGenerator.generate(seed: 1234, width: 32, height: 24)
        let second = TerrainGenerator.generate(seed: 1234, width: 32, height: 24)
        for y in 0..<24 {
            for x in 0..<32 {
                #expect(first[x, y] == second[x, y])
            }
        }
    }

    @Test(arguments: [(16, 16), (16, 512), (512, 16), (79, 66), (512, 512)])
    func keepsToEverySizeAScenarioCanAskFor(size: (Int, Int)) {
        let map = TerrainGenerator.generate(seed: 2000, width: size.0, height: size.1)
        #expect(map.width == size.0 && map.height == size.1)
    }

    @Test func almostEveryTreeStandsInAWood() {
        // Thinning works in place, twice, so a tree cleared late can leave one already passed on
        // its own: a few in a thousand.
        var (trees, lone) = (0, 0)
        for seed in Int32(1)...20 {
            let map = TerrainGenerator.generate(seed: seed)
            for y in 0..<map.height {
                for x in 0..<map.width where map[x, y].kind == .trees {
                    var sides: Sides = []
                    for step in Sides.steps
                    where map.contains(x + step.dx, y + step.dy) && map[x + step.dx, y + step.dy].kind == .trees {
                        sides.insert(step.side)
                    }
                    trees += 1
                    if !isWood(sides) { lone += 1 }
                }
            }
        }
        #expect(trees > 0)
        #expect(lone <= trees / 200)
    }
}
