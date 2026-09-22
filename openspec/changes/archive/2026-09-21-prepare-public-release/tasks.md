## 1. Starting point

- [x] 1.1 Branch from the current `origin/main`, which carries the reader's icon, and bring this
      change's directory along. The local `main` this change was written on is two merges behind.
      Verify that `openspec/specs/reader-ui/spec.md` on the branch contains "The reader shows its
      icon wherever an application's icon is shown", and that
      `openspec validate prepare-public-release --strict` passes

## 2. The version comes from the tag

- [x] 2.1 Set `Version` to `0.0.0` in `Directory.Build.props`, with a comment pointing at
      design.md D1. Verify `dotnet build` stays warning-free, and that a CLI test asserts that
      `--version` prints `0.0.0.0` for a build without a tag
- [x] 2.2 Add the `ComputeReleaseVersion` target and its inline task to `Directory.Build.targets`,
      running before `GetAssemblyVersion`. It fails with `GOBDBUILD002` for a tag it cannot read
      (D1). Verify with
      `dotnet msbuild src/GoBd.Validation.Cli -t:ComputeReleaseVersion -getProperty:Version -p:ReleaseTag=<tag>`:
      - `v1` gives `1.0.0`
      - `v1.2` gives `1.2.0`
      - `v0.3.7` gives `0.3.7`
      - no tag gives `0.0.0`
      - `v1.2-rc1`, `v1.2.3.4` and `v70000` fail with `GOBDBUILD002`, naming the tag and the
        expected form
- [x] 2.3 Verify the version reaches the binaries. Publish `gobd-validate` for this Mac with
      `-p:PublishAot=true -p:ReleaseTag=v0.3.7`, and verify it prints `0.3.7.0` for `--version`.
      Publish `gobd-reader` for `win-x64` with `-p:ReleaseTag=v1` on this Mac, and verify the
      executable's file version reads `1.0.0.0`, for example with `exiftool` or a few lines of
      Python reading the version resource
- [x] 2.4 Write `.github/scripts/verify-release-version.sh`, checking the seven cases of 2.2, and
      run it in `ci.yml`'s test job. Verify it passes locally, and fails when one expected value
      in it is changed
- [x] 2.5 In `release.yml`:
      - pass `-p:ReleaseTag=${{ github.ref_type == 'tag' && github.ref_name || '' }}` to every
        `dotnet publish`
      - ask the build for the macOS bundle version with `-getProperty:Version` instead of
        `${GITHUB_REF_NAME#v}`

      Verify with a `workflow_dispatch` dry run from the branch: every job passes, the validator
      prints `0.0.0.0` in "Verify the artifact runs", and the bundle is assembled as `0.0.0`

## 3. Licence, notice and third-party notices

- [x] 3.1 Split `LICENSE` into plain MIT and a new `NOTICE` with the content of D7, including the
      icon's provenance. Verify that `LICENSE` ends with the MIT disclaimer. Once the branch is
      pushed, verify that
      `gh api "repos/WebDucer/gobd-validator-and-reader/license?ref=<branch>" --jq .license.spdx_id`
      reports `MIT`
- [x] 3.2 Write `THIRD-PARTY-NOTICES.txt` in the layout of D3. Every entry needs a `Packages:`
      line, a `Source:` line and its licence text as its package or upstream project ships it.
      Take the .NET runtime's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` from the runtime pack
      in the NuGet cache, and record its version. Verify:
      - `diff` shows the .NET texts in the file are byte-identical to the pack's
      - every package listed in design.md's Context appears on a `Packages:` line
- [x] 3.3 Embed `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES.txt` in `GoBd.Validation` under the
      logical names of D2, and add the class that returns the licence (`LICENSE`, a blank line,
      `NOTICE`) and the notices. Verify with unit tests:
      - the licence contains the MIT text and the DTD's exclusion
      - the notices contain `.NET`, `DuckDB` and `Apache License`
- [x] 3.4 Add the completeness test of D3 to `GoBd.Validation.Tests/Documentation`, for both tool
      projects' `project.assets.json`. Verify:
      - it passes
      - it fails naming the package when one id is removed from a `Packages:` line
      - it fails naming the id when an id no build ships is added

## 4. `gobd-validate --license` and `--notices`

- [x] 4.1 Add `--license` and `--notices` to `CommandLine` and `CliRunner`, with the precedence
      and output of D4 and two help lines after `--version`. Verify with CLI tests:
      - each option prints its text in full and exits `0`
      - given with a nonexistent export path, each still exits `0` and writes no report
      - `--version --license` prints only the version
      - the help lists both options
- [x] 4.2 Add both options to the README's usage block and to the options `ReadmeUsageTests`
      expects. Verify `ReadmeUsageTests` passes, and fails while either line is missing from the
      README
- [x] 4.3 In `ci.yml`'s publish job, and in `release.yml`'s "Verify the artifact runs", copy the
      native binary into an empty directory and run `--license` and `--notices` there. Check that
      the output contains `MIT License` and `.NET` respectively. Verify every publish job passes,
      on all five runtime identifiers

- [x] 4.4 Keep the licence and what it does not cover apart, as `LICENSE` and `NOTICE` are apart:
      `--license`, `--notice` and `--third-party-notices` (D4). Verify with CLI tests that each
      prints its own text, that the licence carries nothing of the notice, and that the help and
      the README's usage block list all three

## 5. The reader's Help menu

- [x] 5.1 Add `TextWindow` (D5): owned by the main window, not modal, with read-only selectable
      monospaced text in a scroll viewer. Verify with a headless test that a long text can be
      scrolled to its last line
- [x] 5.2 Add the Help menu after File in `ConfigureMenu`, with Licence, Third-party notices and
      About GoBD Reader wired through `Command` (D5). Verify with headless tests:
      - the menu has the three entries
      - each opens a window showing the licence, the notices, or the name and the assembly's
        version
      - choosing an entry again brings its window to the front instead of opening a second
      - an entry works while a `StoreHarness` export is being read, and the reading completes
- [x] 5.3 Start the reader on this Mac. Check that the Help menu sits in the system menu bar and
      that each window reads well in light and dark mode. Record the outcome under "What the
      implementation settled" in design.md
- [x] 5.4 Show a text of more than 400 lines as a virtualised list of its lines, because the
      notices dragged as one block (D5). Verify with headless tests that under a quarter of 2,000
      lines are realised, that the last line is realised after scrolling to it, and that copying
      takes the selected lines or the whole text

- [x] 5.5 Give About a window of its own rather than a paragraph: the icon, the name, a selectable
      version, what the reader does, the licence and copyright line, a link to the project page and
      a button to each text (D5). Verify with headless tests that it shows the name, the version
      and the copyright line from `LICENSE`, carries the icon and the link, and that each button
      opens its text
- [x] 5.6 Separate Licence and Notice in the Help menu, so it offers four entries (D5). Verify with
      headless tests that the entries are Licence, Notice, Third-party notices and About, and that
      the licence window carries nothing of the notice

- [x] 5.7 Put About in the application's own menu on macOS, where the toolkit otherwise leaves
      "About Avalonia", and leave it in Help elsewhere (D5). Verify with headless tests that Help
      offers three entries on macOS and four elsewhere, and that the application menu carries About
      on macOS; and by starting the reader on this Mac

## 6. The store's private root

- [x] 6.1 Change `StoreOptions.DefaultRoot` to `gobd-reader-<user name>` under the temporary
      location on Linux and macOS, leaving Windows unchanged (D6). Verify with a unit test on the
      name for the platform the test runs on
- [x] 6.2 Add the privacy check as the first step of `ExportStore.Open`, and make `StoreRefusal`
      an abstract record with `NotEnoughSpace` and `LocationNotPrivate` (D6). Verify with tests
      that run on Linux and macOS and are skipped on Windows:
      - a root that does not exist is created with mode `700`
      - an existing root with mode `755` is refused as `ReadableByOthers`, and nothing is created
        in it
      - a symbolic link to a private directory is refused as `SymbolicLink`
      - a root with mode `500` (writable by nobody, standing in for another account's) is
        refused as `OwnedByAnother`
      - a root under a custom `TMPDIR`-style location is used
- [x] 6.3 Word the new refusal in `MainWindow.Describe`, naming the directory, the reason and
      `TMPDIR`. Verify with a headless test that opening an export against a `755` root shows
      that text and opens no table
- [x] 6.4 Make the test harnesses leave their store root to the store, or create it private.
      Verify the full suite passes on this Mac, and on Linux in CI
- [x] 6.5 Update `smoke-start-reader.sh` to look for `store.duckdb` under `gobd-reader-*/`, and
      to fail unless that directory's mode is `700`. Verify the Linux smoke start passes in CI on
      x64: it reported the store in `gobd-reader-runner`, mode `700`. The ARM64 start downloads
      that build as an artifact, which the repository's metered storage would not allow at the
      time, so it is checked on the first release
- [x] 6.6 Update the README's paragraph on where the store lives, so it names the per-user
      directory and says it is readable only by the person running the reader. Verify the
      README's description matches `StoreOptions.DefaultRoot` on each platform

## 7. The release workflow's permissions

- [x] 7.1 Set `release.yml`'s workflow-level `permissions` to `contents: read`, and give the
      `release` job `contents: write` (D8). Verify that a `workflow_dispatch` dry run passes, and
      that `grep -n "contents: write" .github/workflows/*.yml` finds only the `release` job

## 8. The fixtures' company

- [x] 8.1 Apply the replacements of D9 to the three fixture exports and to the five test files.
      Verify:
      - every `kunden.csv` keeps its byte length
      - `grep -rn -a -i -E "glaswerk|Singen|Kunden-Stammdaten" src tests README.md docs` finds
        nothing
      - the full suite passes, `ReadmeUsageTests`' example run included

## 9. Mentions of the private repository

- [x] 9.1 Rewrite the mentions in code and tests of D10: the comments in `ExportStore.cs`,
      `ReaderSession.cs` and `ExportStoreTests.cs`, and `IssueFour()` renamed to
      `EmptyBesideFilled()` with its comment. Verify the suite passes
- [x] 9.2 Rewrite the mentions in the archived changes `add-export-reader`, `add-table-query`,
      `harden-export-listing`, `repackage-reader` and `add-reader-icon` (D10), changing only the
      identifiers. Verify with `git diff --word-diff` that no measurement or decision changed
- [x] 9.3 Search the whole tree for `issue #`, `pull request #`, `PR #`, backticked hex strings of
      7 to 40 characters, and `run <8+ digits>`. Verify only the SHA-pinned actions in the
      workflows and `#1` in `add-table-query/tasks.md` remain

## 10. Software bill of materials

- [x] 10.1 Pin the CycloneDX .NET tool in `.config/dotnet-tools.json`. Then run it on both tool
      projects, after a restore for all five runtime identifiers, with
      `--exclude-dev --spec-version 1.6 --output-format Json` (D11). Record under "What the
      implementation settled":
      - whether the .NET runtime is listed
      - whether `Microsoft.NET.ILLink.Tasks`, `Avalonia.BuildServices` and
        `Microsoft.DotNet.ILCompiler` are left out
      - whether every platform's native packages appear
      - the components of each file

      Verify that the recorded components match the `Packages:` lines of
      `THIRD-PARTY-NOTICES.txt`, or that each difference is listed with how D11's script handles
      it
- [x] 10.2 Write `.github/scripts/make-sbom.sh <version> <out-dir>`, with the four steps of D11.
      Step 3 applies only if 10.1 found the runtime missing. Verify on this Mac:
      - both files validate against the CycloneDX 1.6 JSON schema, for example with the
        CycloneDX CLI's `validate --input-version v1_6`
      - each names its tool and the version given
      - the .NET runtime is listed with the SDK's runtime version
      - the script fails, naming the package, when one id is removed from a `Packages:` line
- [x] 10.3 Add an `sbom` job to `release.yml` that uploads `release-sbom`, and the same job to
      `ci.yml` that uploads `sbom` with a retention of 2 days. Both take the version from
      `ComputeReleaseVersion`. Verified: the pull request's CI and the dry run each made both
      files, each matching `THIRD-PARTY-NOTICES.txt` and naming the runtime of the runner's SDK.
      Attaching them to a release waits for the first release, because the repository's metered
      storage would not allow an upload at the time

## 11. Attestations

- [x] 11.1 Extend the `release` job's permissions to those of D8: `contents`, `id-token`,
      `attestations` and `artifact-metadata`, all `write`. Leave every other job, and the
      workflow's default, read-only. Verify that
      `grep -n -E "(contents|id-token|attestations|artifact-metadata): write" .github/workflows/*.yml`
      finds them only in the `release` job
- [x] 11.2 Add `actions/attest` to the `release` job, pinned by commit, before the release is
      created (D12): provenance for `artifacts/*`, then one bill-of-materials attestation per tool
      over its five downloads. Verified: `actionlint` reports no error for `release.yml`, and the
      dry run built and checked everything before skipping the `release` job, as a dry run should.
      The attestations themselves are made by a release, so the first release is what shows them
      working
- [x] 11.3 Add to the README's "Get it" how to verify a download and get its bill of materials,
      with the two commands of D12. Verify the repository name and predicate type match D12, and
      that `ReadmeUsageTests` still passes

## 12. Documentation and closing checks

- [x] 12.1 Update the README:
      - the Licence section points at `LICENSE`, `NOTICE`, `THIRD-PARTY-NOTICES.txt`,
        `--license`, `--notices` and the Help menu
      - Building says a release takes its version from its tag (`-p:ReleaseTag=v1.2.3`), and
        what an untagged build reports

      Verify `ReadmeUsageTests` passes and each linked file exists
- [x] 12.2 Run `dotnet test` with and without `-p:InvariantGlobalization=false` on this Mac, and
      verify both pass. Verify the pull request's CI: its test job passes on Linux, both ways, and
      every other job passes every step it can. What no job could do at the time is upload, which
      the first release shows
- [x] 12.3 Record under "What the implementation settled" in design.md:
      - what the build task looks like
      - the notices' sources and runtime pack version
      - the refusal texts
      - what the CycloneDX tool gave in 10.1, and the tool version pinned
      - that the attestations are first shown working by a release
      - anything that departed from D1 to D12

      Verify `openspec validate prepare-public-release --strict` passes
