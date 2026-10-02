# Tasks

## 1. Preferences persistence

- [x] 1.1 Implement `UserPreferences` data model and `PreferencesStore` resolving the standard platform configuration path with fault-tolerant fallback, and verify with tests in `tests/GoBd.Reader.Ui.Tests`
- [x] 1.2 Verify that preferences save and reload reliably across simulated runs without throwing when storage is unavailable

## 2. Strongly typed UI localization

- [x] 2.1 Implement `UiText` dictionary providing English and German translations for all menus, tabs, dashboard cards, toolbars, chips, and empty-state texts
- [x] 2.2 Add unit tests verifying complete translation parity between English and German for all keys in `UiText`

## 3. Settings window and menus

- [x] 3.1 Implement `SettingsWindow` with Semi.Avalonia styling, offering Theme (System default, Light, Dark) and Language (English, Deutsch) pickers and accessible automation names
- [x] 3.2 Wire `SettingsWindow` into application menus (`GoBD Reader → Settings…` on macOS, `File → Settings…` on other platforms) and keyboard shortcuts (`Cmd+,` / `Ctrl+,`), verifying opening via headless tests

## 4. Dynamic theme and language switching

- [x] 4.1 Connect theme selection to `Application.Current.RequestedThemeVariant` and verify instantaneous visual switching between System, Light, and Dark
- [x] 4.2 Connect language selection to `MainWindow`, updating menus, `StartPageView` dashboard headings, `MessageCatalogue` finding messages, and active `TableTabView` toolbar labels, verified by dynamic switch tests

## 5. Integration and verification

- [x] 5.1 Run full solution tests (`dotnet test`) and verify 100% pass rate
- [x] 5.2 Publish self-contained build for desktop RID and verify zero trim warnings and successful launch
