import Logging

/// The core's log, through swift-log's API. The program the core runs in decides where it goes:
/// the game and the command line send it to stderr, and a test leaves it at swift-log's default.
let coreLog = Logger(label: "citylimits.core")
