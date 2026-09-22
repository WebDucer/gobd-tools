## 1. Pin the grammar in tests first

Each test in this group is written before the code changes and must fail against the current tree
for the reason stated. A test that passes before the change does not test this change.

- [x] 1.1 Add the conforming and non-conforming shapes from design.md D5 to
      `BothEnginesTests.ANumberIsANumberToBothOrToNeither`; verify `1782,90-`, `1782,90+`,
      `1.782,90-` and the padded `   1782,90-` fail today, with both engines reporting `GOBD4001`,
      and that the non-conforming shapes already pass
- [x] 1.2 Add a both-engine test for excess decimals behind a trailing sign (`1,234-` under
      `<Accuracy>2</Accuracy>`) that asserts `AccuracyExceeded` from both engines; verify it fails
      today, because both report a type mismatch instead
- [x] 1.3 Add `ValueInterpreterTests` for the value each accepted shape denotes: `1782,90-` is
      -1782.90, `1782,90+` is 1782.90, `1.782,90-` is -1782.90, and `178290-` under
      `<ImpliedAccuracy>2</ImpliedAccuracy>` is -1782.90. Also add rejection tests for
      `1782,90 -`, `- 1782,90`, `-1782,90-` and `1782,90--`. Verify the accepting tests fail
      today
- [x] 1.4 Add a both-engine test for a table declaring `-` as its decimal symbol, asserting a
      trailing `-` is read exactly as today (design.md D3); verify it passes before and after
      the change

## 2. Streaming engine

- [x] 2.1 In `ValueInterpreter.TryNumber`, after trimming the padding, move a trailing `+` or `-`
      to the front when the value does not already start with a sign and neither declared symbol
      is a sign character (design.md D1, D3); verify 1.3 passes and the streaming side of 1.1,
      1.2 and 1.4 agrees with the expected findings
- [x] 2.2 Update `TryNumber`'s remarks to state where a sign may stand and that it is never
      counted as a decimal; verify the remarks match the delta spec's scenarios

## 3. Store

- [x] 3.1 Widen `DeclaredSql.NumberPattern` to `^(?:[+-]?BODY|BODY[+-])$`, producing the trailing
      alternative only when neither declared symbol is a sign character (design.md D2, D3);
      verify 1.1 and 1.4 pass in both engines
- [x] 3.2 Allow an optional trailing sign after the digits in `DeclaredSql.AccuracyConforms`;
      verify 1.2 passes in both engines
- [x] 3.3 Run `BothEnginesTests`, `DialectAgreementTests` and `RecordConformanceCheckTests`;
      verify all pass with no case removed or weakened

## 4. Documentation and end to end

- [x] 4.1 Update the `GOBD4001` entry in `docs/finding-codes.md`: one sign, `-` or `+`, directly
      before or after the digits. Cite the standard for the minus, and state that the trailing `+`
      is a project decision (design.md D4). Verify `FindingCodeReferenceTests` passes
- [x] 4.2 Add an end-to-end test over an export whose only irregular values carry trailing signs.
      Verify `gobd-validate --contents` exits `0` with no `GOBD4001`, and that the reader opens
      that table as data, not as findings
- [x] 4.3 Confirm nothing else moved: run the CLI over the `good-export`, `broken-export` and
      `repackaged-export` fixtures with and without `--contents`, in text and JSON, before and
      after the change; verify the reports are byte-identical
- [x] 4.4 Run `dotnet build` and confirm zero warnings under warnings-as-errors, then `dotnet test`
      and confirm the full suite passes; publish the CLI with `PublishAot=true` and confirm no
      trim or AOT warnings
