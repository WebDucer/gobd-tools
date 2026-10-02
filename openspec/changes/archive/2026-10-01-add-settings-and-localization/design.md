# Design: Settings and UI Localization

## Context

The GoBD Reader UI uses Avalonia 12.1 with Semi.Avalonia. Currently, the theme variant follows the operating system automatically, with no mechanism for user override. Furthermore, finding codes support German and English in `MessageCatalogue.cs`, but the UI hardcodes `ReportLanguage.English` in all call sites (`MainWindow`, `StartPageView`, `TableTabView`), and UI labels (menus, tabs, toolbars, buttons, empty states) are in English only.

See `proposal.md` for motivation and `specs/reader-ui/spec.md` for requirement specifications.

## Goals / Non-Goals

**Goals:**
- Provide a dedicated, accessible `SettingsWindow` to configure appearance theme and display language.
- Persist user preferences across sessions in the standard platform user configuration directory.
- Apply appearance theme changes dynamically in real time without application restart.
- Support full English and German localization across findings, verdicts, menus, tabs, and toolbars.
- Ensure 100% trim-safety, single-file publish compatibility, and zero reflection overhead.

**Non-Goals:**
- Supporting arbitrary third-party language packs or runtime resource loading from external files.
- Modifying the CLI's language resolution rules (CLI continues to use environment variables and flags).
- Cloud synchronization of user preferences.

## Decisions

### D1 — Preferences Data Model & Cross-Platform Storage (`PreferencesStore`)
- **Choice:** Define a simple record `UserPreferences(ThemePreference Theme, ReportLanguage Language)` and a store `PreferencesStore`.
  - Windows: `%APPDATA%\gobd-reader\preferences.json`
  - macOS: `~/Library/Application Support/gobd-reader/preferences.json`
  - Linux: `~/.config/gobd-reader/preferences.json` (respecting `$XDG_CONFIG_HOME`)
- **Fault-Tolerance:** If the file does not exist, or the environment denies write access (e.g., restricted auditor sandbox), the application defaults to `ThemePreference.System` and the OS-detected language (`LanguageResolver`), retaining any changes in-memory for the current session.
- **Alternatives Considered:**
  - *Registry (Windows) / Defaults (macOS):* Rejected because a single JSON file is cross-platform, inspectable, and requires no platform P/Invoke interop.

### D2 — Settings Interface (`SettingsWindow`)
- **Choice:** Implement a dedicated window `SettingsWindow` matching `AboutWindow` and `TextWindow`:
  - Navigation: Opened via `GoBD Reader → Settings…` (`Cmd+,`) on macOS, `File → Settings…` (`Ctrl+,`) on Windows and Linux.
  - Controls: Two ComboBox pickers:
    1. Appearance: `System default` / `Light` / `Dark`
    2. Language: `English` / `Deutsch`
  - Reactivity: Selection immediately updates the application and persists to disk; window can be dismissed via "Close" button or Escape/standard window close.
- **Alternatives Considered:**
  - *Tab in MainWindow:* Rejected because settings do not represent an opened table and would clutter the data navigation tab bar.

### D3 — Dynamic Theme Variant Application
- **Choice:** When the theme preference is changed or initialized:
  ```csharp
  Application.Current.RequestedThemeVariant = preference switch
  {
      ThemePreference.Light => ThemeVariant.Light,
      ThemePreference.Dark => ThemeVariant.Dark,
      _ => ThemeVariant.Default,
  };
  ```
  Because the application uses dynamic resource bindings, all views adapt immediately.

### D4 — Strongly Typed UI Catalogue (`UiText`)
- **Choice:** Create `UiText` as a static, strongly typed dictionary in C# taking `ReportLanguage` as an argument for every string:
  ```csharp
  public static string SummaryTab(ReportLanguage language) =>
      language == ReportLanguage.German ? "Übersicht" : "Summary";
  ```
- **Rationale:** 
  - 100% compile-time verified: missing translations fail `dotnet build`.
  - Zero reflection: trimmer-safe and NativeAOT-ready.
  - Matches `ReportText.cs` and `MessageCatalogue.cs` conventions already established in the codebase.
- **Alternatives Considered:**
  - *.resx files:* Adds MSBuild generator overhead, runtime ResourceManager reflection, and culture sensitivity issues under trimmed environments.

### D5 — Dynamic Language Refresh
- **Choice:** When the language preference changes, `MainWindow` updates its active language and triggers:
  1. Menu bar re-creation / text updates.
  2. `StartPageView.Show(...)` re-render (which translates headers and passes the new language to `MessageCatalogue.Render(...)`).
  3. `TableTabView.Refresh()` across all open tabs (which updates toolbar labels, chip texts, and finding messages).

## Risks / Trade-offs

- **[Risk] Read-only user directory prevents saving:**
  → *Mitigation:* `PreferencesStore.Save` catches I/O and security exceptions silently, logging or holding the state in-memory so the reader never crashes.
- **[Risk] Active query or filter chips break layout when switching language:**
  → *Mitigation:* Chip descriptors format comparisons using symbols (`=`, `≠`, `<`, `>`) and column names from `index.xml`, while textual comparisons (`contains` vs `enthält`) adapt cleanly through `UiText`.
