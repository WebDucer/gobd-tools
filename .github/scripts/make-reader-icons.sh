#!/usr/bin/env bash
# Makes the reader's icon files from its drawing, and a page to judge them on.
#
# The drawing, src/GoBd.Reader.Ui/Assets/gobd-reader.svg, is the one file drawn by hand. Everything
# else is made here and committed, so that building the reader needs no image tool. Run this by hand
# whenever the drawing or a setting below changes. The drawing has to be:
#
# - a square canvas, the drawing touching it along its wider side, centred by eye
# - without background or margin: the plate and every margin are set here, per platform
# - filled paths only, so that Inkscape draws it as Affinity and a browser do
#
# What it makes, and where it goes without an output directory:
#
#   gobd-reader.ico       src/GoBd.Reader.Ui/Assets/   Windows executable and window
#   gobd-reader.png       src/GoBd.Reader.Ui/Assets/   Linux window
#   gobd-reader.icns      src/GoBd.Reader.Ui/macOS/    macOS bundle
#   gobd-reader-icon.svg  docs/                        README
#
# Every PNG is optimised with oxipng, which is lossless, and the .ico and .icns are packed here from
# those PNGs unchanged. ImageMagick and iconutil both encode the images again, and make files 28
# and nearly 2 times the size. The .icns has no 16 and 32 px entries at 1x, because macOS reads no
# PNG in them; a Retina display uses the @2x ones, and a 1x display scales those down.
#
# Needs Inkscape, oxipng, Python 3 and npx, which fetches svgo on first use. See the
# add-reader-icon change's design.md D1 to D3.
set -euo pipefail

usage="usage: make-reader-icons.sh [output-dir]"
[ "$#" -le 1 ] || { echo "$usage" >&2; exit 2; }

# The plate, on which the drawing's navy stays visible on a light and a dark background.
plate_colour="#FBFAFB"

# Windows and Linux: a rounded plate filling the canvas.
square_sizes="16 20 24 32 40 48 64 256"
square_radius="0.125"      # of the side
square_margin="0.10"       # of the side, from 32 px
square_small_margin="1"    # in pixels, at the sizes below 32 px, so the drawing keeps its lines

# macOS, and the README: the rounded square macOS expects, inset on a 1024 canvas.
mac_canvas="1024"
mac_plate="824"
mac_radius="185"
mac_margin="0.20"          # of the plate

# Level 4 with zopfli: measured, "max" took four times as long for 24 bytes less in all.
oxipng_options=(-o 4 --strip safe --zopfli)
svgo_version="4.1.0"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
drawing="$repo/src/GoBd.Reader.Ui/Assets/gobd-reader.svg"

for tool in inkscape oxipng python3 npx; do
  if ! command -v "$tool" > /dev/null; then
    echo "$tool is not installed, and making the icons needs it" >&2
    exit 1
  fi
done

[ -f "$drawing" ] || { echo "no drawing at $drawing" >&2; exit 1; }

if [ "$#" -eq 1 ]; then
  mkdir -p "$1"
  out="$(cd "$1" && pwd)"
  ico="$out/gobd-reader.ico"
  png="$out/gobd-reader.png"
  icns="$out/gobd-reader.icns"
  readme_svg="$out/gobd-reader-icon.svg"
  preview="$out/preview"
  rm -rf "$preview"
  mkdir -p "$preview"
else
  ico="$repo/src/GoBd.Reader.Ui/Assets/gobd-reader.ico"
  png="$repo/src/GoBd.Reader.Ui/Assets/gobd-reader.png"
  icns="$repo/src/GoBd.Reader.Ui/macOS/gobd-reader.icns"
  readme_svg="$repo/docs/gobd-reader-icon.svg"
  # Kept, so the page can be opened after the script has finished.
  preview="$(mktemp -d)"
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir "$preview/layouts" "$preview/png"

# One SVG per image to draw, each as large in pixels as the image, so Inkscape draws them all in
# one call. The drawing goes into each unchanged, as a nested <svg> placed inside the margin.
python3 - "$drawing" "$preview/layouts" "$plate_colour" \
  "$square_sizes" "$square_radius" "$square_margin" "$square_small_margin" \
  "$mac_canvas" "$mac_plate" "$mac_radius" "$mac_margin" <<'PY'
import re
import sys

(drawing, layouts, colour, square_sizes, square_radius, square_margin, small_margin,
 mac_canvas, mac_plate, mac_radius, mac_margin) = sys.argv[1:]

text = open(drawing, encoding="utf-8").read()
start = text.find("<svg")
root = re.match(r"<svg\b[^>]*>", text[start:])
if start < 0 or not root:
    sys.exit(f"{drawing} has no <svg> element")

box = re.search(r'\bviewBox="([^"]*)"', root.group(0))
numbers = box.group(1).replace(",", " ").split() if box else []
if len(numbers) != 4 or float(numbers[2]) != float(numbers[3]):
    sys.exit(f"{drawing} needs a square viewBox, and has {box.group(0) if box else 'none'}")

# The root element keeps its viewBox and everything else, and loses the size it was exported at.
tag = re.sub(r'\s(?:width|height|x|y)="[^"]*"', "", root.group(0))
inner = text[start + root.end():]

def nested(x, y, side):
    return tag.replace("<svg", f'<svg x="{x:g}" y="{y:g}" width="{side:g}" height="{side:g}"', 1) + inner

def layout(name, pixels, canvas, plate_at, plate, radius, drawing_at, side):
    size = f' width="{pixels}" height="{pixels}"' if pixels else ""
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {canvas:g} {canvas:g}"{size}>\n'
           f'<rect x="{plate_at:g}" y="{plate_at:g}" width="{plate:g}" height="{plate:g}" '
           f'rx="{radius:g}" fill="{colour}"/>\n'
           f"{nested(drawing_at, drawing_at, side)}\n</svg>\n")
    open(f"{layouts}/{name}.svg", "w", encoding="utf-8").write(svg)

for pixels in map(int, square_sizes.split()):
    margin = float(small_margin) if pixels < 32 else pixels * float(square_margin)
    layout(f"square-{pixels}", pixels, pixels, 0, pixels, pixels * float(square_radius),
           margin, pixels - 2 * margin)

canvas, plate, radius = float(mac_canvas), float(mac_plate), float(mac_radius)
plate_at = (canvas - plate) / 2
margin = plate * float(mac_margin)
for pixels in (32, 64, 128, 256, 512, 1024, None):
    layout(f"mac-{pixels}" if pixels else "mac", pixels, canvas, plate_at, plate, radius,
           plate_at + margin, plate - 2 * margin)
PY

(
  cd "$preview/layouts"
  inkscape --export-type=png square-*.svg mac-*.svg 2> "$work/inkscape.log" \
    || { cat "$work/inkscape.log" >&2; exit 1; }
)
mv "$preview"/layouts/*.png "$preview/png/"
oxipng -q "${oxipng_options[@]}" "$preview"/png/*.png

# Both containers are a header and a directory in front of the images, which are written as
# oxipng left them. The .icns entries are the ones macOS reads PNG from.
python3 - "$preview/png" "$square_sizes" "$ico" "$icns" <<'PY'
import os
import struct
import sys

pngs, square_sizes, ico, icns = sys.argv[1:]

def read(name, pixels):
    data = open(os.path.join(pngs, f"{name}.png"), "rb").read()
    width, height = struct.unpack(">II", data[16:24])
    if data[:8] != b"\x89PNG\r\n\x1a\n" or (width, height) != (pixels, pixels):
        sys.exit(f"{name}.png is not a {pixels} px PNG")
    return data

def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "wb").write(data)

sizes = [int(size) for size in square_sizes.split()]
images = [read(f"square-{size}", size) for size in sizes]
offset = 6 + 16 * len(images)
directory = b""
for size, image in zip(sizes, images):
    # A width or height of 0 stands for 256.
    directory += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(image), offset)
    offset += len(image)
write(ico, struct.pack("<HHH", 0, 1, len(images)) + directory + b"".join(images))

entries = [(b"ic11", 32), (b"ic12", 64), (b"ic07", 128), (b"ic13", 256),
           (b"ic08", 256), (b"ic14", 512), (b"ic09", 512), (b"ic10", 1024)]
body = b""
for kind, pixels in entries:
    image = read(f"mac-{pixels}", pixels)
    body += kind + struct.pack(">I", 8 + len(image)) + image
write(icns, b"icns" + struct.pack(">I", 8 + len(body)) + body)
PY

mkdir -p "$(dirname "$png")" "$(dirname "$readme_svg")"
cp "$preview/png/square-256.png" "$png"

# The README's icon is the macOS layout as a vector. svgo removes what the export from Affinity
# carries and nothing draws. Its version is pinned, because its default plugins change between
# versions, and some removed the viewBox, without which an <img width=…> no longer scales.
npx --yes "svgo@$svgo_version" --quiet --multipass --input "$preview/layouts/mac.svg" --output "$readme_svg"
grep -q 'viewBox="0 0 1024 1024"' "$readme_svg" \
  || { echo "svgo removed the viewBox from $readme_svg" >&2; exit 1; }
cp "$readme_svg" "$preview/readme.svg"

# The page shows every image on a light and a dark background, at its size and, for the small
# ones, enlarged with its pixels kept square.
python3 - "$preview" "$square_sizes" <<'PY'
import sys

preview, square_sizes = sys.argv[1:]
square = [int(size) for size in square_sizes.split()]
mac = [32, 64, 128, 256, 512]

def images(name, sizes):
    cells = []
    for size in sizes:
        cells.append(f'<figure><img src="png/{name}-{size}.png" width="{size}" height="{size}">'
                     f"<figcaption>{size}</figcaption></figure>")
    for size in (size for size in sizes if size <= 48):
        cells.append(f'<figure><img class="pixels" src="png/{name}-{size}.png" '
                     f'width="{size * 4}" height="{size * 4}"><figcaption>{size} &times; 4</figcaption></figure>')
    return "".join(cells)

rows = "".join(
    f'<section class="{ground}"><h2>{title}, {ground}</h2>{content}</section>'
    for title, content in (
        ("Windows and Linux", images("square", square)),
        ("macOS", images("mac", mac)),
        ("README", '<figure><img src="readme.svg" width="128" height="128"><figcaption>128</figcaption></figure>'),
    )
    for ground in ("light", "dark"))

open(f"{preview}/index.html", "w", encoding="utf-8").write(f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Reader icons</title><style>
body {{ margin: 0; font: 14px system-ui, sans-serif; }}
section {{ display: flex; flex-wrap: wrap; align-items: flex-end; gap: 24px; padding: 16px 24px 24px; }}
h2 {{ flex-basis: 100%; margin: 0; font-size: 14px; font-weight: 600; }}
.light {{ background: #ffffff; color: #1f2328; }}
.dark {{ background: #1e1e1e; color: #e6e6e6; }}
figure {{ margin: 0; text-align: center; }}
figcaption {{ opacity: 0.7; margin-top: 4px; }}
.pixels {{ image-rendering: pixelated; }}
</style></head><body>{rows}</body></html>
""")
PY

for file in "$ico" "$png" "$icns" "$readme_svg"; do
  echo "wrote $file: $(wc -c < "$file" | tr -d ' ') bytes"
done
echo "preview: $preview/index.html"
