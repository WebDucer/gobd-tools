## Context

See proposal.md for why. The state this design builds on:

- **The reader publishes as a folder.** CI and the release run `dotnet publish src/GoBd.Reader.Ui
  --self-contained true -r <rid>` on a runner of the matching OS (`ubuntu-latest`,
  `windows-latest`, `macos-latest`). `package-reader.sh` zips the folder, because the artifact
  upload drops executable bits, and `verify-reader-publish.sh` checks that the launcher, runtime
  and store are present. That was D14 of `add-export-reader`, which this design supersedes.
- **The validator keeps its jobs.** It publishes with NativeAOT, which cannot compile for another
  operating system, so it keeps a job per platform. This change gives it its identifier (D9) and
  two more jobs, for ARM64 (D10). A build guard keeps the store out of it
  (`Directory.Build.targets`).
- **Most of the size is native.** Of 223 MB on macOS, native libraries are 147 MB, and
  `libduckdb.dylib` alone is 111 MB. Managed assemblies are 75 MB in 205 files.
- **The macOS native libraries carry Intel code.** `libduckdb`, `libSkiaSharp`, `libHarfBuzzSharp`
  and `libAvaloniaNative` arrive as universal binaries, although the reader is only built for
  `osx-arm64`. The runtime's own libraries are arm64 only.
- **A Windows build carries 100 MB of native symbols.** `libSkiaSharp.pdb` (80 MB) and
  `libHarfBuzzSharp.pdb` (20 MB) come from `runtimes/win-x64/native/` in
  `SkiaSharp.NativeAssets.Win32` and `HarfBuzzSharp.NativeAssets.Win32`.
- **The reader names itself in code.** `App.Initialize` sets `Name = "GoBD Reader"`, because
  there is no `Info.plist` for macOS to read.
- **Stores are already claimed and cleaned up.** `StoreLock` holds a `.owner` file open with no
  sharing, and `ExportStore.RemoveAbandonedStores` deletes only the store directories whose claim
  it can take itself. D2 reuses that idea.
- **The pipeline is warning-free by rule.** `Directory.Build.props` treats every warning as an
  error and keeps `NoWarn` empty, and both workflows publish with `-warnaserror`.
- **The store's .NET binding passes only text.** `ExportStore.Query` binds every parameter as a
  string, and no statement selects a `LIST`, `MAP` or `STRUCT` column. D3 relies on this.

## Goals / Non-Goals

**Goals:**
- `dotnet publish -r <rid>` alone produces the shape a person downloads, so a local build and a
  pipeline build are the same thing.
- The macOS job needs no checkout and no .NET, and finishes in about a minute.
- No build warnings, except one exception that is confined to `DuckDB.NET.Data`.
- The reader never deletes an unpacked copy it has not proven unowned.

**Non-Goals:**
- Bit-for-bit reproducible builds.
- A styled disk image: no background picture or arranged window.
- A smoke start of the Windows or macOS build, which would need those runners again.

## Decisions

### D1 — One file on Windows and Linux; an application bundle, not one file, on macOS

On Windows and Linux the reader publishes as a single file that unpacks its native libraries:
`PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract` and `EnableCompressionInSingleFile`.
Managed assemblies are loaded from inside the file. The native libraries are unpacked on first
start beneath `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, or else `~/.net` on Linux and `%TEMP%\.net` on
Windows, and are reused afterwards.

On macOS the reader publishes self-contained, as today, and the bundle holds that output in
`Contents/MacOS`.

- **Why not a single file inside the bundle:** its native libraries would be unpacked outside the
  bundle and outside its signature, and the bundle is already one thing to the person.
- **The shape follows the runtime identifier in the project file,** not flags passed by the
  pipeline. The single-file properties are set when `RuntimeIdentifier` starts with `win-` or
  `linux-`.
- **Measured in trial builds:**

  | Download | Today | One file | One file, trimmed (D3) |
  | --- | --- | --- | --- |
  | Windows x64 | 82 MB zip | 59 MB | 34 MB |
  | Windows ARM64 | — | — | 35.4 MB (D10) |
  | Linux x64 | 65 MB zip | 68 MB | 44 MB |
  | Linux ARM64 | — | — | 41.6 MB (D10) |
  | macOS | 77 MB zip | — | 24 MB disk image (D6) |

  Unpacked on first start: about 53 MB on Windows x64 and 81 MB on Linux x64, the size of the
  native libraries. The Linux ARM64 build unpacked 73 MB in the container.
- **Alternatives rejected:**
  - An executable with the native libraries beside it, zipped: nothing is unpacked, but it is not
    one file.
  - AppImage on Linux: it needs FUSE on the person's machine.
  - An installer (MSI, MSIX, pkg): the person has to install before they can start.

### D2 — Unpacked copies of other versions are removed only when proven unowned

The runtime unpacks a version into `<base>/gobd-reader/<bundle id>/`, for example
`~/.net/gobd-reader/IyOuOQA-puCM/` in the trial. When the reader starts from such a copy, it does
three things:

1. **Finds its own copy.** Task 2.1 settles how. Candidates are the `NATIVE_DLL_SEARCH_DIRECTORIES`
   runtime property and the directory a native library actually loads from. Knowing the copy is
   also how the reader knows it runs from a build that unpacks itself; a folder build or the macOS
   bundle has none, and removes nothing.
2. **Claims its own copy** with a `StoreLock` on that directory, held for the life of the process.
3. **Removes every sibling** under the same `gobd-reader` directory for which `StoreLock.IsUnowned`
   holds, except a sibling that has never been claimed and was written less than ten minutes ago.
   The runtime unpacks into a directory named after its process id and renames it when complete,
   and a reader claims its copy only once its application has started. A copy like that may be
   either, and deleting it makes that reader fail to start (see "What the implementation
   settled"). A sibling that was claimed and released is a finished copy of a reader that has
   quit, and is removed whatever its age.

This work runs in the background after the window opens, and any failure is ignored until the next
start.

- **Why a claim and not age or a version comparison:** the store's engine loads when an export is
  opened, not at start. A reader of another version that is running but has not opened an export
  yet has not loaded `libduckdb`. Deleting its copy would make its next opening fail, and neither
  age nor version can see that. It is the argument `StoreLock` already makes for stores.
- **Only siblings are touched,** never the base directory and never another application's
  directory beside `gobd-reader`.
- **Alternative rejected: leaving copies in place.** The person accepted that at first; removing
  them fits a reader that deletes its stores.

### D3 — Trimmed, with `DuckDB.NET.Data` kept whole and its warnings confined

The reader publishes with `PublishTrimmed` in full mode. That took managed code from 75 MB in 205
files to 12 MB in 62. Trimming reported warnings from two places: the three grid bindings (D4) and
six in `DuckDB.NET.Data`, from reflection in its `LIST`, `MAP` and `STRUCT` readers, list-valued
parameters, and one annotation mismatch.

`DuckDB.NET.Data` is kept out of trimming with `TrimMode=copy` on its `ManagedAssemblyToLink` item,
set in a target before `PrepareForILLink`. Keeping it whole does not remove its warnings, because
the trimmer still analyses the code it keeps:

| Setting for `DuckDB.NET.Data` | The assembly | Its warnings |
| --- | --- | --- |
| trimmed | trimmed | 6 |
| `TrimMode=copy` | unchanged, 166,912 bytes as in the package | 11 |
| `TrimmerRootAssembly` | rewritten, 165,376 bytes | 11 |

So its warnings are confined rather than hidden:

```
  DuckDB.NET.Data   TrimMode=copy            left exactly as shipped
                    TrimmerSingleWarn=true   its warnings collapse into one IL2104
  every assembly    TrimmerSingleWarn=false  any other warning reported in full, with its code
  reader project    NoWarn += IL2104         IL2104 can only come from DuckDB.NET.Data
```

In the trial with these settings, the only warnings left were the grid bindings.

- **Why suppressing is safe here:** the paths the warnings point at take nested column types or
  collection parameters, and the reader uses neither (Context). This has to be re-checked when
  `DuckDB.NET.Data` is upgraded, and the project file says so beside the setting.
- **`IL2104` is appended in the project file**, never set with `-p:NoWarn`. On the command line
  it replaced the SDK's own suppression of `IL2121`, which then surfaced in the trial.
- **Setting `PublishTrimmed` in the project also turns on the trim analyzer in `dotnet build`,**
  so a new reflection-based use in the reader fails the ordinary build and the tests, not only a
  publish.
- **Alternatives rejected:**
  - No trimming: downloads 25 MB (Windows) to 18 MB (macOS) larger.
  - `SuppressTrimAnalysisWarnings`: it hides every library's warnings, not one.

### D4 — Grid columns bind without reflection

Avalonia has no `x:Bind`. Its compiled bindings are `x:DataType` with `{CompiledBinding}` in XAML,
and `CompiledBinding.Create` in C#, which Avalonia 12.1.2 provides. The reader builds its interface
in code, so the C# form applies. `AvaloniaUseCompiledBindingsByDefault` affects only XAML, of which
the reader has none.

- **`#` and Record** become `CompiledBinding.Create((RecordRow row) => row.Position)` and the same
  for `Ordinal`.
- **Value columns** become `CompiledBinding.Create((RecordRow row) => row.Values[column])`, with
  the index copied into a local per column. Avalonia documents indexers in these expressions, but
  not a captured variable as the index. If that is refused, a value column uses a
  `FuncDataTemplate` that sets the text directly, as the foreign-key cells already do.

### D5 — Symbols are embedded; native symbol files are removed from the publish

- **Our assemblies:** `DebugType=embedded` in `Directory.Build.props`. The symbols (about 115 KB of
  portable PDB) move inside the assemblies, so a stack trace in a bug report keeps its file and
  line numbers, and a single file has nothing beside it.
- **Native symbol files:** NuGet copies everything under `runtimes/<rid>/native/` as a native
  library, so `DebugType` does not reach them; every trial build used `DebugType=none` and still
  had them. A target after `ComputeResolvedFilesToPublishList` removes `.pdb` items from
  `ResolvedFileToPublish` in the reader project.
- **Enforced:** `verify-reader-publish.sh` fails on any `.pdb`, so a future package cannot bring
  one back unnoticed.

### D6 — The macOS bundle is assembled on Linux and finished on macOS

```
  GoBD Reader.app/
    Contents/
      Info.plist        from src/GoBd.Reader.Ui/macOS/Info.plist, version filled in
      MacOS/            the osx-arm64 publish output, gobd-reader as CFBundleExecutable
```

**`Info.plist` keys:**
- `CFBundleName` and `CFBundleDisplayName`: `GoBD Reader`
- `CFBundleExecutable`: `gobd-reader`
- `CFBundleIdentifier`: `de.webducer.gobd.reader` (D9). Signing the bundle ad hoc takes it over as
  the signature's identifier, as the trial showed.
- `CFBundlePackageType`: `APPL`
- `CFBundleShortVersionString` and `CFBundleVersion`: the release tag without its `v`, and `0.0.0`
  for other builds
- `LSMinimumSystemVersion`: `14.0`, the oldest macOS .NET 10 supports
- `NSHighResolutionCapable`: true
- No icon key.

**Two scripts, both runnable on a developer's Mac:**
- **`assemble-reader-app.sh <publish-dir> <out> <version>`** builds the layout and archives it
  with `tar`, which keeps modes. It runs on Linux in the pipeline.
- **`make-reader-dmg.sh <app-archive> <dmg>`** runs on macOS:
  1. Unpacks the archive.
  2. Thins every Mach-O file whose `lipo -archs` names more than `arm64`. It asks each file rather
     than keeping a list; today that is four files, and `libduckdb` goes from 111 MB to 54 MB.
  3. Signs the bundle ad hoc with `codesign --force --deep --sign -`.
  4. Checks it with `codesign --verify --deep --strict`.
  5. Makes the image with `hdiutil create -format ULMO` from a folder that holds the application
     and a link to `/Applications`.

- **Why sign the whole bundle:** as assembled, `codesign --verify` rejects it with "code has no
  resources but signature indicates they must be present". The launcher's own signature does not
  cover `Info.plist`. After signing the bundle ad hoc it verifies, with 82 sealed files. Gatekeeper
  still refuses it without a Developer ID, as it refuses the zip today.
- **Why thin on macOS:** `lipo` is part of the system there, while `llvm-lipo` on the Linux runner
  is unverified. Thinning keeps each slice's signature: DuckDB's Developer ID signature, Skia's
  and Avalonia's ad-hoc one all still verify afterwards.
- **Why ULMO:** of the trial images, UDZO was 61.9 MB, ULFO 55.9 MB and ULMO 42.0 MB before
  trimming, and 23.9 MB after. ULMO needs macOS 10.15, below the minimum.
- **`App.Name` stays** for Windows and Linux, and its comment says macOS now reads the name from
  `Info.plist`.
- **The command line on macOS** runs the executable inside the bundle:
  `"/Applications/GoBD Reader.app/Contents/MacOS/gobd-reader" ./export.zip`. With
  `open -a … --args`, a relative path would be resolved against `/`, not the shell's directory.

### D7 — The reader is built on Linux, and only its disk image on macOS

A Linux container (SDK 10.0.401) produced correct builds for both other platforms:
- **macOS:** a launcher the SDK signed ad hoc with its own managed signer, which verifies with
  `codesign` on macOS.
- **Windows:** a single file with the GUI subsystem and the manifest embedded.

Both `ci.yml` and `release.yml` get the same three jobs for the reader:

```
  reader (ubuntu-latest)                        reader-dmg (macos-latest, needs: reader)
  +-----------------------------------------+   +--------------------------------------+
  | publish linux-x64, linux-arm64,         |   | download the bundle archive          |
  |   win-x64, win-arm64, osx-arm64         |-->| make-reader-dmg.sh                   |
  | verify each shape                       |   | checksum, upload                     |
  | smoke-start linux-x64 (D8)              |   +--------------------------------------+
  | assemble-reader-app.sh                  |
  | upload: executable file x4 + checksums, |   reader-arm64-smoke (ubuntu-24.04-arm,
  |         bundle archive                  |-->  needs: reader)
  +-----------------------------------------+   +--------------------------------------+
                                                | smoke-start linux-arm64 (D8)         |
                                                +--------------------------------------+
```

- **Published names:** `gobd-reader-linux-x64`, `gobd-reader-linux-arm64`,
  `gobd-reader-win-x64.exe`, `gobd-reader-win-arm64.exe` and `gobd-reader-osx-arm64.dmg`, each
  with a `.sha256`. The bundle archive is only handed from one job to the next.
- **The disk image is made in CI too, not only for releases.** The spec asks for a downloadable
  build per platform for every revision. A bundle that has not been signed as a whole is not
  one, and the CI build is the one a person tries within 48 hours. The macOS job does no restore
  and no publish, so it costs less than today's macOS reader job. If that minute ever matters more,
  a condition on the job limits it to releases.
- **The validator keeps its jobs, and gains two.** Its macOS job re-signs the binary with the
  validator's identifier (D9). `linux-arm64` on `ubuntu-24.04-arm` and `win-arm64` on
  `windows-11-arm` join the matrix (D10), and the manifest check and the operating-system language
  step run for every `win-*` identifier. The reader no longer uses a Windows runner. Neither of its
  Windows builds is started in CI, as the x64 one is not today; D8 does not reach them.

### D8 — The Linux builds are started in the pipeline

The Linux job starts the published `linux-x64` single file under `xvfb-run`, installing `xvfb` if
the runner lacks it. `DOTNET_BUNDLE_EXTRACT_BASE_DIR` and `TMPDIR` point into the job's workspace,
and the reader is given `tests/GoBd.Validation.Tests/Fixtures/good-export`. The step passes once a
store directory with its `store.duckdb` appears under `$TMPDIR/gobd-reader`, then ends the process.
It fails after 60 seconds. A `reader-arm64-smoke` job on `ubuntu-24.04-arm` does the same for the
`linux-arm64` file the reader job built.

- **What it proves:** the single file unpacked its native libraries, the trimmed reader started,
  the store's engine loaded, and the import began. Those are the parts trimming and unpacking
  could break without a warning.
- **What it does not prove:** that the Windows and macOS builds start, or that the grid and its
  filters work trimmed. The headless UI tests run untrimmed.
- **Alternatives rejected:**
  - Starting the Windows and macOS builds, which puts those runners back in the reader's pipeline.
  - Starting the ARM64 file on the x64 runner under QEMU, which emulates every instruction of a
    .NET start-up.

### D9 — One identifier per tool, wherever a platform takes one

The reader is `de.webducer.gobd.reader` and the validator is `de.webducer.gobd.cli`, the same on
every platform:

| | macOS | Windows | Linux |
| --- | --- | --- | --- |
| Reader | `CFBundleIdentifier` in `Info.plist`, which the bundle's ad-hoc signature takes over (D6) | `assemblyIdentity name` in `src/GoBd.Reader.Ui/app.manifest`, until now `gobd-reader` | none |
| Validator | its ad-hoc signature, re-signed with `codesign --force --sign - --identifier de.webducer.gobd.cli` | `assemblyIdentity name` in a new `src/GoBd.Validation.Cli/app.manifest`, set as `ApplicationManifest` | none |

- **The manifests reach the executables.** The reader's single file carried its manifest when
  cross-built on Linux (D7). NativeAOT copies Windows resources from the compiled assembly into the
  validator's `.exe` (`--win32resourcemodule` in `Microsoft.NETCore.Native.targets`).
- **Why the validator is re-signed on macOS:** an identifier is part of a code signature, and only
  signing again replaces the one the linker wrote. The macOS validator job already runs on macOS,
  so the step goes there, before its smoke tests, which then run the re-signed binary.
- **Linux has no place for it.** A single executable carries no identifier, and Avalonia 12.1.2's
  X11 options offer no window class. A desktop entry would carry it, and is outside this change.

### D10 — ARM64 on Windows and Linux; Intel Macs stay out

Both tools are built for five platforms: `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64` and
`osx-arm64`. An ARM64 Linux machine cannot run an x64 build at all, and Windows on ARM runs one
only through emulation.

- **The reader needs nothing new.** Every native library it carries ships both ARM64 builds:
  `DuckDB.NET.Bindings.Full` 1.5.5, the Skia and HarfBuzz native asset packages, and
  `Avalonia.Angle.Windows.Natives`. Trial publishes on macOS gave a 35.4 MB Windows ARM64 GUI
  program and a 41.6 MB Linux ARM64 file, both warning-free and passing
  `verify-reader-publish.sh`. They come from the Linux job with the others (D7).
- **The validator is built on ARM64 runners.** NativeAOT compiles for another architecture only
  with a cross toolchain on the runner, and an ARM64 binary cannot run its smoke tests on an x64
  runner. GitHub's standard ARM64 runners, `ubuntu-24.04-arm` and `windows-11-arm`, have been
  available to private repositories since 29 January 2026. They have 2 vCPUs and count towards the
  plan's included minutes. Whether their images carry what NativeAOT links with is verified on the
  first run.
- **Intel Macs stay out.** Apple is ending support for them. An `osx-x64` build would need a second
  disk image or a universal bundle, for machines on their way out.
- **Alternatives rejected:**
  - Cross-compiling the validator on the x64 runners: it needs a toolchain per target installed on
    every run, and leaves the ARM64 binaries without smoke tests.
  - Linux ARM64 only: Windows on ARM runs the x64 builds, but emulated, and the reader's Windows
    ARM64 build costs the Linux job one more publish.

## Risks / Trade-offs

- **[Trimming removes a member that is reached by reflection without a warning]** → The analysis is
  only as good as the libraries' annotations. Warnings as errors, the analyzer in every build (D3)
  and the smoke start (D8) cover what can be covered. A trimmed grid is not exercised by any test.
- **[A `DuckDB.NET.Data` upgrade starts using reflection the reader does reach]** → Its warnings are
  suppressed, so the build would not say. The comment beside the setting asks for the D3 check on
  every upgrade, and the smoke start covers opening and importing.
- **[The first start on Windows and Linux is slower]** → 53 to 81 MB are written once, and later
  starts reuse them. Task 2.2 measures it.
- **[An older reader from before this change is running during an update]** → It holds no claim,
  so a newer reader may remove its copy, and its next opening fails until it is restarted. This
  happens once, and only with two versions side by side.
- **[Cleanup removes a copy that another version is still unpacking, or has not claimed yet]** →
  Measured: a second version started just after the first one's window opened failed to start. A
  copy never claimed is therefore left alone for ten minutes after it was written (D2). Should it
  still happen, the next attempt succeeds, because the runtime unpacks again.
- **[The unpack location is not writable, or `$HOME` is unset on Linux]** → The runtime fails
  before any reader code runs. The README names `DOTNET_BUNDLE_EXTRACT_BASE_DIR` for this.
- **[`codesign --deep`]** → Apple discourages it for signing that is distributed with an identity.
  For an ad-hoc signature it is sufficient. A later change that signs with a Developer ID signs
  from the inside out.
- **[Browsers drop the executable bit of the Linux download]** → The README says to
  `chmod +x` it.
- **[Neither Windows single file is started before release]** → As today for x64. Their subsystem
  and manifest are checked instead.
- **[An ARM64 runner image lacks what NativeAOT links with]** → The first run on each shows it. A
  step installs clang and zlib on `ubuntu-24.04-arm`, or Visual Studio's ARM64 C++ tools on
  `windows-11-arm`, only if the image lacks them.
- **[ARM64 runners in a private repository have 2 vCPUs]** → Their jobs take longer than the x64
  ones. The durations are recorded after the first run.

## Migration Plan

- **Release notes:** the reader's artifact names change from `gobd-reader-<rid>.zip` to
  `gobd-reader-linux-x64`, `gobd-reader-linux-arm64`, `gobd-reader-win-x64.exe`,
  `gobd-reader-win-arm64.exe` and `gobd-reader-osx-arm64.dmg`, and the validator gains
  `gobd-validate-linux-arm64` and `gobd-validate-win-arm64.exe`. Earlier releases keep their zips.
- **People with an unpacked folder from an earlier release** delete it themselves; nothing reaches
  into it.
- **Rollback:** revert the workflows, the scripts and the project settings. The cleanup and the
  bindings can stay, because a folder build does not unpack itself and removes nothing.

## What the implementation settled

### Value columns bind with a captured index

`CompiledBinding.Create((RecordRow row) => row.Values[declared])` accepts a copy of the index made
per column, so no value column needed the `FuncDataTemplate` of D4. `GridCellTests` reads the
realised cells: `1|1|A|10,00` in file order, and `1|2|B|20,00` for the first record of a filtered
view, where `#` and Record differ.

### A single file names its unpacked copy in `NATIVE_DLL_SEARCH_DIRECTORIES`

Measured with a throwaway probe that loads DuckDB and asks `dladdr` which file the library was
loaded from:

| Build | `NATIVE_DLL_SEARCH_DIRECTORIES` | `AppContext.BaseDirectory` | DuckDB loaded from |
| --- | --- | --- | --- |
| single file, macOS, `DOTNET_BUNDLE_EXTRACT_BASE_DIR=…/x` | `…/x/Probe/45tncnxj1oXz/` | the executable's folder | `…/x/Probe/45tncnxj1oXz/libduckdb.dylib` |
| single file, Linux arm64, `DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/x` | `/tmp/x/Probe/GT9LYrg0pAVU/` | the executable's folder | `/tmp/x/Probe/GT9LYrg0pAVU/libduckdb.so` |
| single file, Linux arm64, default location | `/root/.net/Probe/GT9LYrg0pAVU/` | the executable's folder | `/root/.net/Probe/GT9LYrg0pAVU/libduckdb.so` |
| folder, macOS | the executable's folder | the executable's folder | beside the executable |

So the reader's own copy is the entry of `NATIVE_DLL_SEARCH_DIRECTORIES` that is not
`AppContext.BaseDirectory` and whose parent directory is named `gobd-reader`. A folder build,
the macOS bundle included, lists only its own folder there and finds no copy. The property is
split on the platform's path separator, so Windows reads it the same way, but it was not
measured on Windows.

### Native symbol files remain in build output, never in a publish

`DebugType=embedded` leaves no `.pdb` for any of the repository's assemblies. A build without a
runtime identifier still copies the Windows native symbols of Skia and HarfBuzz under
`bin/…/runtimes/win-*/native/`, as it copies every platform's native libraries. None of them
reaches a publish (1.5), and `verify-reader-publish.sh` fails on any that did.

### Starting the Linux single file

Measured on the development Mac, in a Linux arm64 container with 4 CPUs, under Xvfb. Each figure
runs from launch to the window being mapped, for the trimmed single file of this change:

| Start | Sample 1 | Sample 2 |
| --- | --- | --- |
| first, nothing unpacked | 1,095 ms | 979 ms |
| second, the copy reused | 708 ms | 709 ms |

Unpacking costs about 300 ms, once. The second start reused a copy that held the first reader's
claim file, so the runtime does not mind a file of the reader's own in it.

### A copy being unpacked is not an abandoned one

The first two-version check failed. It started a second version just after the first one's window
opened, and the second version died with `Failed to open file
[/tmp/unpack/gobd-reader/7ab/libHarfBuzzSharp.so] for writing`. `7ab` was the second reader's
process id, 1963, in hex. The runtime unpacks into a directory named after its process and renames
it to the bundle id when complete; the first reader's cleanup found that directory unclaimed and
deleted it while it was being written. The same gap lies between the rename and the new reader's
claim. So a copy that was never claimed is now left alone for ten minutes after it was written
(D2).

Checked again in the Linux arm64 container under Xvfb, with builds 1.0.1 and 1.0.2 of the reader:

| Sequence | Outcome |
| --- | --- |
| 1.0.1 started without an export, 1.0.2 started the moment its window opened, three times | both started every time, and both copies stayed |
| 1.0.1 quit, then 1.0.2 started again | 1.0.1's copy, claimed and released, was removed although minutes old; 1.0.2's stayed |

Whether a reader whose copy was kept can still open an export was judged by its copy staying
complete, `libduckdb.so` included, not by opening one: nothing can make a reader started in a
container open an export afterwards.

### The pipeline, measured

These are job durations from the last successful CI run on `main` before this change, and from this
change's CI run:

| Reader job | Before | After |
| --- | --- | --- |
| `linux-x64`, on Linux | 58 s | — |
| `win-x64`, on Windows | 118 s | — |
| `osx-arm64`, on macOS | 62 s | — |
| all three, on Linux | — | 196 s |
| disk image, on macOS | — | 47 s |

GitHub bills each job in whole minutes, a macOS minute as ten Linux minutes and a Windows minute as
two. On that basis the reader cost 1 + 2 × 2 + 2 × 10 = 25 Linux minutes a run before, and
4 + 1 × 10 = 14 after. The release dry run took 173 s on Linux and 49 s on macOS.
The validator's jobs kept their durations, except that its macOS job took 31 s against 56 s.

The Linux smoke start opened its store within 28 s in the CI run and within 5 s in the release run.
The first is closer to the 60-second limit than any local start came.

The downloads of the release dry run, each matching its `.sha256`, measure:

| Download | Size |
| --- | --- |
| `gobd-reader-win-x64.exe` | 36,294,778 bytes (34.6 MB) |
| `gobd-reader-linux-x64` | 46,946,011 bytes (44.8 MB) |
| `gobd-reader-osx-arm64.dmg` | 26,578,511 bytes (25.3 MB) |

The disk image built on the runners is 1 MB larger than the one built locally. A dry run skips the
attach job, so its `release-*` download pattern is first exercised by a tag.

### Checked by hand, and not

Checked on the development Mac with the disk image from the release dry run:

- the image verifies, and holds the application beside a link to `/Applications`
- the bundle's signature verifies as `de.webducer.gobd.reader`, and every Mach-O file is arm64 only
- launched from a copy, the application opened `good-export` within 3 s, and macOS names it
  "GoBD Reader"
- installed in `/Applications` by hand, it started once the download quarantine was cleared with
  `xattr`, as the README says, and it started quickly

Not checked:

- clearing the quarantine with *Open Anyway* in System Settings instead of `xattr`
- the Windows file on a Windows machine, since none was at hand. The Windows build was checked only
  by `verify-reader-publish.sh`: one file, a GUI program, a manifest naming
  `de.webducer.gobd.reader`.
- the Linux file on a Linux desktop, since none was at hand. The Linux build was checked by the
  smoke start, in CI on x64 and in a container on arm64.

### ARM64, measured

From the CI run of the commit that added ARM64, and the release dry run beside it. Both were
green, the two new validator jobs included.

| Download | Size |
| --- | --- |
| `gobd-reader-win-arm64.exe` | 37,163,319 bytes (35.4 MB) |
| `gobd-reader-linux-arm64` | 43,674,383 bytes (41.7 MB) |

Each matches its `.sha256`, as do the validator's `gobd-validate-linux-arm64` and
`gobd-validate-win-arm64.exe`, which the dry run staged beside the x64 ones. The disk image of
this run measures 26,353,863 bytes (25.1 MB).

| Job | CI | Release dry run |
| --- | --- | --- |
| validator, `linux-arm64` on `ubuntu-24.04-arm` | 56 s | 57 s |
| validator, `win-arm64` on `windows-11-arm` | 117 s | 140 s |
| reader, all five on `ubuntu-latest` | 230 s | 237 s |
| reader ARM64 smoke start on `ubuntu-24.04-arm` | 23 s | 20 s |
| reader disk image on `macos-latest` | 42 s | 41 s |

**Neither ARM64 runner needed anything installed.** Both Linux images reported "clang and zlib are
already on this image", and `windows-11-arm` carries Visual Studio 2022 Enterprise, with which
NativeAOT linked at the first attempt. The step that reports the toolset stays, so a future image
without it can be read from the log rather than guessed at.

The ARM64 reader unpacked 73 MB and opened its store within 6 s, where the x64 one took 7 s.
