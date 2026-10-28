# shellcheck shell=bash
# Shared setup for the packaging scripts, after Photocraft's. Source it:
# `. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"`.
#
# Sets:
#   ROOT         the repository's root
#   APP          CityLimits
#   PROJECT      the game's project file
#   VERSION      GitInfo's major.minor.patch, the one the game shows and the changelog uses
#                (override: CITYLIMITS_VERSION)
#   DIST         where the packages go (default: $ROOT/dist/release)
#   WORK         where they are put together (default: $ROOT/dist/work)
#   BUILD_SHA    the commit being packaged
#
# Every package is self-contained, so the machine that runs it needs no .NET runtime; pass
# --framework-dependent to a script for a smaller one that does.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP=CityLimits
PROJECT="$ROOT/$APP/$APP.csproj"
export ROOT APP PROJECT

fail() {
    echo "packaging: $*" >&2
    exit 1
}

warn() {
    echo "warning: $*" >&2
}

command -v dotnet >/dev/null || fail "dotnet isn't installed; the .NET 10 SDK is needed to build."

# The version lives in one place, GitInfo.txt, counted on by GitInfo. Its targets come from its
# package, so a fresh clone restores before it can be asked; asked for one property at a time,
# msbuild prints the bare value.
if [[ -z "${CITYLIMITS_VERSION:-}" ]]; then
    dotnet restore "$PROJECT" --nologo -v quiet >/dev/null || fail "couldn't restore the game's packages."
    version_part() {
        dotnet msbuild "$PROJECT" -nologo -t:GitVersion -getProperty:"$1"
    }
    CITYLIMITS_VERSION="$(version_part GitSemVerMajor).$(version_part GitSemVerMinor).$(version_part GitSemVerPatch)"
fi
VERSION="$CITYLIMITS_VERSION"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "couldn't read the version from GitInfo; got '$VERSION'."
export VERSION

DIST="${DIST:-$ROOT/dist/release}"
WORK="${WORK:-$ROOT/dist/work}"
mkdir -p "$DIST" "$WORK"
DIST="$(cd "$DIST" && pwd)"
WORK="$(cd "$WORK" && pwd)"
export DIST WORK

BUILD_SHA="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo unknown)"
export BUILD_SHA

# Publishes the game for a runtime (such as linux-x64) into a folder, without debug symbols.
# The second argument is "true" for a self-contained build, "false" for one that needs .NET.
publish() {
    local rid="$1" self_contained="$2" out="$3"
    rm -rf "$out"
    dotnet publish "$PROJECT" -c Release -r "$rid" --self-contained "$self_contained" --nologo \
        -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o "$out" \
        || fail "couldn't publish the game for $rid."
    [[ -f "$out/Assets/tiles.png" ]] || fail "the published game for $rid has no Assets folder."
}

# Copies the readme, licence, and changelog into a package's folder.
copy_docs() {
    local dest="$1" f
    for f in README.md LICENSE changelog.md; do
        if [[ -f "$ROOT/$f" ]]; then cp "$ROOT/$f" "$dest/"; fi
    done
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
        echo "Downloading $url"
        if command -v curl >/dev/null; then
            curl -fL --retry 3 -o "$dest.part" "$url" || fail "couldn't download $url."
        elif command -v wget >/dev/null; then
            wget -O "$dest.part" "$url" || fail "couldn't download $url."
        else
            fail "neither curl nor wget is installed to download $url."
        fi
        mv "$dest.part" "$dest"
    fi
    got="$(sha256 "$dest")"
    if [[ "$got" != "$expected" ]]; then
        rm -f "$dest"
        fail "$(basename "$dest") has SHA-256 $got, not $expected; it was deleted, so the next run downloads it again."
    fi
    if [[ "$executable" = true ]]; then chmod +x "$dest"; fi
}

# The script's options, shared by every platform: --framework-dependent, and -h or --help.
SELF_CONTAINED=true
parse_options() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --framework-dependent) SELF_CONTAINED=false; shift ;;
            -h | --help) awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; exit 0 ;;
            *) fail "unknown option '$1'; see --help." ;;
        esac
    done
}
