import Foundation
import Testing

@testable import CityLimitsCore

struct SettingsTests {
    @Test func aFileTheCSharpBuildWroteReadsWithItsOwnFieldKept() {
        let text = """
            {"WindowWidth": 1600, "WindowHeight": 900, "Speed": 2, "AutosaveSlots": 2, "Fullscreen": false,
             "Disasters": true, "Fires": false, "Floods": true, "Earthquakes": true, "SaveOlderFormats": true,
             "Meltdowns": false, "AirCrashes": true, "Shipwrecks": true, "Tornadoes": true, "Monsters": false}
            """
        let (settings, warning) = GameSettings.read(text)
        #expect(warning == nil)
        #expect(settings.windowWidth == 1600 && settings.windowHeight == 900)
        #expect(settings.speed == .fast && settings.autosaveSlots == 2)
        #expect(!settings.fires && !settings.meltdowns && !settings.monsters && settings.floods)
        #expect(settings.kept.map(\.key) == ["SaveOlderFormats"])

        // Written back, it reads the same, SaveOlderFormats and all.
        let again = GameSettings.read(settings.text())
        #expect(again.warning == nil)
        #expect(again.settings == settings)
        #expect(settings.text().contains("\"SaveOlderFormats\""))
    }

    @Test func aMissingSettingTakesItsDefaultQuietly() {
        let (settings, warning) = GameSettings.read(#"{"Speed": 0}"#)
        #expect(warning == nil)
        #expect(settings.speed == .paused)
        #expect(settings.windowWidth == 1280 && settings.autosaveSlots == 1 && settings.disasters)
    }

    @Test func aBadSettingTakesItsDefaultAndIsNamed() {
        let (settings, warning) = GameSettings.read(#"{"WindowWidth": 100, "AutosaveSlots": "two", "Fires": 1, "Speed": 3}"#)
        #expect(settings.windowWidth == 1280 && settings.autosaveSlots == 1 && settings.fires)
        #expect(settings.speed == .superFast)
        let named = warning ?? ""
        #expect(named.contains("WindowWidth 100, outside 640 to 3840"))
        #expect(named.contains("AutosaveSlots that isn't a whole number"))
        #expect(named.contains("Fires that isn't true or false"))
    }

    @Test(arguments: ["", "[1, 2]", "{not json"])
    func aFileWithNoSettingsGivesTheDefaults(text: String) {
        let (settings, warning) = GameSettings.read(text)
        #expect(settings == GameSettings())
        #expect(warning != nil)
    }

    @Test func aMissingFileGivesTheDefaultsAndSavingWritesOne() throws {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("citylimits-settings-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: folder) }
        let file = folder.appendingPathComponent("settings.json")
        #expect(GameSettings.load(file).settings == GameSettings())
        #expect(GameSettings.load(file).warning == nil)

        var settings = GameSettings()
        settings.autosaveSlots = 0
        settings.tornadoes = false
        try settings.save(to: file)
        #expect(GameSettings.load(file).settings == settings)
    }

    @Test func disastersNeedTheirMasterSwitch() {
        let city = CitySimulation(map: TerrainGenerator.generate(seed: 1), seed: 1)
        var settings = GameSettings()
        settings.floods = false
        settings.apply(to: city)
        #expect(city.randomFires && !city.randomFloods && city.randomMonsters)
        settings.disasters = false
        settings.apply(to: city)
        #expect(!city.randomFires && !city.randomTornadoes && !city.randomEarthquakes)
    }
}

struct AutosaveTests {
    @Test func anAutosaveIsDueAfterFifteenMinutesOfPlay() {
        var autosave = Autosave()
        let early = autosave.advance(14 * 60, running: true)
        // Paused time, or a broken frame time, doesn't count.
        let paused = autosave.advance(10 * 60, running: false)
        let broken = autosave.advance(.nan, running: true)
        let negative = autosave.advance(-5, running: true)
        #expect(!early && !paused && !broken && !negative)
        let due = autosave.advance(60, running: true)
        #expect(due)
        #expect(autosave.playedSeconds == 0)
    }

    @Test func autosavesSitBesideTheCitysOwnFile() {
        #expect(Autosave.fileName(for: "Port Town", slot: 1) == "Port Town (autosave).cl")
        #expect(Autosave.fileName(for: "Port Town", slot: 2, extension: "fcl") == "Port Town (autosave 2).fcl")
        #expect(Autosave.fileName(for: "Port Town", slot: 3) == nil)
        #expect(Autosave.fileName(for: "", slot: 1) == nil)

        let autosave = URL(fileURLWithPath: "/cities/Port Town (autosave 2).cl")
        #expect(Autosave.isAutosave(autosave))
        #expect(!Autosave.isAutosave(URL(fileURLWithPath: "/cities/Port Town.cl")))
        #expect(Autosave.cityFile(for: autosave).path == "/cities/Port Town.cl")
    }

    @Test func theNextAutosaveTakesAFreeSlotThenTheOldest() throws {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("citylimits-autosave-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: folder) }

        let first = try #require(Autosave.nextFile(in: folder, cityName: "Town", slots: 2))
        #expect(first.lastPathComponent == "Town (autosave).cl")
        try Data().write(to: first)
        let second = try #require(Autosave.nextFile(in: folder, cityName: "Town", slots: 2))
        #expect(second.lastPathComponent == "Town (autosave 2).cl")
        try Data().write(to: second)
        try FileManager.default.setAttributes([.modificationDate: Date(timeIntervalSinceNow: -60)], ofItemAtPath: first.path)
        #expect(Autosave.nextFile(in: folder, cityName: "Town", slots: 2) == first)
        // With one slot, the one slot is always the next.
        #expect(Autosave.nextFile(in: folder, cityName: "Town", slots: 1) == first)
    }

    @Test func aFireHoldsSavingUntilItsOut() throws {
        let city = CitySimulation(map: TerrainGenerator.generate(seed: 4), seed: 4)
        #expect(!city.holdsSaving)
        let spot = try #require((0..<city.map.height).lazy.flatMap { y in (0..<city.map.width).lazy.map { (x: $0, y: y) } }
            .first { city.hazards.canBurn($0.x, $0.y) })
        _ = city.hazards.ignite(spot.x, spot.y)
        #expect(city.holdsSaving)
        city.hazards.putOut(spot.x, spot.y)
        #expect(!city.holdsSaving)
    }
}
