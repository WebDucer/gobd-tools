#!/usr/bin/env bash
# Checks the rule by which a release takes its version from its tag.
#
# A release tag is v<major>, optionally followed by .<minor> and .<patch>. ComputeReleaseVersion in
# Directory.Build.targets turns it into the version every build of the release reports, and fails
# with GOBDBUILD002 for a tag it cannot read. A build without a tag keeps 0.0.0. The rule lives in
# one target that every project imports, so it is asked of both tools here without building
# either. See the prepare-public-release change's design.md D1.
set -euo pipefail

projects=(src/GoBd.Validation.Cli src/GoBd.Reader.Ui)
failures=0

version_for() {
  dotnet msbuild "$1" -nologo -t:ComputeReleaseVersion -getProperty:Version -p:ReleaseTag="$2"
}

expect_version() {
  local tag="$1" expected="$2" project actual
  for project in "${projects[@]}"; do
    if ! actual="$(version_for "$project" "$tag" 2>&1)"; then
      echo "FAIL [$tag] $project: the build failed: $actual" >&2
      failures=$((failures + 1))
    elif [ "$actual" != "$expected" ]; then
      echo "FAIL [$tag] $project: expected $expected, got $actual" >&2
      failures=$((failures + 1))
    else
      echo "ok   [$tag] $project: $actual"
    fi
  done
}

expect_refusal() {
  local tag="$1" project="${projects[0]}" output
  if output="$(version_for "$project" "$tag" 2>&1)"; then
    echo "FAIL [$tag]: the build accepted a tag that is not a release tag" >&2
    failures=$((failures + 1))
  elif ! grep -q "error GOBDBUILD002: '$tag'" <<< "$output"; then
    echo "FAIL [$tag]: the build failed without GOBDBUILD002 naming the tag: $output" >&2
    failures=$((failures + 1))
  else
    echo "ok   [$tag] refused with GOBDBUILD002"
  fi
}

expect_version "v1" "1.0.0"
expect_version "v1.2" "1.2.0"
expect_version "v0.3.7" "0.3.7"
expect_version "" "0.0.0"
expect_refusal "v1.2-rc1"
expect_refusal "v1.2.3.4"
expect_refusal "v70000"

if [ "$failures" -ne 0 ]; then
  echo "$failures check(s) of the release version rule failed" >&2
  exit 1
fi
