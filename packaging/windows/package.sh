#!/usr/bin/env bash
# Builds CityLimits for 64-bit Windows, on any machine with the .NET SDK, since .NET publishes for
# Windows from Linux and macOS too:
#
#   $DIST/citylimits-<version>-windows-x64.zip  the game in a folder; unzip it anywhere and run
#                                               CityLimits.exe
#
# Usage: packaging/windows/package.sh [--framework-dependent]
#
# Needs the .NET 10 SDK, zip, and python3, which checks the executable is for x64.
#
# The game keeps its settings in %APPDATA%\CityLimits and its cities and mods in
# Documents\CityLimits. A copy with a settings.json beside CityLimits.exe, in a folder it can write
# to, keeps everything beside itself instead.

# shellcheck source=../env.sh
. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"
parse_options "$@"
command -v zip >/dev/null || fail "zip isn't installed."
BASENAME="citylimits-$VERSION-windows-x64"
echo "==> $APP $VERSION for Windows x64 (self-contained: $SELF_CONTAINED)"

STAGE="$WORK/windows/$BASENAME"
publish win-x64 "$SELF_CONTAINED" "$STAGE"
[[ -f "$STAGE/$APP.exe" && -f "$STAGE/raylib.dll" ]] || fail "the published game has no $APP.exe or raylib.dll."
copy_docs "$STAGE"

# Machine 0x8664 in the executable's PE header is x64, so a build for another machine is never
# shipped labelled x64.
machine="$(python3 -c 'import struct, sys; d = open(sys.argv[1], "rb").read(); pe = struct.unpack_from("<I", d, 0x3C)[0]; print(hex(struct.unpack_from("<H", d, pe + 4)[0]))' "$STAGE/$APP.exe")"
[[ "$machine" = 0x8664 ]] || fail "$APP.exe is for machine $machine, not x64 (0x8664)."

OUT="$DIST/$BASENAME.zip"
rm -f "$OUT"
(cd "$WORK/windows" && zip -qr "$OUT" "$BASENAME")
echo "wrote $OUT"
echo "==> done"
ls -lh "$OUT"
