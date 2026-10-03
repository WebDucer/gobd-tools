# Design: Improve Reader Accessibility

## Context

See `proposal.md` for the motivation and `specs/reader-ui/spec.md` for the behaviour. This
section only records the current state and the constraints that shape the approach.

- **Colours.** The UI is built in code (`App`, `MainWindow`, `TableTabView`, `StartPageView`).
  Colours come from Semi.Avalonia 12.1.0.1 keys through `ReaderTheme`: `SemiColorPrimary`,
  `Danger`, `Success`, `Border`, `Background0/1` and `PrimaryLight`. Secondary text is dimmed with
  control `Opacity` (0.6–0.75) in about 15 places.
- **Measured contrast.**
  - Light: Danger `#F93920` is 3.73:1 and Success `#3BB346` is 2.72:1; both fail. Primary
    `#0064FA` passes at 5.0:1 on white, but not on every row background (see D1).
  - Dark: everything passes.
- **Semi's high contrast.** Semi ships the variants `SemiTheme.Aquatic`, `Desert`, `Dusk` and
  `NightSky`. They inherit from Dark, Light, Dark and Dark respectively, and are defined in
  Windows system colour names (Window, WindowText, Hotlight, GrayText, Highlight, HighlightText).
  They do **not** define Danger, Success or PrimaryLight.
  - In every HC variant, Hotlight on Highlight measures 1.0–1.8:1. A link drawn on a selected row
    would be invisible.
- **The OS high contrast setting.** Avalonia 12.1.3 reports it as
  `PlatformColorValues.ContrastPreference`.
  - Windows: from the scheme name; "White" is light, all others dark.
  - macOS: from the high contrast appearances.
  - Linux: not reported.
- **The grid.** `TableView` derives from `ListBox`. Rows can take focus and are reached with the
  arrow keys; cells cannot. No automation peers exist for the table, its rows or its cells.
- **Following references.** Following a key reacts only to `PointerReleased`
  (`TableTabView.OnPointerReleased`). The referrer menu opens from `ContextRequested`, at the
  pointer.
- **Navigator.** `navigator.SelectionChanged` opens tables (`MainWindow.OnNavigatorChanged`), so
  arrow keys open a tab for every table passed.
- **Shortcuts.** Each is defined twice, as a `NativeMenuItem.Gesture` and again in a window
  `KeyDown` handler for non-macOS. On macOS the native menu handles key equivalents, before any
  focused control sees them.
- **Font sizes.** Semi's font sizes are `StaticResource` aliases (`ButtonDefaultFontSize` points to
  `SemiFontSizeRegular`), so overriding one token at runtime does not reach the controls. The
  reader itself hard-codes about 20 `FontSize` values and many widths: navigator 300, grid columns
  70/90/160, labels 140, pickers 180, Settings 420.
- **Other facts that shape the approach.**
  - Avalonia's `GridSplitter` moves with the arrow keys (`KeyboardIncrement`).
  - Avalonia's Windows (UIA) and macOS automation back ends raise live region changes.
- **Preferences.** They are stored as JSON with enums written by name. An unknown name makes the
  source-generated converter throw, and `PreferencesStore.Load` then falls back to defaults for
  *every* field.

## Goals / Non-Goals

**Goals:**
- Every colour the reader chooses itself comes from a token defined for every appearance, and a
  test checks its contrast.
- Every command is defined in one place, from which menus, key handling, the shortcut overview and
  tests all derive.
- Keyboard paths reuse the existing navigation code (`ReaderSession.Follow` and `FollowBack`,
  `ReaderTabs.ApplyAsync`), so the keyboard and the mouse cannot diverge.
- Scrolling performance does not regress: cells stay flat single `TextBlock`s, as decided in the
  modernize-reader-ui change (D3).

**Non-Goals:**
- Custom automation peers for the grid (table, row and cell patterns).
- Scaling Semi's own `ComboBox` drop-downs and tooltips if the overlay spike (D5) fails. The spec
  asks only for the reader's dialogs, editors and record actions to be scaled.
- Rewriting the UI in XAML, or changing Semi's control templates beyond focus visuals.

## Decisions

### D1 — The reader's own colour tokens, defined per appearance

**Choice.** Add reader-owned keys to `Application.Resources.ThemeDictionaries`, keyed by `Light`,
`Dark`, `SemiTheme.Aquatic` and `SemiTheme.Desert`. All reader code refers to these keys only,
through `ReaderTheme` constants and `DynamicResource`.

| Token | Light | Dark | HC (Aquatic / Desert) |
|---|---|---|---|
| `ReaderLink` | `#004FB3` Blue7 | `#7FC1FF` Blue6 | Hotlight |
| `ReaderLinkOnSelection` | `#004FB3` | `#A9D7FF` Blue7 | HighlightText |
| `ReaderDangling` | `#B2140C` Red7 | `#FD9983` Red6 | WindowText |
| `ReaderDanglingOnSelection` | `#B2140C` | `#FDBEAC` Red7 | HighlightText |
| `ReaderConformant` | `#25772F` Green7 | `#5DC264` | WindowText |
| `ReaderDefective` | `#B2140C` | `#FD9983` | WindowText |
| `ReaderMutedText` | Grey9 at 80 % | `#F9F9F9` at 80 % | GrayText |
| `ReaderBackground`, `ReaderSurface`, `ReaderChip` | Semi's Background0, Background1, PrimaryLight | same | Window |
| `ReaderSurfaceBorder` | Semi's Border | same | WindowText |
| `ReaderFocus` | `#0064FA` | `#54A9FF` | WindowText |

The light and dark values were chosen against the backgrounds Semi actually resolves, not only
the window: the grid, a hovered row, a selected row (`#EAF5FF` in light) and a hovered selected row
(`#CBE7FE`). Semi's Primary `#0064FA` reaches only 3.9:1 on the last, so links in light use Blue7.
High contrast values are taken from Semi's own HC brushes when the application starts, so they
cannot drift from Semi.

**Why reader-owned keys rather than overriding Semi's.**
- Semi's colours come in sets of three: Danger is Red5, Pointerover Red6, Active Red7. Moving only
  `Danger` would make it equal to its own hover state.
- Several Semi templates reach these colours through `StaticResource` aliases, which an override
  does not reach.
- The HC variants need values for keys Semi does not define in them anyway.

**Removing dimming.** `Opacity` on text is removed. Muted text uses `ReaderMutedText`, and the
close-glyph buttons get the same token as their foreground.

**Selected rows.** Followable and dangling cells stop setting `Foreground` locally. A local value
outranks any style, which is what would make links invisible on a selected row in HC. Instead each
cell gets a class, `link` or `dangling`, and styles in `App` set `Foreground` from the tokens. One
more style, `TableViewRow:selected TextBlock.link` (and its `.dangling` twin), switches to
`ReaderLinkOnSelection` / `ReaderDanglingOnSelection`. The same styles apply to
`TableViewRow:pointerover`, because high contrast draws a hovered row in the selection colour too.
The cell stays a single `TextBlock` (performance).

### D2 — Contrast is checked by a test, not asserted by the spec

For each appearance (Light, Dark, Aquatic, Desert), a headless test does the following:
- sets `RequestedThemeVariant`;
- resolves every reader token and the background it is drawn on: the window, the grid, a card, a
  chip, an editor's flyout, and Semi's selected, hovered and hovered-selected row backgrounds for
  the on-selection pairs;
- composites alpha over that background;
- asserts the WCAG ratio: 4.5 for text, and 3.0 for the focus indicator and, in high contrast,
  for the edges of cards and chips.

A token added later without a passing value fails the build.

### D3 — Appearance choice and following the OS setting

- **Values.** `ThemePreference` gains `HighContrastDark` and `HighContrastLight`, mapped to
  `SemiTheme.Aquatic` and `SemiTheme.Desert`.
- **Two variants, not four.** Aquatic is the counterpart of Windows' default "High Contrast Black".
  Avalonia cannot tell which Windows scheme is active, so offering all four would only invite a
  mismatch.
- **Following the OS.** For `System`, the window reads `PlatformSettings.GetColorValues()`:
  - `ContrastPreference == High`: Aquatic or Desert, according to the reported light/dark variant;
  - otherwise: `ThemeVariant.Default`.
- **Live changes.** It re-evaluates on `PlatformSettings.ColorValuesChanged` while `System` is
  chosen, so a change in the OS applies without a restart.
- **Linux.** Avalonia reports no contrast preference there, so `System` stays light/dark only.

### D4 — Preferences read field by field

`StoredPreferences` holds every field as a nullable primitive: theme and language as strings, zoom
as an integer percentage, navigator width as a double, collapsed as a bool. Reading:
- Each field is parsed on its own: enums with `Enum.TryParse`, ignoring case, and still accepting
  the numbers earlier builds wrote; numbers are clamped to their range.
- A field that is missing or unknown takes its default. Only a file that is not JSON at all falls
  back entirely.

Enums are still written by name.

**Alternative rejected:** a custom JSON converter. Plain strings stay trimming-safe with the
existing source-generated context and need no reflection.

### D5 — Zoom with a layout transform

**Window content.** The main window's content, including the in-window `NativeMenuBar` on
Windows/Linux, is wrapped in a `LayoutTransformControl` with a `ScaleTransform`.
- **Steps:** 100, 110, 125, 150, 175, 200 %.
- **Shared scale.** A small `ZoomLevel` object on `App` holds the current scale; every window
  binds to it.
- **Other windows.** Settings, About, the text windows and the shortcut overview wrap their content
  the same way.

**Why not font tokens.** Overriding Semi's per-control font sizes means touching dozens of keys.
The reader's own fixed widths would clip the text, and icons and paddings would stay small. A
transform enlarges everything in proportion, and WCAG accepts zoom as text resizing (1.4.4).

**Popups** (flyouts, menus, drop-downs, tooltips) open in their own top level, so the window's
transform does not reach them.
- **Spike first:** enable `OverlayPopups` on every platform and move the transform above the
  window's `VisualLayerManager`, through a window theme. Every popup would then scale with the
  window. The cost is that popups cannot extend beyond the window, which a full-size reader window
  barely notices.
- **Fallback if the spike fails:** wrap the content of the reader's own popups, meaning the
  filter, sort, figure and go-to editors and the record actions, in their own
  `LayoutTransformControl`. Semi's drop-downs and tooltips then stay at 100 %, recorded as a known
  limitation in `docs/accessibility.md`.

**Spike outcome: fallback adopted.** A headless spike put Semi's window template's
`VisualLayerManager` inside a 150 % `LayoutTransformControl`, with overlay popups as the headless
platform uses them. The content of flyouts, drop-downs and tooltips did scale. Their *placement*
did not: the popup host positions in the layer's own coordinates with a target position that
already includes the scale, so a flyout and a drop-down landed about 1.5× as far from their target
as they should, and a tooltip landed at the window's corner. Fixing that would mean replacing
Avalonia's popup positioning, and on the desktop it would also mean switching every platform to
overlay popups. So the reader scales the content of its own popups instead:
- **Flyouts** (the filter, sort and figure editors, Go to Record) wrap their content in the same
  zoom control as the windows.
- **The record-actions menu** is a `MenuFlyout`, whose presenter cannot be wrapped without copying
  Semi's template, so each of its entries takes a font size scaled by the zoom level.
- **Semi's drop-down lists and tooltips** stay at 100 %. This is listed as a known limitation in
  `docs/accessibility.md`.

**Windows beside the reader** (`ReaderWindows`) keep within the screen. Settings and About size
themselves to their content, and at 200 % About would be taller than a laptop screen. So when such
a window opens, its maximum size is set to the screen's working area, and its content scrolls. The
same helper moves focus into the window's first control (or its scroller, where it has nothing
else), so a dialog can be used from the keyboard at once.

**Settings** offers the zoom steps in a picker. The View menu and command +/−/0 change it.
Plus/minus match `OemPlus`/`Add` and `OemMinus`/`Subtract`, so that a German keyboard's `+` key
works without Shift.

### D6 — An adjustable, collapsible navigator

**Layout.** The window's layout grid gets three columns:
- the navigator, its width taken from preferences, `MinWidth` 160 and `MaxWidth` half of the
  unscaled window width;
- a `GridSplitter` with an accessible name, which handles the arrow keys itself;
- the tabs.

**Collapsing** swaps the navigator and splitter for a narrow strip holding a single button, "Show
navigator", that restores them. The command (View menu, Ctrl+B or Ctrl+Cmd+S) toggles the same
state.

**Focus.** If the navigator holds keyboard focus when it collapses, focus moves to the tab in
front.

**Selection** keeps tracking the tab in front while collapsed (the `syncing` path in
`OnTabChanged`), so the navigator is current when restored.

**Persistence.** Width is stored in unscaled units, so zoom scales the navigator in proportion and
a stored width means the same thing at every zoom level. Nothing collapses automatically.

### D7 — One command table

**The record.** A `ReaderCommand` has:
- an id;
- a localized label (`Func<ReportLanguage, string>`) with a menu access-key marker;
- gestures for macOS and for Windows/Linux;
- a scope: `Global`, `Records` or `Navigator`;
- a menu placement (menu, group, order);
- `CanExecute` and `Execute`.

`MainWindow` builds the table with its handlers. Everything else derives from it:
1. **Native menus** — File, View, Go, Table, Help. On macOS the menu bar answers the shortcuts it
   matches before the window sees them.
2. **Window key handling** for `Global` commands, through a *tunnelling* `KeyDown` handler, so
   that `Ctrl+Tab` is seen before focus navigation consumes it (see "Who answers a menu's shortcut"
   below).
3. **The shortcut overview window** (F1), grouped by scope and written as the platform writes
   gestures.
4. **Tests**:
   - no gesture is bound twice within a scope and platform;
   - every `Global` command appears in a menu;
   - every label and access key exists in both languages.

**Who answers a menu's shortcut.** The window answers every shortcut that reaches it, on every
platform. On macOS the menu bar sees a ⌘ shortcut first and keeps the ones it answers, so those
never reach the window. But it matches by the character a key types, and shows Avalonia's keys as
they type on a US keyboard. A first version left every shortcut a menu entry shows to the menu bar.
On a German keyboard, Zoom In then did nothing at all: the menu showed "⌘=" for `Key.OemPlus`, the
"+" key never matched it, and the window ignored the key it was handed (found in use, not by a
test). Two consequences:
- **Zoom In on macOS is written as the number pad's plus** (`Key.Add`), which the menu shows as
  "⌘+". Avalonia matches the number pad's plus, minus and point as the same keys as the others, so
  one shortcut answers the "+", "=" and number pad keys alike.
- **One key press runs a command once.** Menu entries and the window run a command through one
  method. It ignores the same command arriving from the other of the two within a quarter of a
  second, so a key press both answered cannot close two tabs.

**Records-scope commands** (record actions, copy record, sideways scrolling) are bound on the grid,
not in native menus. On macOS a menu key equivalent for Cmd+C would otherwise take copy away from
every text field. They appear in the record-actions menu with their gesture shown as
`InputGesture` (display only).

**Access keys.** Menu titles carry access-key markers (`_Datei`, `_File`) in `UiText`. On macOS the
markers are stripped.

**Command state.** `ActionCommand` gains `CanExecute` and a way to raise `CanExecuteChanged`. The
window raises it when the active tab, the walk or the history changes, so menu items enable and
disable correctly.

### D8 — The record-actions menu replaces the referrer menu

`OfferReferrers` becomes `OfferRecordActions(table, row, anchor, fromKeyboard)`. It builds a
`MenuFlyout` with:
1. one item per outgoing foreign key, "Follow KontaktNr → Kontakte". Items whose value refers to
   nothing are marked "(refers to nothing)" and stay selectable, because following them already
   reports that outcome;
2. one item per referring table, "Records in Zahlungen referring to this one";
3. "Copy record".

Items call the existing `Follow` and `FollowBack` paths unchanged.

**Opening it.**
- Enter or the Apps key on a focused row, and `ContextRequested`, which covers right-click and
  Shift+F10.
- When `ContextRequestedEventArgs.TryGetPosition` fails (keyboard), the menu is placed at the
  focused row's container rather than at the pointer.

A click on a cell keeps following directly (`OnPointerReleased` is unchanged).

### D9 — The navigator opens on request

`SelectionChanged` no longer opens tables. Instead:
- `Enter` (tunnelling `KeyDown` on the navigator) opens the selected table and focuses its
  records;
- a pointer release on a table's `TreeViewItem` opens it.

Syncing the selection from the tab in front is unchanged.

### D10 — Focus follows navigation

`TableTabView.Position()` gains a `focus` flag. After `ScrollIntoView`, it posts at `Loaded`
priority, takes the realized container for the position, and focuses it with
`NavigationMethod.Directional`, so the focus indicator shows.

- **Navigations** (`Apply`, `Step`, Back/Forward, Go to record) pass `focus: true`.
- **Tab switches from the keyboard** (Ctrl+Tab, Ctrl+1..9, closing a tab) pass `focus: true`.
- **Clicking a tab header** leaves focus alone.

### D11 — Back and Forward

`NavigationHistory` lives in `ReaderTabs`, which exists once per opened export, so a new export
starts empty.
- **Entries.** It holds `Place(TableNode Table, long? Ordinal)` entries and an index, as a browser
  does.
- **Recording.** `Follow`, `FollowBack` (the start of a walk) and Go to record record the origin
  place and the destination. Anything after the current index is discarded first.
- **Walk steps** record nothing. Back from a walk therefore returns to the walk's origin.
- **Places are stored by record number,** not position, because positions change with every
  filter and sort.
- **Going back or forward** builds a `Navigation(Positioned, …)` for the place, with the position
  the table's own rows give for that record number, and sends it through the existing
  `ReaderTabs.ApplyAsync`. It therefore inherits filter lifting with a notice. If the table's tab
  was closed, `Open` creates a new one.

### D12 — Go to record

A small flyout anchored at the grid offers:
- a number field, read with the display language's group separator;
- "Go";
- a complaint line, announced assertively.

The number must be one of the table's record numbers, `1..RecordCount` for a table that shows its
data. If it is, a `Navigation(Positioned, table, n, rows.IndexOfOrdinal(n), …)` goes through
`ApplyAsync`, so a hiding filter is lifted with the same notice. If not, the flyout says so and the
table stays where it was.

### D13 — Copy record

Command+C on the focused record, or "Copy record", writes two tab-separated lines to the
`TopLevel` clipboard:
- `UiText.RecordColumn` followed by the declared column names;
- the record number followed by the values as shown.

The view's position column (`#`) is left out: it is not data.

A value containing a tab, line break or quotation mark is enclosed in quotation marks with its own
quotation marks doubled, the convention Excel and LibreOffice paste correctly.

### D14 — Scrolling sideways

A `KeyDown` handler on the grid, for Left and Right without modifiers, moves the template's
`PART_ScrollViewer` (named in `ReaderTheme.CreateTableViewTheme`) horizontally by one step and
marks the key handled. Selection and focus stay on the row.

### D15 — Names, record names and announcements

**Editor inputs.** Every input gets `AutomationProperties.Name` (or `LabeledBy` where a visible
label exists): column picker, comparison picker, value and second-value fields, case checkbox,
sort direction, figure kind. Names are set again in `ApplyTexts` on a language change.

**Record names.** `TableView.ContainerPrepared` sets each `TableViewRow`'s
`AutomationProperties.Name`:
- format: "Record 17: KontaktNr 4711, Name Müller, …";
- a value that refers to nothing gets "(refers to nothing)" after it.

`ContainerPrepared` also fires when a container is recycled, so names follow the data. The grid is
rebuilt on a language change anyway.

**Announcements.** Each window gets one **announcer**: a zero-size, transparent `TextBlock`, kept
in the visual tree so it stays in the automation tree. It has `AutomationProperties.LiveSetting`
and `Announce(text, assertive)`, which sets its name and text; the platform back ends raise the
live region change. Everything announced goes through it:
- reading progress, at every 10 % and whenever a table finishes;
- navigation outcomes;
- "filter removed";
- the walk position;
- "table not ready";
- editor complaints, assertively.

Tests read what was announced from the announcer.

**Why one announcer** rather than `LiveSetting` on every status `TextBlock`: the visible progress
text changes every 100 ms, and announcing that would drown a screen reader. One element also gives
tests one place to look.

### D16 — Focus indicators

- **Semi's own indicator, in the reader's colour.** Semi already draws a 2 px focus adorner around
  every focused control (`AdornerLayer.DefaultFocusAdorner`), in the adorner layer and therefore
  regardless of the control's own border. Only its colour falls short: `#98CDFD` in light
  (about 1.7:1) and Primary at 40 % in dark. The light and dark dictionaries set
  `AdornerLayerBorderBrush` to `ReaderFocus`; Semi's high contrast value is already WindowText.
  This replaces a focus style of the reader's own, which would have duplicated the adorner Semi
  draws anyway.
- **Close-glyph buttons** lose their local `Background`, `BorderThickness = default` and
  `Opacity`. They use Semi's borderless button theme, so their pointer states remain, and
  `ReaderMutedText` as their foreground.

### D17 — Menus

| Menu | Entries |
|---|---|
| File | Open Archive…, Open Folder…, Close Tab, Settings… (not on macOS) |
| View | Zoom In, Zoom Out, Actual Size; Show Navigator (check); Next Tab, Previous Tab |
| Go | Back, Forward; Go to Record…; Next / Previous Referring Record |
| Table | Add Filter…, Add Sort Column…, Add Figure…, Back to File Order |
| Help | Keyboard Shortcuts; Licence, Notice, Third-Party Notices; About (not on macOS) |

On macOS, Settings and About stay in the application menu, as now.

"Add Filter…" from the menu or with command+F opens the add-filter button's flyout and focuses its
first input, exactly like clicking the button.

**The keyboard stays in an editor.** A picker gives focus to its list while the list is open, and
Avalonia gives it back only to a picker that can be typed in. Once a column had been chosen from its
list, focus was on a list that had closed, and on macOS, where an editor is a window of its own, it
left the editor. The direction, the figure and Apply could then not be reached from the keyboard
(found in use). Every picker in an editor, and in Settings, now takes focus back when its list
closes, unless the person has put focus elsewhere in the editor. Tab cycles within the editor.

### D18 — Shortcut assignments

These follow platform conventions; the spec lists them. A few choices need explaining:
- **Next/previous referring record.** F3 / Shift+F3 elsewhere and Cmd+G / Cmd+Shift+G on macOS,
  each platform's "find next".
- **Go to record.** Ctrl+G elsewhere, as in Excel and most Windows editors. On macOS that key is
  "find next", so Cmd+L is used, as Xcode and Sublime use it for "go to line".
- **Show/hide navigator.** Ctrl+Cmd+S on macOS (the system's "Show Sidebar") and Ctrl+B elsewhere
  (VS Code's sidebar toggle).
- **Tabs by number.** Command+1..9 brings the tab in that place to the front, the summary being 1.
  Next and previous tab wrap around, so the last tab is one key away from the summary.

### D19 — Tests and documentation

**Headless tests** use `KeyPress` from Avalonia.Headless and cover:
- each keyboard path in the spec: navigator Enter, record actions, follow and return with Back,
  Go to record including the filter case, copy, close tab, F6 cycling, zoom, collapsing the
  navigator;
- the contrast matrix (D2);
- the command table (D7);
- a **name sweep**: with an export open, a table tab, and each editor open, every focusable control
  in the window must yield a non-empty name from `ControlAutomationPeer.CreatePeerForElement(...)
  .GetName()`. This replaces `AccessibilityTests`' button-only check.

**`docs/accessibility.md`** records:
- the yardstick;
- what is supported per platform;
- the known limitations: the grid is not a table to screen readers; no contrast detection on
  Linux; possibly the popups (D5);
- how to report a barrier (GitHub issue, label `UI/UX`).

## Risks / Trade-offs

- **[Risk] The layout transform slows scrolling at high zoom.**
  → Fewer rows are realized at higher zoom, and the transform is a render-time scale. Extend
  `TableViewScrollTests` with a 200 % case and compare realized rows and frame time against 100 %
  manually.
- **[Risk] The overlay-popup spike fails or breaks popup placement.**
  → Fall back to wrapping the reader's own popups (D5). The spec only requires those.
- **[Risk] Live region announcements behave differently per screen reader (NVDA, Narrator,
  VoiceOver), and Linux has no Avalonia AT-SPI bridge.**
  → One announcer keeps the behaviour testable. Check manually with NVDA and VoiceOver, and list
  the results in `docs/accessibility.md`.
- **[Risk] Record names for 50-column tables are long for a screen reader.**
  → The record number comes first, so a listener can move on. This is the cost of not exposing
  table semantics, recorded as a limitation.
- **[Risk] Ctrl+Tab, F6 and F-keys are swallowed by focus navigation or the OS, or need Fn on Mac
  laptops.**
  → A tunnelling window handler sees keys before focus navigation does. Every command also stays
  in a menu, and Tab still moves through all controls.
- **[Risk] Semi's HC variants may style some Semi control in a way our tokens do not cover.**
  → The name sweep and the contrast matrix run under Aquatic and Desert too. Check the remaining
  visuals by hand in both.
- **[Trade-off] Two HC variants instead of four.** People who use Windows' Dusk or Night sky get
  Aquatic in the reader.
- **[Trade-off] Overriding nothing of Semi's palette.** Semi's own danger and success controls
  keep their colours, but the reader uses none of them.

## Migration Plan

- **New readers, old preferences files:** read as before. Missing fields take their defaults.
- **Old readers, files written by a new reader:** an older reader that meets `HighContrastDark` or
  `HighContrastLight` falls back to defaults entirely, as it already does for anything it cannot
  parse. This cannot be fixed in builds already released. The release notes mention it.
- **Rollback:** reverting the change restores the previous behaviour. The preferences file is the
  only persistent state, and it is covered above.

## Open Questions

- **Zoom steps** (100/110/125/150/175/200) and the **navigator's minimum width** (160): tune after
  trying them in the running reader.
- **German menu access keys:** final letters, chosen when the texts are written, avoiding clashes
  within each menu.
