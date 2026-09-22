# Say what the tools are, keep the store private, and take the version from the tag

## Why

What a tool distributed to other people has to be able to say for itself, and what it must not
leave open:

- **Third-party licences.** The builds ship other projects' code without their notices, which
  their licences require. Both tools compile the .NET runtime in, and the reader also carries
  DuckDB, Avalonia, Skia, HarfBuzz, ANGLE and Apache Arrow.
- **The reader's store is readable by others on Linux.** It lives under a directory in `/tmp`
  that every account on the machine shares, created with default permissions. Any other account
  can read the extracted tax data, and a second account cannot open an export at all.
- **Every release reports the same version.** `gobd-validate --version` prints `1.0.0.0`
  whatever release it came from, because nothing sets the version.
- **Nobody can check what a download contains or where it came from.** The downloads are
  unsigned, and no machine-readable list of their components is published.
- **The tree cites identifiers of its own repository.** Code comments and archived changes name
  issues, pull requests, commits and CI runs, which a reader cannot follow and a move would
  break.
- **The fixtures borrow the specification's example company.** They take its name and town from
  the example in the Beschreibungsstandard document.
- **GitHub cannot tell the licence is MIT,** because `LICENSE` carries an appended note.
- **The release workflow gives every job write access,** when only the job that publishes needs
  it.

## What Changes

- **Versions come from the release tag.**
  - A tag `v<major>[.<minor>[.<patch>]]` sets the version of everything a release builds, with
    omitted parts as `0`: `v1` gives `1.0.0.0`, `v0.3.7` gives `0.3.7.0`.
  - A build that is not for a tag reports `0.0.0.0`.
  - A tag of any other form fails the release build, so nothing is released under a version
    nobody chose.
  - The macOS application reports the same version.
- **Each tool carries its licence and third-party notices, and shows them.** Nothing is
  delivered beside the executable.
  - **`gobd-validate`** gains `--license` and `--notices`, beside `--help` and `--version`.
  - **`gobd-reader`** gains a Help menu with the licence, the third-party notices and its
    version.
  - A new `THIRD-PARTY-NOTICES.txt` names every component a build ships, with its licence. A
    test fails when the reader acquires a package the file does not name.
- **The licence reads as plain MIT.** The scope note moves from `LICENSE` into a new `NOTICE`:
  what MIT does not cover (Audicon's DTD), and how the icon was made. `--license` and the
  reader show both.
- **The reader's store is private to the person running it.**
  - On Linux and macOS, the store lives in a directory of that person's own, readable by them
    alone.
  - A directory that is not private is refused, with a message saying where and what to do,
    rather than used.
  - Windows is unchanged: its temporary directory already belongs to one person.
- **Each release carries a software bill of materials per tool, and attestations.**
  - `gobd-validate.cdx.json` and `gobd-reader.cdx.json` are attached to every release. Each
    lists, in CycloneDX JSON, the components that tool ships on any platform, the .NET runtime
    included. The build fails when a bill of materials and the notices disagree.
  - Every file attached to a release gets an attestation of the build that produced it, and each
    tool's downloads are bound to its bill of materials by a second one. Both can be checked with
    `gh attestation verify`.
- **The release workflow grants write access to the publishing job only.**
- **The fixtures use a made-up company:** a new supplier name, town and description, in the
  fixtures and in the tests that quote them.
- **Every mention of an issue, pull request, commit or CI run is rewritten** to say what happened
  instead. This covers code comments, tests and the archived OpenSpec changes.

### Non-goals

- **No code signing.** The builds stay unsigned, as the README already says. The attestations let
  a person check a download, but they do not make an operating system trust it.
- **No bill of materials per download.** One per tool covers all its platforms.
- **No attestations for CI builds or dry runs.** Only what a release publishes is attested.
- **No translation.** The licence and the notices are shown as their authors wrote them, in
  English.
- **No clean-up of the shared location.** A store left in the old shared `/tmp/gobd-reader` by
  a crashed earlier version is not removed. Only private builds of those versions exist.

## Capabilities

### New Capabilities

- `release-builds`: what every release carries and how it is identified:
  - the version a build reports, from the tag it was built for, and what happens without a tag
    or with a malformed one
  - a software bill of materials per tool
  - attestations that trace every download to the build that made it

### Modified Capabilities

- `validator-cli`: added, the executable reports its version, licence and third-party notices
  from the command line, with nothing beside it.
- `reader-ui`:
  - added, the reader shows its version, licence and third-party notices from its Help menu.
  - added, the reader's store is readable only by the person running the reader.

## Impact

- **New files:**
  - `NOTICE`
  - `THIRD-PARTY-NOTICES.txt`, including the .NET runtime's own notices, verbatim
  - `.config/dotnet-tools.json`, pinning the CycloneDX tool
  - `.github/scripts/make-sbom.sh`
- **New dependencies:**
  - the CycloneDX .NET tool, used only by the workflows
  - the `actions/attest` action, pinned by commit like the others
- **Changed files at the root:**
  - `LICENSE` becomes plain MIT.
  - `README.md`:
    - the usage block gains the two options
    - the licence section says where the notices are
    - the store's location is described anew
    - building says how a release takes its version
    - "Get it" says how to verify a download and get its bill of materials
- **Build configuration:**
  - `Directory.Build.props` derives the version from a `ReleaseTag` property, and rejects a
    malformed one.
  - Both tool projects embed `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES.txt`.
- **Workflows:**
  - `release.yml` passes the tag when it builds for one. `ci.yml` never builds for a tag.
  - `release.yml` narrows its permissions. Only the `release` job writes: to the repository's
    contents, and to the attestations it creates.
  - Both workflows gain an `sbom` job. In `release.yml` its output is attached to the release.
  - `release.yml`'s `release` job attests every file it attaches.
  - The macOS bundle version comes from the build rather than from a shell substitution.
  - `ci.yml` checks the version rule, and runs `--license` and `--notices` on each native
    binary.
  - `smoke-start-reader.sh` looks for the store in the per-user directory, and checks its mode.
- **Validator code:**
  - `CommandLine` and `CliRunner` gain `--license` and `--notices`.
  - `ReadmeUsageTests` checks for the two options.
- **Reader code:**
  - `MainWindow` gains the Help menu and a window that shows text.
  - `StoreOptions` and `ExportStore.Open` gain the private root, and a refusal that says why.
- **Tests:**
  - version derivation
  - the new options
  - the private root, run on Linux in CI and on macOS locally
  - notices completeness
  - fixture values, and renamed helpers where they cite an issue
- **Archived OpenSpec changes:** sentences that cite issues, pull requests, commits or CI runs,
  in `add-export-reader`, `add-table-query`, `harden-export-listing`, `repackage-reader` and
  `add-reader-icon`.
- **Size:** the notices add about 100 KB to each tool, most of it the .NET runtime's own list.
