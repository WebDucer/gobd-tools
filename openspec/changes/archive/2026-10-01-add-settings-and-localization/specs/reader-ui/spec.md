# Spec Delta: reader-ui

## MODIFIED Requirements

### Requirement: The reader adapts to system theme variant
A reader used in different working environments and operating system settings must remain legible
and comfortable to read. The system SHALL support both Light and Dark theme variants, SHALL adapt
automatically to the platform's theme preference by default, SHALL allow the person to explicitly
choose between System, Light, and Dark appearance in Settings, and SHALL use theme tokens so that all
text, borders, backgrounds, links, and status badges meet WCAG AA contrast standards in both variants.

#### Scenario: Automatic theme matching
- **WHEN** the reader runs with the System appearance setting on an operating system configured for dark mode or light mode
- **THEN** the reader SHALL present its window, navigator, tabs, grids, and dialogs using that
  theme variant

#### Scenario: Theme legibility for findings and links
- **WHEN** the reader displays conformant status, non-conformant findings, or followable foreign
  keys in either light or dark mode
- **THEN** the colors used SHALL contrast clearly with their respective background and satisfy
  WCAG AA contrast requirements

#### Scenario: Explicit theme override
- **WHEN** a person selects Light or Dark appearance in Settings
- **THEN** the reader SHALL immediately apply that theme variant regardless of the operating system's setting

## ADDED Requirements

### Requirement: User can configure and persist preferences
Preferences belong to the person running the application and must not be lost when the application
closes. The system SHALL provide a Settings window accessible from the application menus and keyboard
shortcuts, SHALL allow selecting the appearance theme and display language, and SHALL persist these
choices in the user's platform configuration directory without writing to the export.

#### Scenario: Opening settings from the menu or shortcut
- **WHEN** a person chooses Settings from the menu or presses the standard shortcut (Cmd+, on macOS, Ctrl+, on other platforms)
- **THEN** the system SHALL present the Settings window, or bring it forward if already open

#### Scenario: Preferences persist across launches
- **WHEN** a person changes the theme or language in Settings and subsequently restarts the reader
- **THEN** the reader SHALL start with the previously chosen theme and language

#### Scenario: Absent or unreadable configuration falls back safely
- **WHEN** no preferences file exists or the file is unreadable
- **THEN** the reader SHALL start with the System theme and the operating system's detected language without reporting an error

### Requirement: The reader presents its interface and findings in the selected language
Reading and auditing records should happen in the language the person understands. The system SHALL
support English and German display languages, and SHALL present its menus, tabs, status messages,
actions, and validation finding descriptions in the chosen language.

#### Scenario: Selecting German
- **WHEN** a person chooses German as the display language
- **THEN** the system SHALL present its menus, tab headers, filter and sort labels, dashboard sections,
  and finding descriptions in German

#### Scenario: Selecting English
- **WHEN** a person chooses English as the display language
- **THEN** the system SHALL present its menus, tab headers, filter and sort labels, dashboard sections,
  and finding descriptions in English

#### Scenario: Switching language updates the view immediately
- **WHEN** the display language is changed in Settings
- **THEN** the open workspace, tabs, summary, and menus SHALL update their displayed text immediately
  without requiring an application restart
