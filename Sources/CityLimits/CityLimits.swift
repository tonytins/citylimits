import CityLimitsCore
import Foundation
import Raylib

@main
@MainActor
struct CityLimits {
    static func main() {
        let programFolder = Bundle.main.executableURL?.deletingLastPathComponent() ?? URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let scenarios = Bundle.module.url(forResource: "Scenarios", withExtension: nil) ?? programFolder.appendingPathComponent("Scenarios")
        let runner = CommandLineRunner(
            version: GitVersion.version, folders: GameFolders.detect(programFolder: programFolder), builtInScenarios: scenarios,
            output: { print($0) },
            error: { FileHandle.standardError.write(Data(($0 + "\n").utf8)) })

        switch runner.run(Array(CommandLine.arguments.dropFirst())) {
        case .exit(let code):
            exit(code)
        case .window(let seed):
            runWindow(seed: seed ?? CommandLineRunner.defaultSeed, assetsFolder: Bundle.module.url(forResource: "Assets", withExtension: nil))
        }
    }

    /// Opens the window and plays a new city grown from the seed until the window closes.
    static func runWindow(seed: Int32, assetsFolder: URL?) {
        SetConfigFlags([.vsyncHint, .windowResizable])
        InitWindow(1280, 800, "CityLimits")
        guard IsWindowReady() else {
            FileHandle.standardError.write(Data("citylimits: the game window couldn't be opened\n".utf8))
            exit(1)
        }
        SetWindowMinSize(800, 600)
        SetTargetFPS(60)
        // Escape cancels a stroke, so it can't also close the game.
        SetExitKey(.null)

        let assets = GameAssets(folder: assetsFolder ?? URL(fileURLWithPath: "Assets"))
        for problem in assets.problems {
            FileHandle.standardError.write(Data("warning: \(problem)\n".utf8))
        }
        let city = CitySimulation(map: TerrainGenerator.generate(seed: seed), seed: seed)
        let session = PlaySession(city: city, assets: assets)
        while !WindowShouldClose() {
            session.update()
            session.draw()
        }
        session.unload()
        assets.unload()
        CloseWindow()
    }
}
