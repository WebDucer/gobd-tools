## Context

See `proposal.md` — Why. This records how the reader is built and why each choice was made,
including two that were reversed during exploration.

The constraints that shape everything below:

- **v1 never opens a data file**, and its CLI publishes as one self-contained native executable
  with no native dependency. That property is the reason a pipeline can adopt it, and this
  change must not cost it.
- **The declaration is the only structure.** A GoBD file frequently has no header row, so column
  names, order, types, delimiters, encapsulator, codepage, symbols, spans, `Range`,
  `SkipNumBytes`, `Epoch` and `Map` all come from `index.xml`.
- **The model and the join logic already exist.** `ForeignKeyResolver.Map` resolves foreign key
  columns onto the referenced table's primary key columns, aliases and positional fallback
  included. Nothing here re-derives that.

## Goals / Non-Goals

**Goals:**

- One definition of every content defect, checked identically by two engines.
- Keep `GoBd.Validation` pure managed and keep the CLI a single file.
- Make a table of several million records readable at constant memory.

**Non-Goals:**

- No shared cache, service, or anything reachable over a network. One machine, one process.
- Not a general table viewer: everything is driven by a declaration, and nothing works without
  one.
- No incremental or resumable import in this change. An interrupted import starts over.

## Decisions

### D1 — The navigator is the document tree; the FK graph is drawn on it

Foreign keys do not form a tree and cannot be presented as one:

```
     [Kunden]                 [Artikel] -----> [Warengruppen]
         ^                        ^
         |                        |
   [Bestellungen]                 |
         ^                        |
         |                        |
   [Bestellpositionen] -----------+          child --> parent
```

`Bestellpositionen` has two parents, so top-down it appears twice or arbitrarily once. No root
reaches everything — rooted at `Kunden` you never see `Artikel`. Self-reference is legal
(`Mitarbeiter.Vorgesetzter -> Mitarbeiter`), so is a cycle. And because `References` resolves
across the whole `DataSet`, edges cross media boundaries, which a `Media`-shaped tree cannot
express.

What _is_ a tree is `DataSet -> Media -> Table`, because that is literally the XML. So the
navigator presents that, columns excluded, and the graph appears in two places where it is
always well defined: a per-table _references_ / _referenced by_ pair, and the reader's own path
through the data.

_Alternative considered:_ a forced tree with duplicated nodes, or one rooted per connected
component. Both make the reader guess at an ownership the format does not declare. A graph
diagram is the honest global view and is a separable later feature.

### D2 — Always import; there is no browse mode

Two candidate strategies existed, and the specs killed one of them:

|                        | Cost                           | Why it lost |
| ---------------------- | ------------------------------ | ----------- |
| Query the CSV in place | No ingestion wait              | See below   |
| Import into the store  | Full pass before the first row | Chosen      |

In-place querying works fine on a headerless file — the reader supplies columns and types
explicitly and nothing is sniffed. Its only advantage was painting the grid without waiting.

But `reader-ui` requires that a table's data is shown **only when the table is consistent**.
Deciding that requires reading the whole table first, so the wait is already paid before
anything can be displayed. In-place querying keeps the wait and adds a rescan to every later
lookup. The advantage does not exist, so the mode does not exist either — one code path, and no
option for the user to get wrong.

### D3 — What the import materialises

```
  columns   declared names and declared types      (never inferred)
  header    off, always                            (a header row is DATA to be checked, not skipped by the reader)
  ordinal   file-order record number, materialised (not derived at query time)
```

The ordinal is not decoration. `reader-ui` navigation positions the reader **at a record**, and
positioning a virtualised list requires that record's index, not its key. Deriving it per query
would mean an ordered scan on every foreign key click. It is materialised once, at import.

Header handling deserves the emphasis: the reader never lets the store skip a header. Whether a
first record is a header is a question `content-validation` answers — including the reordered
and undeclared cases — and it can only answer it by seeing that record.

### D3a — The record number is written into the extract, not derived on import

_Settled by implementation._ The store's CSV reader drops a record it cannot parse. A row number
computed during import therefore counts surviving records rather than records, so one dropped
record near the top of a file makes every number after it wrong — and a finding that cites the
wrong record sends its reader to the wrong line.

So every table is extracted before it is imported: decoded once through its declared codepage,
written out as UTF-8, and **numbered as it is read**, with the record number as the first column.
Extraction was already unavoidable — a ZIP member has no path to hand to the store, three of the
six declared codepages are not encodings its reader accepts, a fixed-length table has no
delimiters at all, and a record delimiter that is not a line ending, which the standard permits,
is not something that reader can be told to use.

The consequence, stated rather than glossed: for the store, this tool's reader decides where one
record ends and the next begins. D4's rule still holds — the store's parser remains the
definition of the readings the declaration does not fix — but it is pinned by holding this reader
to it on the original files, not by the import path.

Everything is stored as text. A typed column would reject exactly the values a content check
exists to report, and the person looking at a defective record needs to see what is actually
there. The declaration drives the checks instead, expressed as predicates over the text.

_And the store is told to detect nothing._ Its CSV reader runs a dialect sniffer even when it has
been given the columns explicitly, and the sniffer votes on what it thinks the file is. On a file
this tool wrote, to a shape it already knows, there is nothing to infer and everything to get
wrong — which is the same principle the reader applies to the export itself, applied one layer
down. Detection is switched off.

It was a table with **no data records** that exposed this: the sniffer, given nothing to sniff,
reports that the file has one column where twenty were declared and refuses the file. A period
with no transactions in it is an ordinary thing for an export to describe, and the reader has to
present it as an empty table rather than fail on it.

### D4 — Two engines, one contract

|          | CLI                     | Reader                             |
| -------- | ----------------------- | ---------------------------------- |
| Engine   | Streaming, pure managed | Embedded analytical store (DuckDB) |
| Ships as | One native executable   | Desktop app with a native store    |
| Answers  | The same findings       | The same findings                  |

The store is DuckDB via `DuckDB.NET` — MIT, as is DuckDB itself, so nothing here constrains the
repository's licence. The `.Full` package variants bundle the native library per platform; the
plain ones expect it to be supplied.

**The CLI's exclusion is about distribution, not licence and not AOT.** The reason a pipeline
adopts this tool is that it is one file with nothing to install, and a bundled native store is a
second file whether or not it publishes cleanly. So the checks are implemented twice. That is a
real cost, accepted deliberately — and it is why `content-validation` is written as observable
defects rather than as an algorithm, and why the bound is defined as _the first N by record
order_ rather than _the first N found_.

That last point is not pedantry. The store reads CSV in parallel, so "the first 50 errors
encountered" varies between runs of the same file. Ordering by record number makes the two
engines agree and makes a report reproducible. The streaming engine can stop at the fiftieth
because it reads in order; the store scans and takes the first fifty by line. Same answer.

_Where the declaration is silent, the store's reading is the definition._ `index.xml` fixes the
dialect completely — column delimiter, record delimiter, text encapsulator, codepage, and for
fixed length the column spans — so nothing about it is guessed. It says nothing about how an
encapsulator is escaped inside an encapsulated value, what one means mid-field in an
unencapsulated value, or whether a trailing record delimiter yields a final empty record. Rather
than invent an answer, the reading DuckDB's CSV parser gives is taken as correct and the
streaming reader is held to it — configuring a mature parser to match a new one is the wrong
direction of travel.

Note what this couples and what it does not: the _definition_ is anchored to DuckDB and pinned
by tests, but the CLI acquires no runtime dependency on it. If the store is ever replaced, those
tests are the record of what the reading was.

_Consequence for testing:_ the engines are held to each other. The same fixtures run through
both, and the findings must match, edge cases included.

_What that comparison is worth, precisely._ The cell checks — types, formats, accuracy, maximum
lengths — and the key checks are written twice, once in C# and once as predicates the store
evaluates. Those are the real comparison, and it has already earned its keep: it caught the two
engines disagreeing about what counts as padding on a numeric value, because .NET trims every
Unicode whitespace character and the store's `trim` strips spaces alone. The set is now named in
one place and applied by both. The structural defects and the header check reach both sides
through the same reader, since the store is fed an extract that reader produced (D3a); for those,
the comparison asserts that a fixture exists and that nothing is lost passing through the store,
not that two implementations agree.

_Two readings where this reader is deliberately stricter than the store's parser:_ leading
whitespace before an encapsulator, which the store drops and this reader keeps, and text after a
closing encapsulator, which the store refuses the whole file for and this reader keeps. Neither
forces a choice in the product, because the store reads an extract this reader produced — but
both are pinned by tests so that a change to either is a decision rather than an accident.

### D5 — Key checks fit in memory, and one array serves many

Both key checks reduce to the same primitive — a sorted array of 64-bit key hashes, eight bytes
per record — rather than a hash set, whose per-entry overhead is two to three times that:

```
  for each table T:
      build sorted hash array of T's declared primary keys
      scan adjacent pairs                        -> duplicate primary keys
      for every foreign key anywhere referencing T:
          stream the child, binary-search each mapped key tuple
      free the array

  peak memory = the LARGEST table, not the sum of them
```

The array a table builds for its own uniqueness check is exactly the probe structure every
inbound foreign key needs, so it is built once and reused. Ordering the work by referenced table
keeps only one array alive.

Hash collisions are resolved by verification rather than tolerated: a candidate duplicate is
confirmed against the actual key values, so there are no false positives.

This sets the CLI's capacity honestly: at eight bytes per record, a 512 MB budget is about 64
million records in the largest single table. Beyond that the CLI reports that the check was not
performed — `validator-cli` requires exactly that, because reporting an unchecked table as clean
is worse than admitting the limit. The store has no such ceiling; it spills to disk.

_Note on sizing:_ foreign key checking is bounded by the **referenced** table, which is often
but not always smaller. A sales order header to line ratio of 1:3 makes the parent a third of
the child, not a rounding error.

### D6 — `TableView`, and the paging list is ours

Avalonia's `DataGrid` is **deprecated**; its documentation directs read-only tabular data to
`TableView` and editing to `TreeDataGrid`. `TableView` (Avalonia 12.1, core `Avalonia.Controls`,
MIT) is the right target: read-only by design, which is precisely this reader's scope, and it
virtualises and recycles rows and cells.

`TreeDataGrid` is excluded on licence, not on merit: it moved to Avalonia Accelerate under
AGPL-3 or a paid licence, and AGPL is copyleft — linking it would drag this MIT repository to
AGPL. A proprietary grid (Syncfusion's, free under its community terms) would leave the licence
intact but require every builder of this repository to hold an entitlement. `TableView` costs
neither.

**Row virtualisation is not data virtualisation.** The control realises only visible rows, but
it still asks the bound collection for `Count` and indexes into it — back it with four million
materialised objects and the recycling saves nothing. So the reader supplies an `IList` that
serves a window of records from the store and materialises nothing outside it. `TableView`
derives from `ListBox`, so selection and scroll-to-index come from the existing items
infrastructure rather than being reimplemented.

_What changed here:_ an earlier draft targeted `DataGrid` with a custom `IDataGridCollectionView`
to work around its walking the whole source. Deprecation removed both the control and the
workaround; the paging `IList` is the simpler shape that replaces it.

_Spike outcome — confirmed._ Avalonia 12.1.2, release build, 5,000,000 records, one window,
scripted scrolling: 400 jumps spread across the whole extent, then 600 wheel-sized steps, then a
jump to record 3,847,221.

| per scenario                  | 8 columns   | 50 columns  | 300 columns |
| ----------------------------- | ----------- | ----------- | ----------- |
| wheel step, p50 / max          | 0.30 / 0.9 ms | 1.7 / 100 ms | 10.3 / 229 ms |
| scrollbar jump, p50 / max      | 6.0 / 84 ms | 34.5 / 343 ms | 351 / 3105 ms |
| heap to show one screen        | 12 MB       | 56 MB       | 224 MB      |
| jump to record 3,847,221       | 15 ms       | 35 ms       | 332 ms      |

What the spike establishes:

- **The source is never walked.** Covering the whole file materialised 4.3% of it; peak residency
  equalled the page cache exactly (4,096 records), and 19 row containers were realised at every
  position, top or bottom. The list's enumerator — deliberately instrumented to record being
  called — was never entered.
- **Positioning works, and it is cheap.** `SelectedIndex` with `ScrollIntoView` lands on record
  3,847,221 itself, with its container realised, in 15 ms. D8's navigation rests on this.
- **What is retained does not grow with how far one has scrolled.** Live heap was 11.8 MB at the
  top of the file and 14.0 MB after traversing all of it. The _resident_ set is not flat — it
  expands with allocation churn to 589 MB during the sweep and is handed back afterwards (335 MB).
  Flat retention is the property that matters; a flat working set was never on offer from a
  collector that returns memory lazily.
- **Serving the pages is not the cost.** Page building averaged 0.4 ms per step at 8 columns and
  10 ms at 300, against step times of 6 ms and 351 ms. The time is the control's.

_Confirmed again in the finished reader, on 3,000,000 records of a 164 MB export:_ import and
first paint 7.7 s; 4,096 records materialised at peak, from 312 pages, out of three million;
managed heap 15 MB before scrolling and 54 MB after covering the whole table; wheel scrolling
0.26 ms a step and dragging the scrollbar 13.6 ms. That last figure was 130 ms until the
measurement found why: pages were addressed with `OFFSET`, which makes the store count past every
row it skips, so a page near the end of a large table cost a scan of the table. Record numbers
are materialised at import (D3a), so a page is addressed by the numbers it wants instead.

_The absence of column virtualisation, measured:_ about 34 microseconds per realised cell, linear
in column count. Fifty columns is comfortable. Three hundred is at the edge — a wheel step at
10 ms still fits inside a frame, but dragging the scrollbar runs at roughly three frames a second
and a single screenful costs 224 MB. A fixed-length table declaring several hundred columns is
legal, so if one turns up in the field the answer is to window the columns the way we window the
records: rebuild `Columns` from the horizontal viewport rather than declaring all of them. That is
a contained change in our layer, and it is deliberately not made now — nothing is gained by
building it before an export needs it.

### D7 — Gate per table, and say when the list is truncated

Dataset-level gating fails badly: three bad dates in one table would withhold five clean ones.
Row-level gating would show a grid and its defects together, which is attractive but means
rendering a table the reader has declared inconsistent. The table is the unit that matches both
the import (a table loads or it does not) and the view (a tab shows a grid or a report).

The bound on findings is a **stop**, not a display cap: analysis of a defective table halts, so
a broken multi-gigabyte file is rejected in seconds rather than read to its end. The cost is
that no total is known, which is why the report must say analysis stopped rather than implying
the list is complete.

The bound is **configurable, defaulting to 50 per table**. A default is required rather than
merely convenient: the whole point is that a wholly broken file costs seconds, and that only
holds if the stop applies without anyone having asked for it. Raising it trades time for
completeness on a file already known to be defective; there is no value in lowering it below
what makes a pattern visible, which is roughly where 50 sits.

Configurability does not weaken reproducibility: for a given bound the findings are still the
first N by record order, so two runs with the same setting agree, and a run with a larger bound
is a superset of a smaller one.

_The bound belongs to the table, not to each pass over it._ Settled during implementation, because
the first version got it wrong: record conformance and key integrity are separate passes, and each
keeping its own count let one table report twice what was asked for. They share one budget per
run, and the notice that a table's list was cut short is claimed from that budget rather than
emitted by whichever pass happened to fill it — otherwise a report would depend on the order the
checks ran in.

### D8 — Navigation positions; it never hides

```
  downstream   a value in a foreign key  -->  referenced table, positioned at that record
               many --> one, lands on one record

  upstream     "what refers to this?"    -->  referring table, positioned at the first match,
               one --> many                   with movement between matches and a count
```

Neither direction filters. Records between matches stay present, because record order is what
makes a record citable in an audit — the same reason the grid offers no sorting. Upstream
navigation is therefore match traversal, not a query.

The unit of navigation is the **foreign key**, not the cell: a composite key is followed as a
tuple and its columns are indicated together. One view per table, reused and repositioned when
reached again.

### D9 — Module boundary

```
  GoBd.Validation        pure managed, AOT, no native dependency
                         structural checks + streaming content checks
  GoBd.Validation.Cli    single-file native publish; never references the store
  GoBd.Reader.Data       DuckDB via DuckDB.NET (MIT): import, key checks, paged reads
  GoBd.Reader.Ui         Avalonia: navigator, TableView, navigation
```

The arrow that must never be drawn is `GoBd.Validation -> GoBd.Reader.Data`. It is worth a build
guard rather than a convention.

The store is written to the **platform temporary location**, keyed by a content hash, never
beside the export: the export is a data carrier and read-only, and an auditor's medium must come
back unchanged.

Its lifecycle is deliberately short:

```
  on start   remove stores left by runs that are no longer alive
  on open    check free space BEFORE importing; refuse rather than fail part-way
  on close   remove this session's store
```

*Deleted on close, so nothing accumulates* — at the cost that reopening the same export imports
it again. That is the right trade for a tool an auditor opens on a medium once, and it removes
the size cap and eviction policy an earlier draft carried: there is nothing to evict.

*A dangling store cannot be identified by age alone.* A second instance of the reader may be
using it right now, and deleting it would pull the ground from under a live session. Each store
therefore carries a lock held open for the life of its process; startup cleanup removes only
stores whose lock no longer belongs to a living process.

*The space check happens before the first byte is written,* because discovering the shortfall
half way through a multi-gigabyte import wastes the time it was meant to save. The estimate is
derived from the declared files' total size; columnar storage is usually smaller, so this errs
toward refusing an import that would in fact have fit — the safe direction.

### D10 — Finding codes extend the existing space

`1xxx` source, `2xxx` DTD and grammar, `3xxx` structure, `9xxx` tool failure are taken. Content
work claims `4xxx` for record conformance — types, formats, lengths, column counts, encoding,
encapsulation, header rows — and `5xxx` for key and referential integrity.

New codes inherit the existing discipline automatically: an entry in `docs/finding-codes.md`,
both translations, and the two-way coverage test that fails if a code exists without
documentation or documentation without a code.

Reader-only codes are deliberately not minted. "The referenced table could not be read" is a
consequence of a `4xxx` finding, not a new defect.

### D11 — A finding carries the offending value, never the record around it

A content report travels: it is attached to a CI job, pasted into a ticket, sent to whoever
produced the export. A record of a GoBD export is customer data, so a finding quoting the whole
line would spread far more of it than the defect requires.

```
  reported       table, record number, column, and the single offending value
  not reported   the rest of the record
```

_Structure is not data._ Column names, counts and positions come from the declaration, so a
finding is free to name them — the header-row findings compare declared names against found
names and quote both, which is correct: those names describe the file's shape, not anyone's
business.

_Some defects have no single cell,_ and they report what they legitimately have: a column-count
mismatch names both counts; an unterminated encapsulator or an over-length fixed record names
the record. Neither needs the record's content to be actionable.

The reader is not bound by this. It shows the table, so a person looking at a defective record
sees it in context. What is constrained is the _report_, because that is what leaves the machine.

### D12 — A report says whether it read the data

The likeliest way a report misleads is a clean result being read as "the export is fine" when no
data file was opened. The findings cannot tell the two apart: both produce none. So the fact is
published rather than inferred — a `contentsExamined` field in the JSON report, and a scope
sentence in the text report that states which of the two runs produced it.

_This moves the JSON schema version to 2._ Every field version 1 promised is still present and
still means the same thing, so a consumer written against 1 keeps working. But a consumer that
decides what a clean report means now has a field it must read, and the version is how it learns
the field is there to read.

### D13 — What the reader gates on, and what it does not

The gate is **record conformance**: whether each of a table's records can be read as its own
declaration describes. Referential integrity is not part of it, and that is a decision rather than
an omission. Checking whether a table's foreign keys resolve requires importing every table it
references, so gating on it would turn opening one table into importing the export — which is the
cost the reader avoids by importing lazily, one table at a time.

Dangling references are therefore reported by the validator, which reads the whole export anyway,
and by the reader at the moment someone follows one: a value that matches no record is shown as
the defect it is rather than as an empty result. What the reader does not do is refuse to show a
table because a reference elsewhere in it does not resolve.

_If that turns out to be the wrong trade,_ the change is contained: the store's key checks already
exist and produce the same findings as the library's, so gating on them is a matter of running
them when a table is opened and accepting the imports that follow.

### D14 — The reader ships as a directory; the CLI ships as a file

The two are distributed differently because they are adopted differently.

```
  gobd-validate   one native file, ~10 MB, nothing to install
                  run on every export, by a pipeline, unattended
  gobd-reader     a self-contained directory, ~220 MB, nothing to install
                  installed once, by a person, to read a medium in front of them
```

The reader is not AOT and will not be for the foreseeable future: it carries a native store and a
UI framework, and neither is a candidate for it. Self-contained is still the right shape — an
auditor's machine has no .NET runtime, and telling them to install one first is how a tool goes
unused. So the artifact is a directory that runs as it stands, per platform, on the same matrix
the CLI is built for.

_The size is DuckDB's, not ours._ Of ~220 MB, `libduckdb` alone is 117 MB; the rest is the .NET
runtime and Skia. Nothing here is trimmable without giving up the store, which is the reason the
reader can hold an export the CLI cannot. It is also precisely why the CLI does not link it (D9):
a pipeline that ran a 220 MB download on every export would stop running it.

_Two kinds of artifact, two lifetimes._ A CI build is evidence about a commit and is worth keeping
only as long as anyone is looking at that commit, so it expires after 48 hours. A release build is
what someone downloads months later, so it is attached to the tag with a checksum and kept as long
as the release is.

_The reader cannot be smoke-tested the way the CLI is,_ because it opens a window rather than
printing a verdict. What the pipeline can check without a display is that the published output
carries the launcher and the native store for its platform; what already covers the rest is the
test suite, which exercises the store and the view models on the same runners.

### D15 — Opening an export is two dialogs, because an export is two things

The standard permits a data carrier as a ZIP archive or as an unpacked folder, and every platform
picker chooses one or the other — a dialog that selects "a file or a folder" is not something the
platforms offer. So the reader asks which, rather than guessing from a single dialog that would
be wrong half the time.

Command-line invocation stays: it is how the reader is scripted, and it is how the pipeline and
the measurements drive it. The picker is the way in for the person who was handed a medium and
has no reason to know what an argument is.

Opening a second export closes the first. The store is deleted when its session closes (D9), and
a reader that accumulated sessions would accumulate stores with them.

## Risks / Trade-offs

**`TableView` is new (12.1) and unproven at this scale for us** → Retired by the spike; see D6
for the measurements. It holds 5,000,000 records at flat retention and positions on a record deep
in the file in 15 ms. The fallback that was held in reserve — a custom-drawn virtualised table,
as the community `Sheet.Avalonia` demonstrates — is not needed.

**`TableView` does not virtualise columns** → Measured, and it is the one place the control
disappoints: about 34 microseconds per realised cell, so 300 columns drag the scrollbar down to
about three frames a second and cost 224 MB for one screenful. Fifty columns is comfortable. The
mitigation, if a wide export turns up, is to window the columns as we window the records — see
D6.

**The checks are implemented twice and could drift** → The specs are written as observable
defects, and the same fixtures are run through both engines with the findings compared. Drift
becomes a test failure rather than a support call.

**Import fails when the file does not match its declaration** → This is the expected path, not
an error path: the mismatch _is_ the finding, and the bound keeps a wholly broken file cheap.

_Settled, having been got wrong once:_ the reader contains a failure to present a table to that
table. An exception from the store used to travel up through the view to the dispatcher and end
the session — one unreadable table taking the whole export with it, which is precisely the
outcome the per-table gate exists to prevent. A table that cannot be presented shows why it
cannot, and every other table stays openable. That has to hold for failures nobody anticipated,
not only for the ones with a finding code, because the ones nobody anticipated are the ones that
reach a user.

**The CLI's key-check ceiling** → Stated, discoverable from help, and never silently exceeded.
An export past it gets a report saying the check did not run, not a clean verdict.

**Import time on a multi-gigabyte export, paid on every open** → Unavoidable given D2, and no
longer amortised now that the store is deleted on close (D9). Accepted: an auditor opens a
medium, reads it, and closes it. If reopening the same export turns out to be a common workflow,
retaining the store and reinstating an eviction policy is a contained change.

**The platform temp location may not be disk** → On many Linux distributions `/tmp` is a tmpfs
and therefore RAM-backed, so importing a multi-gigabyte export there would consume memory rather
than disk and could exhaust the machine. The space check must measure the actual backing store,
and the location must be overridable so a user can point it at real disk.

**Startup cleanup could delete a live store** → Mitigated by the process lock in D9; the failure
mode if it is wrong is severe, so cleanup must prove a store is unowned rather than assume it
from age.
