import CityLimitsCore
import Foundation
import Raylib

/// The images the game draws with, loaded once after the window opens and unloaded before it
/// closes. A missing image is reported, and the game draws without it: the map in flat colours,
/// a tool by its label, a sprite not at all.
@MainActor
final class GameAssets {
    private(set) var tileSheet: Texture2D?
    private(set) var logo: Texture2D?
    private var icons: [Tool: Texture2D] = [:]
    private var selectedIcons: [Tool: Texture2D] = [:]
    private var train: [Texture2D?] = Array(repeating: nil, count: 5)
    // Micropolis' sprites: frame n of each is drawing n - 1.
    private var plane: [Texture2D?] = Array(repeating: nil, count: 11)
    private var helicopter: [Texture2D?] = Array(repeating: nil, count: 8)
    private var ship: [Texture2D?] = Array(repeating: nil, count: 8)
    private var monster: [Texture2D?] = Array(repeating: nil, count: 16)
    private var tornado: [Texture2D?] = Array(repeating: nil, count: 3)
    private var loaded: [Texture2D] = []

    /// What couldn't be loaded, for the log and the news ticker.
    private(set) var problems: [String] = []

    /// Loads everything from `folder`. Call after `InitWindow`.
    init(folder: URL) {
        tileSheet = load(folder.appendingPathComponent("tiles.png"), "The tile sheet")
        logo = load(folder.appendingPathComponent("logo.png"), "The logo")
        let tools = folder.appendingPathComponent("tools")
        for tool in Tool.allCases {
            icons[tool] = load(tools.appendingPathComponent("\(tool.iconName).png"), "The \(tool.name) tool's icon")
            selectedIcons[tool] = load(tools.appendingPathComponent("\(tool.iconName)hi.png"), "The \(tool.name) tool's highlighted icon")
        }
        let sprites = folder.appendingPathComponent("sprites")
        func frames(_ prefix: String, _ count: Int, _ name: String) -> [Texture2D?] {
            (0..<count).map { load(sprites.appendingPathComponent("\(prefix)\($0).png"), "The \(name)'s drawing \($0)") }
        }
        train = frames("obj1-", 5, "train")
        plane = frames("obj3-", 11, "plane")
        helicopter = frames("obj2-", 8, "helicopter")
        ship = frames("obj4-", 8, "ship")
        monster = frames("obj5-", 16, "monster")
        tornado = frames("obj6-", 3, "tornado")
    }

    private func load(_ file: URL, _ what: String) -> Texture2D? {
        guard FileManager.default.fileExists(atPath: file.path) else {
            problems.append("\(what) is missing: \(file.path)")
            return nil
        }
        let texture = LoadTexture(file.path)
        guard IsTextureValid(texture) else {
            problems.append("\(what) couldn't be loaded: \(file.path)")
            return nil
        }
        // Pixel art stays sharp when the camera zooms.
        SetTextureFilter(texture, .point)
        loaded.append(texture)
        return texture
    }

    func icon(_ tool: Tool, selected: Bool) -> Texture2D? {
        selected ? selectedIcons[tool] ?? icons[tool] : icons[tool]
    }

    func trainFrame(_ frame: TrainFrame) -> Texture2D? {
        let index = switch frame {
        case .down: 0
        case .across: 1
        case .turningNorthwest: 2
        case .turningNortheast: 3
        case .tunnel: 4
        }
        return train[index]
    }

    private func frame(_ frames: [Texture2D?], _ number: Int) -> Texture2D? {
        (1...frames.count).contains(number) ? frames[number - 1] : nil
    }

    func planeFrame(_ number: Int) -> Texture2D? { frame(plane, number) }
    func helicopterFrame(_ number: Int) -> Texture2D? { frame(helicopter, number) }
    func shipFrame(_ number: Int) -> Texture2D? { frame(ship, number) }
    func monsterFrame(_ number: Int) -> Texture2D? { frame(monster, number) }
    func tornadoFrame(_ number: Int) -> Texture2D? { frame(tornado, number) }

    /// Releases every texture. Call before `CloseWindow`.
    func unload() {
        for texture in loaded { UnloadTexture(texture) }
        loaded.removeAll()
        tileSheet = nil
        logo = nil
        icons.removeAll()
        selectedIcons.removeAll()
        for index in train.indices { train[index] = nil }
        plane = plane.map { _ in nil }
        helicopter = helicopter.map { _ in nil }
        ship = ship.map { _ in nil }
        monster = monster.map { _ in nil }
        tornado = tornado.map { _ in nil }
    }
}
