## 1. Pin the canonical fingerprint

- [x] 1.1 Add `CanonicalDtd.ExpectedSha256` as a literal constant plus an `IsCanonical` check comparing it to the computed hash; verify a unit test asserts `IsCanonical` is true for the shipped resource
- [x] 1.2 Comment both the library constant and the existing test literal to state that the duplication is deliberate and that collapsing them makes the guard self-proving; verify the comment names the consequence, not just the rule
- [x] 1.3 Add a test asserting the library constant equals the test's own independent literal, so replacing the resource and regenerating the constant cannot pass unnoticed; verify the test name states what it protects
- [x] 1.4 Add a test that feeds `CanonicalDtd`'s comparison logic a deliberately wrong byte sequence and asserts `IsCanonical` is false, so the check is proven to fail rather than only to pass

## 2. Report a failed self-check as a tool failure

- [x] 2.1 Add `GOBD9005` to the code catalogue in the `Tool` category at `Error` severity, with English and German message templates carrying the expected and observed fingerprints; verify the existing catalogue tests covering uniqueness, category ranges and both-language coverage still pass
- [x] 2.2 Run the self-check once at the start of `ExportValidator.Validate`, before the export is opened, and stop the run when it fails; verify a test asserts no export-related finding is produced when the grammar is wrong
- [x] 2.3 Verify a failed self-check yields exit code `3` and not `2`; add a CLI-level test asserting the process reports "the validator could not run" rather than a non-conformant verdict

## 3. Make the DTD-copy findings self-diagnosing

- [x] 3.1 Extend `DtdCopyComparison` to classify which of byte-order mark, line endings and trailing whitespace accounts for a normalisation difference, applying each normalisation in isolation; verify tests cover each artefact alone and a file exhibiting two at once
- [x] 3.2 Carry the expected and observed fingerprints into `GOBD2004` and `GOBD2005`, and reword both message templates in English and German to state which side differs; verify a test asserts both fingerprints appear in the rendered message
- [x] 3.3 Verify the reworded `GOBD2004` names the specific artefact rather than listing all three possibilities; add a test over an LF-converted DTD asserting the message says line endings and not byte-order mark
- [x] 3.4 Confirm `GOBD2004` remains a `Warning` and `GOBD2005` remains an `Error`, and that an otherwise clean export with a re-encoded DTD still exits `1`; verify with a CLI-level test

## 4. Prevent line-ending conversion at the git layer

- [x] 4.1 Add `.gitattributes` marking `*.dtd` as binary; verify `git check-attr text -- src/GoBd.Validation/Resources/gdpdu-01-03-2019.dtd` reports the file as not text
- [x] 4.2 Verify a fresh clone under `core.autocrlf` set to `false`, `input` and `true` each produces the canonical fingerprint, and that re-adding the file under `input` leaves the stored blob unchanged

## 5. Keep messages readable in the JSON report

- [x] 5.1 Serialise the JSON report through a `JsonSerializerOptions` carrying `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` and `TypeInfoResolver = JsonReportContext.Default`; verify the report still publishes NativeAOT with no trim warning, confirming serialisation stayed source-generated
- [x] 5.2 Verify apostrophes, German umlauts and the characters `&`, `<`, `>` appear as themselves; add tests over an English message quoting a table name, a German message containing `ä`/`ü`, and a GOBD3027 finding whose subject is the forbidden characters
- [x] 5.3 Verify the document is still exactly one valid JSON document and that a quotation mark inside a message is escaped as the grammar requires; extend the existing single-document test with a message containing `"` and a backslash
- [x] 5.4 Verify the change is invisible to a parser: assert that every field of a report deserialises to the same value as before, and that `schemaVersion` is unchanged

## 6. End-to-end verification

- [x] 6.1 Add a fixture export whose DTD is the canonical grammar converted to LF, and assert the full CLI run reports `GOBD2004` naming line endings, both fingerprints, and exit code `1`; verify the same export as a ZIP behaves identically
- [x] 6.2 Verify the whole suite still passes with `InvariantGlobalization=false`, so the new message templates did not introduce culture-dependent formatting
- [x] 6.3 Publish NativeAOT and run the binary against the known-good fixture; verify it exits `0`, the self-check passes in a published binary, and no trim or AOT warning appears
