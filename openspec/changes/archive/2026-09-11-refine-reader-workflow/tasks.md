## 1. Open a folder named with a trailing separator

Independent of everything else here and already shipped, so it goes first.

- [x] 1.1 Trim a trailing separator from `FolderExportSource`'s full root before anything is
      compared with it; verify by a test that `good-export/` and `good-export` produce identical
      entries and identical findings, and that the CLI exits `0` for the path with the slash
- [x] 1.2 Verify that the reader's own path normalisation in `ReaderWorkspace` still passes its
      tests unchanged, now that it is redundant

## 2. Prove two connections can share a store (spike)

Runs before the pipeline because D3 depends on its outcome.

- [x] 2.1 While one connection imports a table of at least a million records, page through a
      finished table on a second connection to the same store; verify there are no errors and no
      page takes longer than the 13.6 ms scrollbar step measured before, at twice that at most
- [x] 2.2 Establish whether `DuckDB.NET` can interrupt a running statement from another thread;
      verify by interrupting a long import and observing it stop
- [x] 2.3 Record both outcomes in `design.md`, D2 and D3: confirmed, or the fallback taken;
      verify the design states which

## 3. The opening pipeline

- [x] 3.1 Report extraction progress as bytes read against the entry's length, for folder and ZIP
      exports alike; verify by a test that progress ends exactly at the entry's length and never
      exceeds it
- [x] 3.2 Make extraction and import cancellable between records; verify that cancelling in the
      middle of a large table stops within one record and leaves no store behind once the session
      is disposed
- [x] 3.3 Run the open as a background pipeline in document order (parse and structural checks,
      then extract, import and record-check each table, then key checks across all tables); verify
      by a test that each table's state moves from waiting to reading to ready, defective or
      failed, in order, and that total progress equals the declared bytes
- [x] 3.4 Make a table openable as soon as it is ready, independent of the others; verify by
      opening a ready table while a later one is held mid-import by a deliberately slow source
- [x] 3.5 Say that a table is still being read when it is chosen too early, and present it once
      it is ready; verify at view-model level that the view reports "not ready" and then delivers
      the presentation
- [x] 3.6 Cancel the pipeline when the reader closes or another export is opened; verify the
      worker stops, the session is disposed and its store directory is gone
- [x] 3.7 Page ready tables on the UI's own connection while the worker imports, as the spike
      decided; verify concurrent paging and import in one test
- [x] 3.8 Report progress at most every 100 ms, plus on every state change; verify by counting the
      notifications raised while importing a table of a million records

## 4. The summary on the start page

- [x] 4.1 Build the summary from the pipeline: the verdict, each table with its record count or its
      number of findings, and the findings grouped by table; verify against an export with one
      clean table, one defective table and one table whose references do not resolve
- [x] 4.2 Verify that the summary's findings carry the same codes and messages as
      `gobd-validate --contents` for the same export
- [x] 4.3 Show unfinished tables as waiting or being read, and complete the summary as they finish;
      verify at view-model level that it updates as each table's state changes
- [x] 4.4 Present the summary as the first tab, open on arrival and not closable; verify by the
      view model, and by a manual check in the running reader

## 5. One tab per table

- [x] 5.1 Replace the single content area with a tab host holding one tab per table, reused when
      the table is reached again; verify that ten navigations across two tables leave exactly two
      table tabs
- [x] 5.2 Bring the target tab to the front on every navigation, and make the navigator selection
      follow the active tab; verify that after choosing a table and after following a reference, the
      selected navigator entry is the active tab's table
- [x] 5.3 Keep each tab's position when it is left; verify that returning to a tab after following
      a reference away from it finds the same record in view
- [x] 5.4 Close a tab without affecting the others; verify that the table can be opened again and
      the remaining tabs keep their positions

## 6. The walk bar and navigation messages

- [x] 6.1 Create Previous and Next only when a backwards walk starts, inside the referring table's
      tab, and remove them when the walk is closed, the tab is closed, or another walk starts;
      verify that a forward navigation presents no stepping controls at all
- [x] 6.2 Keep each navigation's message in the tab it concerns; verify by a regression test for
      the reported screen, where "Record 13 for '33592539'" stayed beside an empty `ProductLine`,
      that switching tabs never shows another table's message

## 7. Relationships in the navigator

- [x] 7.1 List _References_ and _Referenced by_ beneath each table as leaf entries naming the other
      table and the key columns; verify against an export with a table that has two parents and
      one child, a reference that crosses media, and a table that references itself
- [x] 7.2 Make choosing a relationship entry do nothing beyond selecting it; verify that neither the
      active tab nor any table's position changes
- [x] 7.3 Remove the header's "References:" and "Referenced by:" text lines that these entries
      replace; verify that the header no longer carries them

## 8. Followable and dangling cells

- [x] 8.1 Style every foreign key column, including all columns of a composite key, with the link
      colour, a hand cursor and a tooltip naming the table it leads to; verify that the column model
      marks exactly the foreign key columns, and check the result by hand in the running reader
- [x] 8.2 With each page fetched, ask the store which of that page's foreign key tuples have no
      match, and mark those cells; verify that a table with more dangling values than the finding
      bound marks all of them, the fifty-first included
- [x] 8.3 Measure paging with marks on the three-million-record export; verify that a scrollbar
      step costs no more than twice the 13.6 ms measured before

## 9. Close out

- [x] 9.1 Verify the gate is unchanged: a table whose records conform but whose references do not
      resolve shows its data, and reports those references as findings
- [x] 9.2 Verify that the full suite passes with zero warnings under warnings-as-errors, and that
      the CLI still publishes as a single native file
- [x] 9.3 Update the README's reader section briefly to cover the summary, the progress, tabs,
      relationships and followable cells; verify the README tests still pass
- [x] 9.4 Record in `design.md` whatever the implementation settled beyond the spike; verify it
      carries no unresolved question
