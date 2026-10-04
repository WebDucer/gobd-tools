# Proposal

## Why

Issue #5 asks for contrast, full keyboard operation, descriptive text for every element, and high
contrast, dark mode and font size adjustment. The previous change gave the reader a baseline,
but measured against the code it falls short in the places that matter most:

- In the light theme, the dangling reference marking measures 3.73:1 and the "conforms" verdict
  measures 2.72:1. The spec says both meet WCAG AA, and neither does.
- Following a foreign key works only with a mouse.
- Moving through the navigator with the arrow keys opens a tab for every table passed.
- The editor fields have no names, and no status message reaches a screen reader.
- There is no high contrast presentation and no way to enlarge the interface.

The reader is not bound by BITV 2.0 today. This change still takes WCAG 2.1 AA, as BITV 2.0 and
EN 301 549 apply it to software, as its yardstick. It does not claim conformance. Accessibility
is meant to be ongoing work, so this change also adds the tests and the documentation that keep
it from eroding.

## What Changes

**Visual adaptation**
- The reader gets its own colour tokens (link, dangling reference, conformant, defective, muted
  text, chip and card surfaces), defined for every theme variant. The light and dark values meet
  WCAG AA. Text is no longer dimmed with control opacity.
- High contrast becomes available as "High contrast (dark)" and "High contrast (light)", built on
  Semi's `Aquatic` and `Desert` variants, next to System, Light and Dark.
- "System" also follows the operating system's high contrast setting where the platform reports
  it (Windows, macOS).
- The whole interface can be zoomed in steps from 100 % to 200 %, including the navigator, the
  dialogs and the reader's own popups. The zoom level is kept across launches.
- The navigator's width can be adjusted, and the navigator can be collapsed to a narrow strip.
  Both are kept across launches.
- **BREAKING** (preferences file only): preferences gain new values and fields. Each field is now
  read on its own, so a value one reader does not know no longer resets the others.

**Keyboard operation**
- One command table defines every command once: its label, its shortcut per platform, and its
  menu. Native menus, key handling, a shortcut overview (F1) and tests all come from it.
- New commands: close tab, next and previous tab, tab 1 to 9, move focus between areas (F6), zoom,
  show or hide the navigator, add filter, next and previous referring record, Back and Forward,
  go to record number, copy record, and the shortcut overview.
- A record-actions menu opens with Enter, Shift+F10, the Menu key or a right-click. It offers
  every reference the record can follow and every table that refers to it.
- The navigator opens a table on Enter or a click, no longer whenever the selection moves.
- After a navigation, focus lands on the record reached. Wide tables can be scrolled sideways from
  the keyboard. Every focusable control shows a visible focus indicator.
- On Windows and Linux, menus get access keys in both languages.

**Descriptive text**
- Every input in the filter, sort and figure editors has an accessible name. Their error messages
  are announced.
- Reading progress, navigation outcomes, the "filter removed" notice and the stepping status are
  announced to assistive technology.
- Each grid row exposes a name built from its record number and values. The grid stays a list; it
  is not exposed as a table.
- Where a value leads, today only in a tooltip, can also be found from the keyboard.

**Ongoing work**
- Automated checks for contrast, accessible names, unique shortcuts and the main keyboard paths.
- `docs/accessibility.md` says what is supported, what is known not to be, and how to report a
  barrier.

**Not in this change**
- Exposing the grid to screen readers as a table with row and column headers, which needs custom
  automation peers.
- Detecting the high contrast setting on Linux, which Avalonia does not report.
- A formal conformance claim or a BITV accessibility statement.

## Capabilities

### New Capabilities
*(None)*

### Modified Capabilities
- `reader-ui`: Theme requirements gain high contrast and contrast values that are actually
  checked. The interface becomes zoomable and the navigator adjustable and collapsible.
  Preferences keep the new settings and are read field by field. The baseline accessibility
  requirement becomes full keyboard operation through a defined command set: a record-actions
  menu, Back and Forward, go to record, copy record, a shortcut overview, and focus that follows
  navigation. Controls, editor fields and grid rows get accessible names, and status messages are
  announced.

## Impact

- **UI project** `src/GoBd.Reader.Ui`:
  - colour tokens and high contrast mapping (`ReaderTheme`, `App`);
  - preferences (`UserPreferences`, `PreferencesStore`) and the settings window;
  - the main window: layout, splitter, menus, key handling and history;
  - the table tab: grid cells, record actions, editors;
  - the start page;
  - a new command table and shortcut overview window;
  - new English and German texts in `UiText`.
- **View models** `ReaderTabs` and `Navigation`: history of navigations, and going to a record
  number by the same rules as following a reference.
- **Dependencies**: none new. This uses Semi.Avalonia 12.1.0.1's high contrast variants and
  Avalonia 12.1.3's `PlatformColorValues.ContrastPreference`, `LayoutTransformControl` and
  `GridSplitter`.
- **Tests** `tests/GoBd.Reader.Ui.Tests`: contrast checks across tokens and variants, a check of
  accessible names across the window, checks for unique shortcuts, headless keyboard tests, and
  tests for reading preferences field by field.
- **Docs**: a new `docs/accessibility.md`, and the README links to it.
