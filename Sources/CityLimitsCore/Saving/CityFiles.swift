import Foundation

/// A saved city as the Load Game list shows it, read without loading the city.
public struct SavedCitySummary: Hashable, Sendable {
    public var file: URL
    public var name: String
    public var date: GameDate
    public var population: Int
    public var funds: Int
    public var saved: Date
}

/// The city formats, by extension: `.fcl`, the flat city format, one JSON object a person or an
/// agent can read and compare; and `.cl`, the binary city format, the game's own save, after
/// Micropolis' city files. Both are format 2.0 and hold exactly the same city.
public enum CityFiles {
    public static let flatExtension = "fcl"
    public static let binaryExtension = "cl"
    public static let maxListed = 200

    private static let magic = Array("CLCITY".utf8)
    private static let chunkOverhead = 12

    // MARK: Writing

    /// The city as a flat save.
    public static func flatText(_ city: CitySimulation) -> String { city.capture().text() }

    /// The city as a binary save.
    public static func binaryBytes(_ city: CitySimulation) -> [UInt8] {
        let save = city.capture()
        func section(_ name: String) -> JSON {
            guard case .object(let fields) = save else { return .null }
            return fields.first { $0.key == name }?.value ?? .null
        }
        var bytes = magic
        MapRows.appendUInt16(CitySaveFormat.version.major, to: &bytes)
        var minor: [UInt8] = []
        MapRows.appendUInt16(CitySaveFormat.version.minor, to: &minor)
        appendChunk("VERS", minor, to: &bytes)
        let features = CitySaveFormat.featureNames.flatMap { name in
            SaveShapes.features.filter { $0.feature == name }.map { JSON.object([("name", .string(name)), ("chunk", .string($0.chunk))]) }
        }
        appendChunk("FEAT", SectionCodec.encode(.object([("features", .array(features))]), SaveShapes.featureList), to: &bytes)
        appendChunk("CITY", SectionCodec.encode(save, SaveShapes.city), to: &bytes)
        appendChunk("MODS", SectionCodec.encode(save, SaveShapes.mods), to: &bytes)
        appendChunk("MAP ", MapRows.encodeWords(city.map, city.hazards), to: &bytes)
        for part in SaveShapes.core {
            appendChunk(part.chunk, SectionCodec.encode(section(part.name), part.shape), to: &bytes)
        }
        for part in SaveShapes.features where section(part.name) != .null {
            appendChunk(part.chunk, SectionCodec.encode(section(part.name), part.shape), to: &bytes)
        }
        return bytes
    }

    private static func appendChunk(_ tag: String, _ payload: [UInt8], to bytes: inout [UInt8]) {
        let tagBytes = Array(tag.utf8)
        bytes += tagBytes
        appendUInt32(UInt32(payload.count), to: &bytes)
        bytes += payload
        appendUInt32(CRC32.compute(payload, continuing: CRC32.compute(tagBytes)), to: &bytes)
    }

    private static func appendUInt32(_ value: UInt32, to bytes: inout [UInt8]) {
        for shift in stride(from: 24, through: 0, by: -8) { bytes.append(UInt8(truncatingIfNeeded: value >> UInt32(shift))) }
    }

    private static func readUInt32(_ bytes: [UInt8], _ at: Int) -> UInt32 {
        bytes[at..<(at + 4)].reduce(0) { $0 << 8 | UInt32($1) }
    }

    // MARK: Reading

    /// Reads a flat save.
    public static func read(flat text: String, definitions: CityDefinitions = .base) throws(FormatError) -> LoadedCity {
        guard !text.trimmingWhitespace.isEmpty else { throw FormatError("The file is empty.") }
        guard text.utf8.count <= CitySaveFormat.maxBytes else { throw tooBig }
        let value: JSON
        do throws(FormatError) {
            value = try JSON.parse(text)
        } catch {
            throw FormatError("The file isn't a city CityLimits can read: \(error.message)")
        }
        return try CitySimulation.restore(value, definitions: definitions)
    }

    private static var tooBig: FormatError {
        FormatError("The file is bigger than \(CitySaveFormat.maxBytes / (1024 * 1024)) MB, so it isn't a city CityLimits saved.")
    }

    /// Reads a binary save into the same sections the flat format holds, refusing damage, then
    /// loads it as a flat save is loaded.
    public static func read(binary bytes: [UInt8], definitions: CityDefinitions = .base) throws(FormatError) -> LoadedCity {
        var (save, notes) = try sections(binary: bytes)
        let loaded = try CitySimulation.restore(save, definitions: definitions)
        notes += loaded.notes
        save = .null
        return LoadedCity(city: loaded.city, notes: notes)
    }

    // The binary file's chunks as a save model, with a note for each chunk it left out.
    private static func sections(binary bytes: [UInt8]) throws(FormatError) -> (JSON, [String]) {
        guard bytes.count <= CitySaveFormat.maxBytes else { throw tooBig }
        guard bytes.count >= magic.count + 2, Array(bytes[0..<magic.count]) == magic else {
            throw FormatError("The file isn't a binary CityLimits city: it doesn't start with CLCITY.")
        }
        let major = MapRows.readUInt16(bytes[...], magic.count)
        guard major == CitySaveFormat.version.major else {
            throw FormatError("format: this is format \(major).x; this version of CityLimits reads \(CitySaveFormat.version.major).x. A city from 0.4 or before can be opened in 0.5 and saved again there.")
        }

        var chunks: [String: ArraySlice<UInt8>] = [:]
        var unknown: [String] = []
        var position = magic.count + 2
        while position < bytes.count {
            guard bytes.count - position >= chunkOverhead else {
                throw FormatError("The file ends partway through a chunk, at byte \(position).")
            }
            let tagBytes = bytes[position..<(position + 4)]
            let tag = String(decoding: tagBytes, as: UTF8.self)
            let name = tag.trimmingWhitespace
            let length = Int(readUInt32(bytes, position + 4))
            guard length <= bytes.count - position - chunkOverhead else {
                throw FormatError("\(name): says it is \(length) bytes, more than the file has left.")
            }
            let payload = bytes[(position + 8)..<(position + 8 + length)]
            guard CRC32.compute(payload, continuing: CRC32.compute(tagBytes)) == readUInt32(bytes, position + 8 + length) else {
                throw FormatError("\(name): is damaged: its checksum doesn't match.")
            }
            position += chunkOverhead + length
            let known = ["VERS", "FEAT", "CITY", "MODS", "MAP "] + SaveShapes.core.map(\.chunk) + SaveShapes.features.map(\.chunk)
            guard known.contains(tag) else {
                guard !unknown.contains(tag) else { throw FormatError("\(name): the file holds it twice.") }
                unknown.append(tag)
                continue
            }
            guard chunks.updateValue(payload, forKey: tag) == nil else { throw FormatError("\(name): the file holds it twice.") }
        }

        if let version = chunks["VERS"] {
            guard version.count == 2 else { throw FormatError("VERS: needs the minor version as one 16-bit number.") }
        }
        for tag in ["CITY", "MODS", "MAP "] + SaveShapes.core.map(\.chunk) where chunks[tag] == nil {
            throw FormatError("\(tag.trimmingWhitespace): the file is missing it.")
        }

        // An unknown chunk is a newer version's feature, named in the features list if it's there.
        var notes: [String] = []
        if !unknown.isEmpty {
            var names: [String: String] = [:]
            if let list = chunks["FEAT"], case .object(let fields) = try SectionCodec.decode(list, SaveShapes.featureList, chunk: "FEAT"),
                case .array(let entries)? = fields.first?.value
            {
                for case .object(let entry) in entries {
                    if case .string(let feature)? = entry.first(where: { $0.key == "name" })?.value,
                        case .string(let chunk)? = entry.first(where: { $0.key == "chunk" })?.value
                    {
                        names[chunk] = feature
                    }
                }
            }
            notes.append("A newer CityLimits saved this city with parts this version doesn't use.")
            notes += unknown.map { "Its \(names[$0] ?? $0.trimmingWhitespace) part is left out, and is left out when you save it." }
        }

        guard case .object(let head) = try SectionCodec.decode(chunks["CITY"]!, SaveShapes.city, chunk: "CITY"),
            case .object(let mods) = try SectionCodec.decode(chunks["MODS"]!, SaveShapes.mods, chunk: "MODS")
        else { throw FormatError("CITY: is damaged.") }
        var fields: [(key: String, value: JSON)] = [("format", .string("\(major).0"))]
        fields += head
        fields += mods
        fields.append(("map", try MapRows.decodeWords(chunks["MAP "]!)))
        for part in SaveShapes.core {
            fields.append((part.name, try SectionCodec.decode(chunks[part.chunk]!, part.shape, chunk: part.chunk)))
        }
        for part in SaveShapes.features {
            if let payload = chunks[part.chunk] {
                fields.append((part.name, try SectionCodec.decode(payload, part.shape, chunk: part.chunk)))
            }
        }
        return (.object(fields), notes)
    }

    // MARK: Files

    /// Reads a city from a file, by its extension.
    public static func read(_ file: URL, definitions: CityDefinitions = .base) throws(FormatError) -> LoadedCity {
        let bytes = try readBytes(file)
        switch file.pathExtension.lowercased() {
        case binaryExtension: return try read(binary: bytes, definitions: definitions)
        case flatExtension: return try read(flat: String(decoding: bytes, as: UTF8.self), definitions: definitions)
        case MicropolisImport.fileExtension: return try MicropolisImport.read(bytes, name: file.deletingPathExtension().lastPathComponent)
        default: throw FormatError("\(file.lastPathComponent) isn't a city file.")
        }
    }

    /// Writes a city to a file in the format its extension names. It's written to a temporary
    /// file first, which then takes the file's place, so a failure leaves the save before it.
    public static func write(_ city: CitySimulation, to file: URL) throws(FormatError) {
        let bytes: [UInt8]
        switch file.pathExtension.lowercased() {
        case binaryExtension: bytes = binaryBytes(city)
        case flatExtension: bytes = Array(flatText(city).utf8)
        default: throw FormatError("\(file.lastPathComponent) isn't a city file.")
        }
        let temporary = file.appendingPathExtension("tmp")
        do {
            try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try Data(bytes).write(to: temporary)
            // rename replaces the file in one step; FileManager's replaceItemAt fails on Linux.
            guard rename(temporary.path, file.path) == 0 else {
                throw FormatError(String(cString: strerror(errno)))
            }
        } catch {
            try? FileManager.default.removeItem(at: temporary)
            let reason = (error as? FormatError)?.message ?? error.localizedDescription
            throw FormatError("\(file.lastPathComponent) couldn't be saved: \(reason)")
        }
    }

    static func readBytes(_ file: URL, limit: Int = CitySaveFormat.maxBytes) throws(FormatError) -> [UInt8] {
        do {
            let attributes = try FileManager.default.attributesOfItem(atPath: file.path)
            if let size = attributes[.size] as? Int, size > limit {
                throw FormatError("\(file.lastPathComponent) is bigger than \(limit / (1024 * 1024)) MB, so it isn't a city CityLimits saved.")
            }
            return Array(try Data(contentsOf: file))
        } catch let error as FormatError {
            throw error
        } catch {
            guard FileManager.default.fileExists(atPath: file.path) else { throw FormatError("There's no city at \(file.path).") }
            throw FormatError("\(file.path) couldn't be read: \(error.localizedDescription)")
        }
    }

    /// The file a city is saved under: its name, with characters a file name can't hold as
    /// underscores. Returns nil for a name a city can't have.
    public static func fileName(for cityName: String, extension: String = binaryExtension) -> String? {
        guard CitySimulation.isValidName(cityName) else { return nil }
        let invalid = Set("<>:\"/\\|?*")
        var safe = String(cityName.trimmingWhitespace.map { invalid.contains($0) || $0.isASCII && $0.asciiValue! < 0x20 ? "_" : $0 })
        // Windows refuses a name ending in a dot or a space, and a few device names outright.
        while let last = safe.last, last == "." || last == " " { safe.removeLast() }
        let stem = safe.split(separator: ".").first.map { $0.uppercased() } ?? ""
        let reserved = ["CON", "PRN", "AUX", "NUL"].contains(stem)
            || (stem.count == 4 && (stem.hasPrefix("COM") || stem.hasPrefix("LPT")) && stem.last!.isNumber)
        if safe.isEmpty || reserved { safe = "_" + safe }
        return "\(safe).\(`extension`)"
    }

    /// The cities in a folder, newest first, as the Load Game list shows them. A file that can't
    /// be read isn't listed.
    public static func list(_ folder: URL) -> [SavedCitySummary] {
        guard let files = try? FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: [.contentModificationDateKey]) else {
            return []
        }
        let cities = files
            .filter { [flatExtension, binaryExtension, MicropolisImport.fileExtension].contains($0.pathExtension.lowercased()) }
            .map { ($0, (try? $0.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast) }
            .sorted { $0.1 > $1.1 }
            .prefix(maxListed)
        return cities.compactMap { file, saved in summary(file, saved: saved) }
    }

    private static func summary(_ file: URL, saved: Date) -> SavedCitySummary? {
        guard let bytes = try? readBytes(file) else { return nil }
        if file.pathExtension.lowercased() == MicropolisImport.fileExtension {
            // An import is listed by what it would open as, which means importing it.
            guard let city = try? MicropolisImport.read(bytes, name: file.deletingPathExtension().lastPathComponent).city else { return nil }
            return SavedCitySummary(file: file, name: city.name, date: city.date, population: city.census.cityPopulation, funds: city.treasury.funds, saved: saved)
        }
        let save: JSON?
        if file.pathExtension.lowercased() == binaryExtension {
            save = try? sections(binary: bytes).0
        } else {
            save = try? JSON.parse(String(decoding: bytes, as: UTF8.self))
        }
        guard let save, var fields = try? JSONFields(save, at: ""), let name = try? fields.string("name"), !name.isEmpty,
            let cityTime = try? fields.int("cityTime"), cityTime >= 0, let funds = try? fields.int("funds")
        else { return nil }
        var census = (try? fields.object("census")) ?? nil
        let population = ["residentPopulation", "commercialPopulation", "industrialPopulation"]
            .reduce(0) { total, key in total + ((try? census?.int(key)) ?? nil ?? 0) }
        return SavedCitySummary(file: file, name: name, date: CityCalendar.date(at: cityTime), population: population, funds: funds, saved: saved)
    }
}
