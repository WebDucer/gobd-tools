# Document the finding codes

## Why

The validator emits **50 finding codes** and documents none of them. A producer who gets
`GOBD3010` in a CI log has the one-sentence message and nothing else: no explanation of what the
construct means, why it is suspect, or what to change in the export. The codes are the tool's
stable public interface — the thing a pipeline suppresses by and a person searches for — and
they are the only part of it with no reference.

The gap is widening rather than static. The last change added `GOBD9005` and reworded two others;
nothing anywhere records what they mean. As the catalogue grows toward Tier 1 and Tier 2, where
the count will roughly triple, an undocumented code is the default outcome unless something
prevents it.

Three things make this worth doing properly rather than as a one-off page:

- **A message is not an explanation.** "A composite key is only partially aliased" states what
  was found. It does not say that the standard leaves the unaliased columns' behaviour
  undefined, which is *why* it is reported.
- **Severity needs justifying.** A producer whose build fails on `GOBD2004` deserves to read why
  a re-encoded DTD counts against them: the standard requires the shipped copy to be
  byte-identical.
- **Documentation rots silently.** A reference that is right today and wrong in three months is
  worse than none, because it is trusted.

## What Changes

- **`docs/finding-codes.md`**: a reference covering every code — what it means, its severity and
  why that severity, what triggers it, and what a producer should change. Organised by the four
  code ranges, which already correspond to the four kinds of problem.
- **A test enforces two-way coverage**: every catalogue code appears in the document, and every
  documented code exists in the catalogue. Adding a code without documenting it fails the build,
  as does documenting one that was removed or renamed.
- **The test also pins severity and category** per entry, so the document cannot quietly disagree
  with the catalogue about whether a code is an error or a warning.
- **The README links to it**, so the reference is reachable from the project's front page.

### Non-goals

- **No generator, and no prose in the library.** The document is written by hand. Moving 50
  producer-facing explanations into `FindingCodeInfo` would put a large body of English prose in
  C# and immediately raise whether it needs German, since everything user-facing in this tool is
  bilingual. Developer documentation does not carry that obligation.
- **English only.** The reports are bilingual because auditors and producers read them; this is
  reference material for whoever integrates the tool.
- **No change to any code's meaning, severity or message.** This change documents the catalogue;
  it does not revise it. If writing the entries exposes a code that is wrong, that is a finding
  to raise, not to fix here.
- **No new CLI surface.** A `--list-codes` command is a plausible future convenience and is not
  part of this.

## Capabilities

### New Capabilities

None. Reference documentation about existing behaviour is not itself a behaviour of the system.

### Modified Capabilities

- `validation-reporting`: adds a requirement that every code the system can emit is documented,
  and that the documentation cannot drift from the catalogue. The finding codes are already
  specified there as the stable machine interface; this makes their documentation part of that
  contract rather than an optional extra.

## Impact

- **Documentation**: a new `docs/finding-codes.md`, and a link from `README.md`. This is the
  first content in `docs/` under its intended purpose as the product's own documentation.
- **Tests**: one new test class reading the document and comparing it against `FindingCodes.All`.
- **Code**: none expected. `FindingCodeInfo` already carries the code, category, severity and an
  invariant one-line summary, which is enough for the test to check against.
- **Ongoing cost**: adding a finding code now also requires a documentation entry. That is the
  point, and it is small — one section per code, written while the reasoning is fresh.
