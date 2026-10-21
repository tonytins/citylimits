#!/usr/bin/env bash
# Builds CityLimits into an AppImage for x86_64 Linux, named for the version the game reports,
# such as CityLimits-0.2.109-x86_64.AppImage. It can be run from any folder.
#
# The AppImage is framework-dependent: it needs the .NET 10 runtime on the machine that runs it.
set -euo pipefail

APP=CityLimits
ARCH=x86_64
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
PROJECT="$ROOT/$APP/$APP.csproj"
APPDIR="$HERE/AppDir"
TOOL="$HERE/appimagetool-$ARCH.AppImage"
TOOL_URL="https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-$ARCH.AppImage"

fail() {
    echo "package-appimage: $*" >&2
    exit 1
}

command -v dotnet >/dev/null || fail "dotnet isn't installed; the .NET 10 SDK is needed to build."

# The version is GitInfo's major.minor.patch, the one the game shows and the changelog uses, not
# the commit hash. Asked for one property at a time, msbuild prints the bare value.
version_part() {
    dotnet msbuild "$PROJECT" -nologo -t:GitVersion -getProperty:"$1"
}
VERSION="$(version_part GitSemVerMajor).$(version_part GitSemVerMinor).$(version_part GitSemVerPatch)"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "couldn't read the version from GitInfo; got '$VERSION'."
OUTPUT="$HERE/$APP-$VERSION-$ARCH.AppImage"
echo "Packaging $APP $VERSION"

rm -rf "$APPDIR" "$OUTPUT"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications"

dotnet publish "$PROJECT" -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=false -o "$APPDIR/usr/bin"

# The desktop entry and the icon go at the AppImage's root, where appimagetool looks for them;
# the icon is the game's logo, the same art as icon.ico.
cp "$HERE/$APP.desktop" "$APPDIR/usr/share/applications/$APP.desktop"
ln -sf "usr/share/applications/$APP.desktop" "$APPDIR/$APP.desktop"
cp "$ROOT/$APP/Assets/logo.png" "$APPDIR/$APP.png"
ln -sf "$APP.png" "$APPDIR/.DirIcon"
cp "$HERE/AppRun" "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun"

if [ ! -x "$TOOL" ]; then
    echo "Downloading appimagetool"
    if command -v curl >/dev/null; then
        curl -fL -o "$TOOL" "$TOOL_URL"
    elif command -v wget >/dev/null; then
        wget -O "$TOOL" "$TOOL_URL"
    else
        fail "neither curl nor wget is installed to download appimagetool."
    fi
    chmod +x "$TOOL"
fi

# Extracting appimagetool instead of mounting it means it runs without FUSE, as on many
# immutable distributions and in containers.
ARCH=$ARCH APPIMAGE_EXTRACT_AND_RUN=1 "$TOOL" "$APPDIR" "$OUTPUT"
echo "Built $OUTPUT"
