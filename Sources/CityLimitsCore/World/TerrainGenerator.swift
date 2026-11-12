/// Lays out a new map's ground from a seed, as Micropolis does: rivers and lakes, or one map in
/// ten an island, then woods. The same seed makes the same map.
public enum TerrainGenerator {
    private static let islandChancePercent = 10
    // The island's land starts this far in from the edge, and the sea eats up to this far into it.
    private static let islandInset = 5
    private static let islandReach = 18
    // The rivers start at least this far from the left and right edges, and the top and bottom.
    private static let riverMarginX = 40
    private static let riverMarginY = 33
    // A river goes back to its usual heading about one step in ten; otherwise a roll of 0 to 200
    // above 90 turns it one way, and another the other way.
    private static let riverStraightenChance = 10
    private static let riverVeerAbove = 90
    private static let lakeMargin = 10
    private static let lakeSpread = 6
    private static let mostLakes = 10
    private static let fewestWoods = 50
    private static let moreWoods = 100
    private static let shortestWood = 50
    private static let longerWood = 150

    // The eight headings, clockwise from north; a heading is taken modulo 8.
    private static let headings: [(dx: Int, dy: Int)] = [
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1),
    ]

    public static func generate(seed: Int32, width: Int = TileMap.defaultWidth, height: Int = TileMap.defaultHeight) -> TileMap {
        var land = Land(width: width, height: height)
        var random = SeededGenerator(seed: seed)

        if random.next(in: 0..<100) < islandChancePercent {
            makeIsland(&land, &random)
        } else {
            let startX = between(&random, riverMarginX, width - riverMarginX, width)
            let startY = between(&random, riverMarginY, height - riverMarginY - 1, height)
            layRivers(&land, &random, startX, startY)
            layLakes(&land, &random)
        }

        plantWoods(&land, &random)
        return TileMap(width: width, height: height, terrain: land.tiles)
    }

    // A place from low to high, or the middle of the side when the map is too small for the range.
    private static func between(_ random: inout SeededGenerator, _ low: Int, _ high: Int, _ size: Int) -> Int {
        high >= low ? random.next(in: low..<(high + 1)) : size / 2
    }

    // The smaller of two rolls, so a splash lands near the edge more often than far from it.
    private static func nearEdge(_ random: inout SeededGenerator) -> Int {
        let first = random.next(in: 0..<(islandReach + 1))
        let second = random.next(in: 0..<(islandReach + 1))
        return min(first, second)
    }

    private static func makeIsland(_ land: inout Land, _ random: inout SeededGenerator) {
        let (width, height) = (land.width, land.height)
        for index in land.tiles.indices { land.tiles[index] = .water }
        for y in islandInset..<max(islandInset, height - islandInset) {
            for x in islandInset..<max(islandInset, width - islandInset) {
                land[x, y] = .grass
            }
        }

        // The sea bites into the coast at random depths, so it isn't a straight line.
        for x in stride(from: 0, to: width - islandInset, by: 2) {
            land.splash(x, nearEdge(&random), wide: true)
            land.splash(x, height - 10 - nearEdge(&random), wide: true)
            land.splash(x, 0, wide: false)
            land.splash(x, height - 6, wide: false)
        }
        for y in stride(from: 0, to: height - islandInset, by: 2) {
            land.splash(nearEdge(&random), y, wide: true)
            land.splash(width - 10 - nearEdge(&random), y, wide: true)
            land.splash(0, y, wide: false)
            land.splash(width - 6, y, wide: false)
        }
    }

    // Two wide rivers run out from the start in opposite directions, then a narrow one wanders off.
    private static func layRivers(_ land: inout Land, _ random: inout SeededGenerator, _ startX: Int, _ startY: Int) {
        let first = random.next(in: 0..<4)
        _ = layRiver(&land, &random, startX, startY, heading: first, usual: first, wide: true)
        let opposite = (first + 4) % headings.count
        let heading = layRiver(&land, &random, startX, startY, heading: opposite, usual: opposite, wide: true)
        let usual = random.next(in: 0..<4)
        _ = layRiver(&land, &random, startX, startY, heading: heading, usual: usual, wide: false)
    }

    // Splashes water along a winding path until it runs off the map, and returns its last heading.
    private static func layRiver(
        _ land: inout Land, _ random: inout SeededGenerator, _ startX: Int, _ startY: Int,
        heading start: Int, usual: Int, wide: Bool
    ) -> Int {
        let reach = wide ? 4 : 3
        var (x, y, heading) = (startX, startY, start)
        // A wander that never left the map would never end, so a river is cut off after this many steps.
        var steps = land.width * land.height
        while land.contains(x + reach, y + reach) && steps > 0 {
            steps -= 1
            land.splash(x, y, wide: wide)
            if random.next(in: 0..<101) < riverStraightenChance {
                heading = usual
            } else {
                if random.next(in: 0..<201) > riverVeerAbove { heading += 1 }
                if random.next(in: 0..<201) > riverVeerAbove { heading -= 1 }
            }
            let step = headings[heading & 7]
            x += step.dx
            y += step.dy
        }
        return heading
    }

    private static func layLakes(_ land: inout Land, _ random: inout SeededGenerator) {
        let lakes = random.next(in: 0..<(mostLakes + 1))
        for _ in 0..<lakes {
            let x = between(&random, lakeMargin, land.width - lakeMargin - 1, land.width)
            let y = between(&random, lakeMargin, land.height - lakeMargin, land.height)
            let splashes = random.next(in: 2..<15)
            for _ in 0..<splashes {
                let splashX = x + random.next(in: -lakeSpread..<(lakeSpread + 1))
                let splashY = y + random.next(in: -lakeSpread..<(lakeSpread + 1))
                // Most of a lake is small splashes; one in five is wide.
                land.splash(splashX, splashY, wide: random.next(in: 0..<5) == 0)
            }
        }
    }

    // Each wood is a random walk that plants trees on the clear land it crosses.
    private static func plantWoods(_ land: inout Land, _ random: inout SeededGenerator) {
        let woods = random.next(in: fewestWoods..<(fewestWoods + moreWoods + 1))
        for _ in 0..<woods {
            var x = random.next(in: 0..<land.width)
            var y = random.next(in: 0..<land.height)
            let length = random.next(in: shortestWood..<(shortestWood + longerWood + 1))
            for _ in 0..<length {
                let step = headings[random.next(in: 0..<headings.count)]
                x += step.dx
                y += step.dy
                guard land.contains(x, y) else { break }
                if land[x, y] == .grass { land[x, y] = .trees }
            }
        }
        thinWoods(&land)
        thinWoods(&land)
    }

    // A tree the sheet can't draw as part of a wood is cleared. The pass works in place, so a
    // cleared tree can clear the next. The map's edge counts as no tree, so woods thin toward it.
    private static func thinWoods(_ land: inout Land) {
        for y in 0..<land.height {
            for x in 0..<land.width where land[x, y] == .trees {
                var sides: Sides = []
                for step in Sides.steps
                where land.contains(x + step.dx, y + step.dy) && land[x + step.dx, y + step.dy] == .trees {
                    sides.insert(step.side)
                }
                if !isWood(sides) { land[x, y] = .grass }
            }
        }
    }

    // The ground being laid out, before it becomes a map.
    private struct Land {
        let width: Int
        let height: Int
        var tiles: [TileKind]

        init(width: Int, height: Int) {
            precondition(width > 0 && height > 0, "a map needs a size")
            self.width = width
            self.height = height
            tiles = Array(repeating: .grass, count: width * height)
        }

        subscript(x: Int, y: Int) -> TileKind {
            get { tiles[y * width + x] }
            set { tiles[y * width + x] = newValue }
        }

        func contains(_ x: Int, _ y: Int) -> Bool { x >= 0 && y >= 0 && x < width && y < height }

        // A rounded splash of water, 9 tiles across when wide and 6 when narrow, its top-left
        // corner at the point. Distances are in half tiles, so an even square has a centre too.
        mutating func splash(_ left: Int, _ top: Int, wide: Bool) {
            let (size, reach) = wide ? (9, 10) : (6, 6)
            for dy in 0..<size {
                for dx in 0..<size {
                    let distance = abs(2 * dx - (size - 1)) + abs(2 * dy - (size - 1))
                    if distance <= reach && contains(left + dx, top + dy) {
                        self[left + dx, top + dy] = .water
                    }
                }
            }
        }
    }
}
