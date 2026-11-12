import Foundation
import Testing

@testable import CityLimitsCore

struct SavingTests {
    // The port town, renamed, with parking fines and a 9% tax, saved by the C# build (legacy/csharp)
    // five steps into a count three years on, in both formats; two years later the C# city was at
    // step 3845 with these funds, people, map, and fires.
    @Test(arguments: ["port-town-0.5.fcl", "port-town-0.5.cl"])
    func aCitySavedByTheCSharpBuildRunsOnAsItDidThere(file: String) throws {
        let loaded = try CityFiles.read(Fixtures.url(file))
        let city = loaded.city
        #expect(loaded.notes.isEmpty)
        #expect(city.name == "Port Town")
        #expect(city.ordinances.isEnacted("parkingFines"))
        #expect(city.budget.taxRate == 9)
        city.run(steps: 2 * CityCalendar.stepsPerYear)
        #expect(city.tickCount == 3845)
        #expect(city.treasury.funds == 72988)
        #expect(city.census.cityPopulation == 7720)
        #expect(SimulationParityTests.hash(city.map) == 0x4DE7_F152_D4FA_5B1D)
        #expect(city.hazards.fires.count == 12)
    }

    @Test func bothCSharpFormatsHoldTheSameCity() throws {
        let flat = try CityFiles.read(Fixtures.url("port-town-0.5.fcl")).city
        let binary = try CityFiles.read(Fixtures.url("port-town-0.5.cl")).city
        #expect(CityFiles.flatText(flat) == CityFiles.flatText(binary))
    }

    // A city saved at any step loads and runs on step for step as if it had never stopped, in
    // either format: the three are run side by side for a year, their saves compared as they go.
    @Test(arguments: [0, 7, CityCalendar.stepsPerYear + 3, 3 * CityCalendar.stepsPerYear + 5])
    func aSavedCityRunsOnInStepWithTheOriginal(steps: Int) throws {
        let original = SimulationParityTests.scriptedPort()
        original.run(steps: steps)
        let flat = try CityFiles.read(flat: CityFiles.flatText(original)).city
        let binary = try CityFiles.read(binary: CityFiles.binaryBytes(original)).city
        for _ in 0..<8 {
            #expect(CityFiles.flatText(flat) == CityFiles.flatText(original))
            #expect(CityFiles.flatText(binary) == CityFiles.flatText(original))
            for city in [original, flat, binary] { city.run(steps: CityCalendar.stepsPerYear / 8) }
        }
        #expect(SimulationParityTests.hash(flat.map) == SimulationParityTests.hash(original.map))
        #expect(SimulationParityTests.hash(binary.map) == SimulationParityTests.hash(original.map))
    }

    @Test func hazardsAndSpritesSurviveASave() throws {
        let city = SimulationParityTests.scriptedTown(nuclear: true)
        city.run(steps: 2 * CityCalendar.stepsPerYear)
        for disaster in Disaster.allCases { city.strike(disaster) }
        #expect(city.hazards.fires.count > 0 && city.hazards.radiation.count > 0)
        #expect(city.menaces.tornado != nil)
        for loaded in [try CityFiles.read(flat: CityFiles.flatText(city)).city, try CityFiles.read(binary: CityFiles.binaryBytes(city)).city] {
            #expect(loaded.hazards.fires.count == city.hazards.fires.count)
            #expect(loaded.hazards.floods.count == city.hazards.floods.count)
            #expect(loaded.hazards.radiation.count == city.hazards.radiation.count)
            #expect(loaded.menaces.tornado == city.menaces.tornado)
            #expect(loaded.menaces.monster == city.menaces.monster)
            #expect(CityFiles.flatText(loaded) == CityFiles.flatText(city))
        }
    }

    @Test func aSaveWithoutAFeatureIsFilledInAndOneFromANewerVersionIsLeftOut() throws {
        let city = SimulationParityTests.scriptedTown()
        city.run(steps: 100)
        guard case .object(var fields) = try JSON.parse(CityFiles.flatText(city)) else { Issue.record("not an object"); return }
        fields.removeAll { ["vehicles", "ordinances", "founding"].contains($0.key) }
        fields.append(("weather", .object([("rain", .bool(true))])))
        let loaded = try CityFiles.read(flat: JSON.object(fields).text())
        #expect(loaded.city.foundedYear == 1900)
        #expect(loaded.notes == [
            "A newer CityLimits saved this city with parts this version doesn't use.",
            "Its weather part is left out, and is left out when you save it.",
        ])
        #expect(!CityFiles.flatText(loaded.city).contains("weather"))
    }

    @Test(arguments: [
        (#""format": "2.0""#, #""format": "3.0""#, "format: this is format 3.0; this version of CityLimits reads 2.x. A city from 0.4 or before can be opened in 0.5 and saved again there."),
        (#""format": "2.0""#, #""format": "1.10""#, "format: this is format 1.10; this version of CityLimits reads 2.x. A city from 0.4 or before can be opened in 0.5 and saved again there."),
        (#""funds": "#, #""cheats": true, "funds": "#, "cheats: is not a field a save has, nor a part a newer version added."),
        (#""random": ""#, #""random": "zz"#, "random: needs 16 hexadecimal digits."),
        (#""tick": 100"#, #""tick": 101"#, "cityTime: a city 101 steps old is at count 6, not 6."),
    ])
    func aBadSaveIsRefusedSayingWhere(find: String, replace: String, message: String) throws {
        let city = SimulationParityTests.scriptedTown()
        city.run(steps: 100)
        let text = CityFiles.flatText(city)
        #expect(text.contains(find))
        let broken = text.replacingOccurrences(of: find, with: replace)
        if message.hasPrefix("cityTime") {
            // 101 steps is still count 6, so it loads; one count further is refused.
            #expect(throws: Never.self) { try CityFiles.read(flat: broken) }
            #expect(throws: FormatError("cityTime: a city 116 steps old is at count 7, not 6.")) {
                try CityFiles.read(flat: text.replacingOccurrences(of: find, with: #""tick": 116"#))
            }
        } else {
            #expect(throws: FormatError(message)) { try CityFiles.read(flat: broken) }
        }
    }

    @Test func aDamagedBinarySaveIsRefused() throws {
        let city = SimulationParityTests.scriptedTown()
        var bytes = CityFiles.binaryBytes(city)
        #expect(throws: FormatError("The file isn't a binary CityLimits city: it doesn't start with CLCITY.")) {
            try CityFiles.read(binary: Array("NOTCITY!".utf8))
        }
        bytes[bytes.count / 2] ^= 0xFF
        #expect(throws: FormatError.self) { try CityFiles.read(binary: bytes) }
        let truncated = Array(CityFiles.binaryBytes(city).dropLast(3))
        #expect(throws: FormatError.self) { try CityFiles.read(binary: truncated) }
    }

    @Test func aMapThatIsntWholeIsRefused() throws {
        let city = SimulationParityTests.scriptedTown()
        let text = CityFiles.flatText(city).replacingOccurrences(of: "\"map\": {\n    \"width\": 64", with: "\"map\": {\n    \"width\": 2000")
        #expect(throws: FormatError("map.width: needs a number from 1 to 1024, not 2000.")) { try CityFiles.read(flat: text) }
    }

    @Test func filesAreWrittenReadAndListed() throws {
        try Fixtures.withTemporaryFolder { folder in
            let city = SimulationParityTests.scriptedTown()
            city.rename("Riverton")
            city.run(steps: CityCalendar.stepsPerYear)
            let binary = folder.appendingPathComponent(CityFiles.fileName(for: city.name)!)
            let flat = folder.appendingPathComponent(CityFiles.fileName(for: city.name, extension: CityFiles.flatExtension)!)
            try CityFiles.write(city, to: binary)
            try CityFiles.write(city, to: binary)
            try CityFiles.write(city, to: flat)
            #expect(CityFiles.flatText(try CityFiles.read(binary).city) == CityFiles.flatText(city))
            let listed = CityFiles.list(folder)
            #expect(listed.count == 2)
            #expect(listed.allSatisfy { $0.name == "Riverton" && $0.funds == city.treasury.funds && $0.date == city.date })
            #expect(throws: FormatError.self) { try CityFiles.read(folder.appendingPathComponent("missing.cl")) }
        }
    }

    @Test(arguments: [("Riverton", "Riverton.cl"), ("What? No!", "What_ No!.cl"), ("CON", "_CON.cl"), ("Dots...", "Dots.cl")])
    func aCityIsSavedUnderASafeName(name: String, file: String) {
        #expect(CityFiles.fileName(for: name) == file)
    }
}
