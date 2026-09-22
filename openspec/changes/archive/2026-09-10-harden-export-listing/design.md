## Context

See `proposal.md` — Why. The implementation was done before this change was written; this records
the decisions it embodies so the next reader does not have to infer them.

State that made the defect possible:

- `ExportPath.Normalise` maps `\` to `/` so that ZIP and folder listings agree. That is right for
  *comparison*, and was quietly wrong for *reconstruction*: `OpenRead` turned the normalised name
  back into a path.
- On Linux and macOS a backslash is an ordinary filename character, so a file inside the export
  could carry a name that normalises into one pointing outside it.
- `DtdCopyLocator` selects any entry whose name ends `.dtd` and hashes it, so reaching an
  arbitrary file needed no cooperation from `index.xml` at all — the declared URLs were never the
  vector, and `ExportPath.Resolve` had always guarded those correctly.

## Goals / Non-Goals

**Goals:**

- Make the listing a boundary that is specified, not one that holds by accident.
- Report an ambiguous medium instead of resolving it silently.
- Make what a finding is about survive a rewording of its message.

**Non-Goals:**

- No change to the JSON contract. See D3.
- No extraction, and no new option, format or exit code.
- Not a general sandbox: the operator's own `--output` path stays trusted, as it always was.

## Decisions

### D1 — Exclude at the listing, and confine at the open

Two barriers, because they fail differently:

```
  listing   excludes escaping names and symlinks   -> nothing downstream can even name the file
  open      confines to the export root            -> a name that reaches the index anyway is refused
```

Excluding at the listing is the one that matters, since `DtdCopyLocator`, the presence checks and
v2's streaming all trust the listing. Confining the open is cheap and covers whatever future path
puts a name into the index.

*Why symlinks are excluded rather than followed:* a link denotes a file the medium does not
carry. Following one is how a link named `gdpdu-01-03-2019.dtd` reads and publishes the hash of
anything the operator can open, and it needs no filename trickery on any platform.

*A linked directory is the case that actually bites,* and checking each file for a link misses
it entirely: the files beneath such a directory are not links themselves, so they arrive under
ordinary in-root names and pass every other test. Verifying this change found the folder walk
still descending into one and publishing an out-of-root file's SHA-256. The walk therefore skips
`FileAttributes.ReparsePoint`, which stops it at the linked directory; nothing else about the
enumeration changes, so which ordinary files are listed is unaffected.

*An open reads the path the walk arrived at,* not one rebuilt from the entry name. Rebuilding is
what made the traversal possible in the first place, and it also broke the guarantee D2 relies
on: with `data/x.csv` beside a file literally named `data\x.csv`, the lookup returned one and
the rebuilt path opened the other, so the length a check saw and the bytes it hashed came from
two different files.

*Why an excluded entry is silently absent* rather than reported: the resulting finding is already
truthful — the medium does not contain a usable file at that name, so `GOBD2003` or `GOBD1003`
says so. Adding a code for "an entry was ignored" would describe the validator's behaviour rather
than the export's defect.

### D2 — A collision is reported, never resolved and never fatal

Three behaviours were possible when two entries resolve to one name, and each had been tried:

| | Outcome |
| --- | --- |
| Throw | `ArgumentException` escaped `ExportSourceFactory`, crashing before any finding |
| Pick one silently | Two importers reach two conclusions from one medium; nothing says so |
| Pick one **and report** | Deterministic, and the ambiguity reaches the producer |

The third. First-wins is chosen for the pick so that lookup and open always describe the same
entry — otherwise the DTD's reported fingerprint could belong to a different member than the one
the listing selected, which is precisely the spoofing shape the report exists to expose.

`GOBD1007` is an **error**, not a warning. This is not "permitted but suspect": the medium cannot
be interpreted unambiguously, and duplicate ZIP members are a documented way to make one tool
read different bytes than another.

### D3 — A finding states its subject; the JSON contract does not move

The subject was recovered by indexing into positional arguments, with the index declared per
code. That coupled a published field to argument order:

```
  before   scope = Arguments[ScopeArgument]     reorder the args, the field changes, nothing notices
  after    scope = Scope.Name                   stated by the check that knows
```

`FindingScope` carries a kind as well as a name, because the flat name alone cannot tell a medium
called `Kunden` from a table called `Kunden`.

*The kind is deliberately not exposed in JSON.* Adding a field would move `schemaVersion` and
break consumers for a distinction none of them has asked for; every existing `scope` value is
byte-identical to before. The kind is there for the deferred UI, and can surface later with a
version bump if a consumer needs it.

*The trade this makes:* the old mechanism was silent-and-wrong, the new one is
explicit-and-forgettable — a check that omits `About` loses its grouping. That is why the guard in
D4 exists.

### D4 — The guard runs the real checks

A test that only asserted `FindingScope.Table("x").Name == "x"` would prove nothing about whether
checks actually attribute their findings. The guard runs the whole validator over the fixture
exports and fails if any finding reaches a report without a subject, excepting a short list of
codes that genuinely concern the export as a whole. It carries its own guard — an assertion that
the fixtures produce findings at all — so it cannot pass by examining nothing.

## Risks / Trade-offs

**A legitimate export using symlinks is silently reduced** → Possible in principle; a data carrier
is meant to carry files, and the resulting "no DTD" or "file missing" finding is accurate about
what the medium delivers. If real exports are found to rely on links, the honest fix is a finding
that says the link was ignored, not following it.

**First-wins is a choice, and an attacker knows which** → Determinism is the property that
matters here, and it is paired with a report: the collision is always visible, so the reader is
never relying on the pick being the "right" one.

**A future check forgets to call `About`** → Caught by D4's guard, which runs the real registry.
It cannot catch a check that attributes the *wrong* subject; that remains a review matter.

**Excluding entries changes what an export reports** → Deliberate. An input that previously
crashed now reports; an out-of-root file that was previously readable is now invisible. Both are
the intended change, and no in-root behaviour moved: every baseline report is byte-identical.
