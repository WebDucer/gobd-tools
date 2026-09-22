## Context

See `proposal.md` for why. What exists today, and shapes the approach:

- **Opening is synchronous and lazy.** `ReaderSession.Open` parses `index.xml` and runs the
  structural checks and the header check. A table is imported the first time it is viewed,
  inside `ReaderSession.View`, on the UI thread, which is why a large table freezes the window.
  The reader never runs the key checks at all.
- **The store holds one `DuckDBConnection`.** Imports, key checks and the grid's paging all share
  it, which is safe today only because everything runs on one thread.
- **Extraction already reads record by record** through `RecordReader`. The entry's length is
  known up front, and for a ZIP member it is the uncompressed size, which is exactly the number
  of bytes the decompressed stream yields.
- **The window has one content area.** A cache of controls per table swaps in and out of it,
  one status line and one Previous/Next bar sit in the header, and `Reset` is the only thing that
  hides that bar. Nothing ever writes the navigator's selection.
- **`FolderExportSource` keeps a trailing separator in its root.** On Unix
  `Path.GetFullPath("export/")` keeps the slash, so the containment check compares against
  `export//` and rejects every entry, `index.xml` included.

## Goals / Non-Goals

**Goals:**

- Nothing that reads data runs on the UI thread.
- One pipeline serves both "open" and "is this table ready", so readiness can never disagree with
  what the summary says.
- The fixes to the walk bar and the selection come from the structure: state that belongs to a
  table lives in that table's tab.

**Non-Goals:**

- Parallel import of several tables. One table at a time keeps progress honest and peak disk
  predictable. The store parallelises within a table already.
- Resuming an interrupted open. A cancelled open is discarded and starts over.
- Any change to what the checks find. The same checks run earlier; the findings and codes are
  the same.

## Decisions

### D1 — Opening is a background pipeline with per-table readiness

```
  open --> parse + structural --> for each table:        --> key checks --> done
           (fast, as today)         extract (bytes read)      across all
                                    import                    tables
                                    record checks  ==> table READY
```

The pipeline runs on a worker and reports progress to the window through the dispatcher. It
reports each table's state (waiting, reading with a percentage, ready, defective, failed) and the
overall bytes read against the total the space check already computes.

A table is **ready** once its own import and record checks are done, which is D7's gate. It does
not wait for the key phase, because the gate does not depend on it. Key findings arrive at the
end, when every table is in the store, and are added to the summary. They never change whether a
table shows its data (D8).

_Why not keep laziness and only move it off the UI thread?_ That would fix the freeze but not the
request. The export's condition would still be unknown until every table had been opened, and
the key checks would still never run.

_Order:_ tables are read in document order. Referenced-first ordering would let key checks start
earlier, but they need every table either way, and document order matches the navigator, which
is what a person watching the progress reads down.

### D2 — Cancelling is a token, and a cancelled store is deleted

A `CancellationToken` passes through extraction, which checks it between records, and through
the pipeline between stages. Closing the window or opening another export cancels it, waits for
the worker to stop, and disposes the session, which deletes the store as it does today. A DuckDB
statement already running is interrupted through the connection rather than awaited.

_Confirmed._ `DuckDB.NET` exposes the interrupt: `DuckDBConnection.NativeConnection.Interrupt()`
maps to `duckdb_interrupt`, is callable from another thread, and raises
`OperationCanceledException` in the thread executing the statement. Measured against a statement
that would otherwise have run for hours: it stopped within 2 ms of the interrupt. The fallback of
waiting a statement out is not needed. `ExportStore.Interrupt()` is that call; the spike is kept
as `StoreConcurrencyTests.AStatementRunningInTheStoreIsStoppedFromAnotherThread`.

_One consequence._ An interrupt that lands while the connection is between statements is not
reported as a cancellation but as the store's own error on the next one. Which of the two arrives
depends on scheduling, so **the token is what says a read was cancelled, never the exception
type**: the pipeline asks `ThrowIfCancellationRequested` before it reports any table as failed, so
a cancelled read cannot leave a finding behind blaming whichever table it happened to be on.

### D3 — Two connections to one store, verified first

The worker imports on one connection while the grid pages through ready tables on another. DuckDB
supports several connections to one database within a process. A connection is not safe to share
across threads, so each thread gets its own and neither is shared.

This was the one assumption the design could not check by reading, so it was the first task.

_Confirmed._ While one connection imported a table of a million records, the other paged a
finished table 2,210 times without an error and without a short page; the slowest page cost
**6.0 ms**, against the 13.6 ms a scrollbar step cost in the finished reader and the 27.2 ms the
spike allowed itself. The fallback — paging from the extract files on disk — is not needed.

Two details the spike settled beyond the question asked:

- `DuckDBConnection.Duplicate()` is **not** the way to get the second connection: it refuses for
  anything but an in-memory database. A second connection opened from the same connection string
  reaches the same in-process database, which is what the store does.
- A table the importing connection creates becomes visible to the paging connection without
  either being reopened. That matters because the grid's connection is opened when the export is,
  long before the later tables exist.

The spike is kept as `StoreConcurrencyTests`.

### D4 — Tabs replace the single content area

A tab host sits where the content area is. Each table gets one tab, created the first time it is
chosen or reached and brought to the front when it is reached again. The existing per-table cache
becomes the tab collection. A tab owns everything that belongs to its table: the grid, its
position, its status line and its walk bar. Leaving a tab cannot leave a stale message behind,
because the message was never shared.

The **start page is a tab of its own**, holding the summary, shown first and not closable. The
navigator selection follows the active tab, set from the tab rather than from wherever the
navigation happened to start.

### D5 — The walk bar exists only during a walk

Previous and Next are created when a backwards walk starts ("records referring to this"), live in
the referring table's tab, and are removed when the walk is closed, the tab is closed, or a
different walk starts. A forward navigation reports its outcome in the tab's status line and shows
no stepping controls at all. That was the bug: one bar served both jobs, and forward navigation
showed it with its buttons disabled.

### D6 — Relationships are informational children in the navigator

Beneath each table the navigator shows two groups, _References_ and _Referenced by_, each entry
naming the table at the other end and the key columns (`-> Kunden (Kunde)`). The entries are leaf
nodes: they cannot expand and they do not navigate. Choosing one selects nothing else. They
replace the unclickable text lines in the header.

This does not reopen D1 of `add-export-reader`, which rejected the reference graph as the
navigator because it is not a tree. Leaf entries are one level deep by construction, so cycles and
self-references cannot recurse.

### D7 — Followable cells styled by column, dangling marks by page

Every column that takes part in a foreign key gets a column-level cell style: link colour, a hand
cursor, and a tooltip naming the table it leads to. A composite key styles all of its columns.
Styling by column adds nothing per cell beyond what realising the cell already costs.

A value that does **not** resolve is marked per page, not from the findings. Key findings stop at
the per-table bound, so marking from them would silently stop at the fiftieth dangling value.
Instead, when the paging list fetches a page it also asks the store which of that page's foreign
key tuples have no match: one anti-join restricted to the page's ordinal range. It has no cap,
and it costs only what is on screen.

### D8 — The gate stays on record conformance

This is recorded as an assumption, per `proposal.md`. Checking keys up front makes dangling
references known, and it would be easy to add them to the gate. It would also make the reader
refuse, over one broken reference, a table of a million records that someone needs to read. They
are findings and marks, not a reason to withhold data.

### D9 — A folder root is normalised where it is made

`FolderExportSource` trims a trailing separator from its full root before anything is compared
with it. The fix belongs there rather than in each caller: the validator, the CLI and the reader
all construct the source, and the reader's own normalisation in `ReaderWorkspace` becomes
redundant but harmless.

## Risks / Trade-offs

- **Opening a large export now takes minutes before the summary is complete.** → Progressive
  readiness: each table opens as soon as it is ready. Progress is measured in bytes, so the wait
  is predictable rather than a spinner.
- **Two connections may contend in DuckDB.** → Verified first (D3), with a fallback that pages from
  the extract files.
- **Cancelling mid-statement may not be immediate.** → The token stops extraction between records
  and a running DuckDB statement is interrupted (D2, confirmed). The interrupt is issued
  repeatedly until the worker actually stops, because one that arrives before the store has begun
  a statement is simply lost and the worker's next stage may be a statement that runs for minutes.
- **Progress updates can flood the dispatcher.** → Reported at most every 100 ms and on each state
  change, never per record.
- **The window's code grows.** → Tab contents, the start page and the navigator move into their
  own controls and view models. The rule stays that data is fetched by view models and never by
  the window.

## What the implementation settled

Beyond the spike (D2, D3), where the code had to choose and the design had not:

### The header check runs per table, inside the pipeline

It was in `ReaderSession.Open`, where it opened every declared data file on the UI thread before
the window appeared. It reads one record, so it is cheap, but it reads — and nothing that reads
data runs on the UI thread. It now runs as the first stage of each table's turn, so its finding
arrives with the table it concerns and no file is opened before its turn. `HeaderRowCheck.ForTable`
became public for it; `CheckEngine` still runs the same check over the whole export for the CLI.

Opening therefore reads no data at all, and the report `ReaderSession.Structure` carries is marked
as not having examined contents. The summary's report is the one that has.

### `IsImported` means "in SQL", not "we tried"

The store recorded a table's layout before extracting it, so a table whose extraction threw looked
imported. The key checks believed it and joined against a table that does not exist, which ended
the read with a catalog error rather than a finding. The layout and the extent are now published
together, at the end of a successful import. This was latent before: the reader never ran the key
checks, so nothing asked.

### The summary's findings are the validator's, as a set rather than a sequence

`ExportSummaryTests` holds the two to the same codes, the same messages and the same verdict on
the same export. The order differs — the reader produces a table's findings as it reads that
table, the CLI runs each check across every table — and the summary groups by table anyway, so
order was not worth constraining. The two engines are still held to the same sequence where that
is the claim, in `EngineComparison`.

### A page is not cached until every mark on it could be asked for

Tables are read in document order, so a table opened early can be paged before the table it
references is in the store. Nothing was compared, so nothing may be marked — and a page cached in
that state would stay unmarked for the rest of the session. Such a page is served and discarded;
once every table is in the store, pages cache as usual.

Marking is also restricted to keys that cover the referenced primary key whole. A key of the wrong
arity is already reported as a defect, and joining on part of one would mark values the summary
says nothing about.

### A tab is identified by the session's own table node

Relationships are resolved by reference within one parse of `index.xml`, which is what makes two
tables sharing a name distinguishable. A caller holding a node from another parse would otherwise
give one table two tabs — precisely what one tab per table exists to prevent — so `ReaderTabs`
maps every node through the session before it looks a tab up.

### The summary is a table, and its sections are headed

The start page was a column of sentences: one line per table, one line per finding, all at body
weight. What a person does here is compare tables — which are done, which is big, which carries
findings — and a column of numbers can be compared at a glance where a column of prose has to be
read line by line. So the tables are a grid (table, state, records, findings) under a ruled header
row, the findings are grouped by table with the code in its own column so the codes read down, and
"Tables" and "Findings" are headings rather than another line of body text. The verdict is the
largest thing on the page and is coloured by what it says, because it is the one fact someone
handed a medium needs first.

Two things that followed from looking at it on a real export:

- **A defective table reports no record count.** The requirement asks for a record count _or_ a
  number of findings, and reading a defective table stops at its bound — so the records it got
  through is not the count of anything a person would want. Its findings are what it has to say.
- **A size is reported in the unit that says something about it.** Rounded to whole megabytes, a
  31 KB export read "0 MB", which looks like nothing was read at all. Measuring progress rather
  than guessing it is pointless if the measurement cannot express what it measured.

### The tab strip is sized for a table per tab

The Fluent theme sizes a tab header at 24 point, for a window with a handful of top-level
sections. A tab here names a table and there is one per table a person has opened, so the strip
is sized like a document's tabs: 16 point, an explicit height rather than one left to the padding,
and room above it so it is not flush with the title bar.

### Measured

- **Marking costs what the page costs.** On three million records with a hundred thousand
  referenced records and a third of the references dangling, the slowest scrollbar step was
  **10.5 ms** — under the 13.6 ms measured before any of this existed, and well under the 27.2 ms
  allowed. The anti-join per page is affordable, and no index was needed.
- **Progress does not flood the dispatcher.** Reading a table of a million records reports at most
  once per 100 ms plus once per state change.

The tests that assert wall-clock budgets run in a collection of their own with parallelisation
disabled. A page budget of tens of milliseconds says something about the store only when the store
is what the machine is busy with; beside two dozen other test classes importing exports on every
core, the same assertion measures the runner.
