# Packaging

Scripts that build CityLimits into the packages players download, one folder for each platform,
after [AmtrakStatus'](https://github.com/tonytins/AmtrakStatus/tree/main/packaging), which were
themselves after the C# build's. They write to `dist/release/` at the repository's root, which git
ignores, and put the packages together in `dist/work/`.

| Script | Builds | Runs on |
| --- | --- | --- |
| `linux/package.sh` | `citylimits-<version>-linux-x86_64.AppImage` and `.tar.gz` | Linux |
| `windows/package.sh` | `citylimits-<version>-windows-x64.zip`, the game in a folder | Windows, in Git Bash |
| `macos/package.sh` | `citylimits-<version>-macos-universal.zip`, holding `CityLimits.app` | macOS |

```sh
packaging/linux/package.sh
```

Swift builds for the machine it runs on, so each script runs on its own platform. Only the Linux
script has been run so far. CityLimits builds only on Linux for now, so the Windows and macOS
scripts are untested, and raylib-swift and Hudini may need changes before they work.

## What they share

`env.sh` sets up every script. It builds the game in release, reads the version from the built
game (`citylimits --version`, less its build metadata: the major.minor.patch GitVersion works out,
the one the changelog uses), and stages a folder with:

- the game, `citylimits`;
- `CityLimits_CityLimits.resources/`, the folder SwiftPM's `Bundle.module` looks for beside the
  executable, holding only the files git tracks under `Sources/CityLimits/Resources`: the art in
  `Assets/`, the interface documents in `Interface/`, and the built-in scenarios in `Scenarios/`;
- the readme, the changelog, and the licence (`LICENSE`).

Each script then checks the staged resources are there and runs the staged game with no window:
its version, `scenario list`, and the Aftershock scenario, which reads a scenario and a saved city
from the resources. `Bundle.module` falls back to the `.build` folder the game was built in, so a
package missing its resources would still run on the machine that built it; that's why the files
are checked, not only the game run. The window isn't opened. Tools are downloaded only at pinned
versions, and a file whose SHA-256 isn't the published one is refused.

| Variable | Default | Meaning |
| --- | --- | --- |
| `CITYLIMITS_VERSION` | the game's own | The version the packages are named for |
| `DIST` | `dist/release` | Where the packages go |
| `WORK` | `dist/work` | Where they're put together, and the downloaded tools kept |

The game keeps its settings, cities, mods, and scenarios in the player's folders, not in the
package; a copy with a `settings.json` beside the executable keeps everything beside itself.

## Linux

The game is built with the Swift runtime inside it (`--static-swift-stdlib`) and carries libxml2,
which Hudini reads the interface documents with. A player's system needs glibc, X11, zlib, and the
C++ runtime, and OpenGL to open the window.

**Which systems it runs on depends on where it's built.** glibc can't be carried in the package, so
a package needs the glibc of the machine that built it, or newer, and the script prints that
version when it finishes. A package built on this machine in November 2026 needs glibc 2.44, so it
won't start on Ubuntu 24.04, which has 2.39. To reach more systems, build on an older one.

Building needs the X11 and GL headers raylib compiles against; on an atomic distribution they come
from Homebrew, through `CPATH` and `LIBRARY_PATH`. The script refuses a build that links a library
from Homebrew.

The AppImage carries its own runtime, so it runs without FUSE. It's built with appimagetool 1.9.1
and the type 2 runtime from 2025-11-08, the C# build's pins, downloaded on the first run. `AppRun`
starts the game from inside the image, `CityLimits.desktop` is its menu entry, and the game's logo
(`Assets/logo.png`) is its icon.

## Windows

Built on Windows with the Swift toolchain, with the Swift runtime inside the game, and zipped with
PowerShell. `citylimits.exe` has no icon, because SwiftPM has no step that embeds one, and it opens
a console window beside the game. There's no installer.

## macOS

`CityLimits.app` holds the game in `Contents/MacOS`, its docs and icon (`icon.icns`) in
`Contents/Resources`, and `Info.plist`, made from `Info.plist.in`. It's one app for Apple silicon
and Intel.

On a Mac, SwiftPM names the resources `CityLimits_CityLimits.bundle`, and `Bundle.module` looks for
them in the app's own folder, beside `Contents`, so that's where the script puts them. `codesign`
may refuse the app for having them there; if it does, the game needs to look in
`Contents/Resources` instead, which is a change to the game, not the script.

The app is signed ad hoc, not with a Developer ID, so Gatekeeper asks before a downloaded copy
first opens: Control-click the app and choose Open. Signing with a Developer ID and notarizing need
an Apple developer account.
