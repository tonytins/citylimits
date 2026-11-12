import Foundation
import Testing

@testable import CityLimitsCore

struct MicropolisImportTests {
    // Bridgeport, a city Tony Bark built, in Micropolis' format. The C# build imported it as
    // Bridgeport-imported-0.5.fcl, and a year on it was at step 3888 with these funds, people, and map.
    @Test func bridgeportImportsAsItDidInTheCSharpBuild() throws {
        let imported = try CityFiles.read(Fixtures.url("Bridgeport.cty"))
        #expect(imported.notes.isEmpty)
        let city = imported.city
        let fromCSharp = try CityFiles.read(Fixtures.url("Bridgeport-imported-0.5.fcl")).city
        #expect(CityFiles.flatText(city) == CityFiles.flatText(fromCSharp))
        city.run(steps: CityCalendar.stepsPerYear)
        #expect(city.name == "Bridgeport")
        #expect(city.tickCount == 3888)
        #expect(city.treasury.funds == 9946)
        #expect(city.census.cityPopulation == 4400)
        #expect(SimulationParityTests.hash(city.map) == 0xA0E7_C265_2EA4_8F64)
    }

    @Test func aFileOfTheWrongSizeIsRefused() {
        #expect(throws: FormatError("A Micropolis city is 27120 bytes, or 27248 with a header, not 10.")) {
            try MicropolisImport.read(Array(repeating: 0, count: 10), name: "Tiny")
        }
    }

    @Test func aBlankFileImportsAsBareGround() throws {
        let city = try MicropolisImport.read(Array(repeating: 0, count: MicropolisImport.fileBytes), name: "  ").city
        #expect(city.name == "Micropolis city")
        #expect(city.map.width == 120 && city.map[60, 50] == .grass)
    }
}

/// Runs the command line as a script would, keeping what it writes.
final class Console {
    var output: [String] = []
    var error: [String] = []

    func runner(folders: GameFolders) -> CommandLineRunner {
        CommandLineRunner(
            version: "0.6.100-test", folders: folders, builtInScenarios: ScenarioTests.builtIn,
            output: { self.output.append($0) }, error: { self.error.append($0) })
    }
}

struct CommandLineTests {
    private func run(_ arguments: String, in folder: URL = FileManager.default.temporaryDirectory) -> (Invocation, Console) {
        let console = Console()
        let invocation = console.runner(folders: .beside(folder)).run(arguments.split(separator: " ").map(String.init))
        return (invocation, console)
    }

    @Test(arguments: [
        ("", Invocation.window(seed: nil)),
        ("--seed 42", .window(seed: 42)),
        ("--help", .exit(ExitCode.success)),
        ("--version", .exit(ExitCode.success)),
        ("--ticks 5", .exit(ExitCode.usage)),
        ("--frobnicate", .exit(ExitCode.usage)),
        ("--seed lots", .exit(ExitCode.usage)),
        ("--headless --ticks 200000", .exit(ExitCode.usage)),
        ("build", .exit(ExitCode.usage)),
        ("scenario demo-town", .exit(ExitCode.usage)),
        ("scenario Demo_Town --headless", .exit(ExitCode.usage)),
        ("scenario demo-town --file x.json --headless", .exit(ExitCode.usage)),
        ("scenario list --json", .exit(ExitCode.usage)),
        ("scenario no-such-thing --headless", .exit(ExitCode.failure)),
        ("scenario demo-town --headless --ticks 10", .exit(ExitCode.usage)),
    ])
    func theCommandLineIsChecked(arguments: String, expected: Invocation) {
        #expect(run(arguments).0 == expected)
    }

    @Test func theVersionIsPrinted() {
        #expect(run("--version").1.output == ["CityLimits 0.6.100-test"])
    }

    @Test func scenariosAreListed() {
        let (invocation, console) = run("scenario list")
        #expect(invocation == .exit(ExitCode.success))
        #expect(console.output.first == "demo-town                Demo town")
        #expect(console.output.contains("first-town               First Town (a challenge)"))
    }

    @Test func aScenarioWritesItsReportAsJSONAndItsLogElsewhere() throws {
        let (invocation, console) = run("scenario first-town --headless --json")
        #expect(invocation == .exit(ExitCode.success))
        #expect(console.output.count == 1)
        let report = try JSON.parse(console.output[0])
        let expected = try JSON.parse(String(contentsOf: Fixtures.url("scenario-reports-0.5/first-town.json"), encoding: .utf8))
        #expect(report == expected)
        #expect(console.error.last?.hasPrefix("Scenario passed:") == true)
    }

    @Test func aFailedCheckExitsWithThree() throws {
        try Fixtures.withTemporaryFolder { folder in
            let file = folder.appendingPathComponent("wrong.json")
            try #"{ "format": "3.0", "name": "Wrong", "seed": 1, "ends": "1900-02", "checks": [ { "at": "1900-02", "fundsEquals": 1 } ] }"#
                .write(to: file, atomically: true, encoding: .utf8)
            let (invocation, console) = run("scenario --file \(file.path) --headless")
            #expect(invocation == .exit(ExitCode.checksFailed))
            #expect(console.output.contains { $0.hasPrefix("warning: check 0 at 1900-02, tick 64: FAILED, expected funds equal 1") })
        }
    }

    // The same run as the C# build's `citylimits --headless --ticks 1536`, whose last lines these are.
    @Test func aHeadlessRunGrowsTheDemoTownAsTheCSharpBuildDid() {
        Fixtures.withTemporaryFolder { folder in
            let (invocation, console) = run("--headless --ticks 1536", in: folder)
            #expect(invocation == .exit(ExitCode.success))
            #expect(console.output.contains("Built a coal plant, a power line, a road, and three zones at 70, 43"))
            #expect(console.output.last == "Headless run finished: 1536 ticks, 1902-01, 9 road tiles, 3 zones, 2 developed, $16560")
            // It wrote the base game's files for this build, and played with them.
            #expect(FileManager.default.fileExists(atPath: folder.appendingPathComponent("common/zones.json").path))
        }
    }
}

struct GameFolderTests {
    @Test func aPortableCopyKeepsEverythingBesideIt() throws {
        try Fixtures.withTemporaryFolder { folder in
            try "{}".write(to: folder.appendingPathComponent("settings.json"), atomically: true, encoding: .utf8)
            let folders = GameFolders.detect(programFolder: folder, environment: ["HOME": "/home/someone"])
            #expect(folders.isPortable)
            #expect(folders.citiesFolder == folder.appendingPathComponent("Cities"))
        }
    }

    @Test func theDesktopsFoldersAreUsedOtherwise() throws {
        try Fixtures.withTemporaryFolder { folder in
            let config = folder.appendingPathComponent("config")
            try FileManager.default.createDirectory(at: config, withIntermediateDirectories: true)
            try #"XDG_DOCUMENTS_DIR="$HOME/Dokumente""#.write(to: config.appendingPathComponent("user-dirs.dirs"), atomically: true, encoding: .utf8)
            let folders = GameFolders.detect(programFolder: folder, environment: ["HOME": "/home/someone", "XDG_CONFIG_HOME": config.path])
            #expect(!folders.isPortable)
            #expect(folders.citiesFolder.path == "/home/someone/Dokumente/CityLimits/Cities")
            #expect(folders.commonFolder == config.appendingPathComponent("CityLimits/common"))
            let plain = GameFolders.detect(programFolder: folder, environment: ["HOME": "/home/someone"])
            #expect(plain.modsFolder.path == "/home/someone/Documents/CityLimits/Mods")
            #expect(plain.settingsFile.path == "/home/someone/.config/CityLimits/settings.json")
        }
    }

    @Test func messagesRepeatAtMostOnceAYear() {
        var filter = CityMessageFilter()
        let passed = [(CityMessage.needPower, 0), (.needPower, 47), (.needPower, 48), (.highCrime, 49)].map { filter.shouldPass($0.0, cityTime: $0.1) }
        #expect(passed == [true, false, true, true])
    }
}
