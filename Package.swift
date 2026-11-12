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
        .package(url: "https://github.com/tonytins/GitVersion.git", from: "0.2.0"),
    ],
    targets: [
        // The world and the simulation: plain Swift with no engine, so tests run without a window.
        .target(name: "CityLimitsCore"),
        // The game: the window, the sessions (play, and later the editor), and the drawing.
        .executableTarget(
            name: "CityLimits",
            dependencies: [
                "CityLimitsCore",
                .product(name: "Raylib", package: "raylib-swift"),
                .product(name: "Hudini", package: "Hudini"),
                .product(name: "GitVersion", package: "GitVersion"),
            ],
            resources: [
                .copy("Resources/Scenarios"),
            ],
            plugins: [
                .plugin(name: "GitVersionPlugin", package: "GitVersion"),
            ],
        ),
        .testTarget(
            name: "CityLimitsCoreTests",
            dependencies: ["CityLimitsCore"],
            exclude: ["Fixtures"],
        ),
    ],
    swiftLanguageModes: [.v6],
)
