#!/usr/bin/env bash
# Builds CityLimits for x86_64 Linux:
#
#   $DIST/citylimits-<version>-linux-x86_64.AppImage  runs without FUSE
#   $DIST/citylimits-<version>-linux-x86_64.tar.gz    the same game as a plain folder
#
# Usage: packaging/linux/package.sh
#
# Needs a Swift 6.3 or later toolchain and the X11 and GL headers raylib builds against (on an
# atomic distro, Homebrew's, through CPATH and LIBRARY_PATH). The Swift runtime is built in, and
# the packages carry the libxml2 the game links against. They run only on systems with this
# machine's glibc or newer; the script prints the version they need. appimagetool and the AppImage
# runtime are downloaded at pinned versions into $WORK and checked against their published SHA-256s.

# shellcheck source=../env.sh
. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"
parse_options "$@"
HERE="$ROOT/packaging/linux"
ARCH=x86_64

# libxml2 (Foundation's XML, which Hudini reads the interface documents with) is linked against
# the system's own, never Homebrew's, and goes in the package's lib/, found through the run path:
# distributions name it libxml2.so.2 or, since libxml2 2.14, libxml2.so.16, so carrying it means
# either kind of system runs the game. A system with no development symlink gets one in a folder
# of its own, put first. awk reads to the end rather than exiting at the first match, which under
# pipefail would end the script with ldconfig's SIGPIPE.
SYSTEM_XML="$(ldconfig -p 2>/dev/null | awk '/libxml2\.so\.[0-9]+ .*x86-64/ && !found { print $NF; found = 1 }')"
[[ -n "$SYSTEM_XML" && -f "$SYSTEM_XML" ]] || fail "the system has no libxml2 to link against."
LINKDIR="$WORK/linux/link"
mkdir -p "$LINKDIR"
ln -sf "$SYSTEM_XML" "$LINKDIR/libxml2.so"
export LIBRARY_PATH="$LINKDIR${LIBRARY_PATH:+:$LIBRARY_PATH}"
# $ORIGIN is for the dynamic linker, not the shell.
# shellcheck disable=SC2016
build_app --static-swift-stdlib -Xlinker -rpath -Xlinker '$ORIGIN/lib'

# Nothing may come from Homebrew or be missing: a player's system has neither.
LINKED="$(ldd "$BIN")"
if grep -q "not found" <<<"$LINKED"; then fail "the game links a library that isn't found:"$'\n'"$LINKED"; fi
if command -v brew >/dev/null && grep -q "$(brew --prefix)" <<<"$LINKED"; then
    fail "the game links a library from Homebrew, which players won't have:"$'\n'"$LINKED"
fi
BASENAME="citylimits-$VERSION-linux-$ARCH"
echo "==> $APP $VERSION for Linux $ARCH"

STAGE="$WORK/linux/$BASENAME"
stage "$STAGE"
strip --strip-unneeded "$STAGE/$EXE"
mkdir -p "$STAGE/lib"
cp -L "$(awk '/libxml2/ { print $3 }' <<<"$LINKED")" "$STAGE/lib/"
check_resources "$STAGE/$RESOURCES"
smoke_test "$STAGE/$EXE"

# ---- .tar.gz ------------------------------------------------------------------------------------
tar -C "$WORK/linux" -czf "$DIST/$BASENAME.tar.gz" "$BASENAME"
echo "wrote $DIST/$BASENAME.tar.gz"

# ---- AppImage -----------------------------------------------------------------------------------
# The desktop entry and the icon go at the AppImage's root, where appimagetool looks for them; the
# icon is the game's logo, the same art as icon.ico and icon.icns.
APPDIR="$WORK/linux/$APP.AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/share/applications"
cp -R "$STAGE" "$APPDIR/usr/bin"
cp "$HERE/$APP.desktop" "$APPDIR/usr/share/applications/$APP.desktop"
ln -s "usr/share/applications/$APP.desktop" "$APPDIR/$APP.desktop"
cp "$ROOT/Sources/CityLimits/Resources/Assets/logo.png" "$APPDIR/$APP.png"
ln -s "$APP.png" "$APPDIR/.DirIcon"
install -m755 "$HERE/AppRun" "$APPDIR/AppRun"
check_resources "$APPDIR/usr/bin/$RESOURCES"
if command -v desktop-file-validate >/dev/null; then
    desktop-file-validate "$APPDIR/usr/share/applications/$APP.desktop"
fi

# The pins the C# build used, from the releases' published digests:
# https://github.com/AppImage/appimagetool/releases/tag/1.9.1
# https://github.com/AppImage/type2-runtime/releases/tag/20251108
# They stay together, since appimagetool would otherwise download the latest runtime itself.
TOOL_VERSION=1.9.1
TOOL_SHA256=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
RUNTIME_VERSION=20251108
RUNTIME_SHA256=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d
TOOL="$WORK/appimagetool-$TOOL_VERSION-$ARCH.AppImage"
RUNTIME="$WORK/type2-runtime-$RUNTIME_VERSION-$ARCH"
download_verified "https://github.com/AppImage/appimagetool/releases/download/$TOOL_VERSION/appimagetool-$ARCH.AppImage" "$TOOL" "$TOOL_SHA256" true
download_verified "https://github.com/AppImage/type2-runtime/releases/download/$RUNTIME_VERSION/runtime-$ARCH" "$RUNTIME" "$RUNTIME_SHA256"

# Extracting appimagetool instead of mounting it means it runs without FUSE, as on many atomic
# distributions and in containers; the AppImage carries the static runtime, so players don't need
# libfuse2 either.
OUT="$DIST/$BASENAME.AppImage"
rm -f "$OUT"
ARCH="$ARCH" APPIMAGE_EXTRACT_AND_RUN=1 "$TOOL" --no-appstream --runtime-file "$RUNTIME" "$APPDIR" "$OUT"
echo "wrote $OUT"

# The finished image, run the way a player runs it.
APPIMAGE_EXTRACT_AND_RUN=1 smoke_test "$OUT"
echo "==> done"
ls -lh "$DIST/$BASENAME".*
# glibc can't be carried in the package, so the machine that builds it sets the oldest system it
# runs on.
GLIBC="$(objdump -T "$BIN" | grep -oE 'GLIBC_[0-9.]+' | sort -Vu | tail -n 1)"
echo "These packages need ${GLIBC/_/ } or newer."
