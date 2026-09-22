# Give the reader its icon

## Why

The reader shows the platform's generic icon wherever an application is shown: the Windows file
in Explorer, the window's title bar and taskbar entry, the macOS application in Finder, the Dock
and the disk image. `repackage-reader` left it out on purpose. Someone with several windows open,
or looking for the reader among their applications, recognises it by its picture before its
name. The reader now has an icon, `gobd-reader.svg`, so it can show it.

## What Changes

- **One source drawing.** `gobd-reader.svg` holds only the drawing: no background and no margin.
  It moves into the reader project.
- **Versions for each platform are generated and committed.** A script, run by hand, derives each
  platform's version from the drawing, with that platform's margins and background plate, and the
  results are committed. No image tool is needed to build the reader or in the pipeline, and the
  reader carries no SVG library.
  - **Windows:** a multi-size `.ico` embedded in the executable, so Explorer, the desktop and the
    taskbar show it.
  - **macOS:** an `.icns` in the application bundle, named in its `Info.plist`, in the layout macOS
    expects: a rounded square inset on its canvas.
  - **Window icon:** the running window shows the icon in its title bar and taskbar entry on
    Windows and Linux.
- **Every generated file is compressed as far as lossless compression goes.**
  - Every PNG is run through oxipng.
  - The `.ico` and `.icns` are packed from those PNGs without being encoded again. The usual
    tools re-encode: ImageMagick makes a 313 KB `.ico` and `iconutil` a 115 KB `.icns`. The same
    images fit in 11 KB and 61 KB.
  - The README's SVG is run through svgo.
- **The icon sits on a light plate everywhere.** The drawing is mostly dark navy, and on a dark
  taskbar, Dock or page it would disappear. The plate keeps it visible on any background.
- **The README shows the icon** at its top, as an SVG with the plate.
- **The pipeline checks for the icon.** The Windows executable is checked for the icon as it is
  already checked for its manifest, and assembling the macOS bundle fails without the `.icns`.

### Non-goals

- **No desktop integration on Linux.** The reader is one file and installs nothing, so there is no
  `.desktop` file. Some desktops, such as GNOME on Wayland, may keep showing a generic icon in
  their application list.
- **No icon for `gobd-validate`.** A command-line tool is rarely shown by its icon, and the icon
  names the reader.
- **No macOS 26 icon format.** Icon Composer's layered `.icon` needs Xcode to compile and adds
  nothing macOS 14 and 15 can show. The `.icns` works on every supported macOS.
- **No social preview image.** GitHub takes one only as an upload in the repository's settings,
  which is outside the repository.
- **No hand-drawn small sizes.** The 16 and 24 px images are generated like the others. Whether
  they need drawing by hand is decided once they can be seen.
- **No icon inside the reader.** The window already shows it, and the start page is for the
  export's condition: it shows no icon, whether an export is open or not.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `reader-ui`: added, the reader shows its icon wherever the platform shows an application's
  icon, recognisably on a light or a dark background.

## Impact

- **New files:**
  - the icon script under `.github/scripts/`. It needs Inkscape, oxipng, Python 3 and Node on the
    machine that runs it, and nothing in the build or the pipeline.
  - the generated `.ico`, `.png` and `.icns` in the reader project
  - the README's generated SVG, `docs/gobd-reader-icon.svg`
- **Moved:** `gobd-reader.svg` from the repository root into the reader project.
- **Build configuration:**
  - `GoBd.Reader.Ui.csproj` gains `ApplicationIcon`, and the icon files as Avalonia resources.
  - `macOS/Info.plist` gains `CFBundleIconFile`.
- **Reader code:**
  - The window sets its icon on Windows and Linux.
- **Scripts:**
  - `assemble-reader-app.sh` copies the `.icns` into `Contents/Resources`.
  - `verify-reader-publish.sh` checks that the Windows executable carries the icon.
  - The workflows are unchanged, because they already run both scripts.
- **Documentation:** the README gains the icon at its top, and its "Building" section says how
  the icon files are made.
- **Size:** the icon files come to about 80 KB, measured on trial files.
  - The `.ico` (11 KB) and the `.png` (6 KB) travel in every build.
  - The `.icns` (61 KB) travels only in the macOS one.
