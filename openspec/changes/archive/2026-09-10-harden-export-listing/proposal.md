# Harden the export listing, and let a finding say what it is about

## Why

A security and correctness review of the whole repository found a defect the specs did not
forbid, because they never said the listing was a trust boundary.

`ExportPath.Normalise` maps `\` to `/`, and a backslash is a legal filename character on Linux
and macOS. A single file inside an export named literally `..\outside\secret.dtd` therefore
produced the entry name `../outside/secret.dtd`, which `OpenRead` rebuilt with
`Path.Combine(root, name)` and opened with no confinement. A symbolic link named
`gdpdu-01-03-2019.dtd` needed no trickery at all and worked on any platform.

Because `DtdCopyLocator` selects any `.dtd` entry and hashes it, an export handed to an auditor
could make the validator read a file anywhere the operator can reach and publish its SHA-256 in
the report — a hash oracle — while a failed open leaked the absolute path through `GOBD9004`.
Both routes were reproduced end to end.

Two further gaps surfaced alongside it:

- **A colliding entry name was resolved silently.** Two entries can normalise to one name — a
  folder holding `data/x.csv` beside a file literally named `data\x.csv`, or a ZIP carrying the
  same member twice, which the format permits and which is a known spoofing vector. The
  validator picked one and said nothing, so two importers could reach two conclusions from one
  medium. (Before that it threw an uncatchable `ArgumentException` and crashed.)
- **The reported scope was inferred, not stated.** `scope` is a published JSON field, and it was
  recovered by indexing into a finding's positional arguments using an index declared per code.
  Reordering a check's arguments to improve its wording silently changed it, and nothing — no
  compiler check, no test — noticed.

The code for all three was implemented before this change was written. These artifacts record the
behaviour as specified, so the specs stop being silent about a boundary the implementation now
enforces.

## What Changes

- **The export listing becomes an explicit trust boundary.** An entry whose normalised name
  escapes the export root, and any symbolic link, never appears in the listing at all; every
  read is additionally confined to the root.
- **`GOBD1007` reports colliding entry names**, so an ambiguous medium is reported rather than
  resolved silently in whichever direction the implementation happens to pick.
- **A finding states what it concerns.** `FindingScope` carries a kind — table, medium,
  extension, DTD, export — and a name, set by the check that produced the finding.

### Non-goals

- **No change to the JSON contract.** `scope` remains the flat name it always was, and every
  scope value is unchanged, so `schemaVersion` does not move. The kind is carried internally for
  the deferred UI and deliberately not exposed.
- **No new report format, option or exit code.**
- **Nothing is extracted.** The ZIP filtering is precautionary for v2, which will open data
  streams from the same listing; today no ZIP entry name can reach the filesystem.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `export-source`: adds requirements that the listing never exposes anything outside the export
  root and that colliding entry names are reported; strengthens the existing presence
  requirement accordingly.
- `validation-reporting`: modifies the requirement describing what a finding carries, so that
  what a finding is about is part of the finding rather than something a reader reconstructs.

## Impact

- **Code** (implemented before this change was written): `ExportPath.IsContainedName`,
  `ExportEntryIndex`, `FolderExportSource`, `ZipExportSource`, `ExportSourceFactory`, `IExportSource`,
  `FindingScope`, `Finding`, `FindingCodes`, `FindingGrouping`, and the 41 check call sites that
  now attribute their findings.
- **Documentation**: `docs/finding-codes.md` gained the `GOBD1007` entry, which the coverage
  test requires.
- **Compatibility**: the JSON report's shape and every scope value are unchanged. A previously
  crashing input now produces a finding; a previously readable out-of-root file is now invisible.
- **Security posture**: the listing is the boundary that keeps an untrusted medium from reaching
  the rest of the filesystem, and it is now specified as such rather than being so by accident.
