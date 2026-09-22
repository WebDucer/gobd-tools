# Read the data files: content validation, and a desktop reader

## Why

v1 answers "is this export *described* correctly?" and deliberately never opens a data file. The
question a producer actually needs answered is "is this export *right*?" — and the defects that
get an export rejected live in the data: a customer number that references no customer, a date
that does not match its declared mask, a duplicate primary key, a column order that silently
shifted because an undeclared header row is present.

Those defects are also invisible in a spreadsheet. A GoBD CSV frequently carries no header row
at all, so the column names exist **only** in `index.xml`. Opening `bestellungen.csv` in Excel
shows anonymous columns of undifferentiated text; only something holding the declaration beside
the file can render it as what it claims to be.

## What Changes

- **Content validation becomes a capability of the library**, so both the CLI and the UI check
  the same things and report them through the same finding codes. Three passes: record
  conformance, primary key uniqueness, and foreign key existence.
- **The CLI gains a content mode.** Companies producing exports can fail a pipeline on "a
  foreign key references a customer that does not exist" — the check most worth gating on. It
  stays a single dependency-free AOT binary; the checks are streaming and hash-based, with a
  documented row ceiling above which the reader is the right tool.
- **A desktop reader** presents the export: a navigator over `DataSet → Media → Table`, a
  read-only grid rendering each table as its declaration defines it, and navigation along
  foreign keys by clicking a cell. It opens the export it is given on the command line, or one
  the person chooses in the application — a ZIP archive or an unpacked folder, since the
  standard permits both.
- **BREAKING (v1 promise):** the CLI's help text no longer states unconditionally that data
  file contents are not examined; it states the scope of the mode being run.

### Non-goals

- **No query surface.** No user-authored filtering, sorting, grouping or aggregation. Row order
  is file order, so a row number stays citable in an audit.
- **No editing.** Nothing writes to the export, ever. It is a data carrier.
- **No semantic checks.** Nothing tells us a `Date` column means the transaction date rather
  than a delivery date or a birthday, so declared `Validity` periods are not compared against
  values. Only a value that fails its own declared `Format` is a defect.
- **No Tier 1/Tier 2 statistical analysis.** Benford, gap analysis and the rest remain out.

## Capabilities

### New Capabilities

- `content-validation`: what the system detects when it reads a data file — record conformance
  against the declared layout, header rows, primary key uniqueness, foreign key existence — and
  the bounded way it reports what it finds.
- `reader-ui`: the desktop reader — navigating the declared structure, showing a table only when
  it is consistent, rendering values as declared, and following foreign keys between tables.

### Modified Capabilities

- `export-source`: today it states that the system SHALL NOT read the content of any data file.
  That prohibition must narrow to what it was protecting — presence checking — so that content
  validation does not contradict it.
- `validator-cli`: the command surface must state the scope of the mode being run rather than
  promising unconditionally that contents are not examined, and must expose the content mode and
  what happens when an export exceeds what the CLI can check.

## Impact

- **New projects.** `GoBd.Reader.Data` (DuckDB via `DuckDB.NET`) and `GoBd.Reader.Ui` (Avalonia,
  rendering with the core `TableView`). Both dependencies are MIT, so the repository stays wholly
  MIT with no entitlement anyone has to acquire. `GoBd.Validation` gains the streaming content
  checks and stays pure managed with no native dependency; `GoBd.Validation.Cli` keeps its
  single-binary AOT publish and never references the store.
- **Reuses what exists.** `ForeignKeyResolver` already maps foreign key columns onto their
  target key columns, including aliases and the positional fallback. The model already carries
  every declaration the reader needs: delimiters, encapsulator, codepage, decimal and grouping
  symbols, type formats, fixed-length spans, `Range`, `SkipNumBytes`, `Epoch` and `Map`.
- **Finding codes.** New bands in the existing `GOBD` space: `4xxx` record conformance, `5xxx`
  key and referential integrity. Each new code inherits the existing discipline — an entry in
  `docs/finding-codes.md`, both translations, and the two-way coverage test.
- **Sequencing.** `content-validation` is independently shippable and useful on its own: it can
  land in the library and the CLI before any UI exists, and the reader is then a viewer over an
  engine that already works.
- **Distribution.** The reader is built per platform by the same pipeline that builds the CLI,
  and downloadable from it: a per-commit artifact from CI, kept for 48 hours, and a versioned
  build attached to each release beside the CLI. Unlike the CLI it is a self-contained directory
  rather than a single native file — it is not AOT, and it carries a native store. That is a
  ~220 MB build per platform, 117 MB of which is DuckDB's own library. Acceptable for something
  an auditor installs once, and exactly why the CLI a pipeline runs on every export is a separate
  binary that costs 10 MB and has nothing to install.
- **Documentation.** The README becomes the entry point for both tools rather than the CLI
  alone, and stays short: how to use each, not why it was built that way.
- **Cost carried deliberately.** The content checks are implemented twice — streaming in the
  library so the CLI stays one file, and over the store so the reader stays interactive. The
  specs are written as observable defects rather than as an algorithm precisely so both engines
  can be held to the same contract.
