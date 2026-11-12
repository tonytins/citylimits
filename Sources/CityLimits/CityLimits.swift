import CityLimitsCore
import Foundation
import Raylib

@main
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
        case .window:
            runWindow()
        }
    }

    // A placeholder screen until play mode is ported: it proves the window, the frame, and the
    // version stamp work.
    static func runWindow() {
        SetConfigFlags([.vsyncHint, .windowResizable])
        InitWindow(1280, 800, "CityLimits")
        guard IsWindowReady() else {
            FileHandle.standardError.write(Data("citylimits: the game window couldn't be opened\n".utf8))
            exit(1)
        }
        SetWindowMinSize(800, 600)
        SetTargetFPS(60)

        let title = "CityLimits"
        let version = "Version \(GitVersion.version)"
        while !WindowShouldClose() {
            let width = GetScreenWidth()
            let height = GetScreenHeight()
            BeginDrawing()
            ClearBackground(Color(r: 24, g: 32, b: 40, a: 255))
            DrawText(title, (width - MeasureText(title, 48)) / 2, height / 2 - 40, 48, .rayWhite)
            DrawText(version, (width - MeasureText(version, 20)) / 2, height / 2 + 20, 20, .lightGray)
            EndDrawing()
        }
        CloseWindow()
    }
}
