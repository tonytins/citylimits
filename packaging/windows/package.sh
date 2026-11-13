#!/usr/bin/env bash
# Builds CityLimits for x86_64 Windows:
#
#   $DIST/citylimits-<version>-windows-x64.zip  the game in a folder; unzip it anywhere
#
# Usage: packaging/windows/package.sh
#
# Runs on Windows, in Git Bash, with the Swift toolchain for Windows: Swift can't build a Windows
# app from another system. The Swift runtime is built in, so players need nothing installed. Not
# yet run on Windows, and raylib-swift hasn't been built there yet.

# shellcheck source=../env.sh
. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"
parse_options "$@"

case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*) ;;
    *) fail "this script runs on Windows, in Git Bash; Swift can't build a Windows app from $(uname -s)." ;;
esac

build_app --static-swift-stdlib
BASENAME="citylimits-$VERSION-windows-x64"
echo "==> $APP $VERSION for Windows x64"

STAGE="$WORK/windows/$BASENAME"
stage "$STAGE"
check_resources "$STAGE/$RESOURCES"
smoke_test "$STAGE/$EXE.exe"

OUT="$DIST/$BASENAME.zip"
rm -f "$OUT"
# Git Bash has no zip; PowerShell's archiver is on every Windows.
powershell.exe -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$STAGE")' -DestinationPath '$(cygpath -w "$OUT")'" \
    || fail "couldn't zip the game."
echo "wrote $OUT"
echo "==> done"
