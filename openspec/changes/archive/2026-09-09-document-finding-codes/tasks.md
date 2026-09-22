## 1. Establish the reference and its verification together

- [x] 1.1 Create `docs/finding-codes.md` with the four range sections and the heading and label format from design D1, documenting one code end to end as the worked example; verify the file exists and the example entry states meaning, trigger, severity rationale and remediation
- [x] 1.2 Add a test that parses the document into entries by matching headings, and assert it extracts a non-empty set whose size equals `FindingCodes.All.Length`; verify the test fails when pointed at a document with no entries, so it cannot pass vacuously
- [x] 1.3 Assert two-way coverage: catalogue minus documented is empty, and documented minus catalogue is empty; verify by temporarily adding an undocumented code and confirming the test names it, then reverting
- [x] 1.4 Assert each entry's stated severity and category equal the catalogue's; verify by temporarily altering one stated severity and confirming the test names the code and both values, then reverting

## 2. Write the entries

- [x] 2.1 Document the six `GOBD1xxx` source codes; verify the coverage test passes for that range and each entry names what a producer should change
- [x] 2.2 Document the eight `GOBD2xxx` grammar and DTD codes, including why a re-encoded DTD counts against the export and why an altered one is an error; verify the coverage test passes for that range
- [x] 2.3 Document the `GOBD3xxx` key and reference codes, `GOBD3001` to `GOBD3010`, explaining positional versus aliased mapping for the alias findings; verify the coverage test passes for those codes
- [x] 2.4 Document the `GOBD3xxx` structure and metadata codes, `GOBD3011` to `GOBD3031`, including the `Map`-as-Time convention and why `GOBD3029` reports a command that is never executed; verify the coverage test passes for those codes
- [x] 2.5 Document the five `GOBD9xxx` tool codes, making clear they report a failure of the validator rather than a defect in the export; verify the coverage test passes for that range
- [x] 2.6 Note in `GOBD3024` that its severity escalates to error when a `ForeignKey` resolves through the duplicated name; verify the entry explains the discrepancy a reader will otherwise hit in their log

## 3. Make it reachable and keep it honest

- [x] 3.1 Link the reference from `README.md` near the scope table; verify the link resolves to `docs/finding-codes.md`
- [x] 3.2 Run the whole suite and confirm the new test passes alongside the existing catalogue tests; verify no existing test was weakened to accommodate the document
- [x] 3.3 Record any code found to be wrong, mis-severitied or misnamed while writing the entries, and report it without changing behaviour in this change; verify the list is reported to the user even when empty
