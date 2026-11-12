import Foundation
import Testing

@testable import CityLimitsCore

struct ScenarioEditTests {
    private static func scratch() throws -> URL {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("citylimits-edit-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        return folder
    }

    @Test(arguments: try FileManager.default.contentsOfDirectory(atPath: ScenarioTests.builtIn.path).filter { $0.hasSuffix(".json") }.sorted())
    func everyBuiltInScenarioWritesBackAsItReads(name: String) throws {
        let file = ScenarioTests.builtIn.appendingPathComponent(name)
        let scenario = try ScenarioLoader.load(file)
        let text = try ScenarioWriter.text(scenario, folder: ScenarioTests.builtIn)
        #expect(try ScenarioLoader.parse(text, folder: ScenarioTests.builtIn) == scenario)
    }

    @Test func theDemoTownWritesBackAsItReads() throws {
        let text = try ScenarioWriter.text(DemoTown.scenario, folder: URL(fileURLWithPath: "/"))
        #expect(try ScenarioLoader.parse(text) == DemoTown.scenario)
    }

    @Test func aNewScenarioIsSavedWithItsCityAndReadsBack() throws {
        let folder = try Self.scratch()
        defer { try? FileManager.default.removeItem(at: folder) }
        let edit = try ScenarioEdit.new(name: "River Run", seed: 7, saveFolder: folder)
        let city = try edit.openCity()
        #expect(city.builder.buildsForFree)
        #expect(city.name == "River Run")
        #expect(edit.hasChanges(city))
        #expect(edit.scenario.isDraft)

        try edit.save(city)
        #expect(!edit.hasChanges(city))
        #expect(edit.file.lastPathComponent == "River Run.json")
        #expect(FileManager.default.fileExists(atPath: folder.appendingPathComponent("River Run.fcl").path))

        // It opens again from its own file, starting from the city saved beside it.
        let again = try ScenarioEdit.open(edit.file, saveFolder: folder)
        #expect(again.scenario.cityFile?.lastPathComponent == "River Run.fcl")
        #expect(again.scenario.ticks == edit.scenario.ticks)
        let reopened = try again.openCity()
        #expect(!again.hasChanges(reopened))
    }

    @Test func detailsChangeTheScenarioOnlyWhenAccepted() throws {
        let folder = try Self.scratch()
        defer { try? FileManager.default.removeItem(at: folder) }
        let edit = try ScenarioEdit.new(name: "Harbour", seed: 3, saveFolder: folder)
        let city = try edit.openCity()

        var details = ScenarioDetails(of: edit.scenario)
        details.name = "Harbour Town"
        details.description = "  Grow the harbour.  "
        details.startYear = 1950
        details.ends = ScenarioDate(year: 1960, month: 6)
        details.funds = 50_000
        details.goals = [GoalDetails(measure: .population, atLeast: 5000), GoalDetails(measure: .funds, atLeast: 0, text: "Stay solvent")]
        details.events = [EventDetails(at: ScenarioDate(year: 1952, month: 3, week: 2), disaster: .flood, repeatWeeks: 48, repeatTimes: 3)]
        let changed = try edit.change(details, city: city)
        #expect(edit.scenario.name == "Harbour")

        edit.accept(changed, city: city)
        #expect(edit.scenario.name == "Harbour Town")
        #expect(edit.scenario.description == "Grow the harbour.")
        #expect(edit.scenario.goals.map(\.text) == ["Have a population of at least 5,000", "Stay solvent"])
        #expect(edit.scenario.strikes.count == 3)
        #expect(city.foundedYear == 1950 && city.treasury.funds == 50_000)
        #expect(ScenarioDetails(of: edit.scenario) == ScenarioDetails(
            name: "Harbour Town", description: "Grow the harbour.", startYear: 1950, ends: ScenarioDate(year: 1960, month: 6), funds: 50_000,
            goals: [GoalDetails(measure: .population, atLeast: 5000), GoalDetails(measure: .funds, atLeast: 0, text: "Stay solvent")],
            events: details.events))
    }

    @Test func detailsAFileWouldRefuseAreRefused() throws {
        let edit = try ScenarioEdit.new(name: "Short", seed: 1, saveFolder: try Self.scratch())
        defer { try? FileManager.default.removeItem(at: edit.saveFolder) }
        let city = try edit.openCity()
        var details = ScenarioDetails(of: edit.scenario)
        details.goals = [GoalDetails(measure: .population, atLeast: 10, atMost: 5)]
        #expect(throws: FormatError.self) { try edit.change(details, city: city) }
        details.goals = []
        details.ends = ScenarioDate(year: 1899 + 1, month: 1)
        details.startYear = 1950
        #expect(throws: FormatError.self) { try edit.change(details, city: city) }
    }

    @Test func movingTheStartMovesTheStrokesTheEditorDoesntShow() throws {
        let edit = ScenarioEdit(scenario: DemoTown.scenario, file: URL(fileURLWithPath: "/demo-town.json"), saveFolder: URL(fileURLWithPath: "/"))
        let city = try edit.openCity()
        var details = ScenarioDetails(of: edit.scenario)
        details.startYear = 1910
        details.ends = ScenarioDate(year: 1912, month: 1)
        let changed = try edit.change(details, city: city)
        #expect(changed.actions.map(\.tick) == DemoTown.scenario.actions.map(\.tick))
        #expect(changed.date(at: changed.actions.last?.tick ?? 0).year == 1911)
    }

    @Test func aSavedCitysStartIsItsOwnOnceItHasRun() throws {
        let folder = try Self.scratch()
        defer { try? FileManager.default.removeItem(at: folder) }
        let first = try ScenarioEdit.new(name: "Settled", seed: 5, saveFolder: folder)
        try first.save(try first.openCity())
        let edit = try ScenarioEdit.open(first.file, saveFolder: folder)
        let city = try edit.openCity()
        #expect(edit.canMoveStart(city))
        city.run(steps: CityCalendar.stepsPerCount)
        #expect(!edit.canMoveStart(city))
        var details = ScenarioDetails(of: edit.scenario)
        details.funds += 1000
        #expect(throws: FormatError.self) { try edit.change(details, city: city) }
    }

    @Test func aTestPutsTheCityBackAsItWas() throws {
        let edit = try ScenarioEdit.new(name: "Trial", seed: 2000, saveFolder: try Self.scratch())
        defer { try? FileManager.default.removeItem(at: edit.saveFolder) }
        let city = try edit.openCity()
        edit.beginTest(city)
        #expect(edit.isTesting)
        #expect(city.challenge != nil)
        #expect(throws: FormatError.self) { try edit.save(city) }
        city.builder.apply(BuildStroke(.road, 40, 50))
        city.run(steps: 100)

        let back = try edit.endTest()
        #expect(!edit.isTesting)
        #expect(back.tickCount == 0)
        #expect(back.challenge == nil)
        #expect(!back.map[40, 50].isRoad)
        #expect(back.builder.buildsForFree)
        #expect(throws: FormatError.self) { try edit.endTest() }
    }
}
