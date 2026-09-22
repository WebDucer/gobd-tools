## 1. The drawing and the icon files

- [x] 1.1 Move `gobd-reader.svg` from the repository root to `src/GoBd.Reader.Ui/Assets/` with
      `git mv` (design.md D1); verify `git status` shows a rename, and that
      `grep -rn "gobd-reader.svg" --exclude-dir=openspec --exclude-dir=.git .` finds no reference to the old path
- [x] 1.2 Write `.github/scripts/make-reader-icons.sh` (D3), with the layout settings of D2, the
      oxipng options and the pinned svgo version as variables at its top, and the drawing's
      requirements of D1 in its header. Verify on this Mac:
      - it writes the `.ico`, the `.png`, the `.icns`, the README's SVG and a preview page into
        an output directory given on the command line
      - every optimised PNG is pixel-identical to the one Inkscape wrote:
        `magick compare -metric AE` gives 0
      - `magick identify` reads the `.ico` as the eight sizes of D3, each a PNG
      - `iconutil -c iconset` on the `.icns` gives back the eight images of D3, each
        pixel-identical to the PNG packed
      - the `.png` is 256 × 256
      - the README's SVG keeps its `viewBox`, and Inkscape renders it pixel-identical to the
        unoptimised `mac` layout, or within a difference too small to see
      - with each of Inkscape, oxipng, Python 3 and `npx` removed from `PATH` in turn, it stops
        and names the one missing
      - record the size of each generated file under "What the implementation settled"
- [x] 1.3 Settle the drawing and the layouts with the person who drew the icon, using the preview
      page:
      - the alignment question in design.md's Open Questions (the 7 pt shift and the navy sliver)
      - the D2 settings
      - whether the 16 and 24 px images are readable
      Record the answers and the final settings under "What the implementation settled" in
      design.md. Verify the person has approved the page made with the final drawing and settings
- [x] 1.4 Generate the files with the approved settings into the places of D1 and add them to the
      repository; verify the four files exist at the D1 paths, and that running the script again
      gives a preview page with no visible difference

## 2. The Windows executable

- [x] 2.1 Set `ApplicationIcon` to `Assets/gobd-reader.ico` in `GoBd.Reader.Ui.csproj`, with a
      comment naming the script that makes the file (D4); verify
      `dotnet publish src/GoBd.Reader.Ui -c Release -r win-x64 -warnaserror` succeeds on this Mac,
      off Windows as in the pipeline
- [x] 2.2 Extend `verify-reader-publish.sh` for `win-*` to find the `.ico`'s 256 px image, byte for
      byte, in the executable, with a few lines of Python 3 (D4); verify it passes for the `win-x64`
      and `win-arm64` publishes, and fails for a `win-x64` publish made without `ApplicationIcon`

## 3. The window icon

- [x] 3.1 Add `gobd-reader.ico` and `gobd-reader.png` as `AvaloniaResource` items, and set the
      window's icon from the `.ico` on Windows and from the `.png` on Linux, and not on macOS
      (D5). Verify:
      - a headless test finds both resources with `AssetLoader.Exists`
      - `dotnet build` stays warning-free with the trim analyzer on
      - `dotnet publish -r linux-x64 -warnaserror` succeeds
- [x] 3.2 Start the `linux-x64` publish with an export under Xvfb, or on a Linux desktop; verify
      with `xprop` that its window carries `_NET_WM_ICON`
- [x] 3.3 On a Windows machine, if one is available, start the `win-x64` publish; verify the
      executable in Explorer, the title bar and the taskbar show the icon, and that the title bar
      shows the 16 px image rather than a scaled 256 px one. If no Windows machine is available,
      record the check as not made under "What the implementation settled"

## 4. The macOS application

- [x] 4.1 Add `CFBundleIconFile` = `gobd-reader` to `src/GoBd.Reader.Ui/macOS/Info.plist`, with a
      comment naming the script that makes the `.icns`. Make `assemble-reader-app.sh` create
      `Contents/Resources`, copy the `.icns` into it, and fail if it is missing (D6). Verify:
      - the archive it makes from an `osx-arm64` publish holds
        `GoBD Reader.app/Contents/Resources/gobd-reader.icns`
      - `plutil -lint` passes on its `Info.plist`
      - with the `.icns` moved away, the script fails and names it
- [x] 4.2 Make the disk image with `make-reader-dmg.sh` on this Mac; verify `codesign --verify
      --deep --strict` passes, and that the disk image window, Finder and the Dock show the icon
      as drawn, not inside a grey tile, on this machine's macOS 26. Verify too that Finder's list
      view shows it at 16 pt, where the `.icns` has only its @2x image (D3), and on a 1x
      external display if one is available

## 5. The start page

- [x] 5.1 Leave the start page without an icon (design.md D7): the icon first built into
      `StartPageView` is taken out again, and its tests with it. Verify `StartPageView.cs` is as it
      was before this change (`git diff` shows nothing for it), and that the UI tests pass

## 6. The README

- [x] 6.1 Put `docs/gobd-reader-icon.svg` at the top of the README, centred at 128 px, with a
      comment naming the script (D8), and add to "Building" that the icon files come from
      `make-reader-icons.sh`, which needs Inkscape, oxipng, Python 3 and Node. Verify once the
      branch is pushed that GitHub shows the icon, on its light and on its dark theme

## 7. Whole

- [x] 7.1 Run `dotnet build`, `dotnet test`, and the five reader publishes with `-warnaserror`,
      each checked with `verify-reader-publish.sh`; verify all pass locally, and that the CI run on
      the branch passes, including the Linux smoke start and the disk image job
- [x] 7.2 Complete "What the implementation settled" in design.md with the final layout settings,
      the answer to the alignment question, and each check made or not
      made by hand; verify no open question remains unanswered there
