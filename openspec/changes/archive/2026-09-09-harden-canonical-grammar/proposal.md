# Harden the canonical grammar against drift

## Why

A real export validated on a second machine reported `GOBD2004` — "the shipped DTD differs from
the canonical grammar only in encoding artefacts". Nothing in the report could say **which side
had moved**: our embedded grammar, or the export's copy. Answering that took a source checkout,
a hash by hand, and three clone experiments.

That opacity is structural, not incidental:

- The expected fingerprint `691051c9…` lives in exactly one place — a **test file**. The shipped
  library computes its own hash and has nothing to compare it against, so a binary carrying a
  corrupted or substituted grammar validates happily and reports nothing.
- `GOBD2004` and `GOBD2005` compare two byte sequences and name only the export's file. The
  comparison is symmetric, so an odd canonical copy and an odd export copy are indistinguishable
  in the report.

The specific drift first suspected — git rewriting line endings on checkout — was measured and
ruled out: the stored blob holds CRLF and all three `core.autocrlf` settings check out the
canonical bytes. But the exposure is real on the other side of the round-trip. Re-committing the
file from a machine with `autocrlf=input`, or an editor that normalises on save, would store LF,
and nothing in the repository prevents it.

## What Changes

- **The pinned fingerprint becomes the definition of the canonical grammar.** `CanonicalDtd`
  gains an expected SHA-256 and a self-check. The embedded resource stops being self-evidently
  correct and becomes an artefact that must satisfy the constant.
- **A failed self-check is a tool failure, not a conformance verdict.** A new `GOBD9005` reports
  it, and the run exits `3` — a validator whose own grammar is wrong cannot say anything
  trustworthy about an export.
- **The DTD-copy findings become self-diagnosing.** `GOBD2004` and `GOBD2005` carry the expected
  hash, the observed hash, and which artefact differs (line endings, byte-order mark or trailing
  whitespace), so a reader can tell the two sides apart without a checkout.
- **`.gitattributes` marks `*.dtd` binary**, so no line-ending conversion can ever touch the
  resource on commit or checkout.
- **The JSON report stops mangling its own messages.** Serialisation currently escapes anything
  outside basic Latin, so `'` becomes `\u0027`, every German umlaut becomes a numeric escape, and
  the finding that reports which characters are forbidden renders them as `\u0026\u003C\u003E`.
  Findings that name both fingerprints are no use if the report is unreadable, so the two belong
  in one change.

### Non-goals

- **`GOBD2004` stays a warning.** The standard requires the data carrier's DTD to be byte-equal
  to the original, so a re-encoded copy is a genuine defect in the export and the finding is
  correct as it stands. Its exit-code cost is accepted deliberately. The new self-check answers a
  different question — whether *our* copy is the original — and the two must not be conflated.
- **No suppression mechanism.** `--ignore <code>` remains a possible future feature; it is not
  needed to make this change coherent.
- **No change to grammar validation itself.** Validation continues to resolve the DOCTYPE to the
  embedded canonical grammar. This change only decides whether that grammar is trustworthy.

## Capabilities

### New Capabilities

None. This hardens an existing capability rather than introducing one.

### Modified Capabilities

- `dtd-validation`: adds a requirement that the embedded canonical grammar verifies against a
  pinned fingerprint before it is used, and strengthens the existing reporting requirement for
  the export's DTD copy so the findings identify which side differs.
- `validation-reporting`: adds a requirement that message text stays readable in the JSON
  report, escaping only what the JSON grammar requires.

## Impact

- **Code**: `CanonicalDtd` (new constant and self-check), `DtdCopyCheck` and `DtdCopyComparison`
  (richer finding arguments), `FindingCodes` (one new code), `MessageCatalogue` (one new entry
  plus reworded `GOBD2004`/`GOBD2005`), `ExportValidator` (runs the self-check once per run),
  `JsonReportWriter` (serialises through an encoder that does not escape readable text).
- **Repository**: a new `.gitattributes`.
- **Compatibility**: `GOBD2004` and `GOBD2005` keep their codes and severities; only their
  message text and argument lists grow. The JSON report's shape is unchanged, so its
  `schemaVersion` does not move — the escaping change alters how values are written, never what
  a parser reads back. Consumers matching on message text — which the reports never promised as
  stable — would see different strings.
- **Verification gap this closes**: the fingerprint is currently asserted only at build time. It
  will hold in the field, in every published binary.
- **Open question**: what the DTD inside the reported real export actually hashes to. If it is
  `691051c9…`, our copy drifted on that machine and this work is a bug fix; if not, the export
  was re-encoded and this work is insurance plus diagnosability. Either way the change is the
  same, so it does not block.
