# shellcheck shell=bash
# Shared setup for the packaging scripts, after AmtrakStatus'. Source it:
# `. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"`.
#
# Sets:
#   ROOT         the repository's root
#   APP          CityLimits, the name of the app, its desktop entry, and its Mac bundle
#   EXE          citylimits, the game's executable
#   RESOURCES    CityLimits_CityLimits.resources, the folder SwiftPM's Bundle.module reads beside
#                the executable
#   DIST         where the packages go (default: $ROOT/dist/release)
#   WORK         where they are put together (default: $ROOT/dist/work)
#   BUILD_SHA    the commit being packaged
#
# VERSION is set by build_app, from the built game's --version (GitVersion's major.minor.patch);
# CITYLIMITS_VERSION overrides it.
#
# Swift builds for the machine it runs on, so each script runs on its own platform: Linux on
# Linux, Windows on Windows (in Git Bash), macOS on a Mac.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP=CityLimits
EXE=citylimits
RESOURCES=CityLimits_CityLimits.resources
export ROOT APP EXE RESOURCES

fail() {
    echo "packaging: $*" >&2
    exit 1
}

command -v swift >/dev/null || fail "swift isn't installed; a Swift 6.3 or later toolchain is needed to build."
command -v git >/dev/null || fail "git isn't installed; the game's version is counted from its history."

DIST="${DIST:-$ROOT/dist/release}"
WORK="${WORK:-$ROOT/dist/work}"
mkdir -p "$DIST" "$WORK"
DIST="$(cd "$DIST" && pwd)"
WORK="$(cd "$WORK" && pwd)"
export DIST WORK

BUILD_SHA="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo unknown)"
export BUILD_SHA

# Builds the game in release, with any extra arguments for swift build, sets BIN to the built
# executable, and VERSION from it. --version prints "CityLimits 0.6.117+main.c599adf7f9b1"; the
# packages are named for the major.minor.patch, as the changelog is.
build_app() {
    local printed
    swift build --package-path "$ROOT" -c release --product "$EXE" "$@" \
        || fail "couldn't build the game."
    BIN="$(swift build --package-path "$ROOT" -c release "$@" --show-bin-path)/$EXE"
    [[ -x "$BIN" || -x "$BIN.exe" ]] || fail "the build has no $EXE in $(dirname "$BIN")."
    if [[ -n "${CITYLIMITS_VERSION:-}" ]]; then
        VERSION="$CITYLIMITS_VERSION"
    else
        printed="$("$BIN" --version)" || fail "the built game didn't print its version."
        VERSION="${printed#CityLimits }"
        VERSION="${VERSION%%+*}"
    fi
    [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$ ]] || fail "couldn't read the game's version; got '$VERSION'."
    export BIN VERSION
}

# Puts what a player needs in a folder: the game; its resources (only the files git tracks, so
# nothing local slips in), in the folder Bundle.module looks for beside the executable; the
# readme, the changelog, and the licence.
stage() {
    local dest="$1" file
    rm -rf "$dest"
    mkdir -p "$dest/$RESOURCES"
    if [[ -f "$BIN.exe" ]]; then cp "$BIN.exe" "$dest/"; else cp "$BIN" "$dest/"; fi
    while IFS= read -r file; do
        mkdir -p "$dest/$RESOURCES/$(dirname "$file")"
        cp "$ROOT/Sources/CityLimits/Resources/$file" "$dest/$RESOURCES/$file"
    done < <(git -C "$ROOT/Sources/CityLimits/Resources" ls-files)
    for file in README.md changelog.md LICENSE; do
        cp "$ROOT/$file" "$dest/"
    done
}

# Checks a staged game's resources are where it looks for them. Bundle.module prefers the folder
# beside the executable, and falls back to the build folder this machine built it in, so a
# package missing them would still run here and fail for everyone else; hence the files are
# checked, not only the game run.
check_resources() {
    local folder="$1" file
    for file in Assets/tiles.png Assets/logo.png Interface/newcity.uix Scenarios/aftershock.json Scenarios/aftershock.fcl; do
        [[ -f "$folder/$file" ]] || fail "the package has no $file in $folder."
    done
}

# Checks a staged game runs with no window: its version, its built-in scenarios, and Aftershock,
# which reads a scenario and a saved city from its resources. The window, and the art and
# interface documents it loads, aren't checked.
smoke_test() {
    local exe="$1" listed
    "$exe" --version >/dev/null || fail "the packaged game didn't start."
    listed="$("$exe" scenario list)" || fail "the packaged game couldn't list its scenarios."
    [[ "$listed" == *aftershock* ]] || fail "the packaged game didn't list its scenarios:"$'\n'"$listed"
    "$exe" scenario aftershock --headless >/dev/null || fail "the packaged game couldn't run Aftershock."
}

# Prints a file's SHA-256.
sha256() {
    if command -v sha256sum >/dev/null; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

# Downloads a file once, and refuses it unless its SHA-256 is the one given. A file already there
# is checked too, so a damaged or swapped download is never used.
download_verified() {
    local url="$1" dest="$2" expected="$3" executable="${4:-false}" got
    if [[ ! -f "$dest" ]]; then
        command -v curl >/dev/null || fail "curl isn't installed to download $url."
        echo "Downloading $url"
        curl -fL --retry 3 -o "$dest.part" "$url" || fail "couldn't download $url."
        mv "$dest.part" "$dest"
    fi
    got="$(sha256 "$dest")"
    if [[ "$got" != "$expected" ]]; then
        rm -f "$dest"
        fail "$(basename "$dest") has SHA-256 $got, not $expected; it was deleted, so the next run downloads it again."
    fi
    if [[ "$executable" = true ]]; then chmod +x "$dest"; fi
}

# The scripts' options: -h or --help.
parse_options() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h | --help) awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; exit 0 ;;
            *) fail "unknown option '$1'; see --help." ;;
        esac
    done
}
