# Design: Modernize Reader UI

## Context

The GoBD Reader currently builds its user interface programmatically in C# using `Avalonia.Themes.Fluent` (Avalonia 12.1.2) and the core `TableView` control. Colors are hardcoded static `SolidColorBrush` instances (`#1A5FB4` link blue, `#A51D2D` error red, `#26823B` conformant green). 

While scrolling large tables (up to 50 columns, millions of rows) is virtualized and sub-millisecond fast via an internal paging `IList` backed by DuckDB, the current visual styling appears raw and unpolished:
- Navigation is a bare `TreeView` with full-width blue selection rectangles.
- Tab items have default borders and raw character close buttons.
- Toolbars feature raw stacked text blocks and plain unstyled buttons.
- The summary start page presents uncarded vertical lists.
- Interactive controls lack `AutomationProperties` for screen readers and consistent focus visuals.

See `proposal.md` for background and motivation.

## Goals / Non-Goals

**Goals:**
- Integrate `Semi.Avalonia` (12.1.x, MIT) as the UI foundation.
- Support automatic OS-driven Light and Dark mode using semantic theme brushes.
- Transform toolbars into modern pill/chip components with clear remove actions and hover states.
- Structure the Start Page into a clean dashboard card layout.
- Maintain sub-millisecond scrolling performance in `TableView` by preserving flat cell visual trees.
- Provide baseline visual and assistive accessibility (WCAG AA contrast, distinct keyboard focus rings, `AutomationProperties.Name` on buttons, tabs, and chips).
- Preserve 100% self-contained single-file publishing and trim safety.

**Non-Goals:**
- Deep screen reader role tree customization beyond standard Avalonia `AutomationProperties` (full custom `AutomationPeer` implementations are deferred to a subsequent change).
- Moving from code-first UI construction to XAML (compiled C# bindings and dynamic column generation from `index.xml` remain code-first).
- Column virtualization inside `TableView` (benchmarks prove 50 columns with flat cells comfortably achieve 60fps).

## Decisions

### D1 — Theme Foundation: `Semi.Avalonia`
- **Choice:** Add `Semi.Avalonia` (12.1.0.1) centrally in `Directory.Packages.props` and instantiate `new SemiTheme()` in `App.cs`.
- **Rationale:** `Semi.Avalonia` is MIT-licensed, actively maintained for Avalonia 12.1, and specifically crafted for data-heavy desktop applications (refined tabs, buttons, inputs, tags, and cards).
- **Alternatives Considered:**
  - *Material.Avalonia:* Evaluated; while popular, its design includes heavy visual layers (elevation drop shadows, ripple effects) that add layout overhead to controls, and `TableView` support was only recently added.
  - *Custom Fluent Styling:* Requires hand-crafting full control templates for every standard control across both light and dark themes.

### D2 — Semantic Color & Brush System
- **Choice:** Eliminate static hardcoded RGB brushes (`LinkBrush`, `DanglingBrush`, `ConformantBrush`, `RuleBrush`). Instead, bind colors dynamically using Avalonia resource lookup (`FindResource` or dynamic style binding) against Semi theme keys or application-defined semantic resources:
  - `ReaderLinkBrush` (accessible blue in light mode, accessible sky blue in dark mode)
  - `ReaderDangerBrush` (accessible red in light mode, accessible coral/red in dark mode)
  - `ReaderSuccessBrush` (accessible green in light mode, emerald green in dark mode)
  - `ReaderBorderBrush` and `ReaderMutedTextBrush`
- **Rationale:** Guarantees WCAG AA (>= 4.5:1) contrast regardless of whether the user or OS runs light or dark mode.

### D3 — Performance Isolation in `TableView`
- **Choice:** Keep cell templates strictly minimal:
  - Plain cells: direct `CompiledBinding` to `RecordRow.Values[declared]`.
  - Foreign key cells: a single recycled `TextBlock` with foreground bound to semantic link/dangling brush, underline/strikethrough text decoration, and hand cursor.
  - Do NOT wrap cell contents in `Border`, `Grid`, or animation containers.
- **Rationale:** At 50 columns and 19 visible rows, ~950 cells are realized on screen. Adding nested containers or drop shadows would multiply realized visuals to ~4,000 elements, directly harming scroll responsiveness.

### D4 — Modernized Toolbar & Chip Architecture
- **Choice:** Refactor the header row in `TableTabView.cs`:
  - Combine Filters, Sorts, and Figures into a clean cohesive toolbar with pill-style chips.
  - Each chip is a rounded container (`CornerRadius = 12`) with a subtle tinted background, clear text, and an accessible close button (`AutomationProperties.Name = "Remove filter: ..."`).
  - Use modern flyout styling with consistent button accents.

### D5 — Start Page Dashboard Layout
- **Choice:** Restructure `StartPageView.cs`:
  - Header: Verdict badge (Pill/Badge style with semantic success/danger background and icon/text).
  - Key Metrics Card: Grid/Stack showing summary totals (Tables read, Total records, Finding count).
  - Tables Section: Clean card table with status indicators (`Ready`, `Reading`, `Defective`).
  - Findings Section: Distinct card containers grouping findings by table.
  - Empty State: Centered icon, title, and clear "Open Archive..." / "Open Folder..." action buttons.

### D6 — Baseline Accessibility Architecture
- **Choice:**
  - Assign `AutomationProperties.Name` to:
    - Tab headers (`"Table: {Name}"`, `"Close {Name}"`)
    - Filter, sort, and figure add buttons and individual chips
    - Summary open actions
    - Previous/Next walk buttons
  - Ensure focusable elements show Semi's high-contrast focus rings when navigated via Tab / arrow keys.

## Risks / Trade-offs

- **[Risk] Headless tests fail due to theme template differences:**
  → *Mitigation:* `TestApp.cs` initializes `App`, which loads the application's actual styles. `Driver.cs` uses logical descendant lookup by label/type rather than brittle visual offsets. Run `dotnet test` to verify all 44 UI tests pass against `SemiTheme`.
- **[Risk] Native trimming or single-file publish issues with `Semi.Avalonia`:**
  → *Mitigation:* `Semi.Avalonia` consists of Avalonia XAML styles and controls without heavy unannotated reflection. Verify trimmer output with `dotnet publish -c Release -r osx-arm64` (or target RID) to ensure zero new trim warnings.
- **[Risk] Cell text color contrast drift during dynamic OS theme switch:**
  → *Mitigation:* Bind cell text foreground dynamically to application theme resources so that realized recycled cells update if the system toggles between light and dark mode at runtime.
