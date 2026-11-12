/// The hospitals and churches the city puts up on its own, as Micropolis does: one of each for
/// every 256 residents, built in place of an empty residential zone, and turned back into one when
/// the city has too many.
public final class CommunityBuildings {
    // The residents each hospital, and each church, serves.
    private static let residentsPerBuilding = 256
    // One the city has too many of turns back into an empty zone one time in this many scans.
    private static let tooManyChance = 21

    public internal(set) var hospitals = 0
    public internal(set) var churches = 0
    /// Above zero when the city wants another, below when it has one too many.
    public internal(set) var hospitalNeed = 0
    public internal(set) var churchNeed = 0

    public init() {}

    public func startCount() { (hospitals, churches) = (0, 0) }

    public func updateNeeds(residents: Int) {
        let wanted = residents / Self.residentsPerBuilding
        hospitalNeed = (wanted - hospitals).signum()
        churchNeed = (wanted - churches).signum()
    }

    /// Counts a hospital or church the scan reached, and may take one there are too many of down.
    public func visit(_ map: TileMap, _ random: inout SeededGenerator, _ x: Int, _ y: Int, hazards: Hazards? = nil) {
        let center = map[x, y]
        let tooMany: Bool
        switch center.use {
        case .hospital:
            hospitals += 1
            tooMany = hospitalNeed < 0
        case .church:
            churches += 1
            tooMany = churchNeed < 0
        default:
            preconditionFailure("(\(x), \(y)) is not a hospital or a church")
        }
        if tooMany && random.next(in: 0..<Self.tooManyChance) == 0 {
            _ = Self.replace(map, hazards, x, y, with: .residential, powered: center.powered)
        }
    }

    /// Builds a wanted hospital or church on an empty residential zone, and says whether it did.
    public func build(on map: TileMap, _ x: Int, _ y: Int, hazards: Hazards? = nil) -> Bool {
        let powered = map[x, y].powered
        if hospitalNeed > 0 && Self.replace(map, hazards, x, y, with: .hospital, powered: powered) {
            hospitalNeed = 0
            return true
        }
        if churchNeed > 0 && Self.replace(map, hazards, x, y, with: .church, powered: powered) {
            churchNeed = 0
            return true
        }
        return false
    }

    // The nine tiles become the new use, the centre keeping its power until the scan looks again;
    // a hole fire left is redrawn with the rest. Nothing changes while any of them burns or floods.
    private static func replace(_ map: TileMap, _ hazards: Hazards?, _ x: Int, _ y: Int, with use: TileUse, powered: Bool) -> Bool {
        if hazards?.anyActiveIn(centerX: x, centerY: y) == true { return false }
        for part in 0..<ZoneLayout.parts {
            let offset = Footprint.offsetFromCenter(part: part, size: ZoneLayout.size)
            map.setState(x + offset.dx, y + offset.dy, TileState(.grass, use, zonePart: part, powered: part == ZoneLayout.centerPart && powered))
        }
        return true
    }
}
