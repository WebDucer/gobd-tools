## Context

See proposal.md for why. The state this design builds on:

- **No version is set anywhere.**
  - No project, props or targets file sets `Version`, so the SDK's default `1.0.0` applies.
  - `CliRunner.Version` prints `Assembly.GetName().Version`, which is `1.0.0.0` in every release.
  - `release.yml` derives the macOS bundle version in the shell, as `${GITHUB_REF_NAME#v}`.
    `assemble-reader-app.sh` accepts one to three numeric parts.
  - CI builds the bundle with `0.0.0`.
- **What the builds ship.** Measured from `project.assets.json` and a local `osx-arm64` publish:
  - **Both tools** carry the .NET runtime (MIT). The CLI compiles it in with NativeAOT; the reader
    carries it self-contained. The runtime pack ships its own `LICENSE.TXT` and
    `THIRD-PARTY-NOTICES.TXT` (77 KB); the ILCompiler package ships the same file.
  - **The reader** also carries:
    - Avalonia, MicroCom.Runtime and Tmds.DBus.Protocol (MIT)
    - SkiaSharp (MIT) with Skia (BSD-3-Clause)
    - HarfBuzzSharp (MIT) with HarfBuzz (Old MIT)
    - ANGLE (BSD-3-Clause), on Windows
    - DuckDB.NET (MIT) with DuckDB (MIT)
    - Apache.Arrow and Apache.Arrow.Scalars (Apache-2.0), which DuckDB.NET brings
  - **Build-only packages** reach no build: `Microsoft.NET.ILLink.Tasks`, `Avalonia.BuildServices`,
    `Microsoft.DotNet.ILCompiler`.
  - The CLI has no package it ships. A build guard (`ForbidNativeStoreDependency`) keeps the store
    out of it.
- **Resources already survive trimming and NativeAOT.** `GoBd.Validation` embeds the canonical DTD,
  and the NativeAOT CLI reads it with `GetManifestResourceStream`.
- **The CLI surface.** `CommandLine` parses by hand. `CliRunner` answers `--version` before
  `--help` and before validating. `ReadmeUsageTests` holds the README's usage block to the help
  line by line, and lists the options it expects to find.
- **The reader's menu** is one `NativeMenu` with File, shown in the system menu bar on macOS and in
  the window elsewhere (`MainWindow.ConfigureMenu`).
- **The store's root** is `StoreOptions.DefaultRoot`, `Path.GetTempPath()/gobd-reader`.
  - `Path.GetTempPath()` honours `TMPDIR` on Linux and macOS. It is `/tmp/` on Linux by default,
    a per-user directory on macOS, and `%TEMP%` on Windows.
  - `StoreLock.TryTake` creates each store directory with default permissions.
  - `ExportStore.Open` sweeps abandoned stores under the root, then checks space, then creates the
    store.
  - `StoreRefusal` models only a shortfall of space, and `MainWindow.Describe` words it.
- **Permissions.** `release.yml` sets `contents: write` for the whole workflow. Only
  `softprops/action-gh-release`, in the `release` job, writes to the repository.
- **What a release carries.**
  - Twenty files: ten downloads and a `.sha256` for each.
  - No bill of materials.
  - The `release` job downloads every artifact named `release-*` and attaches all of them.
- **Attestations.**
  - `actions/attest@v4` creates SLSA build provenance by default, and an SBOM attestation when
    given `sbom-path` (SPDX or CycloneDX JSON).
  - `actions/attest-build-provenance` and `actions/attest-sbom` now only call it.
  - It needs `id-token`, `attestations` and `artifact-metadata` write permissions.
  - GitHub offers attestations for public repositories on every plan; a private one needs
    Enterprise Cloud.
- **Fixtures.** Three fixture exports name `Glaswerk AG` of `Singen` and describe `Kunden` as
  `Kunden-Stammdaten`, as the Beschreibungsstandard's first example does.
  - The fixtures are `binary` in `.gitattributes`.
  - Only their DTD copies are compared byte for byte.
- **Mentions of the private repository.**
  - **The number of the issue that reported a crash** is cited in `ExportStore.cs`,
    `ReaderSession.cs`, `ExportStoreTests.cs` and `ReaderSessionTests.cs` (including a helper
    named after it). It is also cited in the archived `add-export-reader/tasks.md` and
    `add-table-query/design.md`.
  - **Commit hashes, CI run numbers and a pull request number** are cited in the archived
    `harden-export-listing`, `repackage-reader` and `add-reader-icon`.

## Goals / Non-Goals

**Goals:**

- **One rule decides the version,** in one place, for both tools, the pipeline and a developer's
  machine. It can be checked without building a tool.
- **The notices exist once.** Each tool shows them from inside itself, and a build fails to pass
  its tests when a shipped package is not named in them.
- **The store's privacy is checked, not assumed:** on every open, before anything is written.
- **A machine can check a release:** what each tool contains, and that each download is what the
  release workflow built. The bills of materials and the notices come from the same list of
  shipped packages, and the build fails when they part.

**Non-Goals:**

- **No versioning from git history** (commit counts, pre-release suffixes). The tag is the whole
  input.
- **No complete audit of what each native library bundles.** The notices carry what each upstream
  project publishes for its binaries. The completeness test covers packages, not the contents of
  a native library.
- **No move of the store** out of the temporary location, for instance to `~/.cache`. Its location
  and its deletion on close stay as the README describes; only its privacy changes.
- **No claim of conformance to BSI TR-03183-2.** The bills of materials use a format that
  guideline accepts (CycloneDX 1.6), but the guideline asks for more fields than this design
  sets out to fill.
- **No signing key of the project's own.** The attestations are signed through GitHub's OIDC
  identity for the workflow (Sigstore). There is no key to keep or rotate.

## Decisions

### D1 — The version is computed by a build task from a `ReleaseTag` property

**The rule.**

- `Directory.Build.props` sets `Version` to `0.0.0` for every project.
- `Directory.Build.targets` defines a target, `ComputeReleaseVersion`, that runs before the SDK's
  `GetAssemblyVersion`.
  - When `ReleaseTag` is not empty, it hands the tag to a small inline C# task
    (`RoslynCodeTaskFactory`).
  - The task accepts `^v(\d+)(\.(\d+))?(\.(\d+))?$`, fills the omitted parts with `0`, and
    requires each part to be at most 65534, the largest a part of an assembly version may be.
  - It sets `Version` to `<major>.<minor>.<patch>`.
  - The SDK then derives `AssemblyVersion` and `FileVersion` as `<major>.<minor>.<patch>.0`, which
    is what both tools report.
- A tag the task cannot read fails the build with `GOBDBUILD002`. The message names the tag, the
  form a release tag takes, and the largest part. This follows the numbering of `GOBDBUILD001`.

**How the workflows use it.**

- `release.yml` passes `-p:ReleaseTag=${{ github.ref_type == 'tag' && github.ref_name || '' }}` to
  every `dotnet publish`.
  - A dispatch from a branch builds `0.0.0.0`.
  - A dispatch from a tag builds that tag's version.
- `ci.yml` never runs for a tag, so it passes nothing.
- The macOS bundle version is asked of the build instead of cut from the tag in the shell:
  `dotnet msbuild src/GoBd.Reader.Ui -t:ComputeReleaseVersion -getProperty:Version -p:ReleaseTag=…`.
  It gives `1.0.0` for `v1`, where the shell gave `1`.

**Why a task, not property functions or the shell.**

- Property functions can pad the parts, but the bound of 65534 would need a regular expression
  that nobody could check by eye.
- Doing it in the shell would repeat the rule in two workflows and leave a developer's build
  reporting `1.0.0.0`.
- A task is ten lines of ordinary C#. It runs in every build, and the same target answers the
  workflow's question.

**Alternatives.**

- **MinVer or Nerdbank.GitVersioning.** Rejected: a new build dependency, and both derive
  pre-release versions from commit history for untagged commits. The rule here is that an
  untagged build is `0.0.0.0`.
- **Reading `GITHUB_REF_NAME` inside MSBuild.** Rejected: an implicit coupling to one CI, and a
  developer could not ask "what would `v1.2` build?" without faking the environment.

**Checked by** `.github/scripts/verify-release-version.sh`, run in CI's test job. For `v1`,
`v1.2`, `v0.3.7` and no tag, it asserts the version the target reports. For `v1.2-rc1`,
`v1.2.3.4` and `v70000`, it asserts that the target fails with `GOBDBUILD002`.

### D2 — `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES.txt` sit at the root and are embedded once

- **The files.** All three live at the repository root, where a person and GitHub look for them.
- **Embedding.** `GoBd.Validation.csproj` embeds them under fixed logical names (`GoBd.LICENSE`,
  `GoBd.NOTICE`, `GoBd.THIRD-PARTY-NOTICES.txt`). One class in the library, beside
  `CanonicalDtd`, returns each of the three on its own:
  - the licence, which is `LICENSE` and nothing else
  - what it does not cover, which is `NOTICE`
  - the third-party notices
- **They stay apart, as the files do.** `LICENSE` carries nothing but the MIT text, because that is
  what GitHub reads it by, and a tool that printed the two together would say something the file
  does not. The copyright line the reader shows is taken from `LICENSE` itself.
- **Why the library.** Both tools reference it, it already carries the other thing both tools must
  carry (the DTD), and reading a resource in NativeAOT is already proven there.
- **Alternative:** embed in each tool project, through an opt-in property in
  `Directory.Build.targets` in the manner of `ForbidsNativeStore`. Rejected: two copies of the
  reading code, for no gain. The notices file already says which tool ships which component.

### D3 — `THIRD-PARTY-NOTICES.txt`: what it holds, and how it stays complete

**Plain text.** It is printed verbatim by `--notices` and shown verbatim by the reader. Licence
texts contain characters Markdown would reinterpret.

**The layout:**

```
<what this file is, and that each section says which tool ships it>

== In gobd-validate and gobd-reader ==
.NET runtime
  Packages: (none: part of the SDK's runtime pack)
  Source: runtime pack <version>, LICENSE.TXT and THIRD-PARTY-NOTICES.TXT
  <both texts, verbatim>

== In gobd-reader only ==
Avalonia                    Packages: Avalonia, Avalonia.Desktop, ..., MicroCom.Runtime, Tmds.DBus.Protocol
SkiaSharp and Skia          Packages: SkiaSharp, SkiaSharp.NativeAssets.*
HarfBuzzSharp and HarfBuzz  Packages: HarfBuzzSharp, HarfBuzzSharp.NativeAssets.*
ANGLE (Windows builds)      Packages: Avalonia.Angle.Windows.Natives
DuckDB.NET and DuckDB       Packages: DuckDB.NET.Data.Full, DuckDB.NET.Bindings.Full
Apache Arrow                Packages: Apache.Arrow, Apache.Arrow.Scalars
  (each with its Source line and its licence text, verbatim)
```

**What goes into each entry:**

- **Its licence text,** as its package ships it (`LICENSE.txt`, `LICENSE-DuckDB.txt`, `LICENSE`).
- **The upstream project's third-party notices,** where that project publishes them for the
  binaries it ships. That means SkiaSharp and HarfBuzzSharp for their native libraries, and DuckDB
  for the code it vendors.
- **Apache Arrow's `NOTICE`,** with the full Apache-2.0 text. That licence asks for both.

**No versions.** The licences do not require them, and they would change the file on every
upgrade.

**Completeness is a test.** It lives in `GoBd.Validation.Tests/Documentation`, beside
`ReadmeUsageTests`:

- For each tool project, it reads `obj/project.assets.json`. It collects every package whose
  target entry carries runtime, native or `runtimeTargets` assets, which leaves out build-only
  packages.
- It collects every id named on a `Packages:` line.
- It fails when a shipped package is not named, and also when a named package is not shipped, so
  the file cannot keep listing what a build no longer carries.

**Why a test rather than an MSBuild target** like `ForbidNativeStoreDependency`: comparing a text
file with a JSON document is readable C# and unreadable MSBuild, and the test job runs on every
commit anyway.

### D4 — `gobd-validate --license` and `--notices`

- **Three options, for three texts:** `--license` prints `LICENSE`, `--notice` what that licence
  does not cover, and `--third-party-notices` the notices of the components included. The long
  spelling of the last is what keeps `--notice` and it apart at a glance.
- **Parsing.** `CommandLine` gains `ShowLicense`, `ShowNotice` and `ShowNotices`.
- **Precedence.** `CliRunner` answers in the order `--version`, `--license`, `--notice`,
  `--third-party-notices`, `--help`. The first one given wins, and nothing is validated, which
  matches how `--version` behaves today. All of them exit `0`.
- **Output.** Standard output, verbatim, one text per option.
- **Spelling.** The options are spelled `--license`, as command-line options conventionally are;
  the README's prose keeps "Licence".
- **Help and README.** The help gains the three options after `--version`. The README's usage block
  gains the same lines, and `ReadmeUsageTests` adds all three to the options it expects.

### D5 — The reader's Help menu and its text window

**The menu.** `ConfigureMenu` adds a Help menu after File:

- **Licence**, which is the licence and nothing else
- **Notice**, what that licence does not cover
- **Third-party notices**
- **About GoBD Reader**, on every platform but macOS

**About sits where each platform puts it.** On macOS that is the menu named after the application,
set with `NativeMenu.SetMenu(Application.Current, …)`; a window's menu cannot reach it, and without
one macOS leaves the toolkit's own "About Avalonia" there. Everywhere else it is the last entry of
Help.

**About is a window of its own,** not a paragraph of text: the reader's icon at 96 px, its name,
the version — selectable, because a person opens this in order to quote it to someone — one line on
what the reader does, the licence and the copyright line taken from `LICENSE` itself, a link to the
project page, and a button to each of the three texts. Escape closes it.

**On macOS** the Help menu goes into the system menu bar like File, and macOS adds its search
field to a menu named Help, which is harmless.

**The window.** Each entry opens a `TextWindow`:

- It is owned by the main window, and not modal, so that reading an export in the background and
  using the main window carry on.
- Choosing the entry again brings the open window to the front instead of opening a second.
- It sets the text in the monospaced face the summary uses for codes, and wraps long lines.
- **A short text is one block**, selectable across its whole length, in a scroll viewer. The
  licence and About are short.
- **A long text is a list of its lines**, which the list virtualises. The notices run to thousands
  of lines, and as one block every pass laid out all of them: the window dragged when it was
  scrolled. Lines are selected rather than characters there, and copying takes the selected lines,
  or the whole text when none is selected.

**Testing.** The entries are wired through `Command` rather than `Click`, so that the headless
tests in `GoBd.Reader.Ui.Tests` can invoke them from `NativeMenu.GetMenu(window)` and read what
the window shows.

### D6 — The store's root is private, and checked on every open

**The name.**

- **Linux and macOS:** `StoreOptions.DefaultRoot` becomes
  `Path.GetTempPath()/gobd-reader-<user name>`, with the user name from `Environment.UserName`.
  - The name is readable in a message, and unique on a machine.
  - A new name also keeps clear of the `/tmp/gobd-reader` that earlier versions made, which is
    likely to exist and be open to everyone.
- **Windows** keeps `gobd-reader`.

**The check.** A new step, first in `ExportStore.Open`, before the sweep, the space check or any
write. On Linux and macOS:

1. Create the root with `Directory.CreateDirectory(root, UserRead | UserWrite | UserExecute)`.
   The mode applies only if the directory is created, which is why the next steps follow.
2. Refuse if the root is a symbolic link (`LinkTarget` is not null).
3. Refuse if `File.GetUnixFileMode(root)` carries any group or other bit.
4. Create and delete a uniquely named file in it. Refuse if that fails, because the directory
   belongs to another account.

The check applies to whichever root the options name, including the ones tests pass.

**Why refuse rather than repair.** A directory that is too open may be someone else's, and then
its mode is not ours to change. Or it may be open by intent. And what was open before may already
have been read. Saying so and naming the way out is the honest answer.

**Why no owner check.** .NET has no managed call for a file's owner on Unix. The write probe
covers the case that matters: another account's private directory cannot be written to, and
another account's open one fails step 3.

**The refusal.**

- `StoreRefusal` becomes an abstract record with two cases:
  - `NotEnoughSpace`, with today's fields
  - `LocationNotPrivate`, with the directory and one of `ReadableByOthers`, `SymbolicLink`,
    `OwnedByAnother`
- `MainWindow.Describe` words each case, for example: *"The reader keeps an export's data in
  /tmp/gobd-reader-auditor, but other accounts can read that directory. Remove it, or start the
  reader with TMPDIR set to a directory of your own."*
- **Alternative:** a nullable reason on today's record. Rejected: a record in which half the
  fields mean nothing depending on the other half.

**Windows** creates the root as today. Its temporary directory is private by its ACLs, and the
Unix mode calls do not apply there.

### D7 — `LICENSE` becomes plain MIT; `NOTICE` says what it does not cover

- **`LICENSE`** keeps the MIT text and copyright line, and loses the appended note. GitHub then
  identifies it as MIT.
- **`NOTICE`** says:
  - that the MIT licence covers the code, tests, build configuration and specifications
  - that `src/GoBd.Validation/Resources/gdpdu-01-03-2019.dtd`, and the copy each build carries,
    is Audicon's work, redistributed because the standard requires every data carrier to carry
    it, and not covered by MIT
  - that the specification document is not redistributed, and where it is published
  - that the reader's icon was generated with Recraft through Mammouth AI and edited in Affinity
  - that the components the builds contain are listed in `THIRD-PARTY-NOTICES.txt`
- **The README's Licence section** points at all three files, and at `--license`, `--notices` and
  the Help menu.

### D8 — `release.yml` reads by default; only `release` writes

- The workflow-level `permissions` becomes `contents: read`.
- The `release` job gains everything it needs to write, and nothing more:
  - `contents: write`, to create the release
  - `id-token: write`, `attestations: write` and `artifact-metadata: write`, which
    `actions/attest` requires at the version pinned (D12)
- No other job writes: artifact upload and download use the Actions runtime token, not the
  repository's contents permission.
- `ci.yml` already reads only. Its `sbom` job (D11) needs nothing more.

### D9 — The fixtures name a company that does not exist

| Was | Becomes | Where |
| --- | --- | --- |
| `Glaswerk AG` | `Beispiel AG` | the three `index.xml` and `kunden.csv`, `GrammarValidationTests`, `IndexXmlParserTests` |
| `Singen` | `Musterstadt` | the three `index.xml`, `GrammarValidationTests` |
| `Kunden-Stammdaten` | `Stammdaten der Kunden` | two `index.xml` |
| `glaswerk.zip` | `beispiel.zip` | `JsonReportWriterTests`, `TextReportWriterTests` |
| `K001;Glaswerk` | `K001;Beispiel` | `ExportSourceFactoryTests` |

- **Lengths.**
  - `Beispiel AG` has the length of `Glaswerk AG`, so every `kunden.csv` keeps its byte length.
  - `index.xml` changes only within lines 6 to 15, so no line number a test or the README
    quotes moves.
- **Kept:**
  - `Muster GmbH`
  - the table and column names (`Kunden`, `Bestellungen`, `Kunden-Code`), which are ordinary
    German words that the README's example output and many tests quote
  - the DTD copies, which are pinned by their hash

### D10 — Mentions are rewritten to say what happened

The rule: replace the identifier with the event. Only identifiers change; the meaning of each
passage stays.

- **The issue's number** becomes "a crash reported from a real export: an empty table beside a
  filled one". Code comments keep the reason for the code; tests keep the reason for the test.
- **The helper named after the issue** becomes `EmptyBesideFilled()`.
- **In `harden-export-listing`,** "already in" and the commit's hash becomes "implemented before
  this change was written".
- **In `repackage-reader` and `add-reader-icon`,** the measurements stay and the commit hashes,
  run numbers and the pull request's number go. For example, "the last successful CI run on
  `main`" and "this change's CI run".

**Verified once, by search:**

- no `issue #`, `pull request #`, `PR #`, backticked commit hash or `run <8+ digits>` remains
- the exceptions are the SHA-pinned actions in the workflows, and `#1` as a record number in
  `add-table-query/tasks.md`

**No permanent test.** Once the repository is public, citing its public issues is legitimate.

### D11 — One CycloneDX bill of materials per tool, made from the projects

**The generator.** The CycloneDX .NET tool (`CycloneDX`, command `dotnet-CycloneDX`), pinned in a
local tool manifest, `.config/dotnet-tools.json`. The workflows run `dotnet tool restore`, so every
run uses the version the repository names. It works from a project's resolved packages, which
suits builds a scanner cannot look into: a single file on Windows and Linux, and a NativeAOT
binary.

**The script.** `.github/scripts/make-sbom.sh <version> <out-dir>` makes both files, as the other
steps of the pipeline are scripts too:

1. Restore each tool project for all five runtime identifiers, so every platform's native
   packages are resolved.
2. Run, for each tool:
   `dotnet dotnet-CycloneDX <project> --exclude-dev --spec-version 1.6 --output-format Json`
   `--set-name <tool> --set-version <version> --set-type Application --output <out-dir>`
   `--filename <tool>.cdx.json`.
   Without `--runtime` the tool aggregates every runtime identifier restored, which is the
   per-tool file decided on.
3. If the tool leaves out the .NET runtime, add it with `jq` (task 10.1 decides whether this
   step is needed):
   - one component, `Microsoft.NETCore.App`
   - the runtime version the build used (`BundledNETCoreAppPackageVersion` of the SDK that
     builds)
   - licence MIT
   - a CPE, `cpe:2.3:a:microsoft:.net:<version>:*:*:*:*:*:*:*`, the identifier vulnerability
     databases use for .NET
4. Compare each file's components with the `Packages:` lines of `THIRD-PARTY-NOTICES.txt` (D3),
   and fail, naming the difference, when they part.

**The version** comes from D1's `ComputeReleaseVersion`, so a bill of materials names the same
version its downloads report, and `0.0.0.0` outside a release.

**In the workflows.**

- **`release.yml`** gains an `sbom` job on Linux. It needs no other job, and uploads its two
  files as `release-sbom`. The `release` job already downloads every `release-*` artifact and
  attaches what it finds, so the files reach the release without a change there.
- **`ci.yml`** gains the same job and keeps its output for 2 days, like its builds, so a bill of
  materials that cannot be made shows up on the commit that broke it, not on release day.

**CycloneDX 1.6, not the tool's default 1.7.** 1.6 is the floor BSI TR-03183-2 v2.1.0 sets, and
more tools that read bills of materials accept it today.

**Alternatives:**

- **Microsoft's `sbom-tool`.** Rejected: it writes SPDX 2.2 by default, below the floor above.
  It also scans the whole source tree, so the test projects would have to be excluded by hand.
- **`Microsoft.Sbom.Targets`.** Rejected: it hooks only into `dotnet pack`, and nothing here is
  packed.
- **Syft, or any scanner of the published files.** Rejected: it cannot see into a single-file
  bundle or a NativeAOT binary.
- **GitHub's dependency-graph export.** Rejected: one SPDX 2.3 document for the whole
  repository, test packages included, not tied to a release.
- **One bill of materials per download.** Rejected by decision. It would be exact per platform,
  but ten files where two do the job.

### D12 — `actions/attest` attests every file a release attaches

In the `release` job, after the artifacts are downloaded and before
`softprops/action-gh-release` creates the release:

1. **Build provenance** for `artifacts/*`: every file the release attaches, the checksums and
   the two bills of materials included.
2. **Bill-of-materials attestations,** one per tool:
   - `sbom-path: artifacts/gobd-validate.cdx.json`, with the five `gobd-validate-*` downloads as
     `subject-path`
   - `sbom-path: artifacts/gobd-reader.cdx.json`, with the five `gobd-reader-*` downloads

**How it is set up:**

- `actions/attest` is pinned by commit like every other action, and one action serves both
  kinds, since the older two now only call it.
- Its permissions are those of D8.
- The `release` job runs only for a tag, and never on a dry run, so nothing else is ever
  attested.

**How a person checks** (the README's "Get it" says so):

```
gh attestation verify gobd-reader-win-x64.exe -R WebDucer/gobd-tools
gh attestation verify gobd-reader-win-x64.exe -R WebDucer/gobd-tools \
  --predicate-type https://cyclonedx.org/bom
```

**Why attest before creating the release:** a release is never visible without its
attestations. If attesting fails, nothing is published.

## Risks / Trade-offs

- **[A native library bundles more than its package's notices say]** → Each entry carries the
  upstream project's own third-party notices where one exists, and records its source. On each
  upgrade of SkiaSharp, HarfBuzzSharp or DuckDB.NET, the entry is checked against the upstream
  file again. The completeness test cannot see inside a native library.
- **[The .NET runtime's notices change with the SDK]** → The entry records the runtime pack
  version it was taken from. It is refreshed with each major SDK update, which also changes
  `TargetFramework`.
- **[Existing test harnesses create their store root with default permissions]** → D6 refuses
  such a root. Harnesses leave the root to the store, which creates it private, or create it with
  a private mode. The full suite on Linux CI and macOS shows any that do not.
- **[The reader run as root passes the write probe on any directory]** → The symlink and mode
  checks still apply. Running a desktop reader as root is not a supported use.
- **[A GUI-less Linux host has an unusual `Environment.UserName`]** → It only forms a directory
  name. The checks, not the name, provide the privacy.
- **[The inline build task adds compile time]** → MSBuild compiles it once and caches it. It is
  ten lines.
- **[Rewriting archived artifacts changes records]** → Only identifiers change. The decisions,
  measurements and their reasons stay as written.
- **[The CycloneDX tool leaves out the .NET runtime, or keeps a build-only package]** → Task 10.1
  runs it on both projects before the script is written, and D11 settles step 3 from what it
  finds. The comparison with the notices catches any later drift, in CI.
- **[The attestations are first exercised by a release]** → The first release is verified before it
  is announced: `gh attestation verify` for a download of each tool, and again with the CycloneDX
  predicate type. If either fails, the release and its tag are deleted, the workflow fixed, and the
  tag pushed again.
- **[`actions/attest` changes the permissions it requires]** → It is pinned by commit. An update
  to the pin is a pull request, and its README's permissions are compared with D8's then.
- **[A per-tool bill of materials lists components some platforms do not ship]** → Accepted. A
  scanner may raise a false alarm for a platform, but it never misses a component.

## What the implementation settled

### What the CycloneDX tool gives, before the script touches it

CycloneDX 6.2.0, pinned in `.config/dotnet-tools.json`, run with
`--exclude-dev --spec-version 1.6 --output-format Json --disable-package-restore` after both tool
projects were restored for all five runtime identifiers:

- **The .NET runtime is not listed**, for either tool. It is not a package the project resolves:
  the SDK supplies it as a runtime pack. So D11's step 3 is needed.
- **`--exclude-dev` leaves out `Microsoft.NET.ILLink.Tasks` and `Microsoft.DotNet.ILCompiler`**,
  but not everything that only builds:
  - the reader's file lists `Avalonia.BuildServices`, `HarfBuzzSharp.NativeAssets.WebAssembly` and
    `SkiaSharp.NativeAssets.WebAssembly`, which carry nothing any of its builds ship
  - the validator's file lists `runtime.<rid>.Microsoft.DotNet.ILCompiler` for all five runtime
    identifiers, which is the NativeAOT compiler and ships nothing either
- **Every platform's native packages appear**: `SkiaSharp.NativeAssets.Linux`, `.macOS` and
  `.Win32`, the same three of `HarfBuzzSharp`, `Avalonia.Native` and
  `Avalonia.Angle.Windows.Natives`.
- **With those differences taken away, the reader's 26 components are exactly the 26 ids on the
  `Packages:` lines of `THIRD-PARTY-NOTICES.txt`**, and the validator's are none.

How the script deals with the differences: it works out what ships by the rule the notices test
uses (D3): a package ships when its resolved entry carries runtime, native or runtime-specific
assets. Every other package the assets file names is passed to `--exclude-filter`, by name. The
tool then removes it from the components and from the dependency graph alike, which leaves no
dangling reference. The rule is the notices test's, so the bill of materials and the notices
cannot come to disagree about what ships.

### The capability's name

`releases` was the name the proposal gave the new capability, until `.gitignore` swallowed it: the
Visual Studio template it is built on ignores `[Rr]eleases/`, so neither `specs/releases/spec.md`
here nor the `openspec/specs/releases/` that archiving would create could ever be committed. The
capability is `release-builds`, which no rule of that file matches. An exception line would have
worked too, and would have left a trap for the next directory that happens to be called `releases`.

### The version task

`ComputeReleaseVersion` runs before `GetAssemblyVersion`, and only when `ReleaseTag` is set. Its
inline task (`ReadReleaseTag`, `RoslynCodeTaskFactory`, in `Directory.Build.targets`) matches
`^v([0-9]+)(?:\.([0-9]+))?(?:\.([0-9]+))?$`, rejects a part that does not parse or exceeds 65534,
and sets `Version` to three parts. Its two messages:

- `'<tag>' is not a release tag. A release tag is v<major>, optionally followed by .<minor> and
  .<patch>, each a whole number, such as v1, v1.2 or v0.3.7.`
- `'<tag>' is not a release tag: each part of a version may be at most 65534.`

Checked on this Mac: `gobd-validate` published with `-p:ReleaseTag=v0.3.7` prints `0.3.7.0`; the
`win-x64` reader published with `-p:ReleaseTag=v1` carries the file version `1.0.0.0` in its one
`RT_VERSION` resource, read from the PE resource directory. Its bytes also hold the single-file
host's own version block, `10.0.826.23019`, which no resource entry points to.

Two ways to ask the build for a version, and which answers what:

- `-t:ComputeReleaseVersion -getProperty:Version` gives three parts, as the macOS bundle wants.
- `-t:GetAssemblyVersion -getProperty:AssemblyVersion` gives four, as the tools report. The bills
  of materials use this one, so that each names the version its downloads report.

In `release.yml` the tag reaches every build as the variable `RELEASE_TAG`, never written into a
command, so that a tag name is never read as shell. The Windows NativeAOT publish therefore runs
under bash, like the other steps of that job.

### The notices

`THIRD-PARTY-NOTICES.txt` is 259 KB, not the 100 KB the proposal estimated: SkiaSharp and
HarfBuzzSharp publish 140 KB of notices for their native libraries, beside the .NET runtime's 77 KB.
Each entry's `Source:` line records where and at which version its text was taken from, so that
an upgrade knows what to refresh:

| Entry | Taken from |
| --- | --- |
| .NET runtime | `Microsoft.NETCore.App.Runtime`, `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` as they stand in 10.0.8, byte for byte; the same in every runtime pack up to line endings, and in `Microsoft.DotNet.ILCompiler` |
| Avalonia | the Avalonia repository at tag 12.1.2, `licence.md` and `NOTICE.md` |
| MicroCom.Runtime | the MicroCom repository at commit 76785efcafd9, which tags no releases |
| Tmds.DBus.Protocol | the Tmds.DBus repository at tag `rel/0.94.1`, `COPYING` |
| SkiaSharp and HarfBuzzSharp | the packages' `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt` |
| ANGLE | the package's `LICENSE` |
| DuckDB.NET and DuckDB | the packages' `LICENSE.md`, and the DuckDB repository at tag v1.5.5, `LICENSE` |
| Apache Arrow | the package's `LICENSE.txt`, and the Arrow repository at tag `apache-arrow-23.0.0`, `NOTICE.txt` |

Where D3 had SkiaSharp and HarfBuzzSharp as two entries, they are one: both packages carry the same
licence and the same notices, which cover both. DuckDB publishes no notice for the code it includes
from other projects, so its entry carries its licence alone, which is all the packages ship.

### The reader

- **The Help menu's entries** are Licence, Notice and Third-party notices, and About GoBD Reader
  where the platform is not macOS. About names the reader, its version, what it reads, the licence
  and the copyright line, and offers each of the three texts.
- **For the tests,** `MainWindow` gained an internal constructor that takes `StoreOptions`, and an
  internal `Reading`. The Help menu's commands are an `ActionCommand`, a ten-line `ICommand`, since
  Avalonia ships none.
- **Checked on this Mac:** the Help menu sits in the system menu bar, each of its entries opens its
  window, and the application menu shows "About GoBD Reader" rather than the toolkit's own entry.
- **The licence and what it does not cover were one text at first**, printed and shown together.
  They are apart now, as `LICENSE` and `NOTICE` are apart in the repository and for the same
  reason, which cost a third option on the command line and a fourth entry in the menu.
- **About was in the Help menu on macOS too** at first, which left the toolkit's "About Avalonia"
  in the application menu beside it. Setting the application's menu from the window did not help
  either: Avalonia's macOS exporter reads that menu once and, finding none, installs its own
  default there. So `App.Initialize` sets it, while the application starts, and its entry asks the
  application for the reader's window. The Help menu has three entries on macOS and four elsewhere.
- **A clipboard that refuses** leaves the text on the screen rather than taking the reader down:
  the copy runs in an event handler, where anything let escape would be unhandled.
- **Something that is not a directory in the store root's place** is refused like any other
  location that cannot hold a store, rather than raised at a window that has no answer for it.
- **About was that same paragraph** until it was given a window of its own. Its layout was checked
  by rendering it headlessly to an image with Skia, which is how the crowding of its buttons and
  the licence's raw "(c)" were seen; the window sets that as "©". The rendering harness was a
  throwaway: the headless tests draw nothing, as they did before.
- **The test of an entry chosen while an export is being read** cannot hold the reading back, so it
  shows the entry is served and the reading completes, not that the two overlapped.
- **The notices window dragged**, shown on this Mac: 5,040 lines in one `SelectableTextBlock` are
  laid out on every pass. Beyond 400 lines a text is now a virtualised list of its lines, and the
  tests hold it to that: of 2,000 lines, under a quarter are realised, and the last one is realised
  after scrolling to it.

### The private store

- **The refusal texts,** each naming the directory:
  - readable by others: *"The reader keeps an export's data in \<dir\>, but other accounts can read
    that directory. Remove it, or start the reader with TMPDIR set to a directory of your own."*
  - a symbolic link: *"… but that is a symbolic link, which could lead the data anywhere. Remove it,
    or start the reader with TMPDIR set to a directory of your own."*
  - another account's: *"… but it cannot write there: the directory belongs to another account.
    Start the reader with TMPDIR set to a directory of your own."*
- **The startup sweep** (`RemoveAbandonedStores`, which the application runs before its window
  opens) also leaves a root alone that is not private. It may be someone else's, and opening an
  export there is refused anyway.
- **The test harnesses** create their store roots with mode `700`, as the store does. The tests of
  the mode are marked `[UnsupportedOSPlatform("windows")]` and skip themselves there.

### The bills of materials

- **The tool:** CycloneDX 6.2.0, in `.config/dotnet-tools.json`. The .NET 10 SDK's
  `dotnet new tool-manifest` writes the manifest at the repository root; it was moved to `.config/`,
  which the SDK reads as well, as D11 says.
- **Steps 3 and 4 are Python, not `jq`.** Adding the runtime and keeping the dependency graph whole,
  reading the notices by section and comparing both ways is clearer in one short script, and every
  runner has Python.
- **Checked on this Mac:** both files validate as CycloneDX 1.6 with the CycloneDX CLI 0.33.1
  (`validate --input-version v1_6 --fail-on-errors`); each names its tool and the version given; the
  runtime appears as `Microsoft.NETCore.App` 10.0.8; and the script fails, naming
  `MicroCom.Runtime`, when that id is taken off its `Packages:` line.

### The runtime version is the runner's, not this machine's

`global.json` pins no SDK, so the .NET the builds carry is whatever SDK the runner has: 10.0.12 in
the pipeline against 10.0.8 here, on the same day. Two consequences:

- The notices say which version their .NET texts were taken from rather than which version a build
  carries, and point at the bill of materials for that. The texts are the same across these patch
  versions.
- A bill of materials names the runtime of the build it was made beside, which is what a person
  scanning it for vulnerabilities needs. That is why the script asks the build rather than a
  constant.

Pinning the SDK in `global.json` would make the two agree. It would also decide when the tools move
to a new runtime, which is a choice this change has no business making.

### The attestations

`actions/attest` is pinned at v4.2.2, commit 1e69f48acb82d1966a394da916b4c1698aa569d6, whose README
asks for `id-token`, `attestations` and `artifact-metadata` write, as D8 grants. Each tool's
downloads are named one by one rather than by a pattern, so that no checksum is bound to a bill of
materials. They are made by a release and by nothing else, so the first release is what shows them
working.

### Also

- **The validator's checksum step** in `release.yml` passes `--` before its `*`, so that a file name
  starting with a dash cannot be read as an option. `actionlint` had reported it, and its report
  had to be clean.
- **A test that measures time,** `PagingBudgetTests`, failed twice in full runs on this Mac and
  passed alone every time. Nothing in this change touches paging: the two test assemblies run
  beside each other, so the slowest of 200 steps measured the machine. It and
  `ViewPagingBudgetTests` are judged by the 95th percentile now, as `StoreConcurrencyTests` already
  was, and the percentile they share lives in `Measured`.
