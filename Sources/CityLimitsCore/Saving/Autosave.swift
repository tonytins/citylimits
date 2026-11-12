import Foundation

/// When a city autosaves and where to: every 15 minutes of play, into one of up to two slots
/// beside the city's own file, the oldest slot taking the next save.
public struct Autosave: Sendable {
    public static let intervalSeconds = 15.0 * 60
    private static let marker = " (autosave"

    /// Seconds played since the last autosave was due.
    public private(set) var playedSeconds = 0.0

    public init() {}

    /// Counts a frame's time while the city runs, and says when an autosave is due. Time paused,
    /// or with a window holding the city, doesn't count; a time that isn't a number of seconds is
    /// ignored.
    public mutating func advance(_ seconds: Double, running: Bool) -> Bool {
        guard running, seconds.isFinite, seconds >= 0 else { return false }
        playedSeconds += seconds
        guard playedSeconds >= Self.intervalSeconds else { return false }
        playedSeconds = 0
        return true
    }

    /// An autosave's file name: the city's own, marked as its first or second autosave. Returns nil
    /// for a name a city can't have, or a slot that doesn't exist.
    public static func fileName(for cityName: String, slot: Int, extension: String = CityFiles.binaryExtension) -> String? {
        guard GameSettings.autosaveSlotCounts.contains(slot), slot >= 1,
            let own = CityFiles.fileName(for: cityName, extension: `extension`)
        else { return nil }
        let stem = String(own.dropLast(`extension`.count + 1))
        return "\(stem)\(marker)\(slot == 1 ? ")" : " \(slot))").\(`extension`)"
    }

    /// The slot the next autosave goes in: the first that's free, or else the oldest.
    public static func nextFile(in folder: URL, cityName: String, slots: Int, extension: String = CityFiles.binaryExtension) -> URL? {
        let files = (1...max(1, min(slots, GameSettings.autosaveSlotCounts.upperBound))).compactMap { slot in
            fileName(for: cityName, slot: slot, extension: `extension`).map { folder.appendingPathComponent($0) }
        }
        if let free = files.first(where: { !FileManager.default.fileExists(atPath: $0.path) }) { return free }
        func modified(_ file: URL) -> Date {
            (try? file.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
        }
        return files.min { modified($0) < modified($1) }
    }

    public static func isAutosave(_ file: URL) -> Bool {
        file.deletingPathExtension().lastPathComponent.contains(marker)
    }

    /// The city's own file an autosave stands beside.
    public static func cityFile(for autosave: URL) -> URL {
        let name = autosave.deletingPathExtension().lastPathComponent
        let stem = name.range(of: marker, options: .backwards).map { String(name[..<$0.lowerBound]) } ?? name
        return autosave.deletingLastPathComponent().appendingPathComponent(stem).appendingPathExtension(autosave.pathExtension)
    }
}

extension CitySimulation {
    /// Whether saving is held off: while a disaster is under way, or anything burns, a save would
    /// keep the city mid-disaster, so neither the player nor the autosave saves until it's over.
    /// A disaster is under way while there's a fire or a flood, or a monster or tornado on the map.
    public var holdsSaving: Bool {
        hazards.fires.count > 0 || hazards.floods.count > 0 || menaces.monster != nil || menaces.tornado != nil
    }
}
