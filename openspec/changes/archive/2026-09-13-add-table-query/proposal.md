# Filter, sort and total a table's records

## Why

The reader shows each table in file order and nothing else. Its spec forbids sorting, filtering,
grouping and aggregation, a restriction set to keep the first version simple. Someone reading a
table of millions of records cannot answer the first questions an audit asks without leaving the
reader: which bookings hit account 1200 in March, what they total, what the largest amount is. A
spreadsheet does not know the declaration, so it reads `1.234,50` or `01.02.25` however the
machine's locale guesses.

The rule from the first version that stays is that data is never altered. The restriction existed
to keep records citable, because record order was what made a record citable. Giving every record
its file record number in a column of its own keeps it citable, whatever order a view shows it in.

## What Changes

- **Filters per column, by declared type:**
  - `Numeric`: equals, not equals, less or greater (or equal), between, empty, not empty.
  - `Date`: on, before, after, between, empty, not empty.
  - Time, meaning an `AlphaNumeric` column that declares a Time `Map`: on, before, after,
    between, empty, not empty.
  - `AlphaNumeric`: equals, not equals, contains, starts with, ends with, a pattern with `*` and
    `?`, and one of a list. Each can ignore case. Also empty and not empty.
  - Filters on several columns must all hold. What a person enters is read under the table's
    declared decimal and grouping symbols and the column's date or time mask, as the grid shows
    values, never under the machine's locale.
- **Sorting by up to three columns**, each ascending or descending, applied in the order the person
  chose them, by what each column's type means: numbers as numbers, dates as dates. Records equal
  in every sorted column keep file order, and empty values sort last.
- **Filters, sorting and figures are chosen in controls above the table**, not on the column
  headers, so the grid stays a grid and the choices stay visible.
- **Figures over the view.** For each column, the person may choose a figure the column's type
  allows: count, distinct count, sum, minimum, maximum or average. It is computed over the records
  the view holds. Nothing is chosen by default.
- **`#` and Record.** `#` is a record's position in the view. Record is its number as the file
  counts records: the number findings cite and navigation lands on.
- **A view is labelled.** It states how many records it shows out of how many, which filters and
  which sort apply, and one action returns to file order.
- **Following a reference into a filtered tab.** When that tab's filter hides the target record,
  the filter is removed, the tab lands on the record, and a message says so for a few seconds.
- **Columns the reader cannot compute with.** A `Numeric` column holding a value the reader cannot
  represent exactly still shows its values. It offers no filter, sort or figure, and says why on
  the column and in the summary. The summary states this as a limit of the reader, not as a
  finding: the export is not defective.
- **BREAKING (spec):** reader-ui's "No query surface" scenario is removed.

### Non-goals

- **No altered values.** Typed values exist only to filter, sort and compute with, and are never
  displayed. The grid still shows what the file stores, with declared maps applied.
- **No grouping or cross-table analysis.** Figures are quick checks over one table's view.
- **No sorting by more than three columns, no OR between filters, and no filter on whether a
  reference resolves.** Each is a possible follow-up.
- **No export of a view or its figures.**
- **No change to the import's checks, its findings or the table gate.**
- **No memory limit of the reader's own.** The store's engine keeps its default limit. The space
  check made before importing stays as it is.

### Depends on

- `accept-trailing-signs`, which must land first. Typed numbers are read with the same number
  grammar, including a sign after the digits.

## Capabilities

### New Capabilities

- `table-query`: filtering, sorting and figures over a table's records, by declared type. Each is
  a view that never alters a value and keeps every record's number.

### Modified Capabilities

- `reader-ui`:
  - "Data is presented as declared, and only presented" becomes "Data is presented as declared,
    and never altered". File order is the default rather than the only order, every record
    carries its file record number, and the no-query-surface scenario goes.
  - "A foreign key carries the reader to the records it refers to" gains how a navigation treats
    a filtered view.

## Impact

- **Store** (`GoBd.Reader.Data`):
  - A measuring pass at import.
  - A typed query view per table.
  - Position tables for views.
  - A third connection for building views and computing figures.
  - Dangling-reference marks asked for by record numbers instead of a range.
- **Session and view models:** a filter, sort and figure model per tab; reading filter input under
  the declaration; what each column can do; the reader's own notices.
- **UI:**
  - The grid gains the `#` and Record columns, sorting and filter editors per type on the column
    headers, a view banner and a figure footer.
  - Navigation gets a message that disappears after a few seconds.
  - The summary gets a section for the reader's limits.
- **Documentation:** the README's "Nothing is sorted, filtered or written to the export" is
  rewritten to what is now true.
- **Cost:**
  - Changing a filter or sort scans and sorts the table once. Scrolling costs the same as today,
    because a view carries the records it selects rather than pointing back at the table.
  - A tab holding a filtered or sorted view therefore costs disk in the store: a copy of the
    records that view selects, released when the view changes or the tab closes.
  - Sorting a large table may also use disk space in the store's temporary location.
