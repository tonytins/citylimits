import Foundation
import Testing

@testable import CityLimitsCore

/// Files the tests read: some written by the C# build (legacy/csharp), to check the two read the
/// same formats.
enum Fixtures {
    static let folder = URL(fileURLWithPath: #filePath).deletingLastPathComponent().appendingPathComponent("Fixtures")

    static func url(_ name: String) -> URL { folder.appendingPathComponent(name) }

    /// A new, empty folder for a test to write in, removed when `body` returns.
    static func withTemporaryFolder<T>(_ body: (URL) throws -> T) rethrows -> T {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("citylimits-tests-\(UUID().uuidString)")
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: folder) }
        return try body(folder)
    }
}

struct DefinitionTests {
    @Test func theBaseGamesFilesReadBackAsTheBaseGame() throws {
        #expect(try CityDefinitions.base.applying(CityDefinitions.base.specFiles()) == .base)
    }

    @Test func theCSharpBuildsBaseFilesReadAsTheBaseGame() {
        let (definitions, problem) = SpecFolders.loadBase(Fixtures.url("common-0.5"))
        #expect(problem == nil)
        #expect(definitions == .base)
    }

    @Test func aModChangesOnlyWhatItLists() throws {
        let zones = SpecFile("zones.json", """
            { "format": "2.2", "zones": [ { "id": "commercial", "name": "Market", "price": 40 }, { "id": "residential", "houses": 4 } ] }
            """)
        let changed = try CityDefinitions.base.applying([zones])
        #expect(changed.zone(.commercial).name == "Market")
        #expect(changed.zone(.commercial).price == 40)
        #expect(changed.zone(.residential).houses == 4)
        #expect(changed.zone(.residential).price == 100)
        #expect(changed.plant(.coalPowerPlant) == CityDefinitions.base.plant(.coalPowerPlant))
    }

    @Test func aLaterFileWins() throws {
        let first = SpecFile("a/power.json", #"{ "format": "2.0", "plants": [ { "id": "coal", "capacity": 900 } ] }"#)
        let second = SpecFile("b/power.json", #"{ "format": "2.2", "plants": [ { "id": "coal", "capacity": 1200, "price": 10 } ] }"#)
        let changed = try CityDefinitions.base.applying([first, second])
        #expect(changed.plant(.coalPowerPlant).capacity == 1200)
        #expect(changed.plant(.coalPowerPlant).price == 10)
    }

    @Test func everyKindOfFileApplies() throws {
        let changed = try CityDefinitions.base.applying([
            SpecFile("services.json", #"{ "format": "2.2", "services": [ { "id": "fire", "strength": 150, "reach": 2 } ] }"#),
            SpecFile("facilities.json", #"{ "format": "2.2", "facilities": [ { "id": "airport", "pollution": 40, "influences": [ { "effect": "crime", "strength": 10, "reach": 1 } ] } ] }"#),
            SpecFile("infrastructure.json", #"{ "format": "2.2", "infrastructure": [ { "id": "road", "carriesPower": false }, { "id": "bridge", "price": 70 } ] }"#),
            SpecFile("economy.json", #"{ "format": "2.2", "startingFunds": 50000, "taxRate": 6 }"#),
            SpecFile("ordinances.json", #"{ "format": "2.2", "ordinances": [ { "id": "salesTax", "effects": [] } ] }"#),
        ])
        #expect(changed.service(.fireStation).strength == 150)
        #expect(changed.facility(.airport).influences == [InfluenceSpec(effect: .crime, strength: 10, reach: 1)])
        #expect(!changed.infrastructure.roadCarriesPower)
        #expect(changed.infrastructure.bridge == 70)
        #expect(changed.economy == EconomySpec(startingFunds: 50000, taxRate: 6))
        #expect(changed.ordinance("salesTax")?.effects.isEmpty == true)
    }

    @Test(arguments: [
        (#"{ "format": "2.2", "zones": [ { "id": "commercial", "price": -5 } ] }"#,
         "zones.json: zones[0].price: needs a number from 0 to 1000000, not -5."),
        (#"{ "format": "2.2", "zones": [ { "id": "commercial", "cost": 5 } ] }"#,
         "zones.json: zones[0].cost: is not a field definitions have."),
        (#"{ "format": "2.2", "zones": [ { "id": "farm" } ] }"#,
         "zones.json: zones[0].id: 'farm' is not one of residential, commercial, industrial."),
        (#"{ "format": "2.2", "zones": [ { "id": "commercial" }, { "id": "commercial" } ] }"#,
         "zones.json: zones[1].id: 'commercial' is defined twice."),
        (#"{ "format": "2.2", "zones": [ { "id": "commercial", "valueClasses": 2 } ] }"#,
         "zones.json: zones[0].valueClasses: a mod can't change it, because each class needs its own drawings."),
        (#"{ "format": "2.2", "zones": [ { "id": "commercial", "populations": [1, 2] } ] }"#,
         "zones.json: zones[0].populations: needs 5 levels, one for each building drawing, not 2."),
        (#"{ "format": "3.0", "zones": [] }"#,
         "zones.json: format: this is format 3.0; this version of CityLimits reads up to 2.2."),
        (#"{ "format": "1.0", "zones": [] }"#,
         "zones.json: format: this is format 1.0; this version of CityLimits reads 2.x."),
        (#"{ "zones": [] }"#, "zones.json: format: is required."),
        (#"{ "format": "2.2", "zones": [ "#, "zones.json: the file is not valid JSON at line 1, column 31: it ends too soon."),
    ])
    func aBadFileIsRefusedSayingWhere(text: String, message: String) {
        #expect(throws: FormatError(message)) { try CityDefinitions.base.applying([SpecFile("zones.json", text)]) }
    }

    @Test func olderFilesAreFor0Point4() {
        #expect(throws: FormatError("plants.json: isn't a spec file; spec files are \(SpecFormat.fileNames.joined(separator: ", ")).")) {
            try CityDefinitions.base.applying([SpecFile("plants.json", #"{ "format": "2.2", "plants": [] }"#)])
        }
        #expect(throws: FormatError("infrastructure.json: infrastructure[0].carriesPower: is from format 2.1; this file is 2.0.")) {
            try CityDefinitions.base.applying([SpecFile("infrastructure.json", #"{ "format": "2.0", "infrastructure": [ { "id": "road", "carriesPower": false } ] }"#)])
        }
    }

    @Test func modsLoadFromAFolderInIdOrderAndABadOneIsSkippedWhole() throws {
        try Fixtures.withTemporaryFolder { folder in
            func mod(_ id: String, manifestId: String? = nil, _ files: [String: String]) throws {
                let directory = folder.appendingPathComponent(id)
                try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
                try #"{ "format": "2.2", "id": "\#(manifestId ?? id)", "name": "Mod \#(id)", "version": "1.0" }"#
                    .write(to: directory.appendingPathComponent("mod.json"), atomically: true, encoding: .utf8)
                for (path, text) in files {
                    let file = directory.appendingPathComponent(path)
                    try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
                    try text.write(to: file, atomically: true, encoding: .utf8)
                }
            }
            try mod("b-later", ["deep/er/zones.json": #"{ "format": "2.2", "zones": [ { "id": "commercial", "price": 60 } ] }"#])
            try mod("a-first", ["zones.json": #"{ "format": "2.2", "zones": [ { "id": "commercial", "price": 40, "name": "Market" } ] }"#])
            try mod("c-broken", ["zones.json": #"{ "format": "2.2", "zones": [ { "id": "commercial", "price": 1 } ] }"#,
                                 "power.json": #"{ "format": "2.2", "plants": [ { "id": "coal", "capacity": 0 } ] }"#])
            try mod("d-misnamed", manifestId: "other", [:])

            let (definitions, problems) = SpecFolders.loadMods(folder, onto: .base)
            #expect(definitions.zone(.commercial).price == 60)
            #expect(definitions.zone(.commercial).name == "Market")
            #expect(definitions.mods.map(\.id) == ["a-first", "b-later"])
            #expect(problems == [
                "The mod 'c-broken' was skipped: power.json: plants[0].capacity: needs a number from 1 to 1000000, not 0.",
                "The mod 'd-misnamed' was skipped: mod.json: id: 'other' must be the mod's folder name, 'd-misnamed', in lowercase letters, digits, and single hyphens.",
            ])
        }
    }

    @Test func theBaseFilesAreWrittenOnceABuild() throws {
        try Fixtures.withTemporaryFolder { folder in
            let common = folder.appendingPathComponent("common")
            #expect(try SpecFolders.refreshBase(common, build: "0.6.100"))
            #expect(try !SpecFolders.refreshBase(common, build: "0.6.100"))
            #expect(SpecFolders.loadBase(common).definitions == .base)
            try #"{ "format": "2.2", "zones": [ { "id": "industrial", "price": "cheap" } ] }"#
                .write(to: common.appendingPathComponent("zones.json"), atomically: true, encoding: .utf8)
            let (definitions, problem) = SpecFolders.loadBase(common)
            #expect(definitions == .base)
            #expect(problem?.hasSuffix("zones.json: zones[0].price: needs a whole number.") == true)
        }
    }

    @Test(arguments: [("cheap-commerce", true), ("a", true), ("Cheap", false), ("double--hyphen", false), ("-lead", false), ("", false)])
    func idsAreLowercaseWordsJoinedByHyphens(id: String, valid: Bool) {
        #expect(SpecFormat.isValidId(id) == valid)
    }
}

struct JSONTests {
    @Test func textRoundTrips() throws {
        let value = JSON.object([
            ("name", .string("Quote \" and \\ and \n and é")), ("numbers", .array([.int(1), .int(-2)])),
            ("nested", .object([("ok", .bool(true)), ("nothing", .null)])), ("empty", .array([])),
        ])
        #expect(try JSON.parse(value.text()) == value)
        #expect(value.text().contains("[ 1, -2 ]"))
    }

    @Test func badTextIsRefused() {
        #expect(throws: FormatError.self) { try JSON.parse(#"{ "a": 1, "a": 2 }"#) }
        #expect(throws: FormatError.self) { try JSON.parse("[1, 2,]") }
        #expect(throws: FormatError.self) { try JSON.parse("1 2") }
        #expect(throws: FormatError.self) { try JSON.parse(String(repeating: "[", count: 100) + String(repeating: "]", count: 100)) }
        #expect(throws: FormatError("n: needs a whole number.")) {
            var fields = try JSONFields(JSON.parse(#"{ "n": 99999999999999999999 }"#), at: "")
            _ = try fields.int("n")
        }
    }
}
