## Context

See `proposal.md` — Why. This design is deliberately short: the change is one markdown file and
one test. Only the contract between them needs deciding, and it needs deciding because it is the
kind of test that fails open.

Current state:

- `FindingCodes.All` is an `ImmutableArray<FindingCodeInfo>` of 50 entries, each carrying the
  code, its category, its default severity, and an invariant one-line summary.
- The severity in the catalogue is a *default*. Two checks escalate deliberately —
  `TableNameDuplicated` becomes an error when a `ForeignKey` resolves through the ambiguity, and
  `Finding.Create` accepts an explicit severity for that purpose.
- `docs/` holds only `initial-requirements.md`. This is its first content under its intended
  purpose.

## Goals / Non-Goals

**Goals:**

- A reader who has a code and nothing else can find out what to change.
- Adding a code without documenting it fails the build.
- The verification cannot pass by matching nothing.

**Non-Goals:**

- No generator and no prose in the library — settled in the proposal.
- No change to any code's severity, category or message.

## Decisions

### D1 — The document's structure is the parsing contract

The test has to locate each code in prose. That means the format is not cosmetic, and it should
be the simplest thing that is unambiguous: one section per code, whose heading begins with the
code itself.

```
  ### GOBD3010 — Composite key only partially aliased

  **Severity:** warning   **Category:** structure

  <what it means, what triggers it, why this severity, what to change>
```

Codes are extracted by matching headings, not by scanning the whole document, so a code mentioned
in passing inside another entry's prose is not mistaken for an entry of its own. Severity and
category are read from their labelled line, so the test compares stated values against the
catalogue rather than merely checking a word appears somewhere nearby.

### D2 — The test must be unable to pass vacuously

A coverage test that extracts zero codes and compares an empty set against an empty set passes
and proves nothing. This has already bitten twice in this project — a fingerprint assertion that
could have been made self-proving, and an entity-expansion test asserting only "not empty".

So the test asserts, in this order:

```
  1. the document parses to a non-empty set of entries
  2. that set has exactly 50 members            (matching FindingCodes.All.Length)
  3. catalogue minus documented   is empty      (nothing undocumented)
  4. documented minus catalogue   is empty      (nothing stale)
  5. every entry's stated severity and category equal the catalogue's
```

Steps 1 and 2 are the guard on the guard: if the heading format ever changes and the parser
stops matching, the test fails loudly instead of quietly succeeding.

*Severity is stated as the catalogue's default.* Where a check escalates — `GOBD3024` — the entry
says so in prose, because a reader who sees "warning" and then an error in their log deserves the
explanation rather than a contradiction.

### D3 — Order the reference the way the reader arrives at it

By code range, because that is what the reader has in hand:

```
  GOBD1xxx  the export as a package    6 codes
  GOBD2xxx  the DTD and the grammar    8 codes
  GOBD3xxx  what index.xml describes  31 codes
  GOBD9xxx  the validator itself       5 codes
```

Within a range, ascending by code. Numeric order is not thematic order, but it is the order a
reader can navigate without knowing the taxonomy, and the ranges already carry the grouping.

## Risks / Trade-offs

**Entries decay into restating the message** → The specs require an entry to say what triggers
the finding, why the severity, and what to change. The message is one line the reader already
has; the entry earns its place only by adding the other three.

**The parser and the document drift apart** → D2's steps 1 and 2 turn that into a failure rather
than a silent pass.

**Documenting 50 codes surfaces one that is wrong** → Likely, and out of scope by design. It gets
raised, not quietly fixed, so a behaviour change never rides along inside a documentation commit.
