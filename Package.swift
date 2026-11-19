// swift-tools-version: 6.3

import PackageDescription

let package = Package(
    name: "CityLimits",
    products: [
        .executable(name: "citylimits", targets: ["CityLimits"]),
        .library(name: "CityLimitsCore", targets: ["CityLimitsCore"]),
    ],
    dependencies: [
        .package(url: "https://github.com/tonytins/raylib-swift", branch: "main"),
        .package(url: "https://github.com/tonytins/Hudini.git", branch: "main"),
        .package(url: "https://github.com/tonytins/GitVersion.git", from: "0.3.0"),
        .package(url: "https://github.com/apple/swift-argument-parser.git", from: "1.8.0"),
        .package(url: "https://github.com/apple/swift-log.git", from: "1.17.0"),
        .package(url: "https://github.com/apple/swift-configuration.git", from: "1.5.0"),
    ],
    targets: [
        // The world and the simulation: plain Swift with no engine, so tests run without a window.
        // It logs through swift-log's API; the program it runs in decides where the log goes.
        .target(
            name: "CityLimitsCore",
            dependencies: [.product(name: "Logging", package: "swift-log")],
        ),
        // The command line, which tests run as a script does: everything that needs no window.
        .target(
            name: "CityLimitsCommands",
            dependencies: [
                "CityLimitsCore",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
                .product(name: "Logging", package: "swift-log"),
                .product(name: "Configuration", package: "swift-configuration"),
            ],
        ),
        // The game: the window, the sessions (play, and later the editor), and the drawing.
        .executableTarget(
            name: "CityLimits",
            dependencies: [
                "CityLimitsCore",
                "CityLimitsCommands",
                .product(name: "Logging", package: "swift-log"),
                .product(name: "Raylib", package: "raylib-swift"),
                .product(name: "Hudini", package: "Hudini"),
                .product(name: "GitVersion", package: "GitVersion"),
            ],
            resources: [
                .copy("Resources/Assets"),
                .copy("Resources/Interface"),
                .copy("Resources/Scenarios"),
            ],
            plugins: [
                .plugin(name: "GitVersionPlugin", package: "GitVersion"),
            ],
        ),
        .testTarget(
            name: "CityLimitsCoreTests",
            dependencies: ["CityLimitsCore", "CityLimitsCommands"],
            exclude: ["Fixtures"],
        ),
    ],
    swiftLanguageModes: [.v6],
)
