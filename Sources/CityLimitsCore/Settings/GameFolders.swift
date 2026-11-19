import Foundation

/// Where the game keeps its files: settings and the base game's `common/` folder in the config
/// folder, and cities, mods, and scenarios in `Documents/CityLimits`. A portable copy, one with a
/// `settings.json` beside the program, keeps them all beside it.
public struct GameFolders: Sendable {
    public static let folderName = "CityLimits"
    public static let settingsFileName = "settings.json"

    public var settingsFile: URL
    public var commonFolder: URL
    public var modsFolder: URL
    public var citiesFolder: URL
    public var scenariosFolder: URL
    public var isPortable: Bool
    /// Why the folders aren't where they'd usually be, if they aren't.
    public var problem: String?

    /// Every folder beside the program, as a portable copy keeps them.
    public static func beside(_ folder: URL, problem: String? = nil) -> GameFolders {
        GameFolders(
            settingsFile: folder.appendingPathComponent(settingsFileName), commonFolder: folder.appendingPathComponent("common"),
            modsFolder: folder.appendingPathComponent("Mods"), citiesFolder: folder.appendingPathComponent("Cities"),
            scenariosFolder: folder.appendingPathComponent("Scenarios"), isPortable: true, problem: problem)
    }

    /// The folders for this machine: portable when there's a settings file beside the program,
    /// otherwise the XDG config folder and the Documents folder.
    public static func detect(programFolder: URL, environment: [String: String] = ProcessInfo.processInfo.environment) -> GameFolders {
        if FileManager.default.fileExists(atPath: programFolder.appendingPathComponent(settingsFileName).path) {
            return beside(programFolder)
        }
        guard let home = environment["HOME"], home.hasPrefix("/") else {
            return beside(programFolder, problem: "The home folder couldn't be found, so CityLimits keeps its files beside the game")
        }
        let homeURL = URL(fileURLWithPath: home)
        let config = environment["XDG_CONFIG_HOME"].flatMap { $0.hasPrefix("/") ? URL(fileURLWithPath: $0) : nil }
            ?? homeURL.appendingPathComponent(".config")
        let documents = (environment["XDG_DOCUMENTS_DIR"] ?? userDirectory("XDG_DOCUMENTS_DIR", config: config, home: home))
            .map { URL(fileURLWithPath: $0) } ?? homeURL.appendingPathComponent("Documents")
        let game = documents.appendingPathComponent(folderName)
        return GameFolders(
            settingsFile: config.appendingPathComponent(folderName).appendingPathComponent(settingsFileName),
            commonFolder: config.appendingPathComponent(folderName).appendingPathComponent("common"),
            modsFolder: game.appendingPathComponent("Mods"), citiesFolder: game.appendingPathComponent("Cities"),
            scenariosFolder: game.appendingPathComponent("Scenarios"), isPortable: false)
    }

    // The desktop's own name for a folder, from user-dirs.dirs, as `xdg-user-dir` reads it.
    private static func userDirectory(_ key: String, config: URL, home: String) -> String? {
        guard let text = try? String(contentsOf: config.appendingPathComponent("user-dirs.dirs"), encoding: .utf8) else { return nil }
        for line in text.split(separator: "\n") where line.hasPrefix("\(key)=") {
            let value = line.dropFirst(key.count + 1).trimmingCharacters(in: CharacterSet(charactersIn: "\""))
            let path = value.replacingOccurrences(of: "$HOME", with: home)
            return path.hasPrefix("/") && path != home ? path : nil
        }
        return nil
    }

    /// The base game with `common/` written for this build and read back, then the mods applied.
    public func definitions(build: String) -> (definitions: CityDefinitions, problems: [String]) {
        var problems: [String] = []
        do throws(FormatError) {
            try SpecFolders.refreshBase(commonFolder, build: build)
        } catch {
            problems.append(error.message)
        }
        let base = SpecFolders.loadBase(commonFolder)
        if let problem = base.problem { problems.append(problem) }
        let mods = SpecFolders.loadMods(modsFolder, onto: base.definitions)
        return (mods.definitions, problems + mods.problems)
    }
}
