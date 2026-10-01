# Proposal

## Why

The current GoBD Reader user interface uses bare system controls and default layouts that look dated, unpolished, and lack cohesive visual hierarchy. Furthermore, colors are hardcoded as static RGB values rather than theme tokens, preventing readable dark mode support and creating accessibility contrast issues. Adopting an MIT-compatible, modern data-dense theme (Semi.Avalonia) along with structured toolbar chips, clean tab navigation, and baseline accessibility attributes modernizes the reader while preserving its sub-millisecond scrolling performance across millions of records.

## What Changes

- **Theme & Palette Modernization:**
  - Introduce `Semi.Avalonia` as the application theme foundation (MIT-licensed).
  - Support automatic and seamless switching between Light and Dark themes via semantic palette tokens instead of hardcoded RGB brushes.
- **Controls & Layout Refinement:**
  - Modernize table view toolbars: replace raw stacked text lines and unstyled buttons with sleek, interactive chip/pill controls for filters, sorts, and figures.
  - Refine tab styling and tree navigator presentation for cleaner contrast, active item selection, and clearer visual hierarchy.
  - Upgrade the Start Page / Summary view into a structured dashboard layout with clear status badges and metric cards.
  - Enhance empty state presentation with prominent, accessible open actions.
- **High-Performance Guarantee:**
  - Keep `TableView` row and cell visuals ultra-lean (single `TextBlock` presentation without nested border containers or cell animations) so that scrolling tables with up to 50 columns and millions of rows retains full 60fps virtualization.
- **Baseline Accessibility:**
  - Ensure all colors meet WCAG AA contrast standards in both light and dark variants.
  - Ensure clear visual focus indicators on interactive controls.
  - Add basic accessibility naming and roles (`AutomationProperties.Name`) across navigation, tab controls, filter chips, and dialog actions.

## Capabilities

### New Capabilities
*(None)*

### Modified Capabilities
- `reader-ui`: The reader's visual presentation adapts to system theme (light/dark mode) using semantic tokens, interactive controls (tabs, filter/sort chips, navigation items) provide accessible automation names and focus indicators, and the start page presents a structured dashboard of export status and findings.

## Impact

- **UI Project:** `src/GoBd.Reader.Ui` references `Semi.Avalonia` (version 12.1.x) centrally managed via `Directory.Packages.props`.
- **Packaging & Trimming:** `Semi.Avalonia` is MIT and trimmer-compatible for self-contained single-file publishing on Windows, Linux, and macOS.
- **Tests:** `tests/GoBd.Reader.Ui.Tests` continues to run headlessly using `TestApp` with the new theme loaded.
