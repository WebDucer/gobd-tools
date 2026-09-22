## Context

See proposal.md for why. The state this design builds on:

- **The drawing.** `gobd-reader.svg` at the repository root is the drawing alone, on a
  transparent square canvas of 4267 units with no margin. It spans the full width and sits
  centred vertically, 55 units from the top and the bottom. It is made of filled paths only: no
  text, filters, masks or embedded images. Most of it is dark navy (`#0E063C`), which vanishes on
  a dark background.
- **Avalonia draws no SVG.** A window icon (`WindowIcon`) takes a PNG or ICO stream. The SVG
  libraries for Avalonia resolve by reflection. The reader is trimmed, with the trim analyzer on in
  every build and warnings as errors (`repackage-reader` D3).
- **Avalonia's Windows backend** loads a window icon into a small and a big icon
  (`CreateIconFromResourceEx`, `LoadSmallIcon` and `LoadBigIcon` in `Avalonia.Win32`). A window
  without one gets an empty icon.
- **The Windows reader is built on Linux.** `verify-reader-publish.sh` finds the manifest name
  `de.webducer.gobd.reader` in the Linux-built `.exe`. With single-file compression on, the name
  can only be there if the SDK copied the assembly's Win32 resources into the executable. The
  .NET 10 SDK does that in managed code (`ResourceUpdater` over `PEReader` in
  `Microsoft.NET.HostModel`), so it works off Windows. An icon is a Win32 resource like the
  manifest.
- **The macOS bundle has no place for an icon.** `assemble-reader-app.sh` creates only
  `Contents/Info.plist` and `Contents/MacOS/`, and `Info.plist` has no icon key (`repackage-reader`
  D6). `iconutil` exists only on macOS, and the bundle is assembled on Linux.
- **The usual packers throw compression away.** Measured on trial files made from the drawing:
  - oxipng (`-o max --strip safe --zopfli`) makes Inkscape's PNGs about 20% smaller, with
    identical pixels. The 1024 px macOS image goes from 30,767 to 24,644 bytes.
  - ImageMagick stores every size in an `.ico` as an uncompressed bitmap, 256 px included: 313,398
    bytes.
  - `iconutil` re-encodes the PNGs it is given. The `.icns` is 115,594 bytes whether or not they
    were optimised, and its 16 and 32 px images are run-length-encoded ARGB, not PNG.
  - The same optimised PNGs, written into the containers unchanged, make an 11,237-byte `.ico` and
    a 62,704-byte `.icns`.
  - PNG data in the `.icns` entries for 16 and 32 px (`icp4`, `icp5`) reads back from
    `iconutil -c iconset` as noise. Every other entry reads back pixel-identical.
  - oxipng refuses a finished `.icns` or `.ico`: neither is a PNG, so the only place to optimise is
    before packing.

## Goals / Non-Goals

**Goals:**
- One drawing to maintain. Each platform's version follows from it and from a handful of settings.
- Building the reader needs no image tool, locally or in the pipeline.
- The pipeline fails if a Windows or macOS build is missing its icon.
- Every generated file is as small as lossless compression makes it.

**Non-Goals:**
- Byte-for-byte reproducible icon files. They are regenerated only when the drawing changes, and
  the committed files are what counts.
- Icon files for other uses, such as favicons or a Linux icon theme.

## Decisions

### D1 — One drawing; the files made from it are generated and committed

```
src/GoBd.Reader.Ui/Assets/gobd-reader.svg   the drawing, the one file drawn by hand
        |
        |  .github/scripts/make-reader-icons.sh, run by hand
        v
+---------------------------------+-------------------------------------------+
| src/GoBd.Reader.Ui/Assets/      | gobd-reader.ico   Windows executable,     |
|                                 |                   Windows window          |
|                                 | gobd-reader.png   Linux window            |
| src/GoBd.Reader.Ui/macOS/       | gobd-reader.icns  macOS bundle, beside    |
|                                 |                   Info.plist              |
| docs/                           | gobd-reader-icon.svg  README              |
+---------------------------------+-------------------------------------------+
```

The drawing moves from the repository root into the reader project, beside the files made from
it. The script is the only way the other files are made, and each of them says so in the place
that uses it: a comment in the project file, in `Info.plist` and beside the README's image.

**What the drawing must be.** The script relies on this, and its header states it:
- a square canvas, the drawing touching the canvas along its wider side, centred by eye
- no background and no margin
- filled paths only

- **Why commit the generated files:** every `dotnet build` needs the `.ico` and the `.png`, not only
  a release. Generating them during the build would put an image tool on every machine that builds
  the reader and on every runner.
- **Why not draw the SVG at runtime:** it needs an SVG library, which resolves by reflection
  (see Context). The alternative, translating the paths into Avalonia geometry in code, keeps a
  second copy of the drawing that nothing keeps in step.
- **Alternative rejected: exporting each platform's version from Affinity by hand.** It gives several
  hand-made files that drift apart, and a change of plate colour or margin means editing each of
  them.

### D2 — Each platform has a layout; the plate is drawn by the script

The drawing always sits on a light plate, `#FBFAFB`. Its navy parts need a light ground to be
seen, and a plate the script draws is the same on every background. The script builds each layout
as an SVG:
1. the canvas
2. the plate, a rounded rectangle
3. the drawing, embedded unchanged as a nested `<svg>` with its own `viewBox`, placed inside the
   plate's margin.

| Layout | Used for | Canvas | Plate | Drawing's margin inside the plate |
| --- | --- | --- | --- | --- |
| `square` | `.ico`, `.png` | the size itself | fills the canvas; corner radius an eighth of the side | 10% of the side from 32 px; 1 px at 16, 20 and 24 px |
| `mac` | `.icns`, README | 1024, and scaled down for smaller sizes | 824 square, 100 transparent on every side, corner radius 185 | 12% of the plate |

These are starting values. The script writes a preview page (D3), a person judges it, and the
settings are adjusted before the files are committed.

- **Why macOS has its own layout:** macOS draws application icons as a rounded square inset on its
  canvas, and macOS 26 puts an icon that is not in that shape inside a grey tile of its own. The
  `mac` layout is that shape.
- **Why the small sizes have a thinner margin:** at 16 px a 10% margin leaves the drawing about
  13 px, and its yellow lines disappear. Windows uses 16 px in the title bar and for Explorer's
  small icons.
- **Why the README uses the `mac` layout:** it is the icon's canonical shape, and as an SVG it is
  sharp at any size, with no raster to generate.
- **Alternative rejected: a dark variant instead of a plate.** An icon that switches with the theme
  helps the README (`<picture>`). It cannot help the Windows executable, whose icon is fixed, or a
  legacy `.icns`.

### D3 — The script: `make-reader-icons.sh`

`.github/scripts/make-reader-icons.sh`, beside the scripts that assemble the reader, run by hand
whenever the drawing or a setting changes:

1. Checks that Inkscape, oxipng, Python 3 and `npx` are present, and names any that is missing.
2. Writes the layout SVGs of D2 into a temporary directory.
3. Rasterises each needed size with Inkscape, which renders SVG as Affinity and browsers do.
4. Optimises every PNG with oxipng, `-o max --strip safe --zopfli`. This is lossless.
5. Packs the `.ico` itself: 16, 20, 24, 32, 40, 48, 64 and 256 px, each entry an optimised PNG,
   written unchanged after the file's header and directory.
6. Packs the `.icns` itself, with eight entries, each an optimised PNG written unchanged:

   | Entry | Pixels | Stands for |
   | --- | --- | --- |
   | `ic11` | 32 | 16 pt at @2x |
   | `ic12` | 64 | 32 pt at @2x |
   | `ic07` | 128 | 128 pt |
   | `ic13` | 256 | 128 pt at @2x |
   | `ic08` | 256 | 256 pt |
   | `ic14` | 512 | 256 pt at @2x |
   | `ic09` | 512 | 512 pt |
   | `ic10` | 1024 | 512 pt at @2x |

7. Writes `gobd-reader.png`, the optimised 256 px image, and the README's SVG. That SVG is the
   `mac` layout run through svgo, at a version pinned in the script and called through `npx`. The
   script fails if the result has lost its `viewBox`.
8. Writes a preview page, in HTML, that shows every output on a light and a dark background, and
   prints its path. The page is not committed.

The settings of D2, the oxipng options and the svgo version are variables at the top of the
script. The packing is a few lines of Python 3, inside the script.

- **Why pack the containers ourselves:** ImageMagick and `iconutil` both encode the images again,
  which discards the optimisation and more: ImageMagick's `.ico` is 28 times the size of ours, and
  `iconutil`'s `.icns` nearly twice (see Context). Both formats are a header and a directory in
  front of the image data, so writing them takes a few lines.
- **Why every `.ico` entry is a PNG:** Windows has read PNG at every size in an `.ico` since
  Vista, and .NET 10 needs Windows 10. Bitmaps for the sizes below 256 px would add about 37 KB
  that no Windows the reader runs on needs.
- **Why the `.icns` has no 16 and 32 px entries at 1x:** macOS does not read PNG data in them (see
  Context). `iconutil` writes them as run-length-encoded ARGB instead. Doing the same would mean
  decoding pixels and encoding them again in the script, for two images macOS can do without:
  - a Retina display, which every Apple Silicon Mac with a built-in screen has, uses `ic11` and
    `ic12`
  - on an external 1x display, macOS scales those down.
- **Why zopfli:** it takes another 5 to 8% off oxipng's result, and at these sizes it costs
  seconds.
- **Why svgo, pinned:** the README's SVG carries Affinity's export: a DOCTYPE, its own namespace
  and nested transforms. svgo removes them. Its default plugins change between versions, and some
  versions removed the `viewBox`, without which `<img width=…>` does not scale the image. The
  version is fixed and the `viewBox` checked.
- **The drawing itself is not optimised.** It is an export from Affinity, the next export replaces
  it, and it ships nowhere.
- **Why a shell script:** the other build scripts are shell scripts. Every step but the packing is
  a call to an existing tool.
- **Why Inkscape rasterises:** ImageMagick's SVG renderer differs by installation, and `sips`
  renders no SVG.
- **No Mac needed:** Inkscape, oxipng, Python 3 and Node run on every platform the reader is built
  on. `iconutil` is used only to check the `.icns` (tasks.md 1.2), and only there does it need a
  Mac.

### D4 — The Windows executable carries the icon as a resource

`ApplicationIcon` in `GoBd.Reader.Ui.csproj` points at `Assets/gobd-reader.ico`. The SDK puts it
in the assembly's Win32 resources and copies it into the executable, as it does the manifest,
including when building on Linux (see Context).

`verify-reader-publish.sh` checks that the icon arrived. For `win-*`, it looks for the `.ico`'s
256 px image, byte for byte, in the executable, as it already looks for the manifest name.
- **Why this check proves it:** an `.ico` stores each image verbatim, and each becomes an
  `RT_ICON` resource unchanged. The assemblies inside the single file are compressed, so a match
  can only come from the executable's own resources.
- **How:** the byte search is a few lines of Python 3, which the runner has, because `grep`
  searches lines rather than bytes.

### D5 — The window sets its icon on Windows and Linux, not on macOS

The window takes its icon from an Avalonia resource:
- **Windows:** the `.ico`, so the backend picks its small and big icon from the sizes drawn for
  them.
- **Linux:** the 256 px `.png`.
- **macOS:** none. A macOS window has no icon of its own, and the Dock shows the bundle's.

Both files are `AvaloniaResource` items, read with `AssetLoader`.

- **Why set it at all on Windows, when the executable has an icon:** Avalonia gives a window an
  empty icon unless one is set (see Context).
- **Alternative rejected: one PNG everywhere.** On Windows the 256 px image would be scaled down to
  16 px by the system, losing the thinner margin of D2.

### D6 — The macOS bundle carries the `.icns`

- **Where:** `src/GoBd.Reader.Ui/macOS/gobd-reader.icns`, beside `Info.plist`, the other file only
  the bundle uses.
- **`Info.plist`:** gains `CFBundleIconFile` = `gobd-reader`.
- **`assemble-reader-app.sh`:**
  - creates `Contents/Resources` and copies the `.icns` into it
  - fails if the `.icns` is missing, as it fails without a launcher.
- **`make-reader-dmg.sh`** is unchanged. Signing the bundle as a whole seals `Contents/Resources`
  with the rest.
- **This supersedes** "No icon key" in D6 of `repackage-reader`.

### D7 — The start page shows no icon

The start page, the Summary tab, stays as it is. The window already shows the icon on Windows and
Linux, and the Dock does on macOS. The start page is for the export's condition, whether one is
open or not.

- **Changed during implementation.** The first plan showed the icon at 96 px above "No export
  opened", and hid it once a summary was shown. The person who asked for the change decided the
  Summary tab does not need it, and the code for it was taken out again.
- **The `.png` stays.** It is the window's icon on Linux (D5).

### D8 — The README shows the icon at its top

The README opens with the icon above its title, `docs/gobd-reader-icon.svg` at 128 px, centred:
`<p align="center"><img … width="128"></p>`. GitHub renders an SVG in `<img>`. The icon belongs to
the reader, but the README is the repository's front page, and the reader is what most people come
for. "Building" gains a paragraph: the icon files come from `make-reader-icons.sh`, which needs
Inkscape, oxipng, Python 3 and Node.

## Risks / Trade-offs

- **[The committed files drift from the drawing]** → Only the script makes them, and every file
  that uses them says so (D1). Checking them in the pipeline would need the image tools there,
  which D1 avoids.
- **[Inkscape renders differently after an upgrade]** → This matters only when the files are
  regenerated, and the preview page is looked at every time.
- **[A container we pack ourselves is misread by a platform]** → The containers are checked with
  the platforms' own readers:
  - the `.icns` with `iconutil -c iconset`, which reads it back pixel-identical
  - the `.ico` with `magick identify`
  - both by hand: in Finder and the Dock, and on Windows if a machine is available.

  Should some part of Windows show no icon at small sizes, the fallback is bitmap entries below
  256 px. That changes the packer alone.
- **[svgo alters the drawing]** → Its version is pinned, the script checks for the `viewBox`, and
  the preview page shows the README's SVG beside the other outputs.
- **[Avalonia on Windows uses only one image from the `.ico`]** → The icon still shows, only
  scaled. If a check on Windows shows this, the window takes the PNG instead (D5).
- **[Nobody checks by hand on Windows]** → The pipeline builds no Windows reader it can start.
  The executable's icon is checked by the byte search (D4). The window icon on Windows is checked
  by hand if a Windows machine is available, and otherwise recorded as unchecked.
- **[Explorer shows the old icon after an update]** → Windows caches icons by path. This affects
  only a person who replaces the file in place, and clears itself.
- **[GNOME on Wayland shows a generic icon]** → Its dash looks for a `.desktop` file, which a
  reader that installs nothing does not have (proposal, Non-goals).
- **[Size]** → The `.ico` (about 11 KB) and the `.png` (about 6 KB) travel in every build, and
  the `.icns` (about 61 KB) in the macOS one. The `.ico` counts more than once: it sits in the
  executable's resources and, as a Win32 and an Avalonia resource, in the reader's assembly. Packed
  by ImageMagick it would have cost 313 KB each time.

## Migration Plan

- Nothing to migrate: the next build shows the icon. On macOS, the Dock and Finder may keep the
  generic icon for an application that was already copied to Applications, until it is replaced.
- **Rollback:** revert the change. Every file it adds is referenced only from the places it
  changes.

## What the implementation settled

### The drawing and the layouts, approved

The person who drew the icon approved the preview page as generated:
- **The drawing stays as drawn.** In it, the coral panel, its eyes and the three lines sit 7 pt
  higher relative to the "L" than in the first committed drawing, which leaves a thin navy sliver
  along the panel's rounded lower left corner.
- **The layouts keep their starting values** (D2), except the macOS margin. The plate `#FBFAFB`,
  the corner radii and the Windows and Linux margins stay as they were.
- **The macOS margin became 20% of the plate, from 12%.** In the Dock, macOS draws the plate
  larger than its 824 px in the layout suggest, and at 12% the "L" nearly touched the plate's
  edges. The person who drew the icon compared 12, 17 and 22% as macOS draws them, and chose 20.
  The README's SVG uses the same layout, and so has the same margin.
- **The 16 and 24 px images are readable enough.** No hand-drawn small sizes; they can still
  replace the generated ones in the `.ico` later, with no other change.

### oxipng runs at level 4, not "max"

Measured on the script's fourteen images, 70,644 bytes as Inkscape writes them:

| oxipng | Time | Bytes |
| --- | --- | --- |
| `-o max --strip safe` | 3 s | 60,144 |
| `-o 4 --strip safe --zopfli` | 30 s | 56,236 |
| `-o max --strip safe --zopfli` | 110 s | 56,212 |

"max" with zopfli takes nearly four times as long to save 24 bytes, so the script runs level 4
with zopfli. The whole script takes about 30 s. That is not the "seconds" D3 expected, but it is
cheap for something run by hand.

### The script, checked

Run into a scratch directory on macOS 26 with Inkscape 1.4.4, oxipng 10.2.1, Python 3 and
Node 26, and with svgo 4.1.0 pinned:
- **Pixels:** every optimised PNG is pixel-identical to Inkscape's (`magick compare -metric AE`
  gives 0 for all fourteen).
- **`.ico`:** `magick identify` reads eight PNG entries, 16 to 256 px.
- **`.icns`:** `iconutil -c iconset` gives back eight images. Each is pixel-identical to the PNG
  packed for it.
- **`.png`:** 256 × 256.
- **README SVG:** it keeps `viewBox="0 0 1024 1024"`. Drawn by Inkscape at 1024 px, it differs
  from the unoptimised layout in 1.5 pixels, by at most 5% in a channel. svgo takes it from
  4,716 to 2,762 bytes.
- **Missing tools:** with Inkscape, oxipng, Python 3 or `npx` missing from `PATH`, the script
  stops and names the one missing.

Sizes, with the starting settings of D2 (for the final ones, see below):

| File | Bytes |
| --- | --- |
| `gobd-reader.ico` | 11,484 |
| `gobd-reader.png` | 5,868 |
| `gobd-reader.icns` | 61,121 |
| `gobd-reader-icon.svg` | 2,762 |

### Checked by hand, and not

- **Linux (tasks.md 3.2):**
  - Checked on the `linux-arm64` single file, started with `good-export` under Xvfb in an
    Ubuntu 24.04 container.
  - Its window carries `_NET_WM_ICON`, a 128 × 128 icon: Avalonia's X11 backend scales the
    256 px `.png` down.
  - `xprop` finds it once the window has settled. Asked the moment the window appears, it may not
    find it yet.
- **Windows (tasks.md 3.3): not made.** No Windows machine was at hand.
  - The executable's icon is covered by the byte search in `verify-reader-publish.sh` (D4). It
    passes for both Windows builds, made on macOS, and fails for one made without
    `ApplicationIcon`.
  - Unchecked: the window icon on Windows, and whether Avalonia takes the 16 px image from the
    `.ico` for the title bar.
- **macOS (tasks.md 4.2), by the platform's own drawing:**
  - The disk image `make-reader-dmg.sh` makes from the assembled bundle verifies with
    `codesign --verify --deep --strict`.
  - `NSWorkspace` draws the application's icon from the mounted image at 512, 64, 32 and 16 px
    as designed, on its rounded plate and not inside a grey tile, on macOS 26.
  - At 16 px on a 1x context it is scaled from the @2x entry, and stays recognisable.
  - By eye, on the disk image made with the final 20% margin: the person who drew the icon found
    it right in the disk image window, in Finder and in the Dock.
  - `dotnet run` shows the terminal's icon in the Dock, not the reader's. That is expected: it
    starts the executable outside a bundle, and a macOS window has no icon of its own (D5).
- **README (tasks.md 6.1):** seen on GitHub, on its light and its dark theme, once the branch was
  pushed.

### The files as committed

Made with the approved settings, the macOS margin at 20%. A second run gives pixel-identical
images, and `iconutil -c iconset` reads the `.icns` back as its eight images.

| File | Bytes |
| --- | --- |
| `gobd-reader.ico` | 11,484 |
| `gobd-reader.png` | 5,868 |
| `gobd-reader.icns` | 56,971 |
| `gobd-reader-icon.svg` | 2,758 |

### The pipeline

The CI run of this change's pull request passed all nine jobs. Its reader job built the
two Windows files on Linux, and `verify-reader-publish.sh` found the icon in both. That confirms
D4 where it matters: the SDK copies the icon into the executable off Windows. The job then
assembled the bundle with its `.icns`, the disk image job made and signed the image, and both
Linux smoke starts opened the store. Its only annotations are GitHub's notices of coming
runner-image migrations.
