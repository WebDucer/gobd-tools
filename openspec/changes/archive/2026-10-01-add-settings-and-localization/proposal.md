# Proposal

## Why

The GoBD Reader currently runs only in the system-detected theme with no in-app control to override it, and all user interface elements (menus, tabs, toolbars, buttons, and summary headings) are hardcoded in English even though finding codes already support German translations in `MessageCatalogue`. Users need a dedicated Settings interface to customize their appearance (System, Light, Dark) and display language (English, German) with preferences persisting seamlessly across application launches.

## What Changes

- **Settings Window & Menus:**
  - Introduce a dedicated, accessible `SettingsWindow` reachable via platform standard shortcuts (`Cmd+,` on macOS, `Ctrl+,` on Windows/Linux) and menu items (`GoBD Reader → Settings…` / `File → Settings…`).
  - Allow selecting the active **Theme**: `System default`, `Light`, or `Dark`.
  - Allow selecting the active **Language**: `English` or `Deutsch`.
- **Dynamic Application:**
  - Instantly switch the visual theme variant (`Application.Current.RequestedThemeVariant`) across all open windows and controls without restarting the app.
  - Instantly re-render all UI text (menus, start page dashboard, table tabs, filter/sort/figure chips, dialogs) and validation finding messages when the language is changed.
- **Preferences Persistence:**
  - Store user preferences in a lightweight JSON configuration file within the platform's standard user configuration directory (e.g. `~/Library/Application Support/gobd-reader/` on macOS, `%APPDATA%\gobd-reader\` on Windows, `~/.config/gobd-reader/` on Linux).
  - Fall back gracefully to `System` theme and OS-detected language if configuration is absent or unreadable, never modifying the export data carrier.
- **Full UI Localization:**
  - Create a strongly typed, trim-safe catalogue for all UI labels, button texts, menu headers, and status strings in English and German.

## Capabilities

### New Capabilities
*(None)*

### Modified Capabilities
- `reader-ui`: The reader provides a Settings window to configure theme and display language, persists these preferences across launches, and renders its entire user interface and finding messages in the selected language.

## Impact

- **UI Project:** Updates to `MainWindow.cs`, `App.cs`, `StartPageView.cs`, `TableTabView.cs`, and introduction of `SettingsWindow.cs`, `PreferencesStore.cs`, and `UiText.cs` in `src/GoBd.Reader.Ui`.
- **Packaging & Trimming:** No new external package dependencies required; persistence uses System.Text.Json with trim-safe options.
- **Tests:** New headless tests in `tests/GoBd.Reader.Ui.Tests` verifying preferences loading, saving, theme switching, and language translation.
