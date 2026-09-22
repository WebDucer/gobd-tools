#!/usr/bin/env bash
# Starts the Linux reader under a virtual display and waits for it to open an export.
#
# The reader opens a window rather than printing a verdict, so the pipeline cannot run it the way
# it runs the validator. What can be seen from outside is its store: given an export on its
# command line, the reader starts importing at once, into a directory under TMPDIR that holds
# store.duckdb. That file appearing means the single file unpacked its native libraries, the
# trimmed reader started, and the store's engine loaded, which are the things trimming and
# unpacking could break without a single warning. See the change's design.md D8.
#
# The directory holding the stores is the account's own, gobd-reader-<user>, and has to be
# readable by that account alone: the export's data is extracted into it. See the
# prepare-public-release change's design.md D6.
set -euo pipefail

usage="usage: smoke-start-reader.sh <gobd-reader> <export> [timeout-seconds]"
reader="${1:?$usage}"
export_path="${2:?$usage}"
timeout="${3:-60}"

command -v xvfb-run >/dev/null 2>&1 || {
  echo "xvfb-run is needed to start the reader without a display" >&2
  exit 2
}

reader="$(cd "$(dirname "$reader")" && pwd)/$(basename "$reader")"
work="$(mktemp -d)"
group=""

stop() {
  # The reader runs under xvfb-run, which runs a display server beside it. Both are ended by
  # ending the process group they were started in.
  if [ -n "$group" ]; then
    kill -TERM -- "-$group" 2>/dev/null || true
    wait "$group" 2>/dev/null || true
  fi
  rm -rf "$work"
}
trap stop EXIT

mkdir -p "$work/tmp" "$work/unpacked"
export TMPDIR="$work/tmp"
export DOTNET_BUNDLE_EXTRACT_BASE_DIR="$work/unpacked"

setsid xvfb-run --auto-servernum "$reader" "$export_path" > "$work/reader.log" 2>&1 &
group=$!

started=$SECONDS
while [ $((SECONDS - started)) -lt "$timeout" ]; do
  if store="$(compgen -G "$TMPDIR/gobd-reader-*/*/store.duckdb")"; then
    root="$(dirname "$(dirname "$(head -n 1 <<< "$store")")")"
    mode="$(stat -c '%a' "$root" 2>/dev/null || stat -f '%Lp' "$root")"
    if [ "$mode" != "700" ]; then
      echo "the reader keeps its stores in $root with mode $mode, readable by others" >&2
      exit 1
    fi

    unpacked="$(du -sh "$DOTNET_BUNDLE_EXTRACT_BASE_DIR" | cut -f1)"
    echo "the reader unpacked $unpacked, started and opened its store in $root (mode $mode) within $((SECONDS - started))s"
    exit 0
  fi

  if ! kill -0 "$group" 2>/dev/null; then
    echo "the reader exited before it opened its store:" >&2
    cat "$work/reader.log" >&2
    exit 1
  fi

  sleep 1
done

echo "the reader opened no store within ${timeout}s:" >&2
cat "$work/reader.log" >&2
exit 1
