# Accessibility of GoBD Reader

GoBD Reader is meant to be usable by everyone who has to read an export: with a mouse or a
keyboard alone, with a screen reader, at a larger size, and in high contrast. This page says what
the reader does for that, what it does not do yet, and how to tell us about a barrier.

## Yardstick

The reader follows WCAG 2.1 at level AA, the way BITV 2.0 and EN 301 549 apply it to software that
is not a web page. The reader is not currently required to meet BITV 2.0, and this page is not a
statement of conformance. The yardstick says what "accessible" means for each change, and tests
check what can be checked automatically.

Accessibility is ongoing work rather than a feature that is finished. Every change to the reader's
interface is expected to say how it is reached from the keyboard, what it is called for assistive
technology, and which of the reader's colours it uses.

## Colours and contrast

Every colour the reader chooses comes from a set of its own, defined for each appearance it
offers. A test measures every one of them against each background it is drawn on, including a
selected and a hovered row of the grid, a card, a chip and an editor:

- Text, including secondary text, reaches at least 4.5:1.
- The indicator around the control that has keyboard focus reaches at least 3:1.

The reader does not tell anything by colour alone. A value that can be followed is underlined, a
value that refers to nothing is struck through, and the export's verdict says in words whether it
conforms.

## Appearance and high contrast

**Settings → Appearance** offers five choices:

| Choice | What it does |
|---|---|
| System default | Follows the operating system: light or dark, and its high contrast setting |
| Light, Dark | Always light, or always dark |
| High contrast (dark) | Light text on a dark background, in system colours |
| High contrast (light) | Dark text on a light background, in system colours |

With **System default**, the reader switches to high contrast while the operating system's
high contrast setting is on, and back when it is turned off, without being restarted:

- **Windows:** under *Settings → Accessibility → Contrast themes*. The reader uses its light high
  contrast for *Desert* and its dark one for *Aquatic*, *Dusk* and *Night sky*. Windows does not
  tell applications which scheme is in use, only whether it is light.
- **macOS:** *System Settings → Accessibility → Display → Increase contrast*.
- **Linux:** the desktop does not report a high contrast setting to the reader. Choose one of the
  two high contrast appearances in Settings instead.

In high contrast the reader draws status in the system's text colour and tells it apart by
wording, underline and strikethrough, because high contrast themes have no colour of their own for
"defective" or "conformant".

## Zoom and layout

The whole interface can be drawn larger, from actual size to twice that: the navigator, the tabs,
the records, the menu bar inside the window on Windows and Linux, the windows beside the reader,
the filter, sort and figure editors, and the record actions.

- **View → Zoom In / Zoom Out / Actual Size**, or Ctrl with plus, minus or zero (Cmd on macOS).
  The plus on the number pad works too, and on a German keyboard the plus key needs no Shift.
- **Settings → Zoom** offers the steps 100, 110, 125, 150, 175 and 200 %.
- The zoom level is kept for the next start.

The navigator can be made wider for long table names or large text, and collapsed when the records
need the room:

- Drag the boundary between the navigator and the tables, or move it into focus with Tab and press
  the left or right arrow key. It can be at most half as wide as the window.
- **View → Show Navigator**, Ctrl+B (Ctrl+Cmd+S on macOS), collapses the navigator to a narrow
  strip and brings it back. The strip has a button of its own that brings it back too.
- The navigator's width, and whether it is collapsed, are kept for the next start. Zooming never
  collapses it; only you do.

**Known limitation:** the lists that drop down from a picker, and tooltips, are drawn at actual size
whatever the zoom level. A popup opens outside the window it belongs to, and Avalonia does not let
the reader enlarge it there without misplacing it.

## Keyboard

Everything the reader does can be done from the keyboard. Every command stands in a menu, and the
menus show each command's shortcut. **Help → Keyboard Shortcuts** (F1) lists every shortcut for
the platform the reader runs on, in the display language, grouped by where it applies: anywhere in
the window, in a table's records, or in the navigator.

- On Windows and Linux, Alt with the underlined letter of a menu's title opens that menu. The
  letters follow the display language.
- Shortcuts use Cmd on macOS where Windows and Linux use Ctrl, and each platform's own key where
  the two differ, such as Alt+Left and Cmd+[ for Back.
- A text field keeps the keys it uses itself: copy, paste, select all and moving the caret type and
  edit in the field rather than running a command.
- An editor or a window beside the reader takes focus when it opens, Enter applies an editor, and
  Escape closes either (Cmd+W closes a window on macOS too).

## Screen readers

The reader names every control a person can reach with the keyboard, in the display language, and
a test checks that none is left without a name. Beyond that:

- **Records.** Each record of a table is announced by its record number as the file counts records,
  followed by each column's name and value as shown, for example "Record 17: Nr R17, Kunde K2,
  Betrag 30,00". A value that refers to nothing is marked "(refers to nothing)" where it appears.
- **Record actions.** Enter on a record, the Menu key or Shift+F10 offers what can be done with
  it: each reference it can follow, named by its columns and the table it leads to; each table
  whose records refer to it; and copying it. Where a value leads can therefore be heard before it
  is followed.
- **Announcements.** The reader announces, without moving focus: reading progress in tenths and
  each table as it finishes, the outcome of reading, where a navigation landed (including when a
  filter had to give way), the position while stepping through referring records, that a table is
  still being read, and why an editor refused what was typed. A refusal interrupts; everything else
  waits its turn.
- **Focus.** After a navigation, focus is on the record reached, so reading continues there.

**Known limitations:**

- A table's records are presented to screen readers as a list of records, not as a table with row
  and column headers. Moving from cell to cell within a record is not possible; the record's name
  carries all of its values instead.
- Windows (UI Automation) and macOS (VoiceOver) receive the reader's names and announcements.
  Linux has no screen reader bridge in the toolkit the reader is built on.

## Reporting a barrier

If something in the reader cannot be reached, read or understood with the way you work, please
[open an issue](https://github.com/WebDucer/gobd-tools/issues/new) and add the `UI/UX` label.
Say which platform you use, which assistive technology if any, and what you tried to do. A
barrier is a defect, and is treated as one.
