# Packaging

Scripts that build CityLimits into the packages players download, one
folder for each platform, after Photocraft's `packaging/`. Every script
runs on Linux or macOS with the .NET 10 SDK, since .NET publishes for
every platform from any of them, and writes to `dist/release/`, which git
ignores.

| Script | Builds |
|---|---|
| `linux/package.sh` | `citylimits-<version>-linux-x86_64.AppImage` and `.tar.gz` |
| `windows/package.sh` | `citylimits-<version>-windows-x64.zip`, the game in a folder |
| `macos/package.sh` | `citylimits-<version>-macos-arm64.zip` and `-macos-x64.zip`, each holding `CityLimits.app` |

```sh
packaging/linux/package.sh
packaging/windows/package.sh
packaging/macos/package.sh
```

Each takes `--framework-dependent` for a smaller package that needs the
.NET 10 runtime on the player's machine, and `--help`. Without it, the
package carries the runtime and needs nothing installed.

## What they share

`env.sh` sets up every script: it reads the version once, from GitInfo,
the same major.minor.patch the game shows and the changelog uses; it
publishes the game without debug symbols and checks its `Assets` came
with it; it copies the readme, licence, and changelog in; and it
downloads tools only at pinned versions, refusing a file whose SHA-256
isn't the one published for it.

| Variable | Default | Meaning |
|---|---|---|
| `CITYLIMITS_VERSION` | GitInfo's | The version the packages are named for |
| `DIST` | `dist/release` | Where the packages go |
| `WORK` | `dist/work` | Where they are put together, and the downloaded tools kept |

The base game's spec files aren't packaged: they live in the game's code,
and the game writes them into `common/` beside the settings when a new
build first starts.

## Linux

The AppImage carries its own runtime, so it runs without FUSE. It is
built with appimagetool 1.9.1 and the type 2 runtime from 2025-11-08, the
pins Photocraft uses, downloaded on the first run. `AppRun` starts the
game from inside the image, and `CityLimits.desktop` is its menu entry.

## Windows

`CityLimits.exe` carries `icon.ico`. The zip unzips anywhere. The game
keeps its settings in `%APPDATA%\CityLimits`, and cities and mods in
`Documents\CityLimits`; a copy with a `settings.json` beside the
executable, in a folder it can write to, keeps everything beside itself.
There is no installer yet.

## macOS

`CityLimits.app` holds the published game in `Contents/MacOS`, beside its
`Assets`, the icon in `Contents/Resources/CityLimits.icns` (made from
`Assets/logo.png`), and `Info.plist`, from `Info.plist.in`. It needs
macOS 14 or later, as .NET 10 does.

The app is signed ad hoc, not with a Developer ID, so Gatekeeper asks
before a downloaded copy first opens: Control-click the app and choose
Open. On a Mac, the script signs the whole bundle with `codesign`;
elsewhere, .NET has already signed the executable as it published it,
which Apple silicon needs before it runs anything. Signing with a
Developer ID and notarizing need a Mac and an Apple developer account.
