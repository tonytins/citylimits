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

        let folders = GameFolders.detect(programFolder: programFolder)
        switch runner.run(Array(CommandLine.arguments.dropFirst())) {
        case .exit(let code):
            exit(code)
        case .window(let seed):
            runWindow(
                seed: seed, folders: folders, builtInScenarios: scenarios,
                assetsFolder: Bundle.module.url(forResource: "Assets", withExtension: nil))
        }
    }

    /// Opens the window: the loading screen, then the start menu, and the cities chosen from it,
    /// until the player quits or closes the window. A seed from the command line fills in New
    /// Game's seed.
    static func runWindow(seed: Int32?, folders: GameFolders, builtInScenarios: URL, assetsFolder: URL?) {
        if let problem = folders.problem { warn(problem) }
        for folder in [folders.modsFolder, folders.scenariosFolder] {
            do {
                try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            } catch {
                warn("\(folder.path) couldn't be made: \(error.localizedDescription)")
            }
        }
        let settings = SettingsKeeper(file: folders.settingsFile)

        SetConfigFlags([.vsyncHint, .windowResizable])
        InitWindow(Int32(settings.settings.windowWidth), Int32(settings.settings.windowHeight), "CityLimits")
        guard IsWindowReady() else {
            FileHandle.standardError.write(Data("citylimits: the game window couldn't be opened\n".utf8))
            exit(1)
        }
        SetWindowMinSize(800, 600)
        SetTargetFPS(60)
        // Escape cancels a stroke or opens the menu, so it can't also close the game.
        SetExitKey(.null)
        // Fullscreen is read at the start only; the settings say it waits for the next one.
        let fullscreen = settings.settings.fullscreen
        if fullscreen { ToggleBorderlessWindowed() }

        let assets = GameAssets(folder: assetsFolder ?? URL(fileURLWithPath: "Assets"))
        for problem in assets.problems { warn(problem) }

        let loading = LoadingScreen(assets: assets)
        while !loading.isDone && !WindowShouldClose() {
            loading.update()
            loading.draw()
        }

        // Saved cities are read with the definitions a new city would get, mods included.
        let definitions = { folders.definitions(build: GitVersion.version).definitions }
        let menu = StartMenu(
            assets: assets, version: GitVersion.version, settings: settings,
            load: LoadWindow(folder: folders.citiesFolder, definitions: definitions),
            // The game's own challenges, then the player's.
            scenarios: ScenarioWindow(folders: [builtInScenarios, folders.scenariosFolder]), scenariosFolder: folders.scenariosFolder)
        menu.show(fromLoading: true, seed: seed)
        menus: while !WindowShouldClose() {
            var playing: (city: CitySimulation, file: URL?, notes: [String])
            switch menu.run() {
            case .quit:
                break menus
            case .newCity(let name, let wanted):
                // Picking a seed is outside the simulation, so the system's random numbers may
                // choose it; the line printed keeps it, so the city can be grown again with --seed.
                let chosen = wanted ?? Int32.random(in: 0...Int32.max)
                print("A new city, \(name), from seed \(chosen)")
                let (definitions, problems) = folders.definitions(build: GitVersion.version)
                let city = CitySimulation(map: TerrainGenerator.generate(seed: chosen), seed: chosen, definitions: definitions)
                city.rename(name)
                for problem in problems { warn(problem) }
                let note = problems.count == 1 ? problems[0] : "\(problems.count) mods were skipped; the log says why"
                playing = (city, nil, problems.isEmpty ? [] : [note])
            case .loadCity(let loaded, let file):
                print("Playing \(loaded.city.name) from \(file.path)")
                playing = (loaded.city, file, loaded.notes)
            case .playChallenge(let city):
                print("Playing the challenge \(city.challenge?.name ?? city.name)")
                playing = (city, nil, [])
            case .editScenario(let edit, let city):
                print("Editing the scenario \(edit.scenario.name) from \(edit.file.path)")
                let session = EditorSession(edit: edit, city: city, assets: assets, settings: settings)
                while !WindowShouldClose() && session.leaving == nil {
                    session.update()
                    session.draw()
                }
                let leave = session.leaving
                session.unload()
                guard leave == .toMenu else { break menus }
                menu.show(fromLoading: false, seed: seed)
                continue menus
            }

            // A city plays until the player leaves it, and then any city they open from it.
            cities: while true {
                let session = PlaySession(
                    city: playing.city, file: playing.file, assets: assets, settings: settings, citiesFolder: folders.citiesFolder,
                    definitions: definitions)
                for note in playing.notes { session.advise(note) }
                while !WindowShouldClose() && session.leaving == nil {
                    session.update()
                    session.draw()
                }
                let leave = session.leaving
                session.unload()
                switch leave {
                case .open(let loaded, let file):
                    print("Playing \(loaded.city.name) from \(file.path)")
                    playing = (loaded.city, file, loaded.notes)
                case .toMenu:
                    break cities
                case .quit, nil:
                    break menus
                }
            }
            menu.show(fromLoading: false, seed: seed)
        }

        // The window's size is kept for next time, unless it filled the screen.
        if !fullscreen {
            settings.settings.windowWidth = min(max(Int(GetScreenWidth()), GameSettings.windowWidths.lowerBound), GameSettings.windowWidths.upperBound)
            settings.settings.windowHeight = min(max(Int(GetScreenHeight()), GameSettings.windowHeights.lowerBound), GameSettings.windowHeights.upperBound)
        }
        settings.save()
        menu.unload()
        assets.unload()
        CloseWindow()
    }
}
