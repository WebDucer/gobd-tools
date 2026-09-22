## Context

See `proposal.md` — Why. Design-relevant state:

- The repository is empty: no commits, no source. Every structural decision here is
  unconstrained by existing code.
- .NET SDK 10.0.300 is installed.
- `src/GoBd.Validation/Resources/gdpdu-01-03-2019.dtd` is the authoritative grammar, obtained from Audicon/Caseware
  (`caseware.com/de/beschreibungsstandard`). This matters: the specification **PDF prints two
  mutually contradictory versions of the grammar**. Its normative listing (Figure 5) declares
  `Media (Name, Command*, Table*, Command*, AcceptNoTables?)` and a mandatory
  `(VariableLength | FixedLength)`, while its own "Description of elements" section declares
  `Table+` and an optional `(VariableLength | FixedLength)?`. The `.dtd` file agrees with
  Figure 5. **The `.dtd` file is the sole authority; the PDF is background reading.**
- The 1.6 grammar is a strict superset of 1.1 — `DataSet` gains `Extension*`, `Media` relaxes
  `Table+` to `Table*` and gains `AcceptNoTables?`, `ForeignKey` gains `Alias*`, everything
  else is byte-identical. Every conformant 1.1 document therefore validates under 1.6.
- `index.xml` carries no standard-version field. `<Version>` is the *data carrier cession*
  version, which the standard states twice is unrelated. The DOCTYPE system identifier is not
  a reliable discriminator either: the 1.6 document's own Examples 1 and 3 declare
  `gdpdu-01-08-2002.dtd` while Example 4 and its FAQ declare `gdpdu-01-03-2019.dtd`.

A throwaway spike (net10.0, osx-arm64) confirmed the load-bearing platform assumptions before
this design was written:

```
  DTD validation via DtdProcessing.Parse + ValidationType.DTD    ok, JIT and NativeAOT
  custom XmlResolver feeding the DTD from a non-filesystem source ok, JIT and NativeAOT
  resolver refuses SYSTEM "file:///etc/passwd"                    ok
  rejects Table with no VariableLength/FixedLength                ok  (confirms Figure 5)
  accepts empty Media + AcceptNoTables                            ok  (confirms Table*)
  codepages 1252 / 850 / 10000 under NativeAOT                    ok, no PackageReference
  System.Text.Json source generation                              ok
  publish -r osx-arm64 /p:PublishAot=true                         0 trim/AOT warnings, 8.0 MB
```

## Goals / Non-Goals

**Goals:**

- A pure, side-effect-free core library that takes an export source and returns findings, with
  no console, filesystem-writing or process-launching behaviour of its own.
- A validation verdict that is a deterministic function of `index.xml`, the canonical DTD, and
  the export's entry listing — never of anything the export itself supplies as executable or
  interpretable input.
- A check catalogue that is a registry, so Tier 1 and Tier 2 checks are added in v2 without
  restructuring.
- A single self-contained native binary per platform.

**Non-Goals (design level, beyond the proposal's scope boundaries):**

- No plugin or external-configuration mechanism for checks in v1. The catalogue is compiled in.
- No parallelism. v1 reads one small XML file and one directory listing; concurrency would add
  risk and no measurable benefit.
- No abstraction over the XML parser. `System.Xml` is the only DTD-validating parser available
  and there is no second implementation to swap in.

## Decisions

### D1 — The canonical DTD is embedded; the export's copy is evidence, never input

Validation always resolves the DOCTYPE to a DTD compiled into the assembly as a resource. The
`XmlResolver` is effectively a constant function: it returns the embedded bytes and throws for
every other URI, regardless of scheme.

*Why:* it makes the verdict deterministic and removes the entire class of attack in which a
crafted DTD inside an untrusted export influences its own validation. It also means the tool
works on an export that omits the DTD entirely — which is itself a reportable defect, rather
than a reason the tool cannot run.

*Alternative rejected:* resolving the DOCTYPE against the export's DTD copy (what the spike
originally did). It works, but it lets the artifact under test define the standard it is tested
against, and it fails closed on exports that omit the DTD.

The export's DTD copy is then a separate, purely comparative check. Its fingerprint:

```
  gdpdu-01-03-2019.dtd
  sha256  691051c9828ec2bbef71527c4aa77554c09ecd49e862fc6f2bc4b14507c72a0e
  size    10,646 bytes    ASCII, CRLF (317 lines), no BOM
  content element declarations only - no ATTLIST, no ENTITY
```

That the canonical file declares no entities and no attributes bounds the parser's exposure to
whatever internal subset an `index.xml` declares for itself, which is why entity expansion is
capped rather than merely trusted.

*Comparison is graded, not binary.* Byte-identical passes; identical-after-normalising line
endings, BOM and trailing whitespace is a **warning**; anything else is an **error**. A strict
byte comparison alone would fail an otherwise perfect export that a `.gitattributes` rule or a
Linux unzip/rezip cycle had converted to LF — a false accusation of tampering, aimed at exactly
the audience this tool serves.

### D2 — One export-source abstraction, shaped now for v2's streaming needs

`IExportSource` exposes: enumerate entries, test existence, resolve a relative URL to an entry,
and open an entry as a forward-only stream. v1 calls the first three and opens exactly two
entries — `index.xml` and the DTD. ZIP and folder implementations both satisfy it.

*Why include stream-opening in v1 when nothing calls it?* Because the v2 Tier 1 checks are a
forward-only pass per table, which works **inside** a ZIP without extraction — a DEFLATE entry
is forward-readable, and forward-only is all a streaming validator needs. Committing to the
shape now means v2 adds checks rather than re-plumbing access. Extraction to a temporary folder
becomes necessary only for random access, which arrives with the viewer, not with Tier 1.

*Path handling:* separators are normalised, comparison is ordinal and case-sensitive, and every
resolved path is verified to remain under the export root before use. Case-sensitivity is
deliberate — an export that only works on Windows is a defect, since the auditor's environment
is not the producer's.

### D3 — Three phases with a hard gate after parsing

```
   +----------------+     +------------------+     +--------------------+     +-----------+
   | ExportSource   | --> | Parse + DTD      | --> | Semantic checks    | --> | Reporters |
   | zip | folder   |     | validate         |     | over the model     |     | text|json |
   +----------------+     +------------------+     +--------------------+     +-----------+
                                   |                          |                     |
                             findings                    findings              exit code
                                   |
                          [ well-formedness failed ]
                                   |
                                   +--> stop: a document that will not parse
                                        has no model to check
```

Well-formedness failure stops the run — there is no tree to reason about. A *grammar* failure
does not: the document parsed, so the model exists and the semantic catalogue still runs and
still yields useful findings. This distinction is why the parse phase reports DTD violations
through the same finding channel rather than by throwing.

*Model shape:* an immutable object graph mirroring the DTD, each node carrying its source line
and column, captured during parse. Locations cannot be recovered afterwards, so they are
recorded as the tree is built rather than looked up later.

### D4 — Checks are a registry of small, independent units

Each check is a self-contained unit with a stable code, a severity, and a function from the
model to findings. A registry enumerates them; the engine runs all of them and concatenates.

*Why:* the catalogue is long and will roughly triple in v2. Independent units keep each check
individually testable against a small fixture, let `--strict` and future suppression operate
uniformly by code, and make "add a Tier 1 check" a matter of registering one more unit.

*Codes* are namespaced by area and stable for the life of the tool: `GOBD1xxx` source and
packaging, `GOBD2xxx` DTD and grammar, `GOBD3xxx` semantic/structural, `GOBD9xxx` tool failure.
A retired code is never reused for a different check, because downstream pipelines suppress by
code.

*Severity means conformance, not confidence:* `error` for a violation of the standard,
`warning` for permitted-but-suspect, `info` for a neutral observation. Detector certainty never
enters into it — a check that is unsure emits nothing.

### D5 — NativeAOT, with the constraints it imposes taken up front

The spike showed the whole path publishes AOT-clean, so AOT is the plan of record rather than a
stretch goal; self-contained trimmed publish remains available as a fallback if a platform
misbehaves.

Consequences accepted now rather than retrofitted:

- All JSON serialisation goes through a `JsonSerializerContext`. No reflection-based
  serialisation anywhere, in the library or the CLI.
- `System.Text.Encoding.CodePages` needs no `PackageReference` on .NET 10 — verified from a
  clean `obj/` and `bin/`. `Encoding.RegisterProvider` is still called. (NuGet emits `NU1510`
  advising the package be dropped.)
- **All value handling passes an explicit format provider and explicit symbols.** The
  standard's numeric and date semantics come from the `DecimalSymbol`, `DigitGroupingSymbol`,
  `Format` and `Epoch` declarations in `index.xml`; ambient culture is never consulted, and
  string comparison is always ordinal, which is what XML case-sensitivity requires anyway.
  This is a property of the code and holds whatever the build flags say — see D8.
- `InvariantGlobalization=true` as a *deployment* choice, not a correctness mechanism (D8).

### D6 — Solution layout

```
  src/GoBd.Validation/         core library - model, parse, checks, findings.  No I/O policy.
  src/GoBd.Validation.Cli/     CLI - argument parsing, reporters, exit codes
  tests/GoBd.Validation.Tests/ unit tests + fixture exports
  src/GoBd.Validation/Resources/  canonical grammar, embedded as a resource by the library
```

The reporters live in the CLI, not the library: report *content* is specified behaviour, but
report *rendering* is a presentation concern, and keeping it out of the library is what leaves
the door open for the deferred UI to consume findings directly.

*Test stack:* xUnit v3 on the Microsoft Testing Platform, with Shouldly for assertions and
`Microsoft.Testing.Extensions.CodeCoverage` for coverage. Run it with `dotnet test`.

One version constraint is load-bearing and does not announce itself:
`Microsoft.Testing.Extensions.CodeCoverage` must be **18.11.0 or later**. The 18.0.x builds
target MTP v1 and throw `TypeLoadException` at start-up under the MTP v2 that xunit.v3 4.x pulls
in — `OnTestSessionStartingAsync … does not have an implementation`.

`UseMicrosoftTestingPlatformRunner` is the property xunit.v3 documents, and it also makes the
built executable an MTP application when run directly. The MSTest/NUnit spelling
`EnableMicrosoftTestingPlatformRunner` leaves the standalone executable hosting xUnit's own
console runner; `dotnet test` drives either correctly, so the difference only shows when the
binary is executed by hand.

Note for anyone driving the suite from a script: in MTP mode `dotnet test` forwards unknown
arguments to the test module, so VSTest-era flags such as `--nologo` or `-v q` make the run
report "Zero tests ran" with exit code 5. Pass MTP arguments after `--`, or omit them.

### D7 — Exit codes separate "non-conformant" from "the tool broke"

`0` conformant · `1` warnings only · `2` errors present · `3` tool failure. `--strict` maps
warnings onto `2`.

*Why a distinct code for tool failure:* a pipeline must be able to tell "your export is bad"
from "the validator could not run", because those need different humans. Collapsing them into a
single non-zero code is the most common way a validation gate becomes untrustworthy.

### D8 — Culture never enters value handling; localisation is a separate concern

Two things that look related are kept strictly apart.

**Correctness comes from the code.** Every number and date the tool interprets is governed by
the symbols `index.xml` declares — `DecimalSymbol`, `DigitGroupingSymbol`, `Format`, `Epoch` —
and by nothing else. Every value the tool renders uses an explicit invariant format provider.
Ambient culture is never consulted in either direction. A build flag cannot deliver this: a
flag only hides culture-dependent calls, it does not remove them, and the moment the flag
changes they wake up.

So the guarantee is enforced where it can actually be enforced — at compile time. The
culture-implicit analyzer rules are escalated to build errors across the solution:

```
  CA1304  Specify CultureInfo
  CA1305  Specify IFormatProvider
  CA1307  Specify StringComparison for clarity
  CA1310  Specify StringComparison for correctness
```

A bare `value.ToString()`, `decimal.Parse(text)` or `a.StartsWith(b)` therefore fails the
build. That is the real safeguard, and it is independent of how the binary is configured.

**Localisation is then just message text.** Reports are English by default and German when the
operating system is German or when the caller asks, since GoBD is a German regime. What is
translated is prose; verdicts, counts, codes, severities, JSON field names and every value
quoted out of `index.xml` are identical in both languages.

**Invariant globalization is requested through the runtime host option, not the MSBuild
property.** This is not a style preference — it is forced, and the reason is easy to lose:

```
  <InvariantGlobalization>true</InvariantGlobalization>
        -> emits build_property.InvariantGlobalization = true
        -> the .NET analyzers READ that property and SELF-SUPPRESS
           CA1304, CA1305, CA1307, CA1310
        -> the correctness guarantee above silently evaporates

  <RuntimeHostConfigurationOption Include="System.Globalization.Invariant" Value="true" />
        -> identical invariant runtime, JIT and NativeAOT, identical binary size
        -> analyzers stay enabled
```

Measured on this repository: with the MSBuild property set, a file containing
`string.Format("{0}", v)`, `s.ToLower()` and `s.StartsWith("x")` compiles clean. With the
runtime option instead, the same file fails the build with CA1305, CA1304 and CA1310. Runtime
behaviour is identical either way — `System.Globalization.Invariant` is `True` and
`GetCultureInfo("de-DE")` throws under both, in a JIT run and in a published NativeAOT binary,
at the same 1.1 MB.

So the two halves of this decision are only compatible in one configuration. Anyone
"simplifying" the item back to the property will turn off the analyzers without any build
output saying so.

**The deployment rationale.** Invariant mode buys no correctness once the analyzers are in
place — it buys a dependency-free binary. A non-invariant .NET process on Linux loads
ICU from the system and, per Microsoft's documentation, *terminates* at startup with
`Failed to load system ICU` when it is absent; Alpine lists `icu-libs` and `icu-data-full` as
required packages unless the app runs in invariant mode. Minimal CI images routinely lack them.
For a binary whose whole pitch is "drop it into your pipeline and run it", a hard startup
failure on a distroless container is a worse outcome than writing a little language-detection
code.

The measured cost of the alternative is otherwise negligible, which is worth recording so the
trade-off is not re-litigated on size grounds:

```
  InvariantGlobalization   binary    CurrentUICulture under LANG=de_DE   GetCultureInfo("de-DE")
  ----------------------------------------------------------------------------------------------
  true                     1.1 MB    ''  (invariant)                     throws CultureNotFound
  false                    1.2 MB    'de-DE'                             'de-DE'
```

*Consequence:* `CultureInfo` cannot report the OS language, so language is resolved explicitly.
Resolution order, first match wins:

```
  1. explicit CLI language option
  2. GOBD_LANG environment variable
  3. OS language:  LC_ALL / LC_MESSAGES / LANG   (Unix)
                   GetUserDefaultUILanguage      (Windows, an NLS call needing no ICU)
  4. English
```

Only the leading subtag is inspected, and only well enough to answer "German or not" — this is
a two-language switch, not a culture system.

*Message catalogue is compiled in, not satellite assemblies.* Satellite assemblies are separate
files on disk, which defeats the single-binary goal and does not fit NativeAOT cleanly. The
catalogue is plain compiled-in data keyed by finding code.

*Finding codes stay language-neutral.* The code is the machine interface; the message is the
human one, so a CI pipeline parsing the JSON report behaves identically either way.

### D9 — Distribution: GitHub releases for v1

v1 ships as per-RID NativeAOT binaries attached to GitHub releases. Package managers (Homebrew,
WinGet) are deferred: they add release-process machinery that is only worth it once there is a
UI and a broader audience, and nothing about the v1 layout blocks adding them later.

*Alternative rejected for v1:* publishing as a `dotnet tool`. It is the easiest channel, but it
reintroduces the .NET runtime dependency that NativeAOT exists to remove — the opposite of what
a build-pipeline binary wants.

## Risks / Trade-offs

**A clean v1 result will be read as "my export is valid"** → It only means the *description* is
valid; the data files were never opened. Mitigated in the product, not the documentation: the
text report states the boundary on every clean run, and `--help` states it too. This is the
single most likely way v1 misleads its user, which is why it is a specified requirement rather
than a note.

**The specification PDF contradicts itself on the grammar** → Resolved by treating the `.dtd`
file as sole authority (D1, Context). The risk is a future contributor reaching for the PDF;
mitigated by recording the discrepancy here and by a test asserting both disputed constructs —
`Table` without a content model must fail, empty `Media` with `AcceptNoTables` must pass.

**Byte-comparison of the shipped DTD is brittle** → Graded comparison (D1). Residual risk: an
export whose DTD was re-encoded to UTF-16 reports as altered. That is arguably correct.

**AOT verified on osx-arm64 only** → CI publishes and runs the smoke test on linux-x64,
win-x64 and osx-arm64. Nothing in the path is platform-specific, but "should work" is not
evidence.

**Real-world exports deviate constantly; an over-strict tool gets ignored** → The three-tier
severity model exists for this. Anything the standard permits is at most a warning, and
`--strict` is opt-in. If early use shows a check crying wolf, the fix is its severity, not its
removal.

**A culture-dependent call slips into value handling** → Prevented at compile time, not by
configuration: CA1304/CA1305/CA1307/CA1310 are build errors (D8), so an implicit `ToString()`
or `Parse()` cannot reach the repository. The subtle failure mode is that these rules are off by
default *and* self-suppress under the `InvariantGlobalization` MSBuild property — so both the
explicit severities in `.editorconfig` and the runtime-option form of invariant mode are
load-bearing, and neither announces itself when removed. Belt and braces, a test asserts report rendering is
byte-identical under `LANG=de_DE.UTF-8` and `LANG=en_US.UTF-8` for the same findings — and
because correctness no longer rests on `InvariantGlobalization`, that test stays meaningful
even if the flag is ever turned off for other reasons.

**Windows OS-language detection is the one path not covered by the spike** → The Unix side is
measured; `GetUserDefaultUILanguage` is verified on the `win-x64` CI leg, with fallback to
English if the call is unavailable, so a detection failure degrades to the default rather than
crashing.

**Case-sensitive entry matching may produce findings on exports that work today on Windows** →
Intentional, and reported with a dedicated diagnostic that names the case-insensitive match so
the producer sees immediately what the actual difference is.

**Composite foreign keys with partial aliasing are genuinely ambiguous** → The standard does not
say whether unaliased columns of a partially aliased key fall back to positional matching.
Reported as a warning rather than resolved by guessing; the specs record this as
producer-facing ambiguity rather than a defect the tool decides on.

## Migration Plan

Greenfield — nothing to migrate. Deployment is a per-RID NativeAOT publish attached to a
release, plus the smoke test run on each target platform in CI. Rollback is using the previous
binary; the tool holds no state and mutates nothing it examines.

## Open Questions

None outstanding. The two questions this design originally carried — message language and
distribution channel — were resolved by the user and are recorded as D8 and D9.
