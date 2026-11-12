/// A problem reading a file the game doesn't write itself, saying what and where, such as
/// `zones.json: zones[0].price: needs a number from 0 to 1000000, not -5.`
public struct FormatError: Error, Equatable, CustomStringConvertible {
    public let message: String

    public init(_ message: String) {
        self.message = message
    }

    public var description: String { message }
}

/// A JSON value. Objects keep their keys in order, and numbers keep their text, so a whole number
/// is read exactly and one too big for an `Int` is refused rather than rounded.
public enum JSON: Equatable, Sendable {
    case null
    case bool(Bool)
    case number(String)
    case string(String)
    case array([JSON])
    case object([(key: String, value: JSON)])

    public static func == (lhs: JSON, rhs: JSON) -> Bool {
        switch (lhs, rhs) {
        case (.null, .null): true
        case let (.bool(a), .bool(b)): a == b
        case let (.number(a), .number(b)): a == b
        case let (.string(a), .string(b)): a == b
        case let (.array(a), .array(b)): a == b
        case let (.object(a), .object(b)): a.count == b.count && zip(a, b).allSatisfy { $0.key == $1.key && $0.value == $1.value }
        default: false
        }
    }

    public static func int(_ value: Int) -> JSON { .number(String(value)) }

    // MARK: Reading

    /// Parses JSON text. Keys given twice, and anything after the value, are refused.
    public static func parse(_ text: String) throws(FormatError) -> JSON {
        var parser = Parser(Array(text.utf8))
        parser.skipSpace()
        let value = try parser.value(depth: 0)
        parser.skipSpace()
        guard parser.atEnd else { throw parser.error("has more after the end of the value") }
        return value
    }

    private struct Parser {
        // Nesting deeper than this is refused, so a hostile file can't exhaust the stack.
        static let deepest = 64

        let bytes: [UInt8]
        var index = 0

        init(_ bytes: [UInt8]) { self.bytes = bytes }

        var atEnd: Bool { index >= bytes.count }

        func error(_ what: String) -> FormatError {
            var line = 1
            var column = 1
            for byte in bytes[..<min(index, bytes.count)] {
                if byte == UInt8(ascii: "\n") {
                    line += 1
                    column = 1
                } else {
                    column += 1
                }
            }
            return FormatError("the file is not valid JSON at line \(line), column \(column): it \(what).")
        }

        mutating func skipSpace() {
            while index < bytes.count, [0x20, 0x09, 0x0A, 0x0D].contains(bytes[index]) { index += 1 }
        }

        mutating func expect(_ literal: String) throws(FormatError) {
            for byte in literal.utf8 {
                guard index < bytes.count, bytes[index] == byte else { throw error("has something that isn't a value") }
                index += 1
            }
        }

        mutating func value(depth: Int) throws(FormatError) -> JSON {
            guard depth < Self.deepest else { throw error("nests more than \(Self.deepest) deep") }
            guard index < bytes.count else { throw error("ends too soon") }
            switch bytes[index] {
            case UInt8(ascii: "{"): return try object(depth: depth)
            case UInt8(ascii: "["): return try array(depth: depth)
            case UInt8(ascii: "\""): return .string(try string())
            case UInt8(ascii: "t"):
                try expect("true")
                return .bool(true)
            case UInt8(ascii: "f"):
                try expect("false")
                return .bool(false)
            case UInt8(ascii: "n"):
                try expect("null")
                return .null
            default: return try number()
            }
        }

        mutating func object(depth: Int) throws(FormatError) -> JSON {
            index += 1
            var fields: [(key: String, value: JSON)] = []
            var keys = Set<String>()
            skipSpace()
            if index < bytes.count, bytes[index] == UInt8(ascii: "}") {
                index += 1
                return .object(fields)
            }
            while true {
                skipSpace()
                guard index < bytes.count, bytes[index] == UInt8(ascii: "\"") else { throw error("needs a field name in quotes") }
                let key = try string()
                guard keys.insert(key).inserted else { throw FormatError("\(key): is given twice.") }
                skipSpace()
                try expect(":")
                skipSpace()
                fields.append((key, try value(depth: depth + 1)))
                skipSpace()
                guard index < bytes.count else { throw error("ends inside an object") }
                if bytes[index] == UInt8(ascii: ",") {
                    index += 1
                } else if bytes[index] == UInt8(ascii: "}") {
                    index += 1
                    return .object(fields)
                } else {
                    throw error("needs a comma or a closing brace")
                }
            }
        }

        mutating func array(depth: Int) throws(FormatError) -> JSON {
            index += 1
            var items: [JSON] = []
            skipSpace()
            if index < bytes.count, bytes[index] == UInt8(ascii: "]") {
                index += 1
                return .array(items)
            }
            while true {
                skipSpace()
                items.append(try value(depth: depth + 1))
                skipSpace()
                guard index < bytes.count else { throw error("ends inside a list") }
                if bytes[index] == UInt8(ascii: ",") {
                    index += 1
                } else if bytes[index] == UInt8(ascii: "]") {
                    index += 1
                    return .array(items)
                } else {
                    throw error("needs a comma or a closing bracket")
                }
            }
        }

        mutating func string() throws(FormatError) -> String {
            index += 1
            var scalars = String.UnicodeScalarView()
            var run: [UInt8] = []
            func flush() {
                if !run.isEmpty {
                    scalars.append(contentsOf: String(decoding: run, as: UTF8.self).unicodeScalars)
                    run.removeAll()
                }
            }
            while index < bytes.count {
                let byte = bytes[index]
                index += 1
                switch byte {
                case UInt8(ascii: "\""):
                    flush()
                    return String(scalars)
                case UInt8(ascii: "\\"):
                    flush()
                    guard index < bytes.count else { throw error("ends inside a string") }
                    let escape = bytes[index]
                    index += 1
                    switch escape {
                    case UInt8(ascii: "\""): scalars.append("\"")
                    case UInt8(ascii: "\\"): scalars.append("\\")
                    case UInt8(ascii: "/"): scalars.append("/")
                    case UInt8(ascii: "b"): scalars.append("\u{08}")
                    case UInt8(ascii: "f"): scalars.append("\u{0C}")
                    case UInt8(ascii: "n"): scalars.append("\n")
                    case UInt8(ascii: "r"): scalars.append("\r")
                    case UInt8(ascii: "t"): scalars.append("\t")
                    case UInt8(ascii: "u"):
                        var code = try hex4()
                        if (0xD800..<0xDC00).contains(code) {
                            // A surrogate pair: the second half must follow.
                            try expect("\\u")
                            let low = try hex4()
                            guard (0xDC00..<0xE000).contains(low) else { throw error("has half a surrogate pair") }
                            code = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00)
                        }
                        guard let scalar = Unicode.Scalar(code) else { throw error("has an escape that isn't a character") }
                        scalars.append(scalar)
                    default:
                        throw error("has an unknown escape")
                    }
                case 0..<0x20:
                    throw error("has a control character inside a string")
                default:
                    run.append(byte)
                }
            }
            throw error("ends inside a string")
        }

        mutating func hex4() throws(FormatError) -> UInt32 {
            var code: UInt32 = 0
            for _ in 0..<4 {
                guard index < bytes.count, let digit = Self.hexDigit(bytes[index]) else { throw error("has a bad \\u escape") }
                code = code * 16 + digit
                index += 1
            }
            return code
        }

        static func hexDigit(_ byte: UInt8) -> UInt32? {
            switch byte {
            case UInt8(ascii: "0")...UInt8(ascii: "9"): UInt32(byte - UInt8(ascii: "0"))
            case UInt8(ascii: "a")...UInt8(ascii: "f"): UInt32(byte - UInt8(ascii: "a") + 10)
            case UInt8(ascii: "A")...UInt8(ascii: "F"): UInt32(byte - UInt8(ascii: "A") + 10)
            default: nil
            }
        }

        mutating func number() throws(FormatError) -> JSON {
            let start = index
            func digits() -> Int {
                let from = index
                while index < bytes.count, (UInt8(ascii: "0")...UInt8(ascii: "9")).contains(bytes[index]) { index += 1 }
                return index - from
            }
            if index < bytes.count, bytes[index] == UInt8(ascii: "-") { index += 1 }
            guard digits() > 0 else { throw error("has something that isn't a value") }
            if index < bytes.count, bytes[index] == UInt8(ascii: ".") {
                index += 1
                guard digits() > 0 else { throw error("has a number with nothing after its point") }
            }
            if index < bytes.count, bytes[index] == UInt8(ascii: "e") || bytes[index] == UInt8(ascii: "E") {
                index += 1
                if index < bytes.count, bytes[index] == UInt8(ascii: "+") || bytes[index] == UInt8(ascii: "-") { index += 1 }
                guard digits() > 0 else { throw error("has a number with an empty exponent") }
            }
            return .number(String(decoding: bytes[start..<index], as: UTF8.self))
        }
    }

    // MARK: Writing

    /// The value as JSON text, indented two spaces, with lists of plain numbers or short strings
    /// kept on one line so a map's rows and a history read as rows.
    public func text() -> String {
        var out = ""
        write(into: &out, indent: 0)
        out.append("\n")
        return out
    }

    private func write(into out: inout String, indent: Int) {
        switch self {
        case .null: out.append("null")
        case .bool(let value): out.append(value ? "true" : "false")
        case .number(let value): out.append(value)
        case .string(let value): Self.quote(value, into: &out)
        case .array(let items):
            if items.isEmpty {
                out.append("[]")
            } else if items.allSatisfy({ if case .number = $0 { true } else { false } }) {
                out.append("[ ")
                for (index, item) in items.enumerated() {
                    if index > 0 { out.append(", ") }
                    item.write(into: &out, indent: indent)
                }
                out.append(" ]")
            } else {
                out.append("[\n")
                for (index, item) in items.enumerated() {
                    out.append(String(repeating: " ", count: indent + 2))
                    item.write(into: &out, indent: indent + 2)
                    out.append(index + 1 < items.count ? ",\n" : "\n")
                }
                out.append(String(repeating: " ", count: indent) + "]")
            }
        case .object(let fields):
            if fields.isEmpty {
                out.append("{}")
                return
            }
            out.append("{\n")
            for (index, field) in fields.enumerated() {
                out.append(String(repeating: " ", count: indent + 2))
                Self.quote(field.key, into: &out)
                out.append(": ")
                field.value.write(into: &out, indent: indent + 2)
                out.append(index + 1 < fields.count ? ",\n" : "\n")
            }
            out.append(String(repeating: " ", count: indent) + "}")
        }
    }

    private static func quote(_ text: String, into out: inout String) {
        out.append("\"")
        for scalar in text.unicodeScalars {
            switch scalar {
            case "\"": out.append("\\\"")
            case "\\": out.append("\\\\")
            case "\n": out.append("\\n")
            case "\r": out.append("\\r")
            case "\t": out.append("\\t")
            case let control where control.value < 0x20:
                let hex = String(control.value, radix: 16)
                out.append("\\u" + String(repeating: "0", count: 4 - hex.count) + hex)
            default: out.unicodeScalars.append(scalar)
            }
        }
        out.append("\"")
    }
}

/// Reads the fields of a JSON object strictly: each field is asked for by name and type, a
/// problem names where it is, and `finish` refuses any field nobody asked for.
public struct JSONFields {
    public let path: String
    private let fields: [(key: String, value: JSON)]
    private var used = Set<String>()

    /// Reads a value that must be an object. `path` names it in messages, such as `zones[0]`.
    public init(_ value: JSON, at path: String) throws(FormatError) {
        guard case .object(let fields) = value else { throw FormatError("\(path.isEmpty ? "the file" : path): needs an object.") }
        self.path = path
        self.fields = fields
    }

    public func at(_ key: String) -> String { path.isEmpty ? key : "\(path).\(key)" }

    public var keys: [String] { fields.map(\.key) }

    /// The raw value of a field, or nil when it's missing or null.
    public mutating func value(_ key: String) -> JSON? {
        used.insert(key)
        guard let value = fields.first(where: { $0.key == key })?.value, value != .null else { return nil }
        return value
    }

    public mutating func has(_ key: String) -> Bool {
        fields.contains { $0.key == key }
    }

    public mutating func required(_ key: String) throws(FormatError) -> JSON {
        guard let value = value(key) else { throw FormatError("\(at(key)): is required.") }
        return value
    }

    public mutating func int(_ key: String) throws(FormatError) -> Int? {
        guard let value = value(key) else { return nil }
        return try Self.int(value, at: at(key))
    }

    public mutating func int(_ key: String, in range: ClosedRange<Int>) throws(FormatError) -> Int? {
        guard let number = try int(key) else { return nil }
        return try Self.check(number, in: range, at: at(key))
    }

    public mutating func requiredInt(_ key: String, in range: ClosedRange<Int> = Int.min...Int.max) throws(FormatError) -> Int {
        try Self.check(Self.int(required(key), at: at(key)), in: range, at: at(key))
    }

    public mutating func double(_ key: String) throws(FormatError) -> Double? {
        guard let value = value(key) else { return nil }
        guard case .number(let text) = value, let number = Double(text), number.isFinite else {
            throw FormatError("\(at(key)): needs a number.")
        }
        return number
    }

    public mutating func bool(_ key: String) throws(FormatError) -> Bool? {
        guard let value = value(key) else { return nil }
        guard case .bool(let flag) = value else { throw FormatError("\(at(key)): needs true or false.") }
        return flag
    }

    public mutating func string(_ key: String) throws(FormatError) -> String? {
        guard let value = value(key) else { return nil }
        guard case .string(let text) = value else { throw FormatError("\(at(key)): needs text.") }
        return text
    }

    public mutating func requiredString(_ key: String) throws(FormatError) -> String {
        guard let text = try string(key) else { throw FormatError("\(at(key)): is required.") }
        return text
    }

    public mutating func array(_ key: String) throws(FormatError) -> [JSON]? {
        guard let value = value(key) else { return nil }
        guard case .array(let items) = value else { throw FormatError("\(at(key)): needs a list.") }
        return items
    }

    public mutating func requiredArray(_ key: String) throws(FormatError) -> [JSON] {
        guard let items = try array(key) else { throw FormatError("\(at(key)): is required.") }
        return items
    }

    public mutating func ints(_ key: String, in range: ClosedRange<Int> = Int.min...Int.max) throws(FormatError) -> [Int]? {
        guard let items = try array(key) else { return nil }
        var numbers: [Int] = []
        numbers.reserveCapacity(items.count)
        for (index, item) in items.enumerated() {
            numbers.append(try Self.check(Self.int(item, at: "\(at(key))[\(index)]"), in: range, at: "\(at(key))[\(index)]"))
        }
        return numbers
    }

    public mutating func strings(_ key: String) throws(FormatError) -> [String]? {
        guard let items = try array(key) else { return nil }
        var texts: [String] = []
        for (index, item) in items.enumerated() {
            guard case .string(let text) = item else { throw FormatError("\(at(key))[\(index)]: needs text.") }
            texts.append(text)
        }
        return texts
    }

    public mutating func object(_ key: String) throws(FormatError) -> JSONFields? {
        guard let value = value(key) else { return nil }
        return try JSONFields(value, at: at(key))
    }

    /// Refuses any field that wasn't asked for, and `$schema`, which every format allows.
    public func finish(noun: String = "this file has") throws(FormatError) {
        if let unknown = fields.first(where: { !used.contains($0.key) && $0.key != "$schema" }) {
            throw FormatError("\(at(unknown.key)): is not a field \(noun).")
        }
    }

    public static func int(_ value: JSON, at path: String) throws(FormatError) -> Int {
        guard case .number(let text) = value, let number = Int(text) else {
            throw FormatError("\(path): needs a whole number.")
        }
        return number
    }

    @discardableResult
    public static func check(_ value: Int, in range: ClosedRange<Int>, at path: String) throws(FormatError) -> Int {
        guard range.contains(value) else {
            throw FormatError("\(path): needs a number from \(range.lowerBound) to \(range.upperBound), not \(value).")
        }
        return value
    }
}

/// A format's version, major and minor, such as 2.0. A whole number, as files before minor
/// versions wrote, reads as that major version's .0.
public struct FormatVersion: Hashable, Comparable, Sendable, CustomStringConvertible {
    public let major: Int
    public let minor: Int

    public init(_ major: Int, _ minor: Int) {
        self.major = major
        self.minor = minor
    }

    public init?(_ value: JSON) {
        switch value {
        case .number(let text):
            guard let major = Int(text), major >= 0 else { return nil }
            self.init(major, 0)
        case .string(let text):
            let parts = text.split(separator: ".", omittingEmptySubsequences: false)
            guard (1...2).contains(parts.count), parts.allSatisfy({ !$0.isEmpty && $0.allSatisfy(\.isASCII) && $0.allSatisfy(\.isNumber) }),
                let major = Int(parts[0]), let minor = parts.count == 2 ? Int(parts[1]) : 0
            else { return nil }
            self.init(major, minor)
        default:
            return nil
        }
    }

    public static func < (lhs: FormatVersion, rhs: FormatVersion) -> Bool { (lhs.major, lhs.minor) < (rhs.major, rhs.minor) }

    public var description: String { "\(major).\(minor)" }

    /// Reads a file's `format`: it must be the reader's major version, at its minor or before.
    public static func read(_ fields: inout JSONFields, reader: FormatVersion, at: String = "format") throws(FormatError) -> FormatVersion {
        let path = fields.at(at)
        let value = try fields.required(at)
        guard let version = FormatVersion(value) else { throw FormatError("\(path): needs a version such as \"\(reader)\".") }
        guard version.major == reader.major, version.minor <= reader.minor else {
            let reads = version > reader ? "up to \(reader)" : "\(reader.major).x"
            throw FormatError("\(path): this is format \(version); this version of CityLimits reads \(reads).")
        }
        return version
    }
}
