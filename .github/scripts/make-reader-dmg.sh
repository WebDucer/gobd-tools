#!/usr/bin/env bash
# Finishes the reader's macOS application bundle and puts it in a disk image.
#
# This is the one step of the reader's build that needs macOS, and it needs no .NET:
#
# - Thinning. The native libraries arrive as universal binaries, Intel and ARM, although the
#   reader is built for arm64 only; the Intel half of DuckDB alone is 57 MB nothing runs. Each
#   file is asked for its architectures rather than kept on a list here, so a library added later
#   is thinned too. Thinning keeps the signature each slice carries.
# - Signing. The launcher's signature, which the SDK writes, does not cover Info.plist, so the
#   bundle as assembled does not verify, and macOS would call it damaged. Signed as a whole, ad
#   hoc, it verifies, under the identifier Info.plist gives it. Without a Developer ID, Gatekeeper
#   still asks before the first start.
# - The image. ULMO is the smallest format hdiutil writes, and every macOS the reader supports
#   opens it. The image holds the application beside a link to /Applications, so installing is a
#   drag.
#
# See the change's design.md D6 and D9.
set -euo pipefail

usage="usage: make-reader-dmg.sh <app-archive.tar.gz> <image.dmg>"
archive="${1:?$usage}"
image="${2:?$usage}"
app="GoBD Reader.app"
identifier="de.webducer.gobd.reader"

mkdir -p "$(dirname "$image")"
image="$(cd "$(dirname "$image")" && pwd)/$(basename "$image")"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

mkdir "$stage/volume"
tar -xzf "$archive" -C "$stage/volume"
bundle="$stage/volume/$app"
if [ ! -d "$bundle/Contents/MacOS" ]; then
  echo "$archive holds no $app" >&2
  exit 1
fi

thinned=0
while IFS= read -r -d '' file; do
  # Anything that is not Mach-O, which is most of the bundle, has no architectures to report.
  archs="$(lipo -archs "$file" 2>/dev/null || true)"
  if [ -z "$archs" ] || [ "$archs" = "arm64" ]; then
    continue
  fi

  case " $archs " in
    *" arm64 "*) ;;
    *)
      echo "$file carries no arm64 code: $archs" >&2
      exit 1
      ;;
  esac

  lipo "$file" -thin arm64 -output "$file.arm64"
  chmod "$(stat -f %Lp "$file")" "$file.arm64"
  mv "$file.arm64" "$file"
  thinned=$((thinned + 1))
done < <(find "$bundle/Contents/MacOS" -type f -print0)

codesign --force --deep --sign - "$bundle"
codesign --verify --deep --strict "$bundle"

signed="$(codesign -dv "$bundle" 2>&1 | sed -n 's/^Identifier=//p')"
if [ "$signed" != "$identifier" ]; then
  echo "the bundle is signed as '$signed', not $identifier" >&2
  exit 1
fi

ln -s /Applications "$stage/volume/Applications"
rm -f "$image"

# hdiutil on a hosted runner now and then reports its device busy and succeeds when asked again.
for attempt in 1 2 3; do
  if hdiutil create -quiet -volname "GoBD Reader" -srcfolder "$stage/volume" -format ULMO -ov "$image"; then
    break
  fi
  if [ "$attempt" -eq 3 ]; then
    echo "hdiutil could not create $image" >&2
    exit 1
  fi
  sleep 5
done

hdiutil verify -quiet "$image"

echo "made $(basename "$image"): $(du -h "$image" | cut -f1), $thinned native libraries thinned to arm64, signed as $signed"
