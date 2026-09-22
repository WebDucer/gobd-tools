## 1. Solution scaffolding

- [x] 1.1 Create the solution with `src/GoBd.Validation`, `src/GoBd.Validation.Cli` and `tests/GoBd.Validation.Tests` targeting `net10.0`; verify `dotnet build` succeeds and the CLI project references the library
- [x] 1.2 Set repo-wide build properties (`Nullable=enable`, `TreatWarningsAsErrors=true`, `InvariantGlobalization=true`, `IsAotCompatible=true` on the library); verify `dotnet build` still succeeds with no warnings
- [x] 1.3 Escalate the culture-implicit analyzer rules CA1304, CA1305, CA1307 and CA1310 to build errors solution-wide; verify a scratch file containing a bare `decimal.Parse(text)` and a bare `value.ToString()` fails the build, then remove it
- [x] 1.4 Embed `src/GoBd.Validation/Resources/gdpdu-01-03-2019.dtd` into the library as an assembly resource; verify a test reads the resource and asserts its length is 10,646 bytes and its SHA-256 is `691051c9828ec2bbef71527c4aa77554c09ecd49e862fc6f2bc4b14507c72a0e`
- [x] 1.5 Add the test project's fixture layout with one known-good export as both a folder and a ZIP; verify a test opens each and finds `index.xml`

## 2. Findings model

- [x] 2.1 Implement the finding type (stable code, severity, message, optional line/column) and the severity enum; verify unit tests cover a finding with and without a location
- [x] 2.2 Implement the finding-code catalogue with the `GOBD1xxx`/`GOBD2xxx`/`GOBD3xxx`/`GOBD9xxx` ranges; verify a test asserts every declared code is unique
- [x] 2.3 Implement the run result (verdict, per-severity counts) and the strict-mode promotion of warnings; verify unit tests cover clean, warnings-only, errors-present and strict-mode cases

## 3. Export source

- [x] 3.1 Define `IExportSource` (enumerate entries, exists, resolve relative URL, open forward-only stream) and the entry descriptor; verify the interface compiles and is referenced by a stub test double
- [x] 3.2 Implement the folder source; verify tests enumerate a fixture directory tree and locate `index.xml` at the root
- [x] 3.3 Implement the ZIP source reading the central directory without extraction; verify tests enumerate a fixture archive and produce the same entry set as the equivalent folder fixture
- [x] 3.4 Implement source auto-detection from a path (directory to folder source, file to ZIP source); verify tests cover both plus a non-existent path
- [x] 3.5 Implement relative-URL resolution including `../`, with ordinal case-sensitive matching and containment inside the export root; verify tests cover simple, nested and `../` forms, absolute forms (`http://`, `ftp://`, `file://localhost/`, `file:///`), traversal outside the root, and a case-only mismatch reporting the dedicated diagnostic
- [x] 3.6 Emit findings for a missing root `index.xml` and for one present only in a subdirectory; verify tests assert the codes and that validation stops
- [x] 3.7 Add a large-file guard test asserting that validating a fixture whose data files are sparse multi-gigabyte entries never opens them and never extracts

## 4. Parsing and DTD validation

- [x] 4.1 Implement the immutable `index.xml` object model mirroring the DTD, with every node carrying its source line and column; verify a test parses the known-good fixture and asserts locations on nested nodes
- [x] 4.2 Implement the constant `XmlResolver` returning the embedded canonical DTD and throwing for every other URI; verify tests cover the DOCTYPE resolving to the embedded bytes, and refusal of `file:///`, `http://` and `ftp://` system identifiers with no filesystem or network access
- [x] 4.3 Wire DTD validation (`DtdProcessing.Parse`, `ValidationType.DTD`, capped `MaxCharactersFromEntities`), collecting all recoverable grammar violations as findings rather than throwing; verify tests assert multiple violations are reported from one document and that entity expansion is bounded
- [x] 4.4 Add grammar regression tests pinning the two constructs the specification PDF gets wrong: a `Table` with no `VariableLength`/`FixedLength` must fail, and an empty `Media` with `AcceptNoTables` must pass
- [x] 4.5 Add a test asserting a document written to 1.1 conventions validates under the 1.6 grammar, and that a `gdpdu-01-08-2002.dtd` system identifier yields an `info` finding rather than an error
- [x] 4.6 Implement the hard gate: well-formedness failure stops the run, grammar failure continues to the semantic phase; verify tests cover both paths

## 5. DTD-copy conformance

- [x] 5.1 Implement the graded comparison of the export's DTD copy against the canonical bytes (absent, byte-identical, normalisation-equal, altered); verify tests cover all four, with the normalisation case built by converting the canonical file to LF and asserting a warning rather than an error

## 6. Check engine

- [x] 6.1 Implement the check unit abstraction and the registry that enumerates all checks; verify a test asserts the registry is non-empty and that every registered check declares a code from the catalogue
- [x] 6.2 Implement the engine that runs every registered check and concatenates findings; verify a test with two stub checks asserts both run and neither aborts the other on throw

## 7. Tier 0 check catalogue — keys and references

- [x] 7.1 Implement the foreign-key column-declaration check; verify tests cover an undeclared column and an all-declared key
- [x] 7.2 Implement table identity resolution (`Name`, falling back to `URL`) and the `References` resolution check; verify tests cover dangling, URL-identified, ambiguous, and cross-`Media` references
- [x] 7.3 Implement the referenced-table-has-a-primary-key check; verify a test covers a reference to a table declaring only columns
- [x] 7.4 Implement the arity check between `ForeignKey/Name` count and the referenced primary key; verify tests cover a mismatch and a matching composite key
- [x] 7.5 Implement the datatype-match check between foreign key columns and their mapped primary key columns; verify tests cover a mismatch and a match
- [x] 7.6 Implement `Alias` validation (`From` within this key, `To` a primary key column of the target, no duplicate `To`, partial aliasing warning); verify tests cover each of the four cases

## 8. Tier 0 check catalogue — structure and metadata

- [x] 8.1 Implement the `Map`-as-Time convention including the valid time masks, the unequal-masks warning, and `Map` on a non-alphanumeric column; verify tests cover all three
- [x] 8.2 Implement the empty-`Media` check with and without `AcceptNoTables`; verify tests cover both
- [x] 8.3 Implement `FixedRange` coherence (invalid span, overlap, gap, exceeding declared record length); verify tests cover all four
- [x] 8.4 Implement the declared-metadata lint (non-negative integers for `Accuracy`, `ImpliedAccuracy`, `MaxLength`, `SkipNumBytes`, `Epoch` and `Range` bounds; symbol collisions; delimiter collisions; unusable date masks; `Table/Range/From` below one); verify tests cover each rule
- [x] 8.5 Implement the table-identity ambiguity checks (duplicate `Name` with escalation when referenced, duplicate `URL`, duplicate column names within a table); verify tests cover all three
- [x] 8.6 Implement the reserved-character and description-length checks; verify tests cover a reserved character after entity decoding and a 256-character description
- [x] 8.7 Implement `Command` reporting; verify a test asserts each `Command` yields a warning quoting its text, and that no process is started and the named file is never opened or resolved
- [x] 8.8 Implement `Extension` reporting including the error when its URL does not resolve; verify tests cover a resolving and a non-resolving extension
- [x] 8.9 Implement the table-file presence check over the entry listing; verify tests cover a present file, an absent file, and confirm no file content is read

## 9. Localisation

- [x] 9.1 Implement the compiled-in message catalogue keyed by finding code, with English and German entries and no satellite assemblies; verify a test asserts every code in the catalogue has an entry in both languages
- [x] 9.2 Implement language resolution in precedence order (CLI option, `GOBD_LANG`, OS language, English), reading `LC_ALL`/`LC_MESSAGES`/`LANG` on Unix and `GetUserDefaultUILanguage` on Windows without enabling globalization; verify tests cover each precedence level, an unsupported language falling back to English with an informational finding, and an undeterminable OS language falling back to English
- [x] 9.3 Add a guard test asserting that rendering the same findings under `LANG=de_DE.UTF-8` and `LANG=en_US.UTF-8` produces byte-identical output for a fixed language option, and that values quoted from `index.xml` are reproduced verbatim rather than reformatted; verify the test passes with `InvariantGlobalization` both enabled and disabled, so it guards the code rather than the build flag

## 10. Reporters

- [x] 10.1 Implement the text reporter (verdict first, findings grouped by table/element, ordered by severity, per-severity counts); verify a snapshot test over a fixture with mixed severities
- [x] 10.2 Add the scope statement to the text reporter's clean output, stating that data file contents were not examined; verify a test asserts it is present on a no-findings run
- [x] 10.3 Implement the JSON reporter with a schema version and the language used, using a `JsonSerializerContext` with no reflection-based serialisation; verify a snapshot test, a test asserting an empty findings array on a clean run, and a test asserting field names, severities and schema version are identical between an English and a German run
- [x] 10.4 Verify the JSON reporter writes nothing but the JSON document to its stream; add a test asserting the captured stdout parses as a single JSON document

## 11. CLI

- [x] 11.1 Implement the validate command taking an export path, with format selection defaulting to text and an optional output file; verify tests cover ZIP input, folder input, both formats, and file output leaving stdout free for a summary
- [x] 11.2 Implement the exit-code mapping (`0`/`1`/`2`/`3`) and `--strict`; verify tests assert each code including a non-existent path yielding `3` and strict mode promoting warnings to `2`
- [x] 11.3 Add the language option to the command surface and wire it to language resolution; verify tests assert the option overrides `GOBD_LANG` and the OS language, and that German output is produced on request
- [x] 11.4 Add the scope statement to `--help`; verify a test asserts the help text names `index.xml`, the DTD and the file listing, and states data file contents are not examined

## 12. Packaging and end-to-end verification

- [x] 12.1 Enable `PublishAot` for the CLI; verify `dotnet publish -c Release -r osx-arm64` completes with zero trim and AOT warnings
- [x] 12.2 Add a CI matrix publishing and smoke-testing the native binary on `linux-x64`, `win-x64` and `osx-arm64`; verify each runs the known-good fixture and exits `0`, that the `win-x64` leg exercises OS language detection, and that the suite also runs once with `InvariantGlobalization=false` so the locale-invariance guard proves the code rather than the build flag
- [x] 12.3 Add the release workflow attaching the per-RID native binaries to a GitHub release with checksums; verify a dry-run produces one artifact per RID
- [x] 12.4 Add an end-to-end test validating a deliberately broken fixture export — dangling reference, undeclared foreign key column, altered DTD, missing data file — and asserting the exact set of finding codes and exit code `2`
- [x] 12.5 Verify the native binary validates an export on a machine with no .NET runtime present, and that it uses the embedded DTD when run in a directory containing no DTD file
