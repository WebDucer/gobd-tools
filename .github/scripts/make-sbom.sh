#!/usr/bin/env bash
# Makes the software bill of materials of each tool: gobd-validate.cdx.json and gobd-reader.cdx.json,
# in CycloneDX 1.6 JSON, one per tool for all the platforms it is built for.
#
# 1. Each tool project is restored for all five runtime identifiers, so that every platform's
#    native packages are resolved.
# 2. The CycloneDX .NET tool, pinned in .config/dotnet-tools.json, describes the project's resolved
#    packages. It does not know what ships and what only builds, so every package the assets file
#    shows carrying no runtime, native or runtime-specific assets is excluded by name. That is the
#    rule ThirdPartyNoticesTests applies, so the two cannot come to disagree about what ships.
# 3. The .NET runtime is added. The SDK supplies it as a runtime pack, not as a package the project
#    resolves, so the tool never lists it, and it is the component most published vulnerabilities
#    concern. It carries the version the build used, and the CPE vulnerability databases know .NET by.
# 4. The file's packages are compared with the Packages: lines of THIRD-PARTY-NOTICES.txt, and the
#    script fails, naming the difference, when they part.
#
# Run from the repository root. <version> is the version the tools report, such as 1.0.0.0; ask
# the build for it with -t:GetAssemblyVersion -getProperty:AssemblyVersion. See the
# prepare-public-release change's design.md D11.
set -euo pipefail

usage="usage: make-sbom.sh <version> <out-dir>"
version="${1:?$usage}"
out="${2:?$usage}"
rids="linux-x64;linux-arm64;win-x64;win-arm64;osx-arm64"
notices="THIRD-PARTY-NOTICES.txt"

command -v python3 > /dev/null || { echo "python3 is needed to finish the bills of materials" >&2; exit 2; }
[ -f "$notices" ] || { echo "run from the repository root: $notices is not here" >&2; exit 2; }

mkdir -p "$out"
dotnet tool restore > /dev/null
runtime="$(dotnet msbuild src/GoBd.Reader.Ui -nologo -getProperty:BundledNETCoreAppPackageVersion)"

# Prints, comma-separated, the packages an assets file resolves that no build ships.
unshipped() {
  python3 - "$1" <<'PY'
import json, sys

assets = json.load(open(sys.argv[1], encoding="utf-8"))

def carries(library):
    return any(not asset.endswith("/_._")
               for kind in ("runtime", "native", "runtimeTargets")
               for asset in library.get(kind, {}))

shipped = {key.split("/")[0]
           for target in assets["targets"].values()
           for key, library in target.items()
           if library.get("type") == "package" and carries(library)}
resolved = {key.split("/")[0] for key, library in assets["libraries"].items() if library.get("type") == "package"}
print(",".join(sorted(resolved - shipped)))
PY
}

# Adds the .NET runtime, then compares the packages with those the notices name for the tool.
finish() {
  python3 - "$1" "$runtime" "$notices" "$2" <<'PY'
import json, sys

path, runtime, notices, section = sys.argv[1:5]
bom = json.load(open(path, encoding="utf-8"))

ref = "Microsoft.NETCore.App@" + runtime
bom.setdefault("components", []).append({
    "type": "framework",
    "bom-ref": ref,
    "name": "Microsoft.NETCore.App",
    "version": runtime,
    "description": ".NET runtime, part of the build as the SDK's runtime pack",
    "licenses": [{"license": {"id": "MIT"}}],
    "cpe": f"cpe:2.3:a:microsoft:.net:{runtime}:*:*:*:*:*:*:*",
    "scope": "required",
})
root = bom["metadata"]["component"]["bom-ref"]
dependencies = bom.setdefault("dependencies", [])
for entry in dependencies:
    if entry["ref"] == root:
        entry.setdefault("dependsOn", []).append(ref)
        break
else:
    dependencies.append({"ref": root, "dependsOn": [ref]})
dependencies.append({"ref": ref})

with open(path, "w", encoding="utf-8") as file:
    json.dump(bom, file, indent=2)
    file.write("\n")

lines = open(notices, encoding="utf-8").read().splitlines()
named, current = {}, ""
index = 0
while index < len(lines):
    line = lines[index]
    if line.startswith("====") and index + 2 < len(lines) and lines[index + 2].startswith("===="):
        current = lines[index + 1]
        index += 3
        continue
    if line.startswith("Packages: ") and not line.startswith("Packages: ("):
        named.setdefault(current, set()).update(
            part.strip() for part in line[len("Packages: "):].split(",") if part.strip())
    index += 1

expected = named.get(section, set()) if section else set().union(*named.values())
listed = {component["name"] for component in bom["components"] if component["bom-ref"] != ref}
missing, extra = sorted(listed - expected), sorted(expected - listed)
if missing or extra:
    if missing:
        print(f"{path} lists packages {notices} names no notice for: {', '.join(missing)}", file=sys.stderr)
    if extra:
        print(f"{notices} names packages {path} does not list: {', '.join(extra)}", file=sys.stderr)
    sys.exit(1)

print(f"{path}: {len(listed)} package(s) and the .NET runtime {runtime}, matching {notices}")
PY
}

make() {
  local tool="$1" project="$2" section="$3" excluded
  local args=(--exclude-dev --spec-version 1.6 --output-format Json
    --set-name "$tool" --set-version "$version" --set-type Application
    --output "$out" --filename "$tool.cdx.json" --disable-package-restore)

  dotnet restore "$project" -p:RuntimeIdentifiers="\"$rids\"" > /dev/null
  excluded="$(unshipped "$project/obj/project.assets.json")"
  if [ -n "$excluded" ]; then
    args+=(--exclude-filter "$excluded")
  fi

  dotnet dotnet-CycloneDX "$project/$(basename "$project").csproj" "${args[@]}" > /dev/null
  finish "$out/$tool.cdx.json" "$section"
}

# The validator carries only what both tools carry; the reader, everything the notices name.
make gobd-validate src/GoBd.Validation.Cli "In gobd-validate and gobd-reader"
make gobd-reader src/GoBd.Reader.Ui ""
