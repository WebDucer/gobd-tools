## Why

Using the reader on a real export showed where its workflow falls short. A large table freezes
the window while it imports. The validation outcome only appears table by table, as each is
opened. Navigation leaves stale state behind: a Previous/Next bar with disabled buttons, a status
message about a different table, and a navigator selection that no longer matches what is shown.
Nothing distinguishes a value that can be followed from one that cannot. Separately, the CLI
crashes when given a folder path with a trailing separator, which shell tab completion produces
routinely.

## What Changes

- **Validate on open.** Opening an export imports every table and runs the content and key
  checks up front, in the background, with visible progress and the option to cancel. The
  window stays responsive throughout, and each table becomes openable as soon as its own import
  finishes.
- **A summary on the start page.** It shows the verdict, each table's status, and the findings
  grouped by table, under the same codes the validator reports.
- **One tab per table.** Choosing a table or following a reference opens it in its own tab, and
  that tab is reused when the table is reached again. The navigator selection follows the active
  tab.
- **The walk bar belongs to the walk.** Previous and Next appear only while stepping through the
  records that refer to one record. They sit in that table's tab and disappear when the walk ends
  or the tab is left. No status message outlives the table it describes.
- **References in the navigator.** Beneath each table the navigator lists the tables it
  references and the tables that reference it. These entries are informational and do not
  navigate.
- **Cells that can be followed look different.** Values that take part in a foreign key are styled
  so they read as followable.
- **A folder path with a trailing separator** (`gobd-validate export/`) opens the same export as
  `gobd-validate export`. Today it crashes. This is a bug fix.

### The per-table gate is assumed unchanged

The per-table gate stays on record conformance, as decided in D13 of `add-export-reader`. Checking
keys up front makes dangling references known before any table is opened, but they do not hide a
table's data. They are reported in the summary and marked on the cells that carry them. A table
of a million records with one reference that does not resolve is still a table someone needs to
read.

### Non-goals

- No query surface. There is no sorting, filtering or searching, and navigation still positions
  rather than filters.
- The navigator's reference entries do not navigate. Following a reference stays a record-level
  action on a cell.
- No store is kept between sessions. Reopening an export imports it again, as today.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `reader-ui`: the export is validated in full when it is opened, without the window freezing;
  a summary is presented before any table is chosen; each table occupies one tab; the navigator
  shows each table's relationships as information; followable cells are recognisable; and the
  backwards walk and every navigation message are confined to the table they concern.
- `export-source`: a folder export named with a trailing path separator is the same export as
  the one named without it.

## Impact

- **`GoBd.Reader.Ui`** gains the start page with its summary and progress, a tab host in place
  of the single content area, relationship entries in the navigator, per-column cell styling for
  foreign keys, and a walk bar owned by each tab. The session's opening becomes an asynchronous,
  cancellable pipeline.
- **`GoBd.Reader.Data`** reports progress while it extracts, imports off the UI thread, and must
  keep serving pages of finished tables while later tables import.
- **`GoBd.Validation`** normalises a folder root, so the CLI fix ships in the validator.
- **Opening costs more up front.** A multi-gigabyte export takes minutes before its summary is
  complete. That is the price of validating everything on open. It reverses D13's lazy import,
  though not its gate, and progressive readiness means no table waits for the last one to finish.
- **The reader names itself.** It publishes as a bare executable rather than a bundle, so there is
  no `Info.plist` for macOS to read and the menu bar showed Avalonia's default. Unrelated to the
  workflow, fixed alongside it.
- No new dependencies and no new finding codes. The JSON report is unchanged.
