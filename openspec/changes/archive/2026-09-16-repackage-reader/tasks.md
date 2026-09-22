## 1. Build configuration

- [x] 1.1 Set `DebugType` to `embedded` in `Directory.Build.props` (design.md D5); verify a clean
      Release build leaves no `.pdb` under any `bin/Release`, and that `dotnet test` passes
- [x] 1.2 Replace the three reflection bindings in `TableTabView` (D4): `CompiledBinding.Create` for
      `#` and Record, and for value columns with a per-column copy of the index, falling back to a
      `FuncDataTemplate` that sets the text if Avalonia refuses the captured index. Record which
      form the value columns use under "What the implementation settled" in design.md. Verify
      with a headless test that reads the `#`, Record and a value cell of the first rows, adding
      one if none does yet, and that the existing headless tests still pass
- [x] 1.3 Turn on trimming in `GoBd.Reader.Ui.csproj` (D3): `PublishTrimmed`, `TrimmerSingleWarn`
      off, `IL2104` appended to `NoWarn`, and a target before `PrepareForILLink` giving
      `DuckDB.NET.Data` `TrimMode=copy` and `TrimmerSingleWarn=true`, with a comment asking for
      the D3 check on every `DuckDB.NET.Data` upgrade. Verify `dotnet build` is warning-free with
      the trim analyzer on. Verify `dotnet publish -r win-x64 -warnaserror` succeeds, and that
      its `DuckDB.NET.Data.dll` has the same SHA-256 as the one in the NuGet package
- [x] 1.4 Set `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`,
      `EnableCompressionInSingleFile` and `SelfContained` in the reader project when
      `RuntimeIdentifier` starts with `win-` or `linux-` (D1). Verify that `dotnet publish -r
      linux-x64` and `-r win-x64` each leave exactly one file in the publish directory, and that
      `-r osx-arm64` still leaves a self-contained folder
- [x] 1.5 Remove `.pdb` items from `ResolvedFileToPublish` in the reader project (D5); verify the
      `win-x64` publish carries neither `libSkiaSharp.pdb` nor `libHarfBuzzSharp.pdb`, and that
      the `osx-arm64` folder carries no `.pdb`
- [x] 1.6 Rename the identity in `src/GoBd.Reader.Ui/app.manifest` from `gobd-reader` to
      `de.webducer.gobd.reader` (D9); verify the `win-x64` single file carries a manifest with that
      name

## 2. Unpacked copies of other versions

- [x] 2.1 Establish how a running single-file reader finds the directory its native libraries were
      unpacked to (D2), trying the `NATIVE_DLL_SEARCH_DIRECTORIES` runtime property and the path
      a native library loads from. The same answer must say "none" for a folder build and for the
      macOS bundle. Use a throwaway diagnostic, not committed. Record the answer in design.md.
      Verify on a single-file build started with `DOTNET_BUNDLE_EXTRACT_BASE_DIR` pointing at a
      temporary directory, and on a folder build
- [x] 2.2 Measure the first and the second start of the Linux single file, from launch to the
      window, with the unpack location empty and then filled. Record both in design.md; verify
      the numbers are recorded with the machine they were taken on
- [x] 2.3 Implement removal of unowned sibling copies (D2): find the own copy, claim it with
      `StoreLock` for the life of the process, and delete each sibling under the same
      `gobd-reader` directory for which `StoreLock.IsUnowned` holds, ignoring any failure. Verify
      with unit tests over a temporary base directory:
      - an unowned sibling is removed
      - a sibling whose claim a test holds is kept
      - the own copy is kept
      - a directory beside `gobd-reader` is untouched
      - a file that cannot be deleted raises nothing
      - with no own copy found, nothing is removed
- [x] 2.4 Run the removal from startup in the background once the window has opened; verify with
      two single-file builds that differ in version:
      - while the first runs without an export opened, starting the second keeps the first's copy,
        and the first can still open `good-export`
      - once the first has quit, starting the second removes the first's copy

## 3. macOS bundle and disk image

- [x] 3.1 Add `src/GoBd.Reader.Ui/macOS/Info.plist` with the keys of D6, the bundle identifier
      `de.webducer.gobd.reader` and a version token; verify `plutil -lint` passes once the version
      is filled in
- [x] 3.2 Write `.github/scripts/assemble-reader-app.sh <publish-dir> <out> <version>` (D6). Verify
      that, run on Linux, it produces an archive which, unpacked on macOS, holds
      `GoBD Reader.app/Contents/MacOS/gobd-reader` with its executable bit, and an `Info.plist`
      carrying the given version
- [x] 3.3 Write `.github/scripts/make-reader-dmg.sh <app-archive> <dmg>` (D6). Verify on a Mac:
      - `lipo -archs` reports only `arm64` for every Mach-O file in the bundle
      - `codesign --verify --deep --strict` passes, `codesign -dv` reports
        `Identifier=de.webducer.gobd.reader`, and `hdiutil verify` passes on the image
      - mounting the image shows the application beside a link to `/Applications`
      - the application copied to `/Applications` starts, the menu bar shows "GoBD Reader", and it
        opens `good-export`
- [x] 3.4 Correct the comment on `Name` in `App.Initialize`: macOS reads the name from `Info.plist`,
      and the property remains for Windows and Linux. Verify `dotnet build` is warning-free

## 4. Checks for the pipeline

- [x] 4.1 Rewrite `.github/scripts/verify-reader-publish.sh` for the new shapes:
      - Windows and Linux: exactly one file, executable on Linux; the Windows file a GUI program
        whose manifest names `de.webducer.gobd.reader`
      - macOS: the folder carries its launcher, runtime and store
      - no `.pdb` anywhere
      Verify it passes on each publish from 1.4, and fails on a copy with a stray `.pdb` and on a
      Linux publish with a second file beside it
- [x] 4.2 Write `.github/scripts/smoke-start-reader.sh` (D8): start the Linux single file under
      `xvfb-run` with `good-export`, with the unpack location and `TMPDIR` in a work directory,
      and pass once `gobd-reader/*/store.duckdb` appears, failing after 60 seconds. Verify in a
      Linux container with `xvfb` that it passes, and that it fails within the timeout when given
      a path that holds no export

## 5. Pipeline

- [x] 5.1 Replace the reader matrix in `ci.yml` (D7):
      - a `reader` job on `ubuntu-latest` publishes the three runtime identifiers, runs 4.1 and 4.2,
        assembles the bundle with version `0.0.0`, and uploads the two single files and the bundle
        archive
      - a `reader-dmg` job on `macos-latest`, needing `reader`, without checkout or .NET setup, runs
        3.3 and uploads the disk image
      - every artifact kept for 2 days
      Verify on a CI run of this branch: all three builds are downloadable, and the macOS job's
      duration is recorded in design.md
- [x] 5.2 Make the same change in `release.yml`, with a `.sha256` beside each published file and
      the version taken from the tag without its `v`. Verify with a `workflow_dispatch` dry run that
      the attach step lists `gobd-reader-linux-x64`, `gobd-reader-win-x64.exe` and
      `gobd-reader-osx-arm64.dmg`, each with its checksum
- [x] 5.3 Delete `.github/scripts/package-reader.sh`; verify nothing in the repository still refers
      to it
- [x] 5.4 Add `src/GoBd.Validation.Cli/app.manifest` naming `de.webducer.gobd.cli`, and set it as
      `ApplicationManifest` in `GoBd.Validation.Cli.csproj` (D9). Verify in the Windows validator
      job that the NativeAOT `gobd-validate.exe` carries a manifest with that name, and that its
      smoke tests still pass
- [x] 5.5 In the macOS validator jobs of `ci.yml` and `release.yml`, re-sign the binary with
      `codesign --force --sign - --identifier de.webducer.gobd.cli` before its smoke tests (D9).
      Verify in the job that `codesign -dv` reports that identifier and `codesign --verify` passes
- [x] 5.6 Verify the validator's jobs are otherwise unchanged: the only changes to their
      steps are 5.4's manifest check and 5.5, and the validator's artifact names and single-file
      shape are the same as before

## 6. Documentation and final verification

- [x] 6.1 Rewrite the README's "Get it" for the reader:
      - the three downloads with the sizes of the build from 5.1
      - the first start on each platform: open the disk image and drag the application to
        Applications, then *Open Anyway* or `xattr -dr com.apple.quarantine "/Applications/GoBD
        Reader.app"`; SmartScreen's *Run anyway*; `chmod +x`
      - where the unpacked copy lives, `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, and that copies of other
        versions are removed
      Update "Use it" with the command line on macOS (D6), and "Building" with the publish shapes
      and the two bundle scripts. Verify `ReadmeUsageTests` passes, and that every size quoted
      matches the artifacts of the CI run
- [x] 6.2 Run `dotnet build` and confirm zero warnings, then `dotnet test` and confirm every suite
      passes. From the CI run of 5.1:
      - open the disk image on a Mac and open `good-export` in the installed application
      - start the Windows file on a Windows machine and open `good-export`; if no Windows machine is
        at hand, record that the Windows build was checked only by 4.1
      Record in design.md what was verified by hand and what was not

## 7. ARM64 on Windows and Linux

- [x] 7.1 Publish `linux-arm64` and `win-arm64` in the reader jobs of `ci.yml` and `release.yml`
      (design.md D10), run `verify-reader-publish.sh` on both, and upload them as
      `gobd-reader-linux-arm64` and `gobd-reader-win-arm64.exe`, with a `.sha256` beside each in the
      release. Verify on a CI run that five reader builds are downloadable
- [x] 7.2 Add a `reader-arm64-smoke` job on `ubuntu-24.04-arm`, needing `reader`, that starts
      `gobd-reader-linux-arm64` with `good-export` through `smoke-start-reader.sh` (D8). Verify it
      passes in CI, and record its duration in design.md
- [x] 7.3 Add `linux-arm64` on `ubuntu-24.04-arm` and `win-arm64` on `windows-11-arm` to the
      validator's matrix in both workflows (D10). Run the manifest check and the operating-system
      language step for every `win-*` identifier, and install what NativeAOT links with only if an
      image lacks it. Verify that both jobs publish warning-free and pass their smoke tests, and
      that the release dry run stages `gobd-validate-linux-arm64` and `gobd-validate-win-arm64.exe`,
      each with its checksum
- [x] 7.4 Update the README: the five reader downloads with the sizes of the CI run from 7.1, and
      the five supported runtime identifiers in "Building". Verify `ReadmeUsageTests` passes, and
      that every size quoted matches the CI artifacts
- [x] 7.5 Record under "What the implementation settled" in design.md the ARM64 download sizes,
      the durations of the ARM64 jobs, and whether either ARM64 runner needed anything installed;
      verify the entries are there
