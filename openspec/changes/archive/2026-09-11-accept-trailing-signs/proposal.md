# Accept a sign after the number

## Why

The standard permits a negative number to carry its minus sign in front of the figure or behind
it, with no space between sign and digits. Its own examples are `-1782,90` and `1782,90-`
(Beschreibungsstandard 1.6, *Frequently asked questions*, in `reference/`).

Both engines accept a sign only in front. `ValueInterpreter.TryNumber` parses with
`AllowLeadingSign`, and `DeclaredSql.NumberPattern` is `^[+-]?…$`. So every value written the
second way is reported as `GOBD4001`, an error, and the export is reported as not conformant
although the standard allows it. The reader withholds the whole table for the same reason, so a
table the auditor needs is shown as defective instead of as data.

This is not a rare shape: ERP systems commonly write the sign after the figure. Fixing it now
matters because the reader's planned filtering, sorting and aggregation interpret numbers with
this same grammar, and should build on the corrected one.

## What Changes

- **A `Numeric` value may carry one sign, `-` or `+`, directly before its first digit or directly
  after its last.** `1782,90-` is a negative number, and so is `1.782,90-` under a declared
  grouping symbol.
- **A trailing `+` is accepted by project decision, not because the standard says so.** The
  standard mentions only the minus sign. The leading `+` both engines already accept rests on the
  same reading, and a sign that may lead should be allowed to trail. The decision is recorded
  here and in `GOBD4001`'s entry, so it is not mistaken for the standard's wording.
- **A sign is never counted as a digit.** Decimal places are counted between the decimal symbol
  and the last digit, so a trailing sign can neither hide excess decimals from the accuracy check
  nor add a phantom one.
- **What stays a defect:** a space between sign and digits (`1782,90 -`, `- 1782,90`), a sign at
  both ends (`-1782,90-`), and more than one sign at one end. Padding around the value is trimmed
  exactly as before, so a right-aligned fixed-length value such as `   1782,90-` is read.
- **`GOBD4001`'s documentation states the grammar**, including where a sign may stand.

### Non-goals

- **No other change to how numbers are read.** Decimal and grouping symbols, `Accuracy`,
  `ImpliedAccuracy` and the date grammar keep their current rules.
- **No new finding code, severity, option or exit code.** The JSON contract and `schemaVersion`
  are unchanged.
- **The leading sign is unchanged**, including the leading `+`.
- **Key comparison is unchanged.** Keys are compared as the file writes them. Whether `5-` and
  `-5` should count as one key value is a separate question, and this change does not answer it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `content-validation`: the requirement that a data file is read as its declaration defines it
  now states where a number's sign may stand, that it is not a digit, and that a sign separated
  from its digits means the value is not a number.

## Impact

- **Code:** `ValueInterpreter.TryNumber` in the streaming engine, and `DeclaredSql.NumberPattern`
  and `DeclaredSql.AccuracyConforms` in the store. Both engines change together, because they
  must report the same findings.
- **Documentation:** the `GOBD4001` entry in `docs/finding-codes.md`.
- **Tests:** value-interpreter tests, the both-engine agreement tests, and a fixture carrying
  trailing signs.
- **Behaviour:** an export whose only defects were trailing signs used to exit `2` and now exits
  `0`. Its reports lose those `GOBD4001` findings, and the reader now shows the tables it
  withheld. No value the standard forbids becomes accepted, except the trailing `+` decided
  above.
