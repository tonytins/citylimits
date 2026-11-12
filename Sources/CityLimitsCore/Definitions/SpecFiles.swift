import Foundation

/// A spec file's name, its path within its folder, and its text.
public struct SpecFile: Hashable, Sendable {
    public var name: String
    public var text: String

    public init(_ name: String, _ text: String) {
        self.name = name
        self.text = text
    }
}

/// The spec files that change a city's definitions, as mods and the base game's `common/` folder
/// hold them: the definitions format 2.x. Each file lists only what it changes, and is checked
/// whole before anything is used.
public enum SpecFormat {
    public static let version = FormatVersion(2, 2)
    public static let maxFileBytes = 1024 * 1024
    public static let fileNames = ["zones.json", "power.json", "services.json", "facilities.json", "infrastructure.json", "economy.json", "ordinances.json"]

    public static let maxStrength = 1000
    public static let maxReach = 8
    public static let maxPrice = 1_000_000
    public static let maxPopulation = 100_000
    public static let maxCapacity = 1_000_000
    public static let maxInfluence = 255
    public static let maxOrdinanceMoney = 1000
    static let maxNameLength = 80
    static let maxVersionLength = 32
    static let maxDescriptionLength = 500

    // From 2.1, facilities have influences, and roads and rail say whether they carry power;
    // from 2.2, ordinances.
    static let influencesSince = FormatVersion(2, 1)
    static let carriesPowerSince = FormatVersion(2, 1)
    static let ordinancesSince = FormatVersion(2, 2)

    static let zoneIds: KeyValuePairs<String, TileUse> = ["residential": .residential, "commercial": .commercial, "industrial": .industrial]
    static let plantIds: KeyValuePairs<String, TileUse> = ["coal": .coalPowerPlant, "nuclear": .nuclearPowerPlant]
    static let serviceIds: KeyValuePairs<String, TileUse> = ["fire": .fireStation, "police": .policeStation]
    static let facilityIds: KeyValuePairs<String, TileUse> = ["stadium": .stadium, "seaport": .seaport, "airport": .airport]
    static let infrastructureIds = ["road", "bridge", "rail", "tunnel", "powerLine", "underwaterPowerLine", "park", "bulldozer", "clearing"]
    static let influenceEffects: KeyValuePairs<String, InfluenceEffect> = ["landValue": .landValue, "crime": .crime, "pollution": .pollution]
    static let ordinanceEffects: KeyValuePairs<String, OrdinanceEffect> = [
        "residentialDemand": .residentialDemand, "commercialDemand": .commercialDemand, "industrialDemand": .industrialDemand,
        "crime": .crime, "industrialPollution": .industrialPollution, "fires": .fires, "powerCapacity": .powerCapacity, "traffic": .traffic,
    ]

    static func name(_ effect: InfluenceEffect) -> String { influenceEffects.first { $0.value == effect }!.key }
    static func name(_ effect: OrdinanceEffect) -> String { ordinanceEffects.first { $0.value == effect }!.key }

    /// The id a zone, plant, service, or facility is named by in spec files.
    static func id(of use: TileUse) -> String? {
        [zoneIds, plantIds, serviceIds, facilityIds].lazy.compactMap { ids in ids.first { $0.value == use }?.key }.first
    }

    // How far each ordinance effect may go, in its own units: demand as far as it reaches, crime
    // its map's range, and percents from none of it to ten times as much.
    static func range(of effect: OrdinanceEffect) -> ClosedRange<Int> {
        switch effect {
        case .residentialDemand, .commercialDemand, .industrialDemand: -2000...2000
        case .crime: -CityMaps.mostCrime...CityMaps.mostCrime
        // An ordinance can stop fires breaking out, but not start more.
        case .fires: -100...0
        default: -100...900
        }
    }

    /// Whether an id can name a mod or a scenario: lowercase letters, digits, and single hyphens.
    public static func isValidId(_ id: String) -> Bool {
        guard (1...64).contains(id.utf8.count) else { return false }
        let parts = id.split(separator: "-", omittingEmptySubsequences: false)
        return parts.allSatisfy { part in !part.isEmpty && part.utf8.allSatisfy { (0x61...0x7A).contains($0) || (0x30...0x39).contains($0) } }
    }
}

extension CityDefinitions {
    /// These definitions with spec files applied in order, each change on top of the last. The
    /// files are checked whole: the first problem refuses them all, saying which file and where.
    public func applying(_ files: [SpecFile]) throws(FormatError) -> CityDefinitions {
        var result = self
        for file in files {
            do throws(FormatError) {
                try result.apply(file)
            } catch {
                throw FormatError("\(file.name): \(error.message)")
            }
        }
        return result
    }

    private mutating func apply(_ file: SpecFile) throws(FormatError) {
        guard file.text.utf8.count <= SpecFormat.maxFileBytes else {
            throw FormatError("the file is \(file.text.utf8.count) bytes; the most is \(SpecFormat.maxFileBytes).")
        }
        var fields = try JSONFields(JSON.parse(file.text), at: "")
        let version = try FormatVersion.read(&fields, reader: SpecFormat.version)
        let kind = file.name.split(separator: "/").last.map { $0.lowercased() } ?? ""
        switch kind {
        case "zones.json": try applyZones(&fields)
        case "power.json": try applyPlants(&fields)
        case "services.json": try applyServices(&fields)
        case "facilities.json": try applyFacilities(&fields, version)
        case "infrastructure.json": try applyInfrastructure(&fields, version)
        case "economy.json": try applyEconomy(&fields)
        case "ordinances.json": try applyOrdinances(&fields, version)
        default:
            throw FormatError("isn't a spec file; spec files are \(SpecFormat.fileNames.joined(separator: ", ")).")
        }
        try fields.finish(noun: "definitions have")
    }

    // Each entry in a list names what it changes by id, once a file.
    private static func entries<Key: Hashable>(
        _ fields: inout JSONFields, _ list: String, noun: String, ids: (inout JSONFields) throws(FormatError) -> Key,
        each: (inout JSONFields, Key) throws(FormatError) -> Void
    ) throws(FormatError) {
        var seen = Set<Key>()
        for (index, item) in (try fields.array(list) ?? []).enumerated() {
            guard item != .null else { throw FormatError("\(list)[\(index)]: needs \(noun), not null.") }
            var entry = try JSONFields(item, at: "\(list)[\(index)]")
            let key = try ids(&entry)
            guard seen.insert(key).inserted else {
                throw FormatError("\(entry.at("id")): '\((try? entry.string("id")) ?? "")' is defined twice.")
            }
            try each(&entry, key)
            try entry.finish(noun: "definitions have")
        }
    }

    private static func use(_ entry: inout JSONFields, in ids: KeyValuePairs<String, TileUse>) throws(FormatError) -> TileUse {
        let id = try entry.requiredString("id")
        guard let use = ids.first(where: { $0.key == id })?.value else {
            throw FormatError("\(entry.at("id")): '\(id)' is not one of \(ids.map(\.key).joined(separator: ", ")).")
        }
        return use
    }

    private static func name(_ entry: inout JSONFields) throws(FormatError) -> String? {
        guard let text = try entry.string("name") else { return nil }
        let name = text.trimmingWhitespace
        guard (1...SpecFormat.maxNameLength).contains(name.count) else {
            throw FormatError("\(entry.at("name")): needs 1 to \(SpecFormat.maxNameLength) characters, not \(name.count).")
        }
        return name
    }

    private static func price(_ entry: inout JSONFields, _ key: String = "price") throws(FormatError) -> Int? {
        try entry.int(key, in: 0...SpecFormat.maxPrice)
    }

    private mutating func applyZones(_ fields: inout JSONFields) throws(FormatError) {
        var zones = self.zones
        try Self.entries(&fields, "zones", noun: "a zone", ids: { (entry: inout JSONFields) throws(FormatError) in try Self.use(&entry, in: SpecFormat.zoneIds) }) {
            (entry: inout JSONFields, use: TileUse) throws(FormatError) in
            var zone = zones[use]!
            if entry.has("valueClasses") {
                throw FormatError("\(entry.at("valueClasses")): a mod can't change it, because each class needs its own drawings.")
            }
            if let name = try Self.name(&entry) { zone.name = name }
            if let price = try Self.price(&entry) { zone.price = price }
            if let populations = try entry.ints("populations", in: 0...SpecFormat.maxPopulation) {
                // The number of levels is fixed by the drawings: the base game sets it, and a mod must keep it.
                guard populations.count == zone.levels else {
                    throw FormatError("\(entry.at("populations")): needs \(zone.levels) levels, one for each building drawing, not \(populations.count).")
                }
                zone.populations = populations
            }
            if let houses = try entry.int("houses", in: 0...(ZoneLayout.parts - 1)) {
                // Single houses stand on a zone's outer tiles, and only residential zones have their drawings.
                guard houses == 0 || use == .residential else {
                    throw FormatError("\(entry.at("houses")): only a residential zone grows single houses.")
                }
                zone.houses = houses
            }
            if let trips = try entry.strings("trips") {
                // A trip has to be looking for something: one to three different kinds of zone.
                guard !trips.isEmpty else { throw FormatError("\(entry.at("trips")): needs at least one kind of zone to go to.") }
                var uses: [TileUse] = []
                for (index, id) in trips.enumerated() {
                    guard let tripUse = SpecFormat.zoneIds.first(where: { $0.key == id })?.value else {
                        throw FormatError("\(entry.at("trips"))[\(index)]: '\(id)' is not one of residential, commercial, industrial.")
                    }
                    guard !uses.contains(tripUse) else { throw FormatError("\(entry.at("trips"))[\(index)]: '\(id)' is listed twice.") }
                    uses.append(tripUse)
                }
                zone.tripsTo = uses
            }
            zones[use] = zone
        }
        self.zones = zones
    }

    private mutating func applyPlants(_ fields: inout JSONFields) throws(FormatError) {
        var plants = self.plants
        try Self.entries(&fields, "plants", noun: "a plant", ids: { (entry: inout JSONFields) throws(FormatError) in try Self.use(&entry, in: SpecFormat.plantIds) }) {
            (entry: inout JSONFields, use: TileUse) throws(FormatError) in
            if let name = try Self.name(&entry) { plants[use]!.name = name }
            if let price = try Self.price(&entry) { plants[use]!.price = price }
            if let capacity = try entry.int("capacity", in: 1...SpecFormat.maxCapacity) { plants[use]!.capacity = capacity }
        }
        self.plants = plants
    }

    private mutating func applyServices(_ fields: inout JSONFields) throws(FormatError) {
        var services = self.services
        try Self.entries(&fields, "services", noun: "a service", ids: { (entry: inout JSONFields) throws(FormatError) in try Self.use(&entry, in: SpecFormat.serviceIds) }) {
            (entry: inout JSONFields, use: TileUse) throws(FormatError) in
            if let name = try Self.name(&entry) { services[use]!.name = name }
            if let price = try Self.price(&entry) { services[use]!.price = price }
            if let upkeep = try Self.price(&entry, "upkeep") { services[use]!.upkeep = upkeep }
            if let strength = try entry.int("strength", in: 0...SpecFormat.maxStrength) { services[use]!.strength = strength }
            if let reach = try entry.int("reach", in: 0...SpecFormat.maxReach) { services[use]!.reach = reach }
        }
        self.services = services
    }

    private mutating func applyFacilities(_ fields: inout JSONFields, _ version: FormatVersion) throws(FormatError) {
        var facilities = self.facilities
        try Self.entries(&fields, "facilities", noun: "a facility", ids: { (entry: inout JSONFields) throws(FormatError) in try Self.use(&entry, in: SpecFormat.facilityIds) }) {
            (entry: inout JSONFields, use: TileUse) throws(FormatError) in
            if let name = try Self.name(&entry) { facilities[use]!.name = name }
            if let price = try Self.price(&entry) { facilities[use]!.price = price }
            if let cap = try entry.int("demandCap", in: 0...SpecFormat.maxPopulation) { facilities[use]!.demandCap = cap }
            if let pollution = try entry.int("pollution", in: 0...CityMaps.mostPollution) { facilities[use]!.pollution = pollution }
            if let items = try entry.array("influences") {
                // A facility's influences replace its own whole; an empty list clears them.
                let at = entry.at("influences")
                guard version >= SpecFormat.influencesSince else {
                    throw FormatError("\(at): is from format \(SpecFormat.influencesSince); this file is \(version).")
                }
                var influences: [InfluenceSpec] = []
                for (index, item) in items.enumerated() {
                    var influence = try JSONFields(item, at: "\(at)[\(index)]")
                    let name = try influence.requiredString("effect")
                    guard let effect = SpecFormat.influenceEffects.first(where: { $0.key == name })?.value else {
                        throw FormatError("\(influence.at("effect")): '\(name)' is not one of landValue, crime, pollution.")
                    }
                    guard !influences.contains(where: { $0.effect == effect }) else {
                        throw FormatError("\(influence.at("effect")): '\(name)' is given twice.")
                    }
                    let strength = try influence.requiredInt("strength", in: -SpecFormat.maxInfluence...SpecFormat.maxInfluence)
                    let reach = try influence.requiredInt("reach", in: 0...SpecFormat.maxReach)
                    try influence.finish(noun: "definitions have")
                    influences.append(InfluenceSpec(effect: effect, strength: strength, reach: reach))
                }
                facilities[use]!.influences = influences
            }
        }
        self.facilities = facilities
    }

    private mutating func applyInfrastructure(_ fields: inout JSONFields, _ version: FormatVersion) throws(FormatError) {
        var infrastructure = self.infrastructure
        try Self.entries(&fields, "infrastructure", noun: "an entry", ids: { (entry: inout JSONFields) throws(FormatError) -> String in
            let id = try entry.requiredString("id")
            guard SpecFormat.infrastructureIds.contains(id) else {
                throw FormatError("\(entry.at("id")): '\(id)' is not one of \(SpecFormat.infrastructureIds.joined(separator: ", ")).")
            }
            return id
        }) { (entry: inout JSONFields, id: String) throws(FormatError) in
            let price = try Self.price(&entry)
            let carries = try entry.bool("carriesPower")
            guard price != nil || carries != nil else {
                throw FormatError("\(entry.path): needs a price\(id == "road" || id == "rail" ? " or carriesPower" : "").")
            }
            if let price {
                switch id {
                case "road": infrastructure.road = price
                case "bridge": infrastructure.bridge = price
                case "rail": infrastructure.rail = price
                case "tunnel": infrastructure.tunnel = price
                case "powerLine": infrastructure.powerLine = price
                case "underwaterPowerLine": infrastructure.underwaterPowerLine = price
                case "park": infrastructure.park = price
                case "bulldozer": infrastructure.bulldozer = price
                default: infrastructure.clearing = price
                }
            }
            if let carries {
                guard version >= SpecFormat.carriesPowerSince else {
                    throw FormatError("\(entry.at("carriesPower")): is from format \(SpecFormat.carriesPowerSince); this file is \(version).")
                }
                switch id {
                case "road": infrastructure.roadCarriesPower = carries
                case "rail": infrastructure.railCarriesPower = carries
                default: throw FormatError("\(entry.at("carriesPower")): only road and rail may carry power this way, not \(id).")
                }
            }
        }
        self.infrastructure = infrastructure
    }

    private mutating func applyEconomy(_ fields: inout JSONFields) throws(FormatError) {
        if let funds = try fields.int("startingFunds", in: 0...SpecFormat.maxPrice) { economy.startingFunds = funds }
        if let rate = try fields.int("taxRate", in: 0...CityBudget.maxTaxRate) { economy.taxRate = rate }
    }

    private mutating func applyOrdinances(_ fields: inout JSONFields, _ version: FormatVersion) throws(FormatError) {
        guard version >= SpecFormat.ordinancesSince else {
            throw FormatError("is from format \(SpecFormat.ordinancesSince); this file is \(version).")
        }
        var ordinances = self.ordinances
        let known = CityDefinitions.base.ordinances.map(\.id)
        try Self.entries(&fields, "ordinances", noun: "an ordinance", ids: { (entry: inout JSONFields) throws(FormatError) -> String in
            let id = try entry.requiredString("id")
            guard known.contains(id) else {
                throw FormatError("\(entry.at("id")): '\(id)' is not one of \(known.joined(separator: ", ")).")
            }
            return id
        }) { (entry: inout JSONFields, id: String) throws(FormatError) in
            let index = ordinances.firstIndex { $0.id == id }!
            if let name = try Self.name(&entry) { ordinances[index].name = name }
            if let money = try entry.int("money", in: -SpecFormat.maxOrdinanceMoney...SpecFormat.maxOrdinanceMoney) {
                ordinances[index].money = money
            }
            if let items = try entry.array("effects") {
                // Given effects replace the ordinance's whole.
                var effects: [OrdinanceEffect: Int] = [:]
                for (number, item) in items.enumerated() {
                    var each = try JSONFields(item, at: "\(entry.at("effects"))[\(number)]")
                    let name = try each.requiredString("effect")
                    guard let effect = SpecFormat.ordinanceEffects.first(where: { $0.key == name })?.value else {
                        throw FormatError("\(each.at("effect")): '\(name)' is not one of \(SpecFormat.ordinanceEffects.map(\.key).joined(separator: ", ")).")
                    }
                    guard effects[effect] == nil else { throw FormatError("\(each.at("effect")): '\(name)' is given twice.") }
                    effects[effect] = try each.requiredInt("amount", in: SpecFormat.range(of: effect))
                    try each.finish(noun: "definitions have")
                }
                ordinances[index].effects = effects
            }
        }
        self.ordinances = ordinances
    }

    // MARK: Writing

    /// The definitions as spec files with every value written out, as the base game's `common/`
    /// folder holds them.
    public func specFiles() -> [SpecFile] {
        func file(_ name: String, _ fields: [(key: String, value: JSON)]) -> SpecFile {
            let document = JSON.object([
                ("$schema", .string(SchemaLinks.url(for: name, version: SpecFormat.version))),
                ("format", .string(SpecFormat.version.description)),
            ] + fields)
            return SpecFile("\(name).json", document.text())
        }
        func id(_ use: TileUse) -> JSON { .string(SpecFormat.id(of: use)!) }
        return [
            file("zones", [("zones", .array(zones.values.sorted { $0.use < $1.use }.map { zone in
                .object([
                    ("id", .string(zone.id)), ("name", .string(zone.name)), ("price", .int(zone.price)),
                    ("populations", .array(zone.populations.map(JSON.int))), ("houses", .int(zone.houses)),
                    ("trips", .array(zone.tripsTo.map(id))),
                ])
            }))]),
            file("power", [("plants", .array(plants.values.sorted { $0.use < $1.use }.map { plant in
                .object([("id", .string(plant.id)), ("name", .string(plant.name)), ("price", .int(plant.price)), ("capacity", .int(plant.capacity))])
            }))]),
            file("services", [("services", .array(services.values.sorted { $0.use < $1.use }.map { service in
                .object([
                    ("id", .string(service.id)), ("name", .string(service.name)), ("price", .int(service.price)),
                    ("upkeep", .int(service.upkeep)), ("strength", .int(service.strength)), ("reach", .int(service.reach)),
                ])
            }))]),
            file("facilities", [("facilities", .array(facilities.values.sorted { $0.use < $1.use }.map { facility in
                .object([
                    ("id", .string(facility.id)), ("name", .string(facility.name)), ("price", .int(facility.price)),
                    ("demandCap", .int(facility.demandCap)), ("pollution", .int(facility.pollution)),
                    ("influences", .array(facility.influences.map { influence in
                        .object([("effect", .string(SpecFormat.name(influence.effect))), ("strength", .int(influence.strength)), ("reach", .int(influence.reach))])
                    })),
                ])
            }))]),
            file("infrastructure", [("infrastructure", .array([
                infrastructureEntry("road", infrastructure.road, carries: infrastructure.roadCarriesPower),
                infrastructureEntry("bridge", infrastructure.bridge),
                infrastructureEntry("rail", infrastructure.rail, carries: infrastructure.railCarriesPower),
                infrastructureEntry("tunnel", infrastructure.tunnel),
                infrastructureEntry("powerLine", infrastructure.powerLine),
                infrastructureEntry("underwaterPowerLine", infrastructure.underwaterPowerLine),
                infrastructureEntry("park", infrastructure.park),
                infrastructureEntry("bulldozer", infrastructure.bulldozer),
                infrastructureEntry("clearing", infrastructure.clearing),
            ]))]),
            file("ordinances", [("ordinances", .array(ordinances.map { ordinance in
                .object([
                    ("id", .string(ordinance.id)), ("name", .string(ordinance.name)), ("money", .int(ordinance.money)),
                    ("effects", .array(SpecFormat.ordinanceEffects.compactMap { pair in
                        ordinance.effects[pair.value].map { JSON.object([("effect", .string(pair.key)), ("amount", .int($0))]) }
                    })),
                ])
            }))]),
            file("economy", [("startingFunds", .int(economy.startingFunds)), ("taxRate", .int(economy.taxRate))]),
        ]
    }

    private func infrastructureEntry(_ id: String, _ price: Int, carries: Bool? = nil) -> JSON {
        var fields: [(key: String, value: JSON)] = [("id", .string(id)), ("price", .int(price))]
        if let carries { fields.append(("carriesPower", .bool(carries))) }
        return .object(fields)
    }
}

/// Where the formats' JSON schemas are published, for a file's `$schema` line.
public enum SchemaLinks {
    public static let base = "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/"

    public static func url(for name: String, version: FormatVersion) -> String { "\(base)\(name).v\(version).schema.json" }
}

/// A mod's `mod.json`: who the mod is.
public struct ModManifest: Hashable, Sendable {
    public var id: String
    public var name: String
    public var version: String?
    public var description: String?

    /// Reads a mod's manifest, which must name the folder it's in.
    public static func read(_ text: String, folder: String) throws(FormatError) -> ModManifest {
        do throws(FormatError) {
            var fields = try JSONFields(JSON.parse(text), at: "")
            _ = try FormatVersion.read(&fields, reader: SpecFormat.version)
            let id = try fields.requiredString("id")
            guard SpecFormat.isValidId(id), id == folder else {
                throw FormatError("id: '\(id)' must be the mod's folder name, '\(folder)', in lowercase letters, digits, and single hyphens.")
            }
            let name = try fields.requiredString("name").trimmingWhitespace
            guard (1...SpecFormat.maxNameLength).contains(name.count) else {
                throw FormatError("name: needs 1 to \(SpecFormat.maxNameLength) characters, not \(name.count).")
            }
            let version = try fields.string("version")?.trimmingWhitespace
            if let version, !(1...SpecFormat.maxVersionLength).contains(version.count) {
                throw FormatError("version: needs 1 to \(SpecFormat.maxVersionLength) characters, not \(version.count).")
            }
            let description = try fields.string("description")?.trimmingWhitespace
            if let description, description.count > SpecFormat.maxDescriptionLength {
                throw FormatError("description: needs at most \(SpecFormat.maxDescriptionLength) characters, not \(description.count).")
            }
            try fields.finish(noun: "mod.json has")
            return ModManifest(id: id, name: name, version: version, description: description)
        } catch {
            throw FormatError("mod.json: \(error.message)")
        }
    }
}

/// Reads spec files and mods from folders on disk.
public enum SpecFolders {
    public static let maxMods = 64
    public static let maxFiles = 64
    public static let maxDepth = 8
    public static let manifest = "mod.json"
    public static let versionStamp = "version.txt"

    /// A folder's spec files, however deep, by their paths within it, in path order.
    public static func read(_ folder: URL) throws(FormatError) -> [SpecFile] {
        var paths: [String] = []
        func walk(_ directory: URL, _ relative: String, depth: Int) throws(FormatError) {
            guard depth <= maxDepth else { return }
            let entries: [URL]
            do {
                entries = try FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: [.isDirectoryKey])
            } catch {
                throw FormatError("'\(directory.path)' could not be read: \(error.localizedDescription)")
            }
            for entry in entries {
                let name = entry.lastPathComponent
                let path = relative.isEmpty ? name : "\(relative)/\(name)"
                if (try? entry.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true {
                    try walk(entry, path, depth: depth + 1)
                } else if SpecFormat.fileNames.contains(name.lowercased()) {
                    paths.append(path)
                    guard paths.count <= maxFiles else { throw FormatError("it holds more than \(maxFiles) spec files.") }
                }
            }
        }
        guard FileManager.default.fileExists(atPath: folder.path) else { return [] }
        try walk(folder, "", depth: 0)
        var files: [SpecFile] = []
        for path in paths.sorted(by: { $0.utf8.lexicographicallyPrecedes($1.utf8) }) {
            files.append(SpecFile(path, try readBounded(folder.appendingPathComponent(path))))
        }
        return files
    }

    /// A text file's contents, refused when it's bigger than a spec file may be.
    public static func readBounded(_ file: URL, limit: Int = SpecFormat.maxFileBytes) throws(FormatError) -> String {
        let data: Data
        do {
            let size = try FileManager.default.attributesOfItem(atPath: file.path)[.size] as? Int ?? 0
            guard size <= limit else { throw FormatError("\(file.lastPathComponent) is \(size) bytes; the most is \(limit).") }
            data = try Data(contentsOf: file)
        } catch let error as FormatError {
            throw error
        } catch {
            throw FormatError("\(file.lastPathComponent) could not be read: \(error.localizedDescription)")
        }
        guard data.count <= limit else { throw FormatError("\(file.lastPathComponent) is \(data.count) bytes; the most is \(limit).") }
        guard let text = String(data: data, encoding: .utf8) else { throw FormatError("\(file.lastPathComponent) is not UTF-8 text.") }
        return text
    }

    /// Applies every valid mod in a folder, in id order, on top of `start`. A mod with any problem
    /// is skipped whole, and the problems say which and why.
    public static func loadMods(_ folder: URL, onto start: CityDefinitions) -> (definitions: CityDefinitions, problems: [String]) {
        var definitions = start
        var problems: [String] = []
        guard FileManager.default.fileExists(atPath: folder.path) else { return (definitions, problems) }
        var mods: [URL]
        do {
            mods = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: [.isDirectoryKey])
                .filter { (try? $0.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true }
                .sorted { $0.lastPathComponent.utf8.lexicographicallyPrecedes($1.lastPathComponent.utf8) }
        } catch {
            return (definitions, ["The mods folder '\(folder.path)' could not be read: \(error.localizedDescription)"])
        }
        if mods.count > maxMods {
            problems.append("The mods folder holds \(mods.count) mods; only the first \(maxMods) by name are read.")
            mods = Array(mods.prefix(maxMods))
        }
        for mod in mods {
            let name = mod.lastPathComponent
            do throws(FormatError) {
                let manifestFile = mod.appendingPathComponent(manifest)
                guard FileManager.default.fileExists(atPath: manifestFile.path) else { throw FormatError("it has no \(manifest).") }
                let info = try ModManifest.read(try readBounded(manifestFile), folder: name)
                var applied = try definitions.applying(try read(mod))
                applied.mods.append(AppliedMod(id: info.id, name: info.name, version: info.version))
                definitions = applied
            } catch {
                problems.append("The mod '\(name)' was skipped: \(error.message)")
            }
        }
        return (definitions, problems)
    }

    /// Writes the base game's spec files into `common/`, once for each build, by the stamp in
    /// `version.txt`. Returns whether it wrote them.
    @discardableResult
    public static func refreshBase(_ folder: URL, build: String) throws(FormatError) -> Bool {
        let stamp = folder.appendingPathComponent(versionStamp)
        if let written = try? String(contentsOf: stamp, encoding: .utf8), written.trimmingWhitespace == build { return false }
        do {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            for file in CityDefinitions.base.specFiles() {
                try file.text.write(to: folder.appendingPathComponent(file.name), atomically: true, encoding: .utf8)
            }
            // Last, so a refresh cut short is tried again next time.
            try (build + "\n").write(to: stamp, atomically: true, encoding: .utf8)
        } catch {
            throw FormatError("The base game's files could not be written to '\(folder.path)': \(error.localizedDescription)")
        }
        return true
    }

    /// The base game with its `common/` files applied, not counted as a mod. A problem skips the
    /// whole folder, and the game plays with its own numbers.
    public static func loadBase(_ folder: URL) -> (definitions: CityDefinitions, problem: String?) {
        do throws(FormatError) {
            return (try CityDefinitions.base.applying(try read(folder)), nil)
        } catch {
            return (.base, "The base game's files in '\(folder.path)' were skipped: \(error.message)")
        }
    }
}
