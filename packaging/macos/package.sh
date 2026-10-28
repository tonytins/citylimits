#!/usr/bin/env bash
# Builds CityLimits for macOS, Apple silicon and Intel, on any machine with the .NET SDK:
#
#   $DIST/citylimits-<version>-macos-arm64.zip  CityLimits.app for Apple silicon
#   $DIST/citylimits-<version>-macos-x64.zip    CityLimits.app for Intel Macs
#
# Usage: packaging/macos/package.sh [--framework-dependent]
#
# The app is signed ad hoc: on a Mac, codesign signs the whole bundle; elsewhere, .NET signs the
# executable as it publishes it, which Apple silicon needs before it runs anything. Neither is a
# Developer ID, so Gatekeeper asks before a downloaded copy first opens (Control-click, Open).
# Signing with a Developer ID and notarizing need a Mac and an Apple developer account.

# shellcheck source=../env.sh
. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"
parse_options "$@"
command -v zip >/dev/null || fail "zip isn't installed."
HERE="$ROOT/packaging/macos"

for arch in arm64 x64; do
    BASENAME="citylimits-$VERSION-macos-$arch"
    echo "==> $APP $VERSION for macOS $arch (self-contained: $SELF_CONTAINED)"
    BUNDLE="$WORK/macos/$arch/$APP.app"
    rm -rf "$WORK/macos/$arch"

    # The game finds its Assets beside its executable, so the whole published folder goes in
    # Contents/MacOS.
    publish "osx-$arch" "$SELF_CONTAINED" "$BUNDLE/Contents/MacOS"
    [[ -f "$BUNDLE/Contents/MacOS/libraylib.dylib" ]] || fail "the published game for osx-$arch has no libraylib.dylib."
    mkdir -p "$BUNDLE/Contents/Resources"
    cp "$HERE/$APP.icns" "$BUNDLE/Contents/Resources/$APP.icns"
    sed -e "s/@VERSION@/$VERSION/g" -e "s/@BUILD_SHA@/$BUILD_SHA/g" "$HERE/Info.plist.in" >"$BUNDLE/Contents/Info.plist"
    printf 'APPL????' >"$BUNDLE/Contents/PkgInfo"
    copy_docs "$WORK/macos/$arch"
    if command -v plutil >/dev/null; then plutil -lint "$BUNDLE/Contents/Info.plist"; fi

    if command -v codesign >/dev/null; then
        codesign --force --deep --sign - --timestamp=none "$BUNDLE"
        codesign --verify --strict --deep --verbose=2 "$BUNDLE"
    else
        warn "codesign isn't here, so only the executable is signed, by .NET; the bundle isn't."
    fi

    OUT="$DIST/$BASENAME.zip"
    rm -f "$OUT"
    (cd "$WORK/macos/$arch" && zip -qry "$OUT" "$APP.app" README.md LICENSE changelog.md)
    echo "wrote $OUT"
done

echo "==> done"
ls -lh "$DIST"/citylimits-"$VERSION"-macos-*.zip
