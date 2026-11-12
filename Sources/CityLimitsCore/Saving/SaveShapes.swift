/// The shape of a section of a save: its fields in order and the kind of each. One shape drives
/// both formats: the flat format writes a section as JSON, and the binary format writes the same
/// fields in this order, as the C# build's reflection over its save model did.
///
/// In binary, whole numbers are zigzag varints (seven bits a byte), a yes or no a byte, text its
/// UTF-8 length then its bytes (-1 for none), a list its count then its items, a nested section a
/// byte saying it's there (1) or missing (0) then its fields, and a number that may be missing the
/// same byte then the number.
indirect enum Shape: Sendable {
    case int32
    case int64
    case bool
    case double
    /// Text, which may be missing (null).
    case string
    /// Whole numbers; a missing list is written as an empty one.
    case ints
    case list(Shape)
    /// A section that may be missing.
    case object([(String, Shape)])
    /// A number or yes-or-no that may be missing.
    case optional(Shape)
}

/// The shapes of every section of a 2.0 save, as the C# build's save model declares them.
enum SaveShapes {
    static let city: [(String, Shape)] = [
        ("name", .string), ("seed", .int32), ("random", .string), ("tick", .int64), ("cityTime", .int64), ("funds", .int32),
    ]
    static let mod: [(String, Shape)] = [("id", .string), ("name", .string), ("version", .string)]
    static let mods: [(String, Shape)] = [("mods", .list(.object(mod)))]
    static let featureList: [(String, Shape)] = [("features", .list(.object([("name", .string), ("chunk", .string)])))]

    static let budgetYear: [(String, Shape)] = [
        ("taxRate", .int32), ("taxIncome", .int32), ("roadRequest", .int32), ("roadFunded", .int32),
        ("fireRequest", .int32), ("fireFunded", .int32), ("policeRequest", .int32), ("policeFunded", .int32), ("cut", .bool),
    ]
    static let budget: [(String, Shape)] = [
        ("taxRate", .int32), ("roadFunding", .int32), ("fireFunding", .int32), ("policeFunding", .int32),
        ("taxEffect", .int32), ("roadEffect", .int32), ("fireEffect", .int32), ("policeEffect", .int32),
        ("taxFund", .int64), ("roadFund", .int64), ("fireFund", .int64), ("policeFund", .int64), ("cut", .bool),
        ("lastYear", .object(budgetYear)), ("estimated", .object(budgetYear)),
    ]
    static let census: [(String, Shape)] = [
        ("residential", .int32), ("commercial", .int32), ("industrial", .int32),
        ("residentPopulation", .int32), ("commercialPopulation", .int32), ("industrialPopulation", .int32),
        ("residentialHistory", .ints), ("commercialHistory", .ints), ("industrialHistory", .ints),
    ]
    static let demand: [(String, Shape)] = [
        ("residential", .int32), ("commercial", .int32), ("industrial", .int32), ("taxablePopulation", .int32),
        ("residentialPull", .int32), ("commercialPull", .int32), ("industrialPull", .int32), ("taxPushApplied", .int32),
        ("jobsPerResident", .double), ("workersPerJob", .double), ("historyCount", .int32),
        ("residentialHistory", .ints), ("commercialHistory", .ints), ("industrialHistory", .ints),
    ]
    static let power: [(String, Shape)] = [
        ("plants", .ints), ("powered", .list(.string)), ("coalPlants", .int32), ("nuclearPlants", .int32), ("zones", .int32),
        ("poweredBuildings", .int32), ("unpoweredBuildings", .int32), ("capacity", .int32), ("poweredTiles", .int32), ("brownedOut", .bool),
    ]
    static let services: [(String, Shape)] = [
        ("fireStations", .int32), ("policeStations", .int32), ("fireRaw", .ints), ("policeRaw", .ints), ("fire", .ints), ("police", .ints),
    ]
    static let community: [(String, Shape)] = [("hospitals", .int32), ("churches", .int32), ("hospitalNeed", .int32), ("churchNeed", .int32)]
    static let cityMaps: [(String, Shape)] = [
        ("centerX", .int32), ("centerY", .int32), ("landValueAverage", .int32),
        ("landValue", .ints), ("density", .ints), ("terrain", .ints), ("commercialRate", .ints),
    ]
    static let cycle: [(String, Shape)] = [
        ("roadTiles", .int32), ("budgetedRoadTiles", .int32), ("budgetedFireStations", .int32), ("budgetedPoliceStations", .int32),
    ]
    static let traffic: [(String, Shape)] = [("density", .ints)]
    static let roadTraffic: [(String, Shape)] = [("levels", .list(.string)), ("average", .int32)]
    static let rail: [(String, Shape)] = [("track", .list(.string)), ("tiles", .int32), ("budgetedTiles", .int32)]
    static let blockMap: [(String, Shape)] = [("blocks", .ints), ("average", .int32)]
    static let tiles: [(String, Shape)] = [("tiles", .list(.string))]
    static let flood: [(String, Shape)] = [("tiles", .list(.string)), ("cyclesLeft", .int32)]
    static let facilities: [(String, Shape)] = [
        ("stadiums", .int32), ("seaports", .int32), ("airports", .int32),
        ("residentialCapped", .bool), ("commercialCapped", .bool), ("industrialCapped", .bool),
        ("buildings", .list(.object([("use", .string), ("x", .int32), ("y", .int32), ("powered", .bool), ("holes", .ints)]))),
    ]
    static let founding: [(String, Shape)] = [("year", .int32)]
    static let ordinances: [(String, Shape)] = [
        ("enacted", .list(.string)), ("incomeFund", .int64), ("costFund", .int64), ("lastYear", .ints), ("estimated", .ints),
    ]
    static let deal: [(String, Shape)] = [("side", .string), ("kind", .string), ("power", .int32), ("money", .int32), ("endStep", .int64)]
    static let offer: [(String, Shape)] = [
        ("side", .string), ("kind", .string), ("power", .int32), ("money", .int32), ("years", .int32), ("expiresStep", .int64),
    ]
    static let connections: [(String, Shape)] = [
        ("deals", .list(.object(deal))), ("offer", .object(offer)), ("grudgeUntil", .list(.int64)),
        ("incomeFund", .int64), ("costFund", .int64), ("lastYear", .ints), ("estimated", .ints),
    ]
    static let vehicles: [(String, Shape)] = [("plane", .ints), ("helicopter", .ints), ("ship", .ints)]
    static let menaces: [(String, Shape)] = [("tornado", .ints), ("monster", .ints)]
    static let goal: [(String, Shape)] = [
        ("measure", .string), ("atLeast", .optional(.int32)), ("atMost", .optional(.int32)), ("text", .string),
    ]
    static let challenge: [(String, Shape)] = [
        ("name", .string), ("description", .string), ("endStep", .int64), ("won", .optional(.bool)),
        ("goals", .list(.object(goal))),
        ("actions", .list(.object([("step", .int64), ("tool", .string), ("fromX", .int32), ("fromY", .int32), ("toX", .int32), ("toY", .int32)]))),
        ("events", .list(.object([("step", .int64), ("disaster", .string)]))),
    ]

    /// The core sections every save has, with their chunk tags, in the order both formats write them.
    static let core: [(name: String, chunk: String, shape: [(String, Shape)])] = [
        ("budget", "BUDG", budget), ("census", "CENS", census), ("demand", "DEMD", demand), ("power", "POWR", power),
        ("services", "SERV", services), ("community", "COMM", community), ("cityMaps", "CMAP", cityMaps), ("cycle", "CYCL", cycle),
    ]

    /// The features: sections a save may leave out, filled in as the version that added them would.
    /// `roadTraffic` is part of the traffic feature.
    static let features: [(name: String, feature: String, chunk: String, shape: [(String, Shape)])] = [
        ("traffic", "traffic", "TRAF", traffic), ("roadTraffic", "traffic", "RTRF", roadTraffic), ("rail", "rail", "RAIL", rail),
        ("pollution", "pollution", "POLL", blockMap), ("crime", "crime", "CRIM", blockMap), ("fire", "fire", "FIRE", tiles),
        ("flood", "flood", "FLOD", flood), ("facilities", "facilities", "FCLT", facilities), ("founding", "founding", "FNDG", founding),
        ("ordinances", "ordinances", "ORDN", ordinances), ("connections", "connections", "CONN", connections),
        ("radiation", "radiation", "RADN", tiles), ("vehicles", "vehicles", "VHCL", vehicles), ("menaces", "menaces", "MNCE", menaces),
        ("challenge", "challenge", "CHAL", challenge),
    ]
}

/// Writes and reads a section's fields as bytes, by its shape.
enum SectionCodec {
    static func encode(_ value: JSON, _ shape: [(String, Shape)]) -> [UInt8] {
        var bytes: [UInt8] = []
        writeFields(value, shape, into: &bytes)
        return bytes
    }

    static func decode(_ bytes: ArraySlice<UInt8>, _ shape: [(String, Shape)], chunk: String) throws(FormatError) -> JSON {
        var reader = Reader(bytes: bytes, chunk: chunk)
        let value = try reader.fields(shape)
        guard reader.atEnd else { throw FormatError("\(chunk): has bytes left over after its last field.") }
        return value
    }

    private static func field(_ value: JSON, _ name: String) -> JSON {
        guard case .object(let fields) = value else { return .null }
        return fields.first { $0.key == name }?.value ?? .null
    }

    private static func writeFields(_ value: JSON, _ shape: [(String, Shape)], into bytes: inout [UInt8]) {
        for (name, kind) in shape { write(field(value, name), kind, into: &bytes) }
    }

    private static func write(_ value: JSON, _ shape: Shape, into bytes: inout [UInt8]) {
        switch shape {
        case .int32, .int64:
            writeVarint(number(value), into: &bytes)
        case .bool:
            bytes.append(value == .bool(true) ? 1 : 0)
        case .double:
            var double = 0.0
            if case .number(let text) = value { double = Double(text) ?? 0 }
            writeVarint(Int64(bitPattern: double.bitPattern), into: &bytes)
        case .string:
            guard case .string(let text) = value else {
                writeVarint(-1, into: &bytes)
                return
            }
            let utf8 = Array(text.utf8)
            writeVarint(Int64(utf8.count), into: &bytes)
            bytes.append(contentsOf: utf8)
        case .ints:
            write(value, .list(.int32), into: &bytes)
        case .list(let item):
            let items: [JSON] = if case .array(let items) = value { items } else { [] }
            writeVarint(Int64(items.count), into: &bytes)
            for each in items { write(each, item, into: &bytes) }
        case .object(let fields):
            guard case .object = value else {
                bytes.append(0)
                return
            }
            bytes.append(1)
            writeFields(value, fields, into: &bytes)
        case .optional(let inner):
            guard value != .null else {
                bytes.append(0)
                return
            }
            bytes.append(1)
            write(value, inner, into: &bytes)
        }
    }

    private static func number(_ value: JSON) -> Int64 {
        if case .number(let text) = value, let number = Int64(text) { return number }
        return 0
    }

    private static func writeVarint(_ value: Int64, into bytes: inout [UInt8]) {
        var zigzag = UInt64(bitPattern: (value << 1) ^ (value >> 63))
        while zigzag >= 0x80 {
            bytes.append(UInt8(truncatingIfNeeded: zigzag) | 0x80)
            zigzag >>= 7
        }
        bytes.append(UInt8(zigzag))
    }

    private struct Reader {
        let bytes: ArraySlice<UInt8>
        let chunk: String
        var position: Int

        init(bytes: ArraySlice<UInt8>, chunk: String) {
            self.bytes = bytes
            self.chunk = chunk
            position = bytes.startIndex
        }

        var atEnd: Bool { position == bytes.endIndex }

        func damaged(_ what: String) -> FormatError { FormatError("\(chunk): is damaged: \(what).") }

        mutating func fields(_ shape: [(String, Shape)]) throws(FormatError) -> JSON {
            var fields: [(key: String, value: JSON)] = []
            for (name, kind) in shape { fields.append((name, try value(kind))) }
            return .object(fields)
        }

        mutating func value(_ shape: Shape) throws(FormatError) -> JSON {
            switch shape {
            case .int32:
                let number = try varint()
                guard number >= Int64(Int32.min), number <= Int64(Int32.max) else { throw damaged("a number out of range") }
                return .number(String(number))
            case .int64:
                return .number(String(try varint()))
            case .bool:
                switch try byte() {
                case 0: return .bool(false)
                case 1: return .bool(true)
                default: throw damaged("a yes or no that is neither")
                }
            case .double:
                let double = Double(bitPattern: UInt64(bitPattern: try varint()))
                guard double.isFinite else { throw damaged("a number that isn't finite") }
                return .number(String(double))
            case .string:
                let length = try varint()
                if length == -1 { return .null }
                guard length >= 0, length <= Int64(bytes.endIndex - position) else { throw damaged("text longer than the chunk") }
                let end = position + Int(length)
                defer { position = end }
                return .string(String(decoding: bytes[position..<end], as: UTF8.self))
            case .ints:
                return try value(.list(.int32))
            case .list(let item):
                // Every item takes a byte at least, so a count beyond what's left is damage, not a
                // reason to allocate.
                let count = try varint()
                guard count >= 0, count <= Int64(bytes.endIndex - position) else { throw damaged("a list longer than the chunk") }
                var items: [JSON] = []
                items.reserveCapacity(Int(count))
                for _ in 0..<Int(count) { items.append(try value(item)) }
                return .array(items)
            case .object(let fields):
                switch try byte() {
                case 0: return .null
                case 1: return try self.fields(fields)
                default: throw damaged("a section that is neither there nor missing")
                }
            case .optional(let inner):
                switch try byte() {
                case 0: return .null
                case 1: return try value(inner)
                default: throw damaged("a value that is neither there nor missing")
                }
            }
        }

        mutating func byte() throws(FormatError) -> UInt8 {
            guard position < bytes.endIndex else { throw damaged("it ends too soon") }
            defer { position += 1 }
            return bytes[position]
        }

        mutating func varint() throws(FormatError) -> Int64 {
            var zigzag: UInt64 = 0
            var shift: UInt64 = 0
            while shift < 64 {
                let next = try byte()
                zigzag |= UInt64(next & 0x7F) << shift
                if next & 0x80 == 0 {
                    return Int64(bitPattern: zigzag >> 1) ^ -Int64(bitPattern: zigzag & 1)
                }
                shift += 7
            }
            throw damaged("a number too long")
        }
    }
}

/// The CRC-32 a binary save's chunks carry, as PNG's.
enum CRC32 {
    private static let table: [UInt32] = (0..<256).map { index in
        var value = UInt32(index)
        for _ in 0..<8 { value = value & 1 != 0 ? 0xEDB8_8320 ^ (value >> 1) : value >> 1 }
        return value
    }

    static func compute<Bytes: Sequence>(_ bytes: Bytes, continuing crc: UInt32 = 0) -> UInt32 where Bytes.Element == UInt8 {
        var value = ~crc
        for byte in bytes { value = table[Int((value ^ UInt32(byte)) & 0xFF)] ^ (value >> 8) }
        return ~value
    }
}
