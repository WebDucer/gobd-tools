# Tasks

## 1. Colour tokens, contrast and focus visuals

- [x] 1.1 Define the reader's colour tokens (`ReaderLink`, `ReaderLinkOnSelection`, `ReaderDangling`, `ReaderDanglingOnSelection`, `ReaderConformant`, `ReaderDefective`, `ReaderMutedText`, `ReaderBackground`, `ReaderSurface`, `ReaderSurfaceBorder`, `ReaderChip`, `ReaderFocus`). Put them in `Application.Resources.ThemeDictionaries` for `Light`, `Dark`, `SemiTheme.Aquatic` and `SemiTheme.Desert`, with the values from design D1, and add a constant for each to `ReaderTheme`. Verify with a headless test that every token resolves under all four variants.
- [x] 1.2 Add `ContrastTests`. For each of the four variants, resolve every text token and its background, composite alpha, and assert 4.5:1, or 3:1 for `ReaderFocus`, Semi's focus indicator and, in high contrast, `ReaderSurfaceBorder`. Include the on-selection pairs against Semi's selected, hovered and hovered-selected row backgrounds. Verify the test passes, and fails when Light `ReaderDangling` is temporarily set to Semi's Red5 `#F93920`.
- [x] 1.3 Replace the Semi colour keys used directly in `StartPageView`, `TableTabView`, `MainWindow`, `SettingsWindow` and `AboutWindow` with the reader's tokens, and replace every `Opacity` on text with `ReaderMutedText`. Verify: no `Opacity` setting on a `TextBlock` or text button remains (grep), and the existing UI tests pass.
- [x] 1.4 Mark followable and dangling cells with the classes `link` and `dangling` instead of a local `Foreground`. Add `App` styles for both, plus `TableViewRow:selected` and `:pointerover` variants that use the on-selection tokens, keeping one `TextBlock` per cell. Extend `GridCellTests`: such a cell has its class, has no local foreground, and under Aquatic resolves HighlightText when its row is selected.
- [x] 1.5 Point Semi's focus indicator (`AdornerLayerBorderBrush`) at `ReaderFocus` in light and dark (design D16). Move the tab and chip close buttons to Semi's borderless button theme, with no local background, border thickness or opacity. Verify with headless tests that Semi's focus indicator resolves to `ReaderFocus`, and that those buttons carry no local `Background` or `BorderThickness`.
- [x] 1.6 Create `docs/accessibility.md`: the yardstick (WCAG 2.1 AA as BITV 2.0 / EN 301 549 apply it, no conformance claim), a section on colours and contrast, and how to report a barrier (GitHub issue, label `UI/UX`). Link it from `README.md`, and verify the link resolves.

## 2. Appearances and preferences

- [x] 2.1 Check with a headless test that `SemiTheme` resolves its high contrast resources under `Aquatic` and `Desert` without extra includes, for example `SemiColorWindow`. If it needs any, add them in `App`. Verify the test passes.
- [x] 2.2 Make `StoredPreferences` and `PreferencesStore.Load` read field by field (design D4). Theme and language become strings parsed with `Enum.TryParse`, still accepting the numbers older builds wrote. Add zoom (percent), navigator width and navigator collapsed, clamped to their ranges. Extend `PreferencesStoreTests`:
  - an unknown theme name keeps the stored language;
  - numeric enums still load;
  - out-of-range zoom or width is clamped;
  - a missing field takes its default;
  - a file that is not JSON gives the full defaults.
- [x] 2.3 Add `ThemePreference.HighContrastDark` and `HighContrastLight`, mapped to `SemiTheme.Aquatic` and `SemiTheme.Desert`. Offer all five choices in `SettingsWindow`, with English and German texts in `UiText`. Verify in `SettingsWindowTests` that choosing High contrast (dark) sets `RequestedThemeVariant` to Aquatic and persists it, and that the `UiTextTests` parity check passes.
- [x] 2.4 Write a pure resolver from (preference, platform contrast preference, platform variant) to `ThemeVariant`, and use it in `MainWindow.ApplyTheme`. Subscribe to `PlatformSettings.ColorValuesChanged` while `System` is chosen. Verify with unit tests covering all combinations: High plus Dark gives Aquatic, High plus Light gives Desert, no preference gives Default, and explicit choices ignore the platform.
- [x] 2.5 Add an "Appearance and high contrast" section to `docs/accessibility.md`, including that Linux reports no high contrast setting. Verify the section is present.

## 3. Command table, menus and workspace shortcuts

- [x] 3.1 Extend `ActionCommand` with `CanExecute` and a way to raise `CanExecuteChanged`. Add the `ReaderCommand` record (id, localized label with access-key marker, gestures for macOS and for other platforms, scope, menu placement, can-execute, execute) and a table builder (design D7). Add `CommandTableTests`:
  - no gesture is bound twice within a scope and platform;
  - every `Global` command is placed in a menu;
  - every label and access key exists in English and German.
- [x] 3.2 Build the native menus File, View, Go, Table and Help from the command table (design D17). Move the existing open, settings and help entries over. Mark access keys on Windows and Linux and strip them on macOS. Update `HelpMenuTests` and the menu-related tests, and verify they pass.
- [x] 3.3 Replace the window's `KeyDown` gesture handling with a tunnelling handler driven by the command table. On Windows and Linux it handles `Global` gestures. On every platform it handles Ctrl+Tab, F6 and command+1..9. It leaves keys that a focused text field uses to the field. Verify with headless `KeyPress` tests: command+O still opens the picker path, and Ctrl+C inside a filter `TextBox` is not handled by the window.
- [x] 3.4 Implement Close Tab (command+W, and Ctrl+F4 on Windows/Linux), next and previous tab with wrap-around (Ctrl+Tab, Ctrl+Shift+Tab, Cmd+Shift+] and Cmd+Shift+[), and tab by number (command+1..9). Focus moves into the tab that comes to the front. Verify with headless tests: closing a table tab, nothing closing with the summary in front, the window staying open, wrap-around in both directions, and command+1 bringing the summary forward.
- [x] 3.5 Add the shortcut overview window, generated from the command table, grouped by scope and written as the platform writes gestures. Open it with F1 and from Help → Keyboard Shortcuts, in the display language. Escape closes it and returns focus. Verify with a headless test that it lists every command that has a gesture, in English and in German.
- [x] 3.6 Add a "Keyboard" section to `docs/accessibility.md` that points to F1 and the menus as the reference for shortcuts. Verify the section is present.

## 4. Zoom and the navigator's layout

- [x] 4.1 Spike design D5: enable `OverlayPopups` and place the scale transform above the window's `VisualLayerManager`. Check whether flyouts, the record menu, drop-downs and tooltips scale and stay correctly placed. Record the outcome, adopted or fallback, under D5 in `design.md`.
- [x] 4.2 Add a `ZoomLevel` on `App` with the steps 100/110/125/150/175/200, and wrap the main window's content, including the in-window menu bar, in a `LayoutTransformControl` bound to it. Add Zoom In, Zoom Out and Actual Size: View menu, command with `OemPlus`/`Add`, `OemMinus`/`Subtract` and `D0`/`NumPad0`. Add a zoom picker to Settings, and persist the level. Verify with headless tests: the keys step the scale within its bounds, the picker applies it, and a new window starts at the stored level.
- [x] 4.3 Apply the zoom to the Settings, About, text and shortcut overview windows (each sized by its content or scrolling, so nothing is clipped), and to the filter, sort and figure editors' flyouts, by wrapping each popup's content (the fallback adopted in 4.1). Go to Record (6.3) and the record actions (5.3) are zoomed where they are built. Verify with headless tests that each window and editor content carries the current scale.
- [x] 4.4 Lay out the navigator in a three-column grid with a `GridSplitter` (accessible name "Navigator width", in both languages). Bound the width at 160 and half the unscaled window width, and persist it unscaled. Verify with headless tests that arrow keys on the focused splitter change the width within its bounds, and that a new window restores the stored width.
- [x] 4.5 Add collapsing: a narrow strip with a "Show navigator" button, the View → Show Navigator check item, and Ctrl+B (Ctrl+Cmd+S on macOS). If the navigator has focus when it collapses, focus moves to the tab in front. Selection keeps tracking the tab in front while collapsed, and the state is persisted. Verify with headless tests for each of these, including that changing the zoom does not change the state.
- [x] 4.6 Extend `TableViewScrollTests` with a 200 % zoom case that asserts rows stay virtualized (only the visible rows realized). Compare scrolling at 100 % and 200 % and record the result. Headless measurement: 14 rows realised at 100 %, 7 at 200 %; fifty jumps through 10,000 rows took about 3.5 s at 100 % and 1.4 s at 200 % (headless, so only the ratio means anything). The check on a real display is part of the walkthrough in 8.3.
- [x] 4.7 Add a "Zoom and layout" section to `docs/accessibility.md`, including any popups that do not scale per 4.1. Verify the section is present.

## 5. Keyboard paths in tables and the navigator

- [x] 5.1 Stop the navigator opening tables on `SelectionChanged`. Open on Enter, which then focuses the records, and on a pointer release over a table's entry. Keep the selection syncing to the tab in front. Verify with headless tests: arrow keys through all tables open no tab, Enter opens and focuses the records, and a click opens.
- [x] 5.2 Add focus to `TableTabView.Position()`: after `ScrollIntoView`, focus the realized row container through a dispatcher post. Use it for navigations and for keyboard tab switches. Verify with a headless test that after following a reference, the focused element is the row of the target record number.
- [x] 5.3 Replace `OfferReferrers` with `OfferRecordActions` (design D8). It lists the outgoing keys (with a "refers to nothing" mark), the referring tables and Copy Record. It opens on Enter, the Apps key and `ContextRequested`, and when opened from the keyboard it is placed at the row. Update `ReferrerMenuTests` and add tests: Enter opens it, Shift+F10 opens it, choosing a key follows it exactly as a click does, a dangling key carries its mark, and clicking a cell still follows directly.
- [x] 5.4 Implement F6 and Shift+F6 cycling between the navigator, the table's toolbar and its records (or the summary), skipping a collapsed navigator. Verify with headless tests for both directions and the collapsed case.
- [x] 5.5 Wire Add Filter (command+F and the Table menu), Add Sort Column, Add Figure and Back to File Order to the active tab. Opening an editor focuses its first input, Enter applies it (the editor presses its Apply button itself once the input has not taken the key; a default button does not reach a flyout), and Escape closes it and returns focus to the button that opened it. Verify with headless tests: the filter editor opened with command+F takes focus, Enter applies it, Escape returns focus to its button, Back to File Order from the menu lifts it, the Table menu opens the sort and figure editors and is disabled on the summary; the existing filter, sort and figure tests still pass.
- [x] 5.6 Bind next and previous referring record (F3 and Shift+F3, Cmd+G and Cmd+Shift+G on macOS), enabled only during a walk. Verify with headless tests that the keys step the walk and do nothing outside one.
- [x] 5.7 Scroll the grid sideways with Left and Right (design D14). Verify with a headless test that the horizontal offset changes while the selected index and the focused row stay the same.
- [x] 5.8 Let Cmd+W close the Settings, About, text and shortcut overview windows on macOS, next to Escape everywhere (the text window gains Escape too). Verify with a headless test that each window closes with every closing key of the platform the tests run on, and that the closing keys are Escape and Cmd+W on macOS and Escape alone elsewhere.

## 6. Back and Forward, Go to Record, Copy Record

- [x] 6.1 Add `NavigationHistory` to `ReaderTabs` (design D11). It holds places by record number, discards forward entries on a new navigation, records the origin of a walk, and is not affected by tab switches or navigator choices. Verify with unit tests in `ReaderTabsTests` covering each of these rules.
- [x] 6.2 Implement Back and Forward (Alt+Left and Alt+Right, Cmd+[ and Cmd+] on macOS, and the Go menu with enablement) through `ReaderTabs.ApplyAsync`. Verify with headless tests:
  - follow then Back returns to the origin record, focused;
  - Forward repeats the navigation;
  - Back into a closed tab reopens it;
  - Back into a view whose filter hides the record removes the filter and shows the notice;
  - opening another export clears the history.
- [x] 6.3 Implement Go to Record (Ctrl+G, Cmd+L on macOS): a flyout with a number field read in the display language, a range check against the table's record count, a complaint on failure, and `ApplyAsync` with filter lifting. It is unavailable on the summary and for tables without data. Verify with headless tests: a valid number positions and focuses the record, "1.234" is accepted in German, an out-of-range number leaves the table unchanged and shows the complaint, and a hidden record lifts the filter.
- [x] 6.4 Implement Copy Record (command+C on the grid, and in the record actions). It writes a header line and a values line, tab-separated, without the position column, quoting values Excel-style. Verify with unit tests of the formatter (tab, line break and quotation mark inside values) and a headless clipboard test. Also verify that command+C inside a filter `TextBox` still copies the field's text.

## 7. Names, record names and announcements

- [x] 7.1 Give accessible names to every input of the filter, sort, figure and Go to Record editors, the reading `ProgressBar`, the navigator splitter and the strip's show button, set again on a language change. Add the texts in English and German to `UiText` and verify the `UiTextTests` parity check passes.
- [x] 7.2 Name each record in `TableView.ContainerPrepared`: "Record N: Column Value, …" with a "refers to nothing" mark, in the display language. Verify with headless tests that a row's automation name matches for a plain record and for a dangling one, and that a recycled container takes the new record's name.
- [x] 7.3 Add one announcer per window (design D15) and send these through it:
  - reading progress (every 10 % and when a table finishes);
  - navigation outcomes;
  - "filter removed";
  - the walk position;
  - "table not ready";
  - editor complaints, assertively.

  Verify with headless tests that read the announcer after each kind of event, including that progress is not announced on every 100 ms update.
- [x] 7.4 Replace `AccessibilityTests` with a name sweep. With the summary open, a table tab open, each editor opened in turn and the Settings window open, every focusable control must yield a non-empty name from its automation peer, in English and in German. Verify the sweep passes.
- [ ] 7.5 Check by hand with NVDA on Windows and VoiceOver on macOS: row names, announcements and the record actions. Record the results and the known limitations (no table semantics, no Linux screen reader bridge) in `docs/accessibility.md`.

## 8. Integration

- [x] 8.1 Run `dotnet test` for the whole solution and verify that every test passes.
- [x] 8.2 Publish the reader self-contained for the local runtime identifier, as the CI publish step does. Verify there are no new trim warnings and that the published reader starts and opens `examples/example-export.zip`.
- [x] 8.3 Walk through the reader without a mouse on Windows and on macOS: open an export, choose a table, filter, follow a reference, go Back, go to a record, copy it, zoom to 200 %, collapse the navigator and switch to high contrast through the OS setting. Record any barrier found as an issue or fix it before archiving.
- [x] 8.4 Run `openspec validate improve-reader-accessibility --strict` and verify the change is valid.
