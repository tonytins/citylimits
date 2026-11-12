import Foundation
import Testing

@testable import CityLimitsCore

struct ScenarioTests {
    /// The game's built-in scenarios, beside the game's sources.
    static let builtIn = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        .appendingPathComponent("Sources/CityLimits/Resources/Scenarios")

    static let catalog = ScenarioCatalog(folders: [builtIn])

    @Test func theCatalogListsTheDemoTownFirst() {
        #expect(Self.catalog.ids() == [
            "demo-town", "aftershock", "bridges", "first-town", "hard-times", "rail", "small-town", "smokestack", "zones-and-bulldozer",
        ])
    }

    // Each built-in scenario, run here, reports exactly what the C# build's run reported
    // (Fixtures/scenario-reports-0.5, from legacy/csharp on the same files).
    @Test(arguments: ["demo-town", "aftershock", "bridges", "first-town", "hard-times", "rail", "small-town", "smokestack", "zones-and-bulldozer"])
    func aBuiltInScenarioRunsAsItDidInTheCSharpBuild(id: String) throws {
        let report = try ScenarioRunner().run(Self.catalog.load(id), source: id)
        let expected = try JSON.parse(String(contentsOf: Fixtures.url("scenario-reports-0.5/\(id).json"), encoding: .utf8))
        #expect(report.passed)
        #expect(report.json() == expected)
        if report.json() != expected {
            // The difference is easier to read as text.
            #expect(report.json().text() == expected.text())
        }
    }

    @Test func aChallengeIsJudgedAtItsEndAndSurvivesASave() throws {
        let scenario = try Self.catalog.load("first-town")
        let city = try CitySimulation.begin(scenario)
        #expect(city.name == "First Town")
        var finished: (won: Bool, goals: [GoalReport])?
        city.onEvent = { if case .challengeFinished(let won, let goals) = $0 { finished = (won, goals) } }

        city.run(steps: scenario.ticks / 2)
        let saved = try CityFiles.read(binary: CityFiles.binaryBytes(city)).city
        #expect(saved.challenge?.name == "First Town")
        #expect(saved.challenge?.endStep == city.challenge?.endStep)
        #expect(CityFiles.flatText(saved) == CityFiles.flatText(city))

        city.run(steps: scenario.ticks / 2 + 1)
        #expect(finished?.won == false)
        #expect(finished?.goals.count == scenario.goals.count)
        #expect(city.challenge?.won == false)
        // The city plays on after, and the goals are judged once.
        finished = nil
        city.run(steps: 100)
        #expect(finished == nil)

        // The loaded city reaches the same end.
        saved.run(steps: scenario.ticks / 2 + 1 + 100)
        #expect(CityFiles.flatText(saved) == CityFiles.flatText(city))
    }

    @Test func challengesAreListedAndTestsAreNot() {
        let listed = Self.catalog.challenges().compactMap { $0.scenario?.name }
        #expect(listed.sorted() == ["Aftershock", "First Town", "Hard Times", "Smokestack"])
    }

    private static func scenario(_ fields: String) -> String {
        #"{ "format": "3.0", "name": "Test", "seed": 1, "ends": "1901-01"\#(fields) }"#
    }

    @Test(arguments: [
        (#"{ "format": "2.2", "name": "Old", "seed": 1, "ends": "1901-01" }"#,
         "format: this is format 2.2; this version of CityLimits reads 3.x. A scenario from 0.4 can be opened in 0.5's editor and saved again there."),
        (scenario(#", "cheats": true"#), "cheats: is not a field a scenario has."),
        (scenario(#", "actions": [ { "at": "1902-01", "tool": "Road", "from": { "x": 1, "y": 1 } } ]"#),
         "actions[0].at: 1902-01 is outside the scenario, which runs from 1900-01 to 1901-01."),
        (scenario(#", "actions": [ { "at": "1900-01", "tool": "Highway", "from": { "x": 1, "y": 1 } } ]"#),
         "actions[0].tool: 'Highway' is not one of Road, ZoneResidential, ZoneCommercial, ZoneIndustrial, Bulldoze, CoalPowerPlant, NuclearPowerPlant, PowerLine, FireStation, PoliceStation, Park, Rail, Stadium, Seaport, Airport."),
        (scenario(#", "actions": [ { "at": "1900-01", "tool": "Road", "from": { "x": 5000, "y": 1 } } ]"#),
         "actions[0].from.x: needs a number from -512 to 1023, not 5000."),
        (scenario(#", "actions": [ { "at": "1900-01", "tool": "Road", "from": { "x": 1, "y": 1 }, "expect": "Built:OffTheMap" } ]"#),
         "actions[0].expect: a stroke that was built has no reason, so 'Built:OffTheMap' can never be met."),
        (scenario(#", "checks": [ { "at": "1900-01", "tile": { "x": 500, "y": 1 }, "use": "Road" } ]"#),
         "checks[0].tile: (500, 1) is outside the 120 by 100 map."),
        (scenario(#", "checks": [ { "at": "1900-01", "count": "Road", "fundsEquals": 5 } ]"#),
         "checks[0]: a count check takes only equals, atLeast, or atMost."),
        (scenario(#", "checks": [ { "at": "1900-01", "week": 5, "fundsEquals": 5 } ]"#),
         "checks[0].week: needs a number from 1 to 4, not 5."),
        (scenario(#", "goals": [ { "measure": "Population", "atLeast": 10, "atMost": 5 } ]"#),
         "goals[0]: atLeast 10 is more than atMost 5, so it can never be met."),
        (scenario(#", "events": [ { "at": "1900-06", "disaster": "Fire", "repeat": { "weeks": 10, "times": 10 } } ]"#),
         "events[0].repeat: striking 10 times, 10 weeks apart, runs past the scenario's end, 1901-01."),
        (scenario(#", "city": "town.fcl""#), "seed: a scenario that starts from a saved city takes its seed, map, money, and date from the city, so it can't also have seed."),
    ])
    func aBadScenarioIsRefusedSayingWhere(text: String, message: String) {
        #expect(throws: FormatError(message)) { try ScenarioLoader.parse(text) }
    }

    @Test func aScenarioCantReadACityOutsideItsFolder() throws {
        try Fixtures.withTemporaryFolder { folder in
            let file = folder.appendingPathComponent("escape.json")
            try #"{ "format": "3.0", "name": "Escape", "city": "../elsewhere.fcl", "ends": "1901-01" }"#.write(to: file, atomically: true, encoding: .utf8)
            #expect(throws: FormatError("city: '../elsewhere.fcl' needs to be a file beside the scenario, or in a folder beside it.")) {
                try ScenarioLoader.load(file)
            }
        }
    }

    @Test func goalsAreWordedFromTheirMeasure() {
        #expect(ScenarioGoal.text(.population, atLeast: 2000, atMost: nil) == "Have a population of at least 2,000")
        #expect(ScenarioGoal.text(.crime, atLeast: nil, atMost: 60) == "Keep average crime at most 60")
        #expect(ScenarioGoal.text(.funds, atLeast: -500, atMost: 1000) == "Have funds from -$500 to $1,000")
    }

    @Test func aRunCanStopEarlyButNotBeforeItsLastAction() throws {
        let demo = DemoTown.scenario
        #expect(throws: FormatError.self) { try ScenarioRunner().run(demo, source: "demo-town", ticks: 10) }
        let report = try ScenarioRunner().run(demo, source: "demo-town", ticks: demo.ticks)
        #expect(report.finalTick == demo.ticks)
    }
}
