## 1. De-risk the grid (spike)

Runs first because it is the only task whose outcome could still move a design decision. It
touches no production code — a throwaway project, deleted when the answer is recorded.

- [x] 1.1 Bind Avalonia `TableView` to an `IList` that serves a window of synthetic records and
      materialises nothing outside it; verify a 5,000,000-record source scrolls smoothly and that
      working-set memory stays flat while scrolling from start to end
- [x] 1.2 Jump to record 3,847,221 and select it — the primitive foreign key navigation depends
      on; verify the view positions there and the record is indicated, with no full materialisation
- [x] 1.3 Repeat 1.1 with a table of ~300 columns; verify what the absence of column
      virtualisation costs, since every column is always realised
- [x] 1.4 Record the outcome in `design.md` under D6 — confirmed, or the fallback taken. If the
      spike fails, stop and revise D6 before continuing; verify `design.md` states which

## 2. Read a data file as its declaration defines it

Groups 2–6 deliver `content-validation` and the CLI, which are independently shippable ahead of
any UI.

- [x] 2.1 Add the record reader over a declared `VariableLength` table — declared column
      delimiter, record delimiter and text encapsulator; verify against fixtures covering an
      encapsulated value containing the delimiter, an encapsulated value containing the record
      delimiter, and a doubled encapsulator
- [x] 2.2 Add fixed-length reading using each column's declared span; verify a fixture whose
      columns are read at the declared positions, including a span that runs to the record end
- [x] 2.3 Decode bytes using the declared codepage rather than a guess; verify a non-UTF-8
      fixture decodes correctly and that a byte sequence invalid in the declared codepage is
      surfaced rather than replaced silently
- [x] 2.4 Apply the declared record range and byte skip; verify excluded records are not reported
      as data records and that the first data record is the one the declaration implies
- [x] 2.5 Interpret values by declared type — `Numeric` under the table's decimal and grouping
      symbols, `Date` under the column's format mask, `AlphaNumeric` — and apply the declared
      two-digit-year window; verify a table-level symbol change alters interpretation of the same
      bytes
- [x] 2.6 Apply declared `Map` redefinitions while keeping the stored value retrievable; verify a
      mapped value presents as its target and its raw form is still available
- [x] 2.7 Confirm the reader never consults the file for structure: verify by a test that a file
      whose first record contains plausible column names is still read as data

## 3. Report records that do not conform

- [x] 3.1 Add the `4xxx` code band with a finding per defect class — type/format mismatch,
      accuracy exceeded, maximum length exceeded, column count mismatch, undecodable bytes,
      unterminated encapsulator, fixed-length record length mismatch; verify each has an entry in
      `docs/finding-codes.md`, both translations, and that the two-way coverage test passes
- [x] 3.2 Attribute every content finding to its table, record and column; verify the reported
      subject survives rewording a message template, per the existing scope guard
- [x] 3.3 Carry the offending value alone in a finding, never the surrounding record; verify by a
      test that plants a recognisable canary in neighbouring columns of a defective record and
      asserts it appears in no report, in either format
- [x] 3.4 Implement the finding bound as a stop, defined as the first N by record order, with N
      configurable and defaulting to 50 per table; verify a fixture with more defects than the
      bound stops early, reports exactly the first N by record number, and states that analysis
      stopped
- [x] 3.5 Verify raising the bound yields a superset of the smaller bound's findings on the same
      fixture, and that the verdict is identical under both
- [x] 3.6 Verify a table with fewer defects than the bound is read to its end and reports all of
      them

## 4. Check the header row

- [x] 4.1 Compare an excluded first record against the declared column names; verify the exact
      match reports nothing
- [x] 4.2 Report an error when the excluded record carries the declared names in a different
      order; verify the finding names the table and both orders, since this is the case where
      every value is otherwise attributed to the wrong column
- [x] 4.3 Stay silent when the excluded record bears no resemblance to the declared names; verify
      a preamble fixture produces no header finding
- [x] 4.4 Report an undeclared header — no record excluded, but record 1 carries the declared
      names; verify the finding fires and names the record that would otherwise be read as data

## 5. Check declared keys

- [x] 5.1 Add the `5xxx` code band for key and referential integrity; verify documentation, both
      translations and the coverage test as in 3.1
- [x] 5.2 Build the sorted 64-bit key-hash array for a table's declared primary key; verify eight
      bytes per record by measurement, not by assertion
- [x] 5.3 Report duplicate primary keys, confirming each candidate against the actual key values
      so a hash collision cannot produce a false positive; verify with a fixture containing a
      deliberate collision
- [x] 5.4 Report an empty primary key and a partially present composite key; verify both
- [x] 5.5 Check foreign key values against the referenced table's key array, mapping columns via
      the existing `ForeignKeyResolver`; verify a dangling value is reported and a composite key
      is matched as a tuple rather than column by column
- [x] 5.6 Reuse one parent's array across every inbound foreign key and free it before the next
      parent; verify peak memory tracks the largest single table rather than the sum, by
      measuring a fixture with several referenced tables
- [x] 5.7 Report that a reference could not be checked when the referenced table could not be
      read, rather than reporting its values as unresolved; verify with a fixture whose parent is
      malformed
- [x] 5.8 Report a table beyond the engine's capacity as unchecked, and ensure the verdict is not
      conformant on the strength of a check that did not run; verify by lowering the budget in a
      test rather than by generating a 64-million-record fixture

## 6. Expose content checking from the CLI

- [x] 6.1 Add the opt-in content option; verify that without it no data file is opened, by a test
      that counts opens through the export source
- [x] 6.2 Fold content findings into the existing verdict and exit-code rules; verify a content
      error yields the same exit code as a structural error, and that `--strict` behaves as it
      does for warnings today
- [x] 6.3 Add the option that sets the per-table finding bound, defaulting to 50; verify the
      default applies unasked and that an explicit value is honoured
- [x] 6.4 Update the help text to state the scope of the mode being run, the bound and its
      default, and to make the key-check capacity limit discoverable; verify the help output and
      update the README usage block
- [x] 6.5 State in every report whether contents were examined; verify in both text and JSON, and
      decide and record whether this moves `schemaVersion`
- [x] 6.6 Publish with `PublishAot` and verify content checking works from the single native
      executable with no additional file present — the property that makes the CLI adoptable
- [x] 6.7 Verify the 12 existing baseline reports are unchanged when the content option is absent

## 7. Reader data layer

- [x] 7.1 Add `GoBd.Reader.Data` referencing `DuckDB.NET`; verify by a build guard that
      `GoBd.Validation` and `GoBd.Validation.Cli` cannot reference it
- [x] 7.2 Import a table with declared column names and types, `header` off always, and a
      materialised file-order ordinal; verify the ordinal is dense, starts at the first data
      record, and matches the record numbers the streaming engine reports
- [x] 7.3 Store under the platform temporary location keyed by a content hash; verify nothing is
      ever written beside the export, and that the location is overridable so it can be pointed
      at real disk where the temp location is RAM-backed
- [x] 7.4 Check free space on the store's actual backing volume before writing the first byte,
      estimated from the declared files' total size; verify an import that would not fit is
      refused up front rather than failing part-way, and that the check measures the backing
      volume rather than assuming the temp path is on disk
- [x] 7.5 Delete this session's store on close, and on start delete stores whose owning process
      is no longer alive; verify a store held by a second live instance survives startup cleanup,
      and that an abandoned store from a killed process is removed
- [x] 7.6 Serve paged reads by ordinal range and total count; verify a page request materialises
      only that page
- [x] 7.7 Implement key checks over the store; verify they produce the same findings as group 5
      on the same fixtures
- [x] 7.8 Collect import rejects and order them by line number before taking the first N; verify
      repeated runs of the same defective file report identical findings despite parallel reading

## 8. Hold the two engines to each other

- [x] 8.1 Run every content fixture through both engines and compare findings by code, record,
      column and order; verify they match exactly, and that the test fails if either engine is
      changed alone
- [x] 8.2 Add fixtures for the readings the declaration does not define — an encapsulator inside
      an encapsulated value, an encapsulator mid-field in an unencapsulated one, and a trailing
      record delimiter; verify both engines agree, with the store's reading taken as correct
- [x] 8.3 Add a fixture per defect class from groups 3–5 so the comparison covers each; verify by
      a coverage assertion that no `4xxx` or `5xxx` code is absent from the comparison set

## 9. Reader shell and grid

- [x] 9.1 Add `GoBd.Reader.Ui` on Avalonia with the navigator over `DataSet → Media → Table`,
      columns excluded; verify a multi-medium fixture renders both media and their tables
- [x] 9.2 Show, for a selected table, the tables it references and the tables that reference it;
      verify against a fixture where a table has two parents and one child, and where a reference
      crosses a medium boundary
- [x] 9.3 Gate per table: present data when the table conforms, present its findings when it does
      not; verify one defective table does not withhold the others in the same export
- [x] 9.4 State when a findings list was truncated by the bound; verify the presentation does not
      imply the list is exhaustive
- [x] 9.5 Render the grid with `TableView` over the paging list from group 7, with declared column
      names, declared formatting and file record order; verify a table of several million records
      scrolls at flat memory in the real application, not only in the spike
- [x] 9.6 Verify no sorting, filtering, grouping or aggregation is reachable, and that nothing in
      the reader writes to the export

## 10. Navigate by foreign key

- [x] 10.1 Follow a foreign key from a value to the referenced record: position the referenced
      table at that record and indicate it; verify the landing ordinal is correct
- [x] 10.2 Treat a composite key as one key — act on any of its values, indicate all participating
      columns; verify with a two-column key
- [x] 10.3 Navigate backwards: position the referring table at the first referring record, allow
      movement between matches, and state how many there are; verify the count and that records
      between matches remain present
- [x] 10.4 Report a value that refers to nothing rather than presenting an empty result as a
      resolved reference; verify the reader shows it as the finding it is
- [x] 10.5 Refuse navigation into a table that could not be read, stating the reason; verify the
      affected cells are marked rather than offering a click that cannot complete
- [x] 10.6 Reuse one view per table, repositioning it when the table is reached again; verify ten
      successive navigations produce no duplicate views

## 11. Close out

- [x] 11.1 Verify the full suite passes with zero warnings under warnings-as-errors, and that the
      CLI still publishes AOT-clean
- [x] 11.2 Update the README with the content mode, its capacity limit, and what the reader is
      for; verify the usage block matches the actual help output
- [x] 11.3 Resolve the design's open questions that the implementation settled — cache location
      and cap, how much of a record a finding carries, dialect edge cases; verify `design.md`
      records the answers rather than leaving them open

## 12. Open an export from within the reader

- [x] 12.1 Offer opening an export from the application, choosing either a ZIP archive or a
      folder; verify both forms open and present the declared structure, since the standard
      permits both and no platform picker selects either with one dialog
- [x] 12.2 Leave what is open untouched when the choice is dismissed; verify by a test that the
      session, its store and the presented table are the same afterwards
- [x] 12.3 State why a chosen file or folder could not be opened, in the terms the validator
      uses; verify a folder with no `index.xml` says so rather than presenting an empty reader
- [x] 12.4 Close the previous export when a second is opened, releasing its store; verify the
      first session's store directory is gone and no second store is left behind, since a reader
      that accumulated sessions would accumulate stores with them
- [x] 12.5 Keep the command line working as the way the reader is scripted; verify opening by
      argument and opening by choice reach the same state

## 13. Ship the reader from the pipeline

- [x] 13.1 Publish `GoBd.Reader.Ui` self-contained per platform, on the matrix the CLI is built
      for; verify the build succeeds for each and that the published output is a directory that
      runs as it stands
- [x] 13.2 Check the published output carries what it needs before it is uploaded — the platform
      launcher and the native store — and fail the job when either is absent; verify by a check
      that fails on a deliberately incomplete publish, because a reader that cannot open an
      export is worse than no artifact
- [x] 13.3 Upload the reader as a downloadable CI artifact per platform, kept for 48 hours; verify
      the retention is stated in the workflow rather than left to the repository default, since a
      220 MB artifact per platform per commit is not something to keep by accident
- [x] 13.4 Attach a versioned reader build to each release beside the CLI, with a checksum, as
      the release workflow already does for the CLI; verify a dry run stages both
- [x] 13.5 Verify the CLI's artifacts are unchanged by all of this — same name, same single file,
      same smoke tests — because the pipeline's existing promise is what a pipeline depends on

## 14. A table with nothing in it, and failures that stay contained

Reported from a real export as a crash on selecting a table. The reader ended the session on the
first table that had no records.

- [x] 14.1 Tell the store to detect nothing when reading an extract, since the columns are given
      and the file is one this tool wrote; verify a table whose file holds no data records
      imports as a table of no records instead of failing, which is the reported defect
- [x] 14.2 Verify a table excluded down to nothing by its declared range behaves identically —
      the same emptiness reached a different way, and the store cannot tell them apart
- [x] 14.3 Present an empty table as an empty table under its declared columns; verify the
      reader shows its columns and no rows, and reports no finding against it
- [x] 14.4 Verify the two engines still agree on a table with no records, so the CLI reports
      nothing about it either
- [x] 14.5 Contain a failure to present a table to that table: verify that a table which cannot
      be presented shows why, that every other table stays openable, and that the session
      survives — including for a failure with no finding code, since those are the ones that
      reach a user
- [x] 14.6 Add the reported case as a regression fixture end to end, an export carrying one
      empty table beside a table with records; verify selecting either leaves the reader usable

## 15. Documentation

Last, so it describes what shipped. Short, concise, precise: rationale stays in `design.md`.

- [x] 15.1 Retitle and re-introduce the README for both tools — the CLI for producers gating a
      pipeline, the reader for reading a medium; verify each is named in the first paragraph
- [x] 15.2 Cut the README's reader section to usage: open by argument or picker (ZIP or folder),
      navigate, follow a key, what an inconsistent or empty table shows; verify it carries no
      rationale that `design.md` already records
- [x] 15.3 State how to get and run the reader — release download or 48-hour CI artifact, size,
      nothing to install — and add its publish command to Building; verify the command runs
- [x] 15.4 Condense Scope so nothing repeats the usage block, and replace the README examples with
      output the tool actually produces for the shipped fixtures; verify by running them
- [x] 15.5 Correct `docs/finding-codes.md`: the reader reports the same codes and always examines
      contents, and the range table is aligned; verify the reference tests still pass
- [x] 15.6 Delete `docs/initial-requirements.md`, which the proposal and specs supersede and which
      cites a PDF the repository deliberately does not ship; verify nothing links to it
