# Ship the reader as one file, and as an application on macOS

## Why

The reader downloads as a zip of about 230 files: 65 to 82 MB, and 170 to 240 MB once unpacked. On
macOS it is not an application at all but an executable in a folder, which Finder shows as a
Unix program. Someone handed a medium wants one thing to download and start, as the validator
already is. Measured on the current reader, the same content fits in one file of 34 MB on Windows
and 44 MB on Linux, and in a disk image of 24 MB on macOS.

Both tools also reach only x64 on Windows and Linux. An ARM64 Linux machine cannot run either of
them at all, and Windows on ARM runs them only through emulation.

It is also the reader's builds that keep the pipeline on expensive runners. A macOS minute costs
ten Linux minutes on this private repository, and every CI run and release publishes the reader
on a macOS runner and a Windows runner, although nothing in its build needs either.

## What Changes

- **Windows and Linux: one executable file, for x64 and ARM64.** `gobd-reader-win-x64.exe`,
  `gobd-reader-win-arm64.exe`, `gobd-reader-linux-x64` and `gobd-reader-linux-arm64`,
  self-contained, with nothing beside them. On first start the native libraries (the store's
  engine, Skia, HarfBuzz) are unpacked beneath `%TEMP%\.net` or `~/.net`, about 55 MB on Windows
  and 80 MB on Linux, and reused on every later start.
- **Unpacked copies of other versions are removed** when the reader starts, unless a reader that
  is still running uses them.
- **macOS: an application in a disk image.** `gobd-reader-osx-arm64.dmg` holds
  `GoBD Reader.app`. The bundle carries the `Info.plist` that names the application, its native
  libraries keep only their arm64 code (the Intel code is 66 MB nothing runs), and the bundle is
  signed as a whole, ad hoc, so macOS can verify it.
- **Trimmed.** Unused managed code is removed at publish, taking it from 75 MB to 12 MB. The
  store's .NET binding, `DuckDB.NET.Data`, is kept exactly as shipped, and its trim warnings are
  suppressed: the one exception to the zero-warnings rule, and confined to that assembly. The
  three grid bindings that resolve by reflection become compiled bindings.
- **No debug symbol files are shipped.** The 100 MB of native symbols the Skia and HarfBuzz
  packages put beside a Windows build are dropped, and the reader's own symbols are embedded in
  its assemblies, so a stack trace keeps its file and line numbers.
- **Identifiers.** The reader identifies itself as `de.webducer.gobd.reader`, and the validator
  as `de.webducer.gobd.cli`, wherever a platform has a place for one: the bundle identifier and
  its signature on macOS, and the application manifest on Windows. A Linux executable has no such
  place.
- **ARM64 on Windows and Linux, for both tools.** The reader's ARM64 builds come from the same
  Linux job as its others. The validator gains NativeAOT jobs on GitHub's ARM64 runners,
  `ubuntu-24.04-arm` and `windows-11-arm`, where its smoke tests run natively. The Linux ARM64
  reader is started on `ubuntu-24.04-arm`.
- **Built on Linux.** One Linux job publishes the reader for all five platforms and assembles the
  macOS bundle. A short macOS job, which needs no .NET, only finishes the bundle and makes the disk
  image. The validator keeps a job per platform: NativeAOT cannot compile for another operating
  system, and compiles for another architecture only with a cross toolchain.
- **The Linux builds are started in the pipeline.** Under a virtual display, with a fixture
  export, each proves that the trimmed single file unpacks, starts and loads the store. Until now
  the reader's builds were only checked for the files they carry.
- **The README's "Get it"** says what to download per platform, what the first start asks of the
  person on each, and where the unpacked copy lives. "Building" shows the new publish shapes.

### Non-goals

- **No code signing with a Developer ID, no notarization, no Authenticode.** The first start
  warns as it does today, and the README keeps saying how to get past it.
- **No icon.** The application shows the platform's generic icon.
- **No opening an export from Finder**, by dropping it on the Dock icon or with *Open With*. That
  needs declared document types and handling of macOS file-open events.
- **No Intel Macs.** Apple is ending support for them, so both tools stay arm64-only on macOS.
- **No installer**, whether MSI, MSIX, pkg or AppImage.
- **No change to the validator's existing artifacts beyond its identifier.** They keep their
  names, stay one file each, and are built by the same jobs. Two ARM64 artifacts are added beside
  them.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `reader-ui`:
  - "The reader is distributed as a runnable build per platform" states what a person downloads:
    one executable file on Windows and Linux, and an application in a disk image on macOS.
  - Added: a build that unpacks itself does not leave copies of other versions behind.

## Impact

- **Build configuration:**
  - `GoBd.Reader.Ui.csproj` publishes a single file for Windows and Linux, trims, keeps
    `DuckDB.NET.Data` whole, and drops native symbol files.
  - `Directory.Build.props` embeds symbols in every assembly.
  - The reader's `app.manifest` names `de.webducer.gobd.reader`.
  - `GoBd.Validation.Cli.csproj` gains an `ApplicationManifest` naming `de.webducer.gobd.cli`.
- **Reader code:**
  - `TableTabView` binds its `#`, Record and value columns without reflection.
  - Startup removes unpacked copies of other versions, claiming its own copy the way `StoreLock`
    claims a store.
  - The comment in `App.cs` that says there is no `Info.plist` is corrected.
- **New files:** an `Info.plist` template for the bundle, and the validator's `app.manifest`.
- **Pipeline:**
  - The reader jobs in `ci.yml` and `release.yml` move to one Linux job that publishes five
    builds, plus a short macOS job and a smoke start on an ARM64 Linux runner.
  - The validator's matrix gains `linux-arm64` on `ubuntu-24.04-arm` and `win-arm64` on
    `windows-11-arm`.
  - The validator's macOS jobs re-sign its binary ad hoc with the identifier
    `de.webducer.gobd.cli`.
  - `package-reader.sh` is replaced by a script that assembles the bundle and one that makes the
    disk image. `verify-reader-publish.sh` checks the new shapes.
- **Documentation:** the README's "Get it" and "Building" sections, which name five platforms.
- **Earlier decision:** supersedes D14 of `add-export-reader`, "The reader ships as a directory;
  the CLI ships as a file".
- **Cost to the person:**
  - On Windows and Linux, the first start writes the unpacked libraries before the window opens;
    later starts reuse them.
  - A trimmed build is a form the test suite does not run. Warnings as errors and the smoke starts
    of the Linux builds stand between that and a release.
