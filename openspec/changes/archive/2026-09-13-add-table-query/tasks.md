## 0. Before starting

- [x] 0.1 Confirm `accept-trailing-signs` is merged; verify `BothEnginesTests` accepts `1782,90-`
      on this branch's base

## 1. Spikes, settled before anything is built on them

Each spike records its outcome under "What the implementation settled" in design.md. Its outcome
decides a later task, so it is not optional.

- [x] 1.1 Build `t_n_q` (design.md D1, D2) over a synthetic conforming table of 4 million records;
      measure a filter plus a sort into `v_k` (D4), a page read through `v_k`, and the figures of
      D8, including a distinct count over a column of unique values. Record the timings and
      decide whether `t_n_q` stays a view or becomes a table
- [x] 1.2 Sort a large table under a deliberately small DuckDB memory limit, set in the test only;
      verify the spill files appear inside the store directory and are gone after
      `ExportStore.Dispose`
- [x] 1.3 Establish what Avalonia 12 `TableView` supports and record it; no header interaction is
      needed, because D10 now puts filters, sorting and figures in controls above the grid
- [x] 1.4 Check whether `DuckDB.NET.Data.Full` 1.5.5 ships ICU collation; record the text sort
      order chosen, and resolve the open question in design.md

## 2. Typed reading in the store

- [x] 2.1 Add the measuring aggregate of D3 to a conforming table's import (`Numeric` integer
      digits and decimals, the count of values that are not times in a Time column), and expose
      each column's capabilities; verify with tests that a 39-digit value turns the column off, a
      column without `Accuracy` gets the measured scale, and a table with no records measures
      without error
- [x] 2.2 Add the typed readings of D2 to `DeclaredSql`: `Numeric` with a sign at either end,
      grouping and `ImpliedAccuracy`; `Date` with the mask and `Epoch`; Time with `HH`, `MM`,
      `SS` and `TT`; `AlphaNumeric` through the `Map` `CASE`. Verify with an agreement test that
      every conforming `Numeric` and `Date` case of `BothEnginesTests` yields the same number or
      date as `ValueInterpreter` and `Masks.TryDate`
- [x] 2.3 Create `t_n_q` when a conforming table's import completes; verify it exposes the text
      columns unchanged and one typed column per capable column, and that selecting every typed
      column of every fixture table raises no error

## 3. Views and figures in the store

- [x] 3.1 Open the `querying` connection with its own `Interrupt` (D5); extend
      `StoreConcurrencyTests` to import, page and build a view at once on the three connections
- [x] 3.2 Build `v_k` from a filter-and-sort description, with every value passed as a parameter
      (D4, D7); verify by a test per operator and type, and by tests that equal values keep file
      order and that empty values come last in both directions
- [x] 3.3 Page through `v_k` and look up a record's position in it; verify page contents against
      the view's order, and that a hidden record answers -1
- [x] 3.4 Ask for dangling marks by the page's record numbers (D6); verify a sorted view marks the
      same values as file order does
- [x] 3.5 Compute the figures of D8; verify exact sums at the declared scale, an overflowing sum
      reported as not computable, the average rounded and marked, empty values counted
      separately, and `1,5` and `1,50` counted as one distinct value
- [x] 3.6 Drop a `v_k` when it is superseded or its tab closes; verify no `v_` table remains after
      a sequence of changes and closes

## 4. Session and tab model

- [x] 4.1 Give `TableTab` its view: filters, sort, chosen figures and the current row list. Verify
      in `ReaderTabsTests` that the view survives leaving and returning, is gone after closing,
      and does not touch other tabs
- [x] 4.2 Read filter input under the declaration (D7); verify with German and English symbols, a
      two-digit-year mask under a non-default `Epoch`, a `TT` time mask, a 30-digit number, and
      refused input stating the expected form
- [x] 4.3 Prepare views on a worker, debounced and superseding (D5); verify that the previous view
      stays until the new one is ready, that a superseded build is interrupted, and that a
      failing build leaves the tab on its previous view with a message
- [x] 4.4 Handle navigation into a hidden record (D9) for a forward follow and a walk step. Verify
      in `NavigationTests` that the filter is removed, the sort kept, the tab positioned at the
      record and the notice expires, and that a visible target keeps the filter
- [x] 4.5 Add reader notices to `ExportReading`, apart from findings (D3); verify that the summary
      lists a too-large column with no finding code, and that its findings are still the
      validator's set for the same export

## 5. Reader UI

- [x] 5.1 Show `#` (position in the view) and Record (file record number) as the first two
      columns; verify both on a fixture whose declaration excludes a header row, where `#1` is
      Record 2
- [x] 5.2 Add a test project of its own for the controls, `tests/GoBd.Reader.Ui.Tests`, on
      Avalonia's headless platform, and add it to the solution. Kept apart from the unit tests:
      those need no window and must stay quick, while these stand up a windowing platform and
      drive controls. Verify it runs in `dotnet test` alongside the others
- [x] 5.3 Add the sort control above the grid (D10): up to three columns, each with a direction,
      applied in the order they were added and removable; a fourth is refused with a reason.
      Verify by driving the control headlessly
- [x] 5.4 Add the filter control above the grid, with an editor per type — `Numeric`, `Date`, Time
      and `AlphaNumeric` — each showing the symbol or mask it expects; a column the reader cannot
      filter is not offered and says why. Verify headlessly that each editor applies, refuses
      what it cannot read, and clears
- [x] 5.5 Add the view banner: "N of M records", removable filters, the sort, "Back to file
      order" and "no record matches"; verify headlessly
- [x] 5.6 Add the figures control and the strip beneath the grid: a column-and-function choice
      limited by type, results in the declared symbols or mask, marked when rounded or not
      computable; verify headlessly
- [x] 5.7 Show that a view is being prepared, the expiring navigation notice, and the summary's
      section for the reader's limits; verify headlessly, and by running the reader on the
      `good-export` fixture

## 6. Documentation and verification

- [x] 6.1 Rewrite the README's "Use it" section and its sentence "Nothing is sorted, filtered or
      written to the export" to describe filters, sorting, figures, `#` and Record; verify
      `ReadmeUsageTests` passes
- [x] 6.2 Scroll a filtered and sorted view of the three-million-record export — the one the page
      budget was measured on, so the two are comparable — and verify a page stays inside that
      budget and the reader's resident records stay within the paging window, as
      `PagingBudgetTests` measures for file order
- [x] 6.3 Run `dotnet build` and confirm zero warnings under warnings-as-errors, then `dotnet test`
      and confirm both suites pass; publish the reader for one runtime identifier and confirm the
      published build starts and opens an export. The controls themselves are held by the
      headless tests, which run against the same assemblies the publish carries — nobody has
      filtered by hand in a published build, and this does not claim otherwise
