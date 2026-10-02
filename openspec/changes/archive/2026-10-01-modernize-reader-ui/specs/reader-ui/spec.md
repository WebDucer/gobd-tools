# Spec Delta: reader-ui

## MODIFIED Requirements

### Requirement: The outcome of opening is summarised before any table is chosen
Someone handed a medium first needs to know what condition it is in, and only then which table
to read. The system SHALL present the outcome of reading the export before any table is chosen:
whether it conforms, the state of each table, and what was found, grouped by the table it
concerns, presented in a structured dashboard layout.

#### Scenario: The export's condition is summarised
- **WHEN** an export has been read
- **THEN** the reader SHALL present its verdict, each table with either its record count or its
  number of findings, and the findings grouped by table

#### Scenario: The summary uses the validator's terms
- **WHEN** a finding is presented in the summary
- **THEN** it SHALL carry the same code and message the validator reports when it checks the same
  export's contents, so that the two cannot be read as disagreeing

#### Scenario: The summary shows tables that are not finished
- **WHEN** some tables are still being read
- **THEN** the summary SHALL show which tables are finished, which is being read and which are
  waiting, and SHALL complete itself as they finish

#### Scenario: The summary is structured into dashboard sections
- **WHEN** an export is being read or has finished reading
- **THEN** the summary SHALL present verdict, reading progress, table status, and findings
  in visually separated dashboard cards rather than a single uninterrupted list of plain text

## ADDED Requirements

### Requirement: The reader adapts to system theme variant
A reader used in different working environments and operating system settings must remain legible
and comfortable to read. The system SHALL support both Light and Dark theme variants, SHALL adapt
automatically to the platform's theme preference, and SHALL use theme tokens so that all text,
borders, backgrounds, links, and status badges meet WCAG AA contrast standards in both variants.

#### Scenario: Automatic theme matching
- **WHEN** the reader runs on an operating system configured for dark mode or light mode
- **THEN** the reader SHALL present its window, navigator, tabs, grids, and dialogs using that
  theme variant

#### Scenario: Theme legibility for findings and links
- **WHEN** the reader displays conformant status, non-conformant findings, or followable foreign
  keys in either light or dark mode
- **THEN** the colors used SHALL contrast clearly with their respective background and satisfy
  WCAG AA contrast requirements

### Requirement: Interactive controls provide baseline accessibility
A reader must be operable through keyboard navigation and recognizable by assistive technologies.
The system SHALL provide distinct visual focus indicators across interactive controls, and SHALL
assign accessible names to navigation elements, tabs, filter chips, and dialog actions.

#### Scenario: Accessible names for controls
- **WHEN** assistive technology inspects interactive elements such as table tabs, close buttons,
  filter and sort chips, or action buttons
- **THEN** each element SHALL expose an accessible name describing its action or content

#### Scenario: Visible keyboard focus
- **WHEN** a person navigates interactive controls using keyboard input
- **THEN** the currently focused control SHALL display a clear, distinct visual focus indicator
