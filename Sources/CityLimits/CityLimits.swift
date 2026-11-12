import Foundation
import Raylib

@main
struct CityLimits {
    static let usage = """
        Usage: citylimits [--version] [--help]

        Opens the game window. The headless commands from the C# build return as the port goes on.
        """

    static func main() {
        let arguments = Array(CommandLine.arguments.dropFirst())
        switch arguments.first {
        case nil:
            runWindow()
        case "--version":
            print("CityLimits \(GitVersion.version)")
        case "--help", "-h":
            print(usage)
        case let other?:
            FileHandle.standardError.write(Data("citylimits: unknown option '\(other)'\n\n\(usage)\n".utf8))
            exit(2)
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
