# GoBD Export Validator — v1 (core library + CLI)

## Why

Companies that produce GoBD/GDPdU data-carrier exports ("Datenträgerüberlassung") have no
cheap, unattended way to check an export before handing it to a tax auditor. Today the only
reliable verification is to import the export into auditor-side software (IDEA + SmartX),
which is commercial, manual, and happens *after* delivery — so defects surface as audit
friction and rework rather than as a failed build.

The defects are also easy to produce and hard to see by eye: a `ForeignKey` naming a column
that was never declared, a `References` pointing at a table that is not in the `DataSet`, an
`Alias` mapping onto a non-key column, a modified `gdpdu-*.dtd` shipped alongside the data.
All of these are detectable from `index.xml` and the archive's file listing alone, in
milliseconds, without reading a single byte of the (potentially multi-GB) data files.

v1 makes that check a build step: a single self-contained binary that a producer can run in
CI or in the export pipeline and get a machine-readable verdict from.

## What Changes

- **New .NET 10 solution**: a UI-agnostic core library plus a NativeAOT console executable.
- **Export sources**: validate either a **ZIP archive** or an **unpacked folder**. Both are
  presented to the validator through one abstraction; only `index.xml` and the DTD entry are
  ever read.
- **DTD conformance**: `index.xml` is validated against **Beschreibungsstandard 1.6**
  (`gdpdu-01-03-2019.dtd`), always using the canonical DTD embedded in the application. The
  DTD copy shipped inside the export is never used to drive the parse; it is compared to the
  canonical bytes as a separate finding.
- **Tier 0 check catalogue**: metadata-only semantic checks over the parsed `index.xml` and
  the archive's entry listing — foreign keys, aliases, primary keys, table-name resolution,
  URL resolution and file presence, fixed-length ranges, the 1.6 `Map`-as-Time convention,
  and `Media`/`AcceptNoTables` coherence.
- **Findings model**: every check yields a finding with a stable code, a severity
  (`error` / `warning` / `info`), and a source location in `index.xml`.
- **Two reporters**: human-readable text and JSON (for CI and agent consumption).
- **Exit codes** so the CLI can gate a pipeline, with `--strict` promoting warnings to errors.

### Non-goals for v1 (deliberate, revisit in v2)

- **No data-file reading.** Column counts, type parseability, encodings, delimiters,
  `MaxLength` and record lengths are Tier 1 and are deferred. An export can pass v1 and still
  contain malformed CSV; the reports must say so plainly.
- **No cross-row or cross-table checks.** Primary-key uniqueness and foreign-key *value*
  integrity are Tier 2, deferred to v2 alongside DuckDB.
- **No UI.** The desktop viewer and foreign-key navigation are out of scope; the framework
  choice (Avalonia / Uno) is deliberately left open.
- **Never execute `Command` elements.** The standard defines `Command` as OS commands run
  around the import (real exports ship `uncompress.bat`). The validator only ever *reports*
  their presence. This is a security boundary, not an unimplemented feature.
- **No version negotiation.** Standard 1.6 is a strict superset of 1.1, so every older
  conformant `index.xml` also validates against the 1.6 grammar. One grammar, no fallbacks.

## Capabilities

### New Capabilities

- `export-source`: presenting a GoBD export — ZIP archive or unpacked folder — as an
  enumerable set of entries, resolving the standard's relative `URL` values to entries, and
  bounding all access inside the export root.
- `dtd-validation`: validating `index.xml` against the canonical embedded Beschreibungs-
  standard 1.6 DTD, and reporting on the DTD copy shipped inside the export.
- `structural-validation`: the Tier 0 semantic check catalogue over the parsed `index.xml`
  and the entry listing.
- `validation-reporting`: the findings model (code, severity, location), severity policy,
  and the text and JSON report formats.
- `validator-cli`: the command-line surface — arguments, source auto-detection, reporter
  selection, and exit codes.

### Modified Capabilities

None — this is a greenfield repository with no existing specs.

## Impact

- **Repository**: currently empty (no commits, no source). This change introduces the entire
  solution structure, so there is no existing code to migrate or break.
- **Toolchain**: .NET 10 SDK (10.0.300 present). NativeAOT publishing per RID.
- **Dependencies**: intentionally minimal. `System.Xml` for DTD validation,
  `System.IO.Compression` for ZIP, `System.Text.Json` with source generation. A spike
  confirmed `CodePagesEncodingProvider` needs no `PackageReference` on .NET 10 and that the
  whole path publishes NativeAOT with zero trim warnings.
- **Normative references**: `src/GoBd.Validation/Resources/gdpdu-01-03-2019.dtd` is checked in and embedded as an
  assembly resource — the standard requires every data carrier to ship it, so redistributing it
  is what the standard intends. The specification document is published by Audicon GmbH and is
  *not* redistributed here; obtain it from
  <https://www.caseware.com/de/beschreibungsstandard>. Where the two disagree, the `.dtd` file
  is authoritative.
- **Deferred cost**: v2 will add data-file streaming and DuckDB. `export-source` is shaped
  now so that opening a data stream is an additive change, not a rewrite.
