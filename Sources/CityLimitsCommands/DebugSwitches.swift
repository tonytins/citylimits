import Configuration
import Foundation
import Logging

/// Switches for debugging, read through swift-configuration from the environment. They're for
/// people working on CityLimits, never for players: the player's own choices live in
/// `settings.json`. Each key reads as an environment variable, `citylimits.logLevel` as
/// `CITYLIMITS_LOG_LEVEL`.
public struct DebugSwitches: Sendable {
    /// How much swift-log's log says: trace, debug, info (the default), notice, warning, error,
    /// or critical.
    public var logLevel = Logger.Level.info
    /// Switches given a value they can't take, each said in a line, so none is quietly ignored.
    public var problems: [String] = []

    /// Reads the switches from `environment`, the process's own unless a test gives one.
    public init(environment: [String: String] = ProcessInfo.processInfo.environment) {
        let config = ConfigReader(provider: EnvironmentVariablesProvider(environmentVariables: environment))
        if let text = config.string(forKey: ["citylimits", "logLevel"]) {
            if let level = Logger.Level(rawValue: text.lowercased()) {
                logLevel = level
            } else {
                let levels = Logger.Level.allCases.map(\.rawValue).joined(separator: ", ")
                problems.append("CITYLIMITS_LOG_LEVEL is '\(text)', not one of \(levels), so the log stays at info.")
            }
        }
    }

    /// Sends swift-log's log to stderr at the switches' level, and says which switches were
    /// refused. Call once, before anything logs.
    public func bootstrapLogging() {
        let level = logLevel
        LoggingSystem.bootstrap { label in
            var handler = StreamLogHandler.standardError(label: label)
            handler.logLevel = level
            return handler
        }
        let logger = Logger(label: "citylimits.debug")
        for problem in problems { logger.warning("\(problem)") }
    }
}
