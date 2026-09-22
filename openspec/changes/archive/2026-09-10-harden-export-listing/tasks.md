## 1. Verify the listing boundary

The code was implemented before this change was written; these tasks confirm each specified
behaviour is actually held by the tree, not merely believed to be. Treat a task as failed — not as a
formality — if its check does not produce the stated result.

- [x] 1.1 Confirm `ExportPath.IsContainedName` rejects a `..` segment, a rooted name and a name that
      only escapes after `\` normalisation, and that `ExportPath` exposes no path-rebuilding helper
      that bypasses it; verify the `ExportPath` tests in
      `tests/GoBd.Validation.Tests/Sources/ExportPathTests.cs` cover all three shapes and pass
- [x] 1.2 Confirm `FolderExportSource.EnumerateEntries` skips entries whose `LinkTarget is not null`
      and entries failing `IsContainedName`; verify by a test that plants a symlink and an escaping
      name in a test-owned workspace and asserts neither appears in the listing
- [x] 1.3 Confirm `FolderExportSource.OpenRead` resolves through `Path.GetFullPath` and refuses
      anything not under the root plus a separator; verify a test opens the escaping name directly
      and is refused even though the listing already excluded it
- [x] 1.4 Confirm `ZipExportSource` applies the same containment to member names; verify a test
      builds an archive with a `../` member and asserts it is absent from the listing
- [x] 1.5 Verify no hash or path of an out-of-root file can reach a report: run the validator over
      the planted workspace from 1.2 with a symlink named `gdpdu-01-03-2019.dtd` and confirm the
      report contains neither the target's SHA-256 nor its path
- [x] 1.6 Confirm the folder walk does not descend into a symbolically linked directory, and that
      excluding those does not change which ordinary files are listed; verify by tests planting a
      linked directory and a nested hidden file, and by re-running the crafted export from 1.5

## 2. Verify collision reporting

- [x] 2.1 Confirm `ExportEntryIndex` keeps the first entry per normalised name and counts the rest;
      verify a unit test asserts first-wins for both lookup and open, so the entry that is hashed is
      the entry that was listed
- [x] 2.2 Confirm a folder whose two files resolve to one name yields exactly one `GOBD1007` naming
      that name, and that the run completes with a full report rather than an exception; verify by
      test and by running the CLI against such a folder
- [x] 2.3 Confirm a ZIP carrying the same member name twice yields the same `GOBD1007`; verify by a
      test that writes the duplicate member directly
- [x] 2.4 Confirm an export without collisions emits no `GOBD1007`: verify across all 12 baseline
      reports, none of which may contain the code
- [x] 2.5 Confirm `GOBD1007` is registered as an error, is documented in `docs/finding-codes.md`
      with meaning, trigger and remedy, and has both catalogue translations; verify the two-way
      code-coverage test passes

## 3. Verify explicit finding scope

- [x] 3.1 Confirm `ScopeArgument` is gone from `FindingCodeInfo` and no reporter indexes into
      `Arguments` to recover a subject; verify by grep over `src/` returning nothing
- [x] 3.2 Confirm every check that concerns a table, medium, extension or the DTD calls
      `Finding.About(...)` with the matching `FindingScopeKind`; verify the guard test in
      `tests/GoBd.Validation.Tests/Findings/FindingScopeTests.cs` runs the real registry over the
      fixture exports, fails on any unattributed finding outside its export-wide allow list, and
      asserts the fixtures produce findings at all
- [x] 3.3 Confirm a table and a medium sharing one name stay distinguishable through
      `FindingScopeKind`; verify by a test asserting the two scopes are unequal
- [x] 3.4 Confirm the subject survives rewording: change a message template in a scratch copy, run
      the reporters, and confirm the reported scope is unchanged

## 4. Verify nothing else moved

- [x] 4.1 Confirm the JSON contract is unchanged: `schemaVersion` holds its previous value and no
      field was added or removed; verify by diffing all 12 baseline JSON reports byte-for-byte
- [x] 4.2 Confirm the text reports are byte-identical to the same baseline
- [x] 4.3 Run `dotnet build` and confirm zero warnings under warnings-as-errors, then `dotnet test`
      and confirm the full suite passes
- [x] 4.4 Publish the CLI with `PublishAot=true` and confirm it produces no trim or AOT warnings and
      that the published binary validates a fixture export
