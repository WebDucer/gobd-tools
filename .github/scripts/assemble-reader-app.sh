#!/usr/bin/env bash
# Assembles the reader's macOS application bundle from an osx-arm64 publish, and archives it.
#
# Runs anywhere, and in the pipeline on Linux: a bundle is a folder layout and a property list,
# and nothing in making one needs macOS. What does need it, thinning the native libraries, signing
# the bundle as a whole and making the disk image, is left to make-reader-dmg.sh.
#
# The archive is a tarball because the artifact upload between the two jobs drops executable bits,
# and a launcher without its bit is not an application. See the change's design.md D6.
set -euo pipefail

usage="usage: assemble-reader-app.sh <publish-dir> <archive.tar.gz> <version>"
publish="${1:?$usage}"
archive="${2:?$usage}"
version="${3:?$usage}"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
template="$here/../../src/GoBd.Reader.Ui/macOS/Info.plist"
icon="$here/../../src/GoBd.Reader.Ui/macOS/gobd-reader.icns"
app="GoBD Reader.app"

if [ ! -x "$publish/gobd-reader" ]; then
  echo "no executable launcher in $publish; is it an osx-arm64 publish?" >&2
  exit 1
fi

# Info.plist names it, and without it macOS shows a generic icon. It is made by
# make-reader-icons.sh. See the add-reader-icon change's design.md D6.
if [ ! -f "$icon" ]; then
  echo "no icon at $icon; make-reader-icons.sh makes it" >&2
  exit 1
fi

# macOS reads CFBundleShortVersionString as up to three integers separated by periods, so a tag
# like v0.7 arrives here as 0.7, and a build that is not a release as 0.0.0.
if ! [[ "$version" =~ ^[0-9]+(\.[0-9]+){0,2}$ ]]; then
  echo "not a bundle version: $version" >&2
  exit 2
fi

mkdir -p "$(dirname "$archive")"
archive="$(cd "$(dirname "$archive")" && pwd)/$(basename "$archive")"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

mkdir -p "$stage/$app/Contents/MacOS" "$stage/$app/Contents/Resources"
cp -Rp "$publish/." "$stage/$app/Contents/MacOS/"
cp "$icon" "$stage/$app/Contents/Resources/"
sed "s/@VERSION@/$version/g" "$template" > "$stage/$app/Contents/Info.plist"

rm -f "$archive"
tar -czf "$archive" -C "$stage" "$app"

echo "assembled $app $version: $(( $(wc -c < "$archive") / 1048576 )) MB"
