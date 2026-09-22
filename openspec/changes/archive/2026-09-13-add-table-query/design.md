## Context

See proposal.md for why. The state this design builds on:

- **The store holds text.** Every column is imported as `VARCHAR` (`ExportStore.Create`), for
  reasons that still hold: a value that fails its declaration must still be quoted by the defect
  check, which is the store's half of the both-engine agreement, and the grid shows values as the
  file stores them (`TablePageList.Present`).
- **Only conforming tables are shown.** A table reaches the grid only if every record matches its
  declaration (the reader's D8). So every non-empty `Numeric` or `Date` value in a shown table
  already matches its type, which makes typed reading of it safe.
- **Paging is addressed by record number.** `ExportStore.Page` selects `_ordinal` in a contiguous
  range, so a page anywhere in the table costs the same. `_ordinal` is written at extraction, so
  both engines agree on record numbers (`TableExtraction`, D3 of the first reader change).
- **Two connections.** The import writes on one, and the grid pages on the other from the UI
  thread. A connection is never shared across threads.
- **Marks are asked for per page.** `UnresolvedBetween` joins against the referenced table for the
  record numbers `first..last` of the page on screen.
- **A tab owns its table's state.** `TableTab` holds the position, the status line and the walk.
- **No memory settings.** The store sets no memory limit, thread count or temporary directory,
  which leaves DuckDB its defaults.
- **Map is for `AlphaNumeric` columns only**, per the standard. A Map whose From and To are the
  same time mask declares a Time column, and `Masks.IsTimeMask` already recognises one.

## Goals / Non-Goals

**Goals:**
- Typed filtering, sorting and figures without storing a second copy of the data and without
  touching the import's checks.
- Scrolling a view costs what scrolling the table costs today.
- The reader's own memory does not grow with the table. The engine spills to disk instead.
- One place where each typed reading is defined, so moving it from computed to stored later
  changes nothing that uses it.

**Non-Goals:**
- Materialised typed columns, unless the spike (task 1.1) shows computing them on the fly is too
  slow.
- Comparing keys by numeric value. Key checks keep comparing text, so `5-` and `-5` are different
  keys but equal numbers in a filter. That is deliberate, and the `table-query` spec does not
  claim otherwise.

## Decisions

### D1 — Typed values come from a view per table, never from storage

At the end of a conforming table's import the store creates `t_n_q`, a SQL view over `t_n`. It
carries `_ordinal`, every text column `c0..cn` unchanged, and one typed column per `Numeric`,
`Date` or Time column (`n3`, `d1`, `h4`). Filters, sorts and figures read `t_n_q`. The grid never
does: it pages `t_n` and shows text.

- **Why a view:** no import cost, no extra disk, no change to `CellDefects` or the agreement
  tests. The name is the seam. If on-the-fly typing ever proves too slow,
  `CREATE TABLE t_n_q AS …` with the same columns replaces the view and no caller changes.
- **Measured, and settled as a view.** Typing on the fly costs about 1.3 s for the figures over 4
  million records and 0.5–1.7 s to build a view, against 0.07–0.6 s from a stored typed table that
  would add 2.7 s and its own disk to every import, including exports nobody ever filters. A
  filtered or sorted view carries its typed values with it (D4), so that cost is paid once per
  view rather than per figure; only figures over a whole unfiltered table pay it each time.
- **Alternative rejected: typed `read_csv`.** It applies its own number and date rules, not the
  declared ones, and it rejects or fails rows before the defect check can quote them. That is
  the lesson of the crash reported from a real export, on a table with no records.
- **Alternative rejected: typed values in place of text.** Rendering a `DECIMAL` back would show
  `12,00` where the file holds `   +12,00`, which alters the evidence.

### D2 — Typed readings follow the declaration, and are guarded

Each typed column is `CASE WHEN c <> '' THEN <reading> END`. Empty stays absent, and a conforming
table has no other value a reading can fail on.

- **`Numeric`:**
  1. Trim the padding the grammar already defines.
  2. Take one sign from either end, per `accept-trailing-signs`.
  3. Remove grouping symbols and turn the decimal symbol into `.`.
  4. Cast to `DECIMAL(38, s)`.
  - For `ImpliedAccuracy` n, the point is inserted n digits from the right *before* the cast, so
    nothing is divided and nothing rounds.
  - The scale `s` is the declared `Accuracy`, else `ImpliedAccuracy`, else the largest number of
    decimals measured at import (D3). It is never the DTD's default of 0, because the validator
    accepts decimals in a column without `Accuracy`, and casting `12,50` at scale 0 would alter
    it.
- **`Date`:** `make_date` from the mask's fixed positions. A two-digit year takes the century from
  the table's `Epoch`, as `Masks.TryDate` does, never from DuckDB's `%y` pivot.
- **Time:** from the mask's `HH`, `MM`, optional `SS` and optional `TT` positions, with `TT`
  turning 12-hour times into 24-hour ones. A Time column is `AlphaNumeric`, so the gate
  guarantees nothing about it. The reading uses `try` semantics, and a value that is not a time
  becomes NULL, which D3 counts.
- **`AlphaNumeric`:** the value as the grid shows it. For a column declaring maps that is
  `CASE c WHEN 'From' THEN 'To' … ELSE c END`, with declared values quoted by
  `DeclaredSql.Literal`, never passed in from data.

Every reading is built in `DeclaredSql`, next to the conformance predicates it relies on.

### D3 — The import measures once, and decides what each column can do

One aggregate over the text per conforming table records:
- for each `Numeric` column, its largest count of integer digits and of decimals;
- for each Time column, how many non-empty values are not times.

From that, each column gets its capabilities:

| Column | Outcome |
| --- | --- |
| `Numeric`, integer digits + scale ≤ 38 | typed; filters, sort and figures offered |
| `Numeric`, more than 38 | no typed column; values shown; a hint on the column and a reader notice |
| Time with values that are not times | typed; those values count as absent; the count is stated |

- **Why the whole column is turned off:** dropping only the value that does not fit would make a
  sum silently short by one value.
- **Why a notice rather than a finding:** the export is not defective, and the summary must carry
  the validator's codes and no others (reader-ui). Notices go in a list of their own on
  `ExportReading`, shown as a separate section of the summary.

### D4 — A view is a table of the records it selects, carrying their values

A view with a filter or sort is built as:

```
CREATE TABLE v_k AS
  SELECT row_number() OVER (ORDER BY <typed key> <dir> NULLS LAST, _ordinal) - 1 AS pos,
         _ordinal, c0..cn, <the typed columns of t_n_q>
  FROM t_n_q
  WHERE <predicates>
  ORDER BY pos
```

- **Paging** is `SELECT … FROM v_k WHERE pos >= ? AND pos < ? ORDER BY pos`. Nothing is joined and
  nothing outside the page is touched, so a page of a view costs what a page of the table costs
  today.
- **Figures** read `v_k`, whose typed columns are already computed (D8).
- **Finding a record** (`PositionOfOrdinal`) is `SELECT pos FROM v_k WHERE _ordinal = ?`. It
  answers -1 when the view hides the record, which is what D9 acts on.
- **File order with no filter keeps today's contiguous paging.** No `v_k` is built for it, so an
  export nobody filters costs nothing.
- **Lifetime:** a `v_k` belongs to one tab. It is dropped when that tab's view is superseded or the
  tab closes, and everything goes with the store when it is closed.
- **Why in the store file, not `TEMP`:** tables in the file go through DuckDB's buffer manager and
  can be evicted, so the reader's memory stays flat whatever the view selects.

**Why the view carries the records rather than pointing at them.** A sorted page's record numbers
are scattered through the table, so nothing can be skipped when reading them back. Measured on 4
million records, one page of 512:

| Paging a sorted view | Median | Max |
| --- | --- | --- |
| join `v_k` to `t_n` on `_ordinal` | 46 ms | 98 ms |
| two steps: the page's record numbers, then `IN` | 76 ms | 93 ms |
| the same by `rowid` | 76 ms | 81 ms |
| an ART index on `_ordinal`, joined or `IN` | 47–54 ms | 86 ms |
| 512 point lookups through that index | 134 ms | 138 ms |
| **`v_k` carrying the values, by `pos` range** | **1.3 ms** | **1.4 ms** |
| file order today, for comparison | 1.2 ms | 1.2 ms |

`StoreConcurrencyTests` holds a page to 27 ms, twice what dragging the scrollbar cost in the
finished reader. Only the carrying table is inside it, and it is the only one a person would not
feel.

**What it costs:** while a tab holds a filtered or sorted view, the store carries a copy of the
records that view selects — their text and their typed values. A filter shrinks it; a sort of a
whole table copies that table. Building one is slower than a table of record numbers alone: a
4-million-record sort took 4.0 s rather than 2.2 s. This is disk in the store directory, which is
removed with the store, and it is the reason D11's space estimate has to allow for it.

### D5 — A third connection prepares views and figures, off the UI thread

The existing `reading` connection pages on the UI thread and must stay quick. Building a view and
computing figures run on a worker, on a third connection, `querying`.

- A newer request for the same tab interrupts the running statement (`Interrupt` on `querying`)
  and replaces it. Edits in a filter field are debounced so typing does not start a build per
  keystroke.
- The previous view stays on screen until the new `v_k` is complete (table-query, "keeps showing
  the previous view").
- A failure, including DuckDB running out of temporary space, is reported on the tab. The
  previous view stays, and no other tab is affected.
- A table `querying` creates is visible to `reading` without reopening, as tables the import
  creates already are (the reader's D3). `StoreConcurrencyTests` gains the three-connection case.

### D6 — Marks are asked for by the page's record numbers

`UnresolvedBetween(first, last)` assumes a page covers a contiguous range of record numbers. A
sorted page scatters across the whole table, so the range would cover everything. It becomes
`… WHERE c._ordinal IN (page's record numbers)`, which is correct in every order and no more
expensive in file order.

### D7 — What a person enters is read by the validator's own rules, and passed as parameters

- **Numbers** are read with the same grammar `ValueInterpreter` uses, under the table's declared
  symbols, and passed as the normalised invariant string cast to `DECIMAL(38, s)` in SQL. .NET's
  `decimal` holds 28 digits and the column may hold 38.
- **Dates** are read with `Masks.TryDate` under the column's mask and the table's `Epoch`. Times
  are read under the column's time mask.
- **Input that fails** is refused in the editor, which shows the form it expects, for example
  `decimal symbol ,` or `DD.MM.YYYY`.
- **Text patterns:** `\`, `%` and `_` are escaped, `*` becomes `%` and `?` becomes `_`, and the
  statement uses `LIKE … ESCAPE '\'`. Contains, starts with and ends with are patterns built the
  same way. Ignoring case uses `ILIKE`, or `lower()` on both sides for equals and one of.
- **Nothing a person types is written into a statement.** Every value is a parameter. Only
  declared names and values reach SQL text, and those through `DeclaredSql.Identifier` and
  `Literal`.

The parsing helpers these need are internal to `GoBd.Validation` today, and are exposed to the
reader rather than copied.

### D8 — Figures are one statement per change, exact or refused

`SELECT <figures> FROM v_k` — whose typed columns D4 already computed — or `SELECT <figures> FROM
t_n_q` for a table in file order, runs on `querying` whenever the filters or the chosen figures
change. Nothing is joined: a view holds the records it selects.

- **Sum** is `DECIMAL` arithmetic. An overflow is caught and shown as "cannot be computed
  exactly", never retried in `DOUBLE`.
- **Average** is computed in .NET from the exact sum and count, rounded half away from zero to the
  column's scale, and marked as rounded. When the sum does not fit a .NET `decimal`, it too says
  it cannot be computed exactly.
- **Distinct count** counts typed values for typed columns, so `1,5` and `1,50` are one value, and
  shown values for text columns. It is always exact: `approx_count_distinct` is never used.
- **Empty values** are counted in the same statement (`count(*) FILTER (WHERE c = '')`).
- **Results are rendered** with the table's declared symbols and the column's mask, like the
  values beside them.

### D9 — Navigation into a hidden record removes the filter, keeps the sort

When `Follow` or a walk step reaches a tab whose view answers -1 for the target record:
1. The tab's filters are removed and its sort is kept.
2. The view is rebuilt: sorted only, or plain file order when there is no sort.
3. The tab is positioned at the record.

A notice, "Filter removed to show record 88124", is held on `TableTab` with an expiry of a few
seconds, beside the lasting status line the navigation already writes.

- **Alternative rejected: asking first.** Following keys is frequent, and a dialog on every hidden
  target stops the work it serves.
- **Deferred:** a "Restore filter" action on the notice. It can be added later without changing
  this behaviour.

### D10 — The choices are made above the grid, not on it

Avalonia 12's `TableView` has no sorting API, no header events and no footer row: a column offers
`Header`, `HeaderTemplate`, `ActualWidth` and resizing, and that is all. Rather than rebuild header
interaction inside a control that does not have it, every choice is made in a bar above the grid:

```
 +--------------------------------------------------------------------------+
 | Filters  [Konto = 1200 x] [Datum 01.01.2025-31.12.2025 x] [+ filter]      |
 | Sort     [1 Betrag desc x] [2 Datum asc x] [+ column]                     |
 | Figures  [Betrag sum x] [Konto distinct x] [+ column]                     |
 | Showing 1,204 of 4,112,009 records          [Back to file order]          |
 +--------+--------+------------+--------+--------------+                    |
 |   #    | Record | Datum      | Konto  | Betrag       |                    |
 +--------+--------+------------+--------+--------------+
 |  ...                                                                      |
 +--------------------------------------------------------------------------+
 | Betrag  sum 1.234.567,89      Konto  3 distinct      1,204 records        |
 +--------------------------------------------------------------------------+
```

- **Each control is a column picker plus what to do with that column.** A column the reader cannot
  filter, sort or compute with (D3) is not offered, and says why.
- **Sorting takes up to three columns**, each with its direction, in the order they were added. D4's
  statement orders by them in that order and then by `_ordinal`. A fourth is refused with a reason
  rather than silently dropping one.
- **The figures strip sits beneath the grid**, each figure labelled with its column, so it needs no
  alignment to the columns and no footer row in the control.
- **The grid keeps its two jobs:** showing values as the file stores them, and following
  references. It gains only the `#` column (`pos + 1`, the position in the view) and Record
  (`_ordinal`).
- **Why not headers:** it would mean drawing interactive content inside `TableViewColumnHeader`
  beside its resize grip, for choices that then stay invisible until a column is scrolled into
  view. A person filtering a table of twelve columns should see every filter at once.

### D11 — Memory and space are the engine's defaults, and failures are the tab's

- DuckDB keeps its default memory limit, at the user's decision.
- Beyond that limit, sorts and window functions spill into `temp_directory`, which for a database
  in a file defaults to `<store>/store.duckdb.tmp`: inside the store directory, on the volume the
  space check measured, and deleted with the store. No setting of our own is needed.
- **A view needs room of its own.** `v_k` carries the records it selects (D4), so a tab sorting a
  whole table holds a second copy of that table's values until its view changes or the tab
  closes. That is on top of the store itself and of any spill.
- The space check before import stays as it is: it runs once, before a byte is imported, and
  cannot know which tables a person will go on to filter.
- Both a view's copy and a spill therefore happen after that check. Running out of space while
  preparing a view is handled as D5 describes — reported on that tab, previous view kept — rather
  than prevented.

## Risks / Trade-offs

- **[A view's copy fills the volume]** → A view carries the records it selects (D4), so sorting a
  whole table copies that table, and several tabs multiply it. One view exists per tab and is
  dropped when it is superseded or the tab closes; a build that runs out of space is reported on
  that tab (D5) and leaves the previous view standing.
- **[Distinct counts need memory in proportion to the distinct values]** → They are bounded by the
  engine's memory limit and spill. Measured on a column of 4 million unique values.
- **[A key and a filter disagree about `5-` and `-5`]** → Deliberate (Non-Goals); the filter
  editor states that it compares numbers.
- **[A view of more than 2³¹ records]** → The paging list caps its count at `int.MaxValue` today.
  Views inherit that limit and do not widen it.
- **[Typing on the fly was too slow]** → Measured and settled: it is fast enough as a view, and a
  view carries its typed values so no figure pays for them twice.

## Migration Plan

None for data: a store lives for one session. The README section on using the reader is
rewritten. Rolling back removes the view layer, and the store's tables are unaffected.

## What the implementation settled

### Typed values stay a view; a view carries its records

Measured on 4 million records with DuckDB 1.5.5, the engine the reader ships:

| | typed view | stored typed table |
| --- | --- | --- |
| filter a date range + sort by amount (1.17M selected) | 550 ms | 174 ms |
| sort the whole table by amount | 1,689 ms | 621 ms |
| text contains, ignoring case, + sort | 119 ms | 143 ms |
| figures over the filtered view | 1,309 ms | 72 ms |
| figures over the whole table, with a distinct count of 4M unique values | 1,345 ms | 103 ms |
| added to every import | — | 2,652 ms |

The view costs no import time and no disk for an export nobody filters, which is most of them.
Carrying the typed values in `v_k` (D4) brings figures over a view back to the stored table's
speed; only figures over a whole unfiltered table pay the 1.3 s.

### The paging join was too slow, and a carrying view is not

The measurements are in D4. A page of a sorted view costs 46–98 ms through a join, 76 ms by record
number or `rowid`, 47–54 ms with an ART index, and 1.3 ms from a view that carries its records —
against a 27 ms budget and 1.2 ms for a page in file order.

### ICU collation ships with the engine, and German order is used

`duckdb_extensions()` reports `icu` installed and loaded, and 130 locale collations are available.
`COLLATE de` orders `apfel < Apfel < Äpfel < Ofen < Österreich < zebra < Zebra`, which is the same
on every machine, so it satisfies the spec's "not the machine's locale". It costs 1.5 s to sort 4
million values, against 0.56 s for binary order. Text sorts therefore use `COLLATE de`.

### Spills land inside the store directory

For a database in a file, DuckDB's `temp_directory` defaults to `<store>/store.duckdb.tmp`. That is
the volume the space check measured and the directory the store deletes when it closes, so D11
needs no setting of its own.

### `TableView` has no header interaction and no footer

Avalonia 12.1.2 exposes `Columns`, `CanUserResizeColumns`, and per column `Header`,
`HeaderTemplate`, `CellTemplate`, `Binding`, `Width` and `ActualWidth`. There is no sorting API, no
header event and no footer row. This is why D10 puts filters, sorting and figures above the grid
rather than on it.
