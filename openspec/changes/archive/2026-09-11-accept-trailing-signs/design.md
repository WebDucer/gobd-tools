## Context

See proposal.md for why. The number grammar exists twice, deliberately: once in the streaming
engine and once in the store. Each finds defects by its own means, and the both-engine tests hold
them to reporting the same findings (the reader's design.md D4). This change has to keep them in
step.

- **Streaming engine.** `ValueInterpreter.TryNumber` trims the padding, splits on the declared
  decimal symbol, checks and removes digit grouping, requires digits after at most one leading
  sign, and parses with `AllowLeadingSign`. `decimals` is the length of the part after the
  decimal symbol.
- **Store.** `DeclaredSql.NumberPattern` builds `^[+-]?INTEGER FRACTION$` and tests it against the
  trimmed value. `AccuracyConforms` tests `^.*DEC[0-9]{n+1,}$` for excess decimals.

The accuracy pattern is the trap. Widening only the number grammar would make `1,234-`, under an
accuracy of 2, a number whose excess decimals go unreported: the accuracy pattern needs the digits
at the very end of the value, and the sign is in the way. The accuracy check has to change with
the grammar, in both engines.

## Goals / Non-Goals

**Goals:**
- One rule, expressed in both engines by each engine's own means, not by a shared implementation.
- Every existing reading unchanged for values that carry no trailing sign.

**Non-Goals:**
- Comparing keys by numeric value. Keys are compared as the file writes them (`KeyHash` hashes
  the text), so `5-` and `-5` remain different key values.

## Decisions

### D1 — The streaming engine moves a trailing sign to the front, then reads as today

After trimming, a value ending in `+` or `-` that does not start with a sign has its last
character moved to the front. Everything after that is the existing code: the leading-sign
check, grouping, the decimals count and `AllowLeadingSign` parsing.

- **Why:** the only new logic is one normalisation step. The decimals count never sees the sign,
  which is what the "a sign is not a digit" scenario requires.
- **Both ends and doubled signs stay rejected without new code.** `-1782,90-` starts with a
  sign, so nothing moves and the trailing `-` fails the digit check. `5--` becomes `-5-`, whose
  body `5-` is not digits.
- **Alternative rejected:** teaching the split, grouping and digit checks about a trailing sign.
  Each step would need to know about it, and the decimals count is where that would go wrong.

### D2 — The store widens its patterns with an alternative, not a rewrite

- `NumberPattern` becomes `^(?:[+-]?BODY|BODY[+-])$`, where `BODY` is the current integer and
  fraction. The alternation states "one sign, at one end" directly.
- The accuracy pattern becomes `^.*DEC[0-9]{n+1,}[+-]?$`, so excess decimals are found whether
  or not a sign follows them.
- **Why not strip the sign with `regexp_replace` first?** It would be a second transformation to
  keep in step with the first. The alternation keeps each test a single pattern over the trimmed
  value, as today, and the store stays a different means to the same rule, not a port of D1.

### D3 — A declared symbol that is itself a sign character turns the trailing sign off

The declaration may, in principle, name `-` or `+` as its decimal or grouping symbol. For such a
table `1-` cannot be told apart: it could be a trailing sign or a decimal symbol. Both engines
then keep today's reading and recognise no trailing sign. This is the conservative choice: it
changes nothing for a declaration that was already ambiguous.

### D4 — The trailing `+` is documented as a project decision

The `GOBD4001` entry in `docs/finding-codes.md` states the grammar: one sign, `-` or `+`,
directly before or after the digits. It cites the standard for the minus, and says the trailing
`+` is accepted by symmetry with the leading `+` already accepted, not because the standard
states it.

### D5 — Agreement is tested case by case, in both engines

The both-engine number cases gain the shapes this change touches:

| Value | Expected |
| --- | --- |
| `1782,90-`, `1782,90+`, `1.782,90-` | a number |
| `   1782,90-` (padded) | a number |
| `178290-` under `ImpliedAccuracy` 2 | -1782.90 |
| `1,234-` under `Accuracy` 2 | excess decimals |
| `1782,90 -`, `- 1782,90`, `-1782,90-`, `1782,90--`, `+-5` | not a number |
| trailing sign under a declared `-` decimal symbol | today's reading (D3) |

Each case asserts the same finding from both engines.

## Risks / Trade-offs

- **[A pipeline expects such an export to fail]** → The exit code falls from `2` to `0` only where
  trailing signs were the sole defect, and the standard permits them. The proposal's Impact
  section states it.
- **[The two engines drift]** → D5's table runs in both engines, and a case added to one without
  the other fails the agreement tests.
- **[Keys that differ only in where the sign stands]** → Unchanged by design (see Non-Goals).
  The planned typed filtering will read `5-` and `-5` as equal numbers while key checks still
  treat them as different values; that change must say so.
- **[A declared symbol equal to a sign character]** → D3 keeps today's reading; no known export
  declares one.

## Migration Plan

None. No format, option or report shape changes. Reverting the change restores the previous
grammar.
