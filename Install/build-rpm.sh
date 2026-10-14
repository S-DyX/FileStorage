#!/usr/bin/env bash
# Builds the bsa-filestorage .rpm package (FileStorage.Api.Host as a systemd service).
#
# Run on Linux, in WSL or in the .NET SDK container (needs rpmbuild; when it is missing and the script runs
# as root, it is installed with apt-get/dnf/yum). The .NET SDK of the project's target framework is needed
# unless PUBLISH_DIR is given.
# Options (environment variables):
#   RID=linux-x64|linux-arm64     target runtime (default linux-x64)
#   SELF_CONTAINED=true|false     include the .NET runtime in the package (default false)
#   VERSION=x.y.z                 package version (default <Version> of the project)
#   PUBLISH_DIR=/path             use an existing `dotnet publish` output instead of publishing
#   OUT_DIR=/path                 where the .rpm is written (default Install/out)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/FileStorage.Api.Host"
PROJECT="$PROJECT_DIR/FileStorage.Api.Host.csproj"

PACKAGE=bsa-filestorage
APP_DIR=/opt/bsa-filestorage
ETC_DIR=/etc/bsa-filestorage
UNIT_DIR=/usr/lib/systemd/system
RID="${RID:-linux-x64}"
SELF_CONTAINED="${SELF_CONTAINED:-false}"
VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1 | tr -d '\r')}"
# target framework of the project (net9.0 -> 9.0): the package requires the matching ASP.NET Core runtime
TFM="$(sed -n 's:.*<TargetFramework>net\([0-9.]*\)</TargetFramework>.*:\1:p' "$PROJECT" | head -n 1 | tr -d '\r')"
OUT_DIR="${OUT_DIR:-$SCRIPT_DIR/out}"

case "$RID" in
	linux-x64) ARCH=x86_64 ;;
	linux-arm64) ARCH=aarch64 ;;
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

if ! command -v rpmbuild >/dev/null; then
	if [ "$(id -u)" -eq 0 ] && command -v apt-get >/dev/null; then
		apt-get update -qq && apt-get install -y -qq rpm >/dev/null
	elif [ "$(id -u)" -eq 0 ] && command -v dnf >/dev/null; then
		dnf install -y -q rpm-build
	elif [ "$(id -u)" -eq 0 ] && command -v yum >/dev/null; then
		yum install -y -q rpm-build
	fi
	command -v rpmbuild >/dev/null || { echo "rpmbuild is not installed (apt: rpm, dnf/yum: rpm-build)" >&2; exit 1; }
fi

# staging in a Linux temp folder: permissions and symlinks are not reliable on Windows drives (/mnt/d)
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
STAGE="$WORK_DIR/stage"

if [ -z "${PUBLISH_DIR:-}" ]; then
	command -v dotnet >/dev/null || { echo "dotnet SDK is not installed (or set PUBLISH_DIR)" >&2; exit 1; }
	PUBLISH_DIR="$WORK_DIR/publish"
	echo "Publishing $PROJECT ($RID, self-contained: $SELF_CONTAINED)"
	dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained "$SELF_CONTAINED" \
		-p:UseAppHost=true -p:DebugType=None -p:DebugSymbols=false -o "$PUBLISH_DIR"
fi
[ -f "$PUBLISH_DIR/FileStorage.Api.Host.dll" ] || { echo "FileStorage.Api.Host.dll not found in $PUBLISH_DIR" >&2; exit 1; }

mkdir -p "$STAGE$APP_DIR" "$STAGE$ETC_DIR" "$STAGE$UNIT_DIR" \
	"$STAGE/var/bsa/filestorage" "$STAGE/var/log/bsa/FileStorage.Api.Host"
cp -a "$PUBLISH_DIR/." "$STAGE$APP_DIR/"
rm -f "$STAGE$APP_DIR"/appsettings.Development.json "$STAGE$APP_DIR"/*.pdb

# configuration lives in /etc (%config(noreplace) keeps local changes on upgrade); the application reads it
# from its own folder (AppDomain.BaseDirectory), so the files there are symlinks to /etc
cp "$SCRIPT_DIR/config/appsettings.json" "$STAGE$ETC_DIR/appsettings.json"
cp "$SCRIPT_DIR/config/filestorage.json" "$STAGE$ETC_DIR/filestorage.json"
# logging for a server (Info to files, warnings to the systemd journal); the project's nlog.config is for debugging
cp "$SCRIPT_DIR/config/nlog.config" "$STAGE$ETC_DIR/nlog.config"
for path in "$STAGE$ETC_DIR"/*; do
	file="$(basename "$path")"
	rm -f "$STAGE$APP_DIR/$file"
	ln -s "$ETC_DIR/$file" "$STAGE$APP_DIR/$file"
done

if [ "$SELF_CONTAINED" = "true" ]; then
	EXEC_START="$APP_DIR/FileStorage.Api.Host"
	REQUIRES="glibc, libgcc, libstdc++, zlib, openssl-libs, libicu"
else
	EXEC_START="/usr/bin/dotnet $APP_DIR/FileStorage.Api.Host.dll"
	REQUIRES="aspnetcore-runtime-$TFM"
fi
sed "s|@EXEC_START@|$EXEC_START|" "$SCRIPT_DIR/debian/bsa-filestorage.service" | tr -d '\r' \
	> "$STAGE$UNIT_DIR/bsa-filestorage.service"

find "$STAGE" -type d -exec chmod 0755 {} +
find "$STAGE" -type f -exec chmod 0644 {} +
[ -f "$STAGE$APP_DIR/FileStorage.Api.Host" ] && chmod 0755 "$STAGE$APP_DIR/FileStorage.Api.Host"

TOP_DIR="$WORK_DIR/rpmbuild"
mkdir -p "$TOP_DIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS,BUILDROOT}
tr -d '\r' < "$SCRIPT_DIR/rpm/bsa-filestorage.spec" > "$TOP_DIR/SPECS/bsa-filestorage.spec"
rpmbuild -bb --target "$ARCH" \
	--define "_topdir $TOP_DIR" \
	--define "stagedir $STAGE" \
	--define "pkgversion $VERSION" \
	--define "pkgrequires $REQUIRES" \
	"$TOP_DIR/SPECS/bsa-filestorage.spec"

mkdir -p "$OUT_DIR"
find "$TOP_DIR/RPMS" -name "*.rpm" -exec cp {} "$OUT_DIR/" \;
echo "Package: $(ls "$OUT_DIR"/${PACKAGE}-${VERSION}-*.rpm | tail -n 1)"
