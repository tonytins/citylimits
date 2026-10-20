#!/bin/bash
set -e

APP=CityLimits
VERSION=$(git -C . describe --tags --always 2>/dev/null || echo "0.1.0")
ARCH=x86_64
APPDIR=AppDir

rm -rf $APPDIR $APP-$VERSION-$ARCH.AppImage

# Build the app
dotnet publish $APP/$APP.csproj -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=false -o $APPDIR/usr/bin

# Create AppDir structure
mkdir -p $APPDIR/usr/bin
mkdir -p $APPDIR/usr/share/applications
mkdir -p $APPDIR/usr/share/icons/hicolor/256x256/apps

# Move published files
cp -r $APPDIR/usr/bin/* $APPDIR/usr/bin/ 2>/dev/null || true
# Actually publish goes to usr/bin already

# Copy desktop and icon
cp $APP.desktop $APPDIR/usr/share/applications/
cp $APP.desktop $APPDIR/
cp Screenshot.png $APPDIR/CityLimits.png 2>/dev/null || cp Screenshot.png $APPDIR/usr/share/icons/hicolor/256x256/apps/CityLimits.png
ln -sf usr/share/applications/$APP.desktop $APPDIR/$APP.desktop
cp AppRun $APPDIR/AppRun
chmod +x $APPDIR/AppRun

# Download appimagetool if not present
if [ ! -f appimagetool-$ARCH.AppImage ]; then
    wget -O appimagetool-$ARCH.AppImage https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-$ARCH.AppImage
    chmod +x appimagetool-$ARCH.AppImage
fi

ARCH=$ARCH ./appimagetool-$ARCH.AppImage $APPDIR $APP-$VERSION-$ARCH.AppImage
