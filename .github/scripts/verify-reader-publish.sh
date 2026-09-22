#!/usr/bin/env bash
# Checks that a published reader has the shape a person downloads, before it goes any further.
#
# Windows and Linux: one executable file and nothing beside it. The Windows file has to be a GUI
# program, or a console window opens behind the reader; its manifest has to name the reader; and
# it has to carry the reader's icon. macOS: a self-contained folder carrying the launcher, the
# runtime and the native store, which assemble-reader-app.sh turns into the application bundle.
# Everywhere: no debug symbol files.
#
# A build that got any of this wrong would still start on the machine that built it, be
# downloaded, and fail or sprawl on an auditor's machine, and nothing in the pipeline would have
# said so. See the repackage-reader change's design.md D1, D5 and D9, and the add-reader-icon
# change's D4.
set -euo pipefail

usage="usage: verify-reader-publish.sh <publish-dir> <rid>"
publish="${1:?$usage}"
rid="${2:?$usage}"
identifier="de.webducer.gobd.reader"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
icon="$here/../../src/GoBd.Reader.Ui/Assets/gobd-reader.ico"

fail() {
  echo "$*" >&2
  exit 1
}

[ -d "$publish" ] || fail "no publish directory at $publish"

symbols="$(find "$publish" -name '*.pdb' | tr '\n' ' ')"
[ -z "$symbols" ] || fail "debug symbol files in $publish: $symbols"

case "$rid" in
  win-* | linux-*)
    launcher="gobd-reader"
    if [[ "$rid" == win-* ]]; then
      launcher="gobd-reader.exe"
    fi

    entries="$(ls -A "$publish" | tr '\n' ' ')"
    [ "$entries" = "$launcher " ] || fail "expected $launcher alone in $publish, found: $entries"

    if [[ "$rid" == win-* ]]; then
      file -b "$publish/$launcher" | grep -q '(GUI)' \
        || fail "$launcher is not a GUI program: $(file -b "$publish/$launcher")"
      grep -a -q "name=\"$identifier\"" "$publish/$launcher" \
        || fail "$launcher carries no manifest naming $identifier"

      # An .ico keeps each image as it is, and each becomes a resource of the executable
      # unchanged. The assemblies inside the single file are compressed, so finding the 256 px
      # image byte for byte can only mean the executable's own resources hold it. grep reads
      # lines, not bytes, hence Python.
      python3 - "$icon" "$publish/$launcher" <<'PY' || fail "$launcher does not carry $icon"
import struct
import sys

icon, executable = (open(path, "rb").read() for path in sys.argv[1:])
count = struct.unpack("<H", icon[4:6])[0]
for entry in range(count):
    start = 6 + 16 * entry
    width, _, _, _, _, _, size, offset = struct.unpack("<BBBBHHII", icon[start:start + 16])
    # A width of 0 stands for 256.
    if width == 0:
        sys.exit(0 if icon[offset:offset + size] in executable else 1)
sys.exit(1)
PY
    else
      [ -x "$publish/$launcher" ] || fail "$launcher is not executable"
    fi

    echo "reader for $rid is one file: $launcher, $(du -h "$publish/$launcher" | cut -f1)"
    ;;
  osx-*)
    for required in gobd-reader libcoreclr.dylib libduckdb.dylib gobd-reader.dll GoBd.Reader.Data.dll GoBd.Validation.dll; do
      [ -f "$publish/$required" ] || fail "missing from $publish: $required"
    done
    [ -x "$publish/gobd-reader" ] || fail "gobd-reader is not executable"

    echo "reader for $rid carries its launcher, runtime and store"
    ;;
  *)
    echo "unknown runtime identifier: $rid" >&2
    exit 2
    ;;
esac
