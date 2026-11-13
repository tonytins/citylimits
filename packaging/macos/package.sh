#!/usr/bin/env bash
# Builds CityLimits for macOS, for Apple silicon and Intel in one app:
#
#   $DIST/citylimits-<version>-macos-universal.zip  holding CityLimits.app
#
# Usage: packaging/macos/package.sh
#
# Runs on a Mac with Xcode or its command line tools: Swift can't build a Mac app from another
# system. The app is signed ad hoc, not with a Developer ID, so Gatekeeper asks before a downloaded
# copy first opens: Control-click the app and choose Open. Not yet run on a Mac.

# shellcheck source=../env.sh
. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"
parse_options "$@"
HERE="$ROOT/packaging/macos"

[[ "$(uname -s)" == Darwin ]] || fail "this script runs on a Mac; Swift can't build a Mac app from $(uname -s)."

build_app --arch arm64 --arch x86_64
BASENAME="citylimits-$VERSION-macos-universal"
echo "==> $APP $VERSION for macOS (arm64 and x86_64)"

# The game in Contents/MacOS and its docs in Contents/Resources. On a Mac, SwiftPM names the
# resources CityLimits_CityLimits.bundle and Bundle.module looks for them in the app's own folder,
# beside Contents, so that's where they go. codesign may refuse anything there as unsealed; if it
# does, the game has to look in Contents/Resources instead.
MAC_RESOURCES=CityLimits_CityLimits.bundle
STAGE="$WORK/macos/$BASENAME"
stage "$STAGE"
BUNDLE="$WORK/macos/$APP.app"
rm -rf "$BUNDLE"
mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"
mv "$STAGE/$EXE" "$BUNDLE/Contents/MacOS/$EXE"
mv "$STAGE/$RESOURCES" "$BUNDLE/$MAC_RESOURCES"
mv "$STAGE"/* "$BUNDLE/Contents/Resources/"
cp "$ROOT/icon.icns" "$BUNDLE/Contents/Resources/$APP.icns"
check_resources "$BUNDLE/$MAC_RESOURCES"

sed -e "s/@VERSION@/$VERSION/g" -e "s/@BUILD_SHA@/$BUILD_SHA/g" "$HERE/Info.plist.in" >"$BUNDLE/Contents/Info.plist"
plutil -lint "$BUNDLE/Contents/Info.plist" >/dev/null || fail "Info.plist isn't valid."

codesign --force --deep --sign - "$BUNDLE" || fail "couldn't sign the app."
codesign --verify --strict --deep "$BUNDLE" || fail "the app's signature isn't valid."
smoke_test "$BUNDLE/Contents/MacOS/$EXE"

OUT="$DIST/$BASENAME.zip"
rm -f "$OUT"
ditto -c -k --keepParent "$BUNDLE" "$OUT"
echo "wrote $OUT"
echo "==> done"
