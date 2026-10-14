#!/usr/bin/env bash
# Builds the bsa-filestorage .deb package (FileStorage.Api.Host as a systemd service).
#
# Run on Linux, in WSL or in the .NET SDK container (needs dpkg-deb; the .NET SDK of the project's target
# framework unless PUBLISH_DIR is given):
#   ./Install/build-deb.sh
# Options (environment variables):
#   RID=linux-x64|linux-arm64     target runtime (default linux-x64)
#   SELF_CONTAINED=true|false     include the .NET runtime in the package (default false)
#   VERSION=x.y.z                 package version (default <Version> of the project)
#   PUBLISH_DIR=/path             use an existing `dotnet publish` output instead of publishing
#   OUT_DIR=/path                 where the .deb is written (default Install/out)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/FileStorage.Api.Host"
PROJECT="$PROJECT_DIR/FileStorage.Api.Host.csproj"

PACKAGE=bsa-filestorage
APP_DIR=/opt/bsa-filestorage
ETC_DIR=/etc/bsa-filestorage
RID="${RID:-linux-x64}"
SELF_CONTAINED="${SELF_CONTAINED:-false}"
VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1 | tr -d '\r')}"
# target framework of the project (net9.0 -> 9.0): the package depends on the matching ASP.NET Core runtime
TFM="$(sed -n 's:.*<TargetFramework>net\([0-9.]*\)</TargetFramework>.*:\1:p' "$PROJECT" | head -n 1 | tr -d '\r')"
OUT_DIR="${OUT_DIR:-$SCRIPT_DIR/out}"

case "$RID" in
	linux-x64) ARCH=amd64 ;;
	linux-arm64) ARCH=arm64 ;;
	*) echo "Unsupported RID: $RID (linux-x64, linux-arm64)" >&2; exit 1 ;;
esac
if [ -z "$TFM" ]; then
	echo "Cannot read <TargetFramework> from $PROJECT" >&2
	exit 1
fi
if [ -z "$VERSION" ]; then
	echo "Cannot read <Version> from $PROJECT; set VERSION" >&2
	exit 1
fi
command -v dpkg-deb >/dev/null || { echo "dpkg-deb is not installed" >&2; exit 1; }

# staging in a Linux temp folder: permissions and symlinks are not reliable on Windows drives (/mnt/d)
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
STAGE="$WORK_DIR/${PACKAGE}_${VERSION}_${ARCH}"

if [ -z "${PUBLISH_DIR:-}" ]; then
	command -v dotnet >/dev/null || { echo "dotnet SDK is not installed (or set PUBLISH_DIR)" >&2; exit 1; }
	PUBLISH_DIR="$WORK_DIR/publish"
	echo "Publishing $PROJECT ($RID, self-contained: $SELF_CONTAINED)"
	dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained "$SELF_CONTAINED" \
		-p:UseAppHost=true -p:DebugType=None -p:DebugSymbols=false -o "$PUBLISH_DIR"
fi
[ -f "$PUBLISH_DIR/FileStorage.Api.Host.dll" ] || { echo "FileStorage.Api.Host.dll not found in $PUBLISH_DIR" >&2; exit 1; }

mkdir -p "$STAGE$APP_DIR" "$STAGE$ETC_DIR" "$STAGE/lib/systemd/system" "$STAGE/DEBIAN"
cp -a "$PUBLISH_DIR/." "$STAGE$APP_DIR/"
rm -f "$STAGE$APP_DIR"/appsettings.Development.json "$STAGE$APP_DIR"/*.pdb

# configuration lives in /etc (dpkg keeps local changes on upgrade); the application reads it
# from its own folder (AppDomain.BaseDirectory), so the files there are symlinks to /etc
cp "$SCRIPT_DIR/config/appsettings.json" "$STAGE$ETC_DIR/appsettings.json"
cp "$SCRIPT_DIR/config/filestorage.json" "$STAGE$ETC_DIR/filestorage.json"
for file in nlog.config; do
	if [ -f "$PUBLISH_DIR/$file" ]; then
		cp "$PUBLISH_DIR/$file" "$STAGE$ETC_DIR/$file"
	elif [ -f "$PROJECT_DIR/$file" ]; then
		cp "$PROJECT_DIR/$file" "$STAGE$ETC_DIR/$file"
	fi
done
: > "$STAGE/DEBIAN/conffiles"
for path in "$STAGE$ETC_DIR"/*; do
	file="$(basename "$path")"
	rm -f "$STAGE$APP_DIR/$file"
	ln -s "$ETC_DIR/$file" "$STAGE$APP_DIR/$file"
done
# conffiles: the entries of debian/conffiles present in the package
while IFS= read -r entry || [ -n "$entry" ]; do
	entry="${entry%$'\r'}"
	if [ -n "$entry" ] && [ -f "$STAGE$entry" ]; then
		echo "$entry" >> "$STAGE/DEBIAN/conffiles"
	fi
done < "$SCRIPT_DIR/debian/conffiles"

if [ "$SELF_CONTAINED" = "true" ]; then
	EXEC_START="$APP_DIR/FileStorage.Api.Host"
	DEPENDS="libc6, libgcc-s1 | libgcc1, libstdc++6, zlib1g, libssl3t64 | libssl3 | libssl1.1, libicu76 | libicu74 | libicu72 | libicu70 | libicu67 | libicu66"
else
	EXEC_START="/usr/bin/dotnet $APP_DIR/FileStorage.Api.Host.dll"
	DEPENDS="aspnetcore-runtime-$TFM"
fi
sed "s|@EXEC_START@|$EXEC_START|" "$SCRIPT_DIR/debian/bsa-filestorage.service" \
	> "$STAGE/lib/systemd/system/bsa-filestorage.service"

for script in postinst prerm postrm; do
	tr -d '\r' < "$SCRIPT_DIR/debian/$script" > "$STAGE/DEBIAN/$script"
	chmod 0755 "$STAGE/DEBIAN/$script"
done

# permissions: directories 0755, files 0644, the app host executable 0755
find "$STAGE" -type d -exec chmod 0755 {} +
find "$STAGE" -type f ! -path "$STAGE/DEBIAN/*" -exec chmod 0644 {} +
[ -f "$STAGE$APP_DIR/FileStorage.Api.Host" ] && chmod 0755 "$STAGE$APP_DIR/FileStorage.Api.Host"
chmod 0755 "$STAGE/DEBIAN"/postinst "$STAGE/DEBIAN"/prerm "$STAGE/DEBIAN"/postrm

SIZE="$(du -sk --exclude=DEBIAN "$STAGE" | cut -f1)"
sed -e "s|@VERSION@|$VERSION|" -e "s|@ARCH@|$ARCH|" -e "s|@SIZE@|$SIZE|" -e "s/@DEPENDS@/$DEPENDS/" \
	"$SCRIPT_DIR/debian/control.in" | tr -d '\r' > "$STAGE/DEBIAN/control"

mkdir -p "$OUT_DIR"
DEB="$OUT_DIR/${PACKAGE}_${VERSION}_${ARCH}.deb"
dpkg-deb --root-owner-group --build "$STAGE" "$DEB"
echo "Package: $DEB"
