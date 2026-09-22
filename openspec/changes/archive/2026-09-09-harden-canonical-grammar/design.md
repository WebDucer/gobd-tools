## Context

See `proposal.md` — Why. Design-relevant state:

- `CanonicalDtd` loads the embedded resource and exposes `Sha256`, computed from whatever bytes
  it finds. It has no notion of what that hash *should* be.
- The expected value `691051c9…` appears in one place only: `CanonicalDtdTests`. Tests do not
  ship, so the guard exists at build time and nowhere else.
- `DtdCopyComparison.Compare` grades the export's copy against `CanonicalDtd.Bytes` and returns
  `Absent` / `Identical` / `NormalisationDifference` / `Altered`, carrying the export's hash.
  `DtdCopyCheck` turns that into `GOBD2003` / `GOBD2004` / `GOBD2005`.
- Measured, not assumed: the stored blob holds CRLF, and clones under `core.autocrlf` `false`,
  `input` and `true` all produce the canonical bytes. Checkout is not the exposure; commit is.

## Goals / Non-Goals

**Goals:**

- Make a wrong embedded grammar impossible to use silently, in the field and not only in CI.
- Make a DTD-copy finding answer "which side differs" without a source checkout.
- Close the commit-side line-ending exposure permanently.

**Non-Goals (design level):**

- Not a tamper-proofing measure. See D7.
- No change to how `index.xml` is validated, nor to the four graded outcomes.
- No new dependency. `SHA256.HashData` over 10 KB is already on the path.

## Decisions

### D1 — The pinned fingerprint defines the canonical grammar

Today the embedded resource *is* the grammar, so nothing can contradict it and drift is
invisible. The constant becomes the definition and the resource becomes an artefact that must
satisfy it:

```
  before   embedded resource ---- IS ------> canonical grammar
  after    pinned fingerprint --- DEFINES -> canonical grammar
           embedded resource ---- MUST MATCH --^
           export's DTD --------- COMPARED ----^
```

This is the same move as the v1 decision that made the embedded grammar authoritative and the
export's copy mere evidence, applied one level further down.

### D2 — Two independent literals, or the guard is theatre

The trap is circular verification. If the library holds the constant and the test asserts
`computed == CanonicalDtd.ExpectedSha256`, then replacing the resource and regenerating the
constant keeps the test green — the guard proves only that the constant matches itself.

```
  BAD                                       GOOD
  lib:  Expected = "691051c9…"              lib:  Expected = "691051c9…"
  test: computed == lib.Expected            test: computed      == "691051c9…"   (own literal)
        ^ one literal, self-proving               lib.Expected  == "691051c9…"
                                                  ^ two literals, two files
```

Changing the grammar must require editing both, so it is a deliberate act visible in one diff.
The test literal is not duplication to be refactored away; a comment must say so, because it
looks exactly like duplication.

### D3 — A failed self-check is a tool failure, reported not thrown

Verification runs once per run, at the start of `ExportValidator.Validate`, before the export is
opened. Two options were considered:

| | Diagnosis | Fits the finding model |
| --- | --- | --- |
| Throw from a static constructor | `TypeInitializationException` wrapping the real cause | no |
| Emit `GOBD9005` and stop | names expected and observed fingerprints | yes |

The second. It also lands on the right exit code for free: `3` means "the validator could not
run", which is exactly the truth when its own grammar is wrong — as opposed to `2`, which would
blame the export for the tool's defect.

The run stops after the finding. A validator that cannot trust its grammar has nothing useful to
say about an export, and half a report is worse than none.

### D4 — Findings name both fingerprints and the specific artefact

`GOBD2004` currently names only the export's file, and the underlying comparison is symmetric —
an odd canonical copy and an odd export copy produce identical text. Both codes gain the expected
and observed fingerprints. `GOBD2005` already carries the observed one; it gains the expected.

For `GOBD2004`, "line endings, byte-order mark or trailing whitespace" is a list of
possibilities, not an answer. Classify by applying each normalisation in isolation and reporting
those that account for the difference:

```
  strip BOM only                 -> equal?  BOM
  convert line endings only      -> equal?  line endings
  trim trailing whitespace only  -> equal?  trailing whitespace
  none alone suffices            ->         name every step that changed the bytes
```

The last row matters: a file that is both re-encoded and BOM-prefixed must report both, not pick
one.

### D5 — `.gitattributes` prevents; the fingerprint detects

Layers, each catching what the others cannot:

```
  .gitattributes *.dtd binary   PREVENTS conversion on commit and checkout
  fingerprint test              DETECTS at build time                    (exists)
  runtime self-check            DETECTS in the shipped binary            (new)
```

`.gitattributes` cannot catch a manual file replacement, a bad merge, or the 1.1 grammar dropped
in under the 1.6 name. The self-check catches all three. Neither subsumes the other, and both
are nearly free.

### D6 — Message text changes, report shape does not

Only the message strings and their argument lists grow. Codes, severities, JSON field names and
the JSON structure are untouched, so `schemaVersion` does not move. Consumers matching on codes —
the documented contract — are unaffected; consumers matching on message text were never promised
stability.

### D6b — The JSON report escapes only what JSON requires

`System.Text.Json`'s default `JavaScriptEncoder` escapes everything outside basic Latin plus
every HTML-sensitive character, so its output can be pasted into an HTML page unchanged. We never
wanted that guarantee and are paying for it three times over:

```
  '      -> \u0027     every quoted table, column and file name
  a u A  -> \u00E4 ...  every German message, for the primary audience
  & < >  -> \u0026 ...  GOBD3027, whose subject IS those characters
```

Switch to `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, which still escapes what the grammar
requires — quotation mark, backslash, control characters — and lets the rest through.

*Mechanical note:* `Encoder` is not settable through `[JsonSourceGenerationOptions]`. It must go
on a `JsonSerializerOptions` carrying `TypeInfoResolver = JsonReportContext.Default`, or be passed
to the context's constructor. Either keeps serialisation source-generated and NativeAOT-safe;
only the call site changes, not the model.

*The name deserves attention.* "Unsafe" means the output must never be emitted raw into an HTML
page or a `<script>` element, because HTML-sensitive characters are no longer escaped. This
report goes to standard output or a file for a pipeline to parse, which is the documented-safe
use. A consumer who renders it in a web page owns the HTML encoding — which was always where that
responsibility belonged.

*What does not change:* the document's structure, its field names, and every value a parser reads
back. Only the written form differs, so `schemaVersion` stays where it is.

### D6c — Prose supplied by a check is localised at render time

Naming which artefact differs put the first *prose* into a finding's arguments. Every argument
before it was a name copied verbatim out of `index.xml` — a table, a column, a hash — where being
language-neutral is exactly right. "line endings" is not that, and it surfaced immediately: a
German report reading "unterscheidet sich ... in line endings".

A check must not know the report language; that is what keeps checks independent of presentation.
So the check emits a stable token and the catalogue owns the wording:

```
  check        -> "@lineEndings"                      language-neutral, as arguments must be
  catalogue    -> en: "line endings"                  wording lives with the other wording
                  de: "Zeilenenden"
  render       -> substitutes the phrase for the token
```

The `@` marker keeps a token distinguishable from an ordinary argument that happens to look like
one, and several tokens travel in one argument separated by `.`, joined at render time. An
unknown token renders as itself rather than throwing: a wrong word in a message is a much smaller
failure than a report that cannot be produced.

Tokens never reach a consumer. The JSON report carries `message`, never the arguments it was
built from, so this changes nothing about the machine contract.

### D7 — What this is not

Drift protection, not tamper-proofing. Anyone able to modify the embedded resource can modify the
constant beside it. The threat model is accident — a normalising editor, a bad merge, a wrong file
copied in, a corrupted build — and against that it is effective. Claiming more would be false
assurance, and the code comment should say so plainly.

## Risks / Trade-offs

**Someone "simplifies" the two literals into one** → The whole guard becomes self-proving and
nothing announces it. Mitigated by a comment at both literals stating why the duplication is
deliberate, and by a test whose name says what it protects.

**Classifying the differing artefact is more code than a boolean** → Contained: three
independent normalisation steps, each already implemented for the existing comparison, applied
one at a time. The complexity is in reporting, not in the verdict, so a bug there cannot change
whether an export passes.

**`GOBD2004` still costs exit code 1** → Deliberate and out of scope. The standard requires the
carrier's DTD to be byte-equal to the original, so a re-encoded copy is a real defect in the
export. This change makes it diagnosable, not silent.

**The self-check adds a failure mode that did not exist** → A binary that previously ran with a
subtly wrong grammar now refuses to run at all. That is the intent: a wrong grammar produces
wrong verdicts, and a refusal is cheaper to notice than a false pass.

## Migration Plan

None required. No persisted state, no configuration, no report-shape change. A published binary
whose grammar is already canonical behaves identically apart from richer message text.
