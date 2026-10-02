# Tasks

## 1. Theme foundation

- [x] 1.1 Add Semi.Avalonia 12.1.x package centrally in Directory.Packages.props and reference it in GoBd.Reader.Ui.csproj, verify `dotnet restore` succeeds
- [x] 1.2 Replace FluentTheme with SemiTheme in App.cs (default to OS theme variant) and verify the reader starts headlessly via `dotnet test tests/GoBd.Reader.Ui.Tests`
- [x] 1.3 Update TestApp headless bootstrap to load SemiTheme and verify all 44 existing UI tests still pass via `dotnet test tests/GoBd.Reader.Ui.Tests`

## 2. Semantic theme tokens (light/dark)

- [x] 2.1 Remove hardcoded static RGB brushes for links, dangling keys, conformant/non-conformant states and replace with theme-bound semantic resources, verify no `Color.FromRgb(0x1A` / `0xA5` / `0x26` literals remain in `src/GoBd.Reader.Ui`
- [x] 2.2 Verify light and dark variants meet WCAG AA contrast for text, links, and status badges by capturing both theme screenshots and checking contrast ratios
- [x] 2.3 Verify runtime OS theme switch (light to dark and back) updates navigator, tabs, grid headers, and cells without restart

## 3. Toolbar and chips

- [x] 3.1 Refactor TableTabView header into a cohesive toolbar with pill-style filter/sort/figure chips (rounded container, tinted background, remove action) and verify filter add/remove still works via headless Driver tests
- [x] 3.2 Style add-filter/add-sort/add-figure flyout editors with consistent Semi inputs and buttons, verify editors open, apply, and refuse invalid input as before via existing FilterControlTests/SortControlTests/FigureControlTests
- [x] 3.3 Polish totals/footer strip into an anchored status bar and verify figures recompute on filter change via existing figure tests

## 4. Tabs and navigator

- [x] 4.1 Style tab strip and tab headers (readable at a dozen open tables, clear active indicator, accessible close affordance) and verify open/reuse/close tab behavior via existing tab tests
- [x] 4.2 Style navigator TreeView selection, badges, and hierarchy affordances and verify selection still tracks the table in view

## 5. Start page dashboard and empty state

- [x] 5.1 Restructure StartPageView into dashboard cards (verdict badge, metrics, tables status, findings grouped by table) and verify verdict plus per-table counts still render via SummaryAndNoticeTests and ViewBannerTests
- [x] 5.2 Redesign empty/no-export state with centered actions for Open Archive and Open Folder, verify actions trigger the same open paths as the File menu

## 6. Baseline accessibility

- [x] 6.1 Add AutomationProperties.Name to tabs, close buttons, filter/sort/figure chips and add buttons, walk Previous/Next, and summary actions, verify names are present via a headless automation-property test
- [x] 6.2 Verify visible keyboard focus indicators on all interactive controls by tabbing through toolbar, tabs, navigator, and dialogs in both light and dark themes

## 7. Release and regression verification

- [x] 7.1 Run full test suite (`dotnet test`) and verify zero failures across validation and reader UI projects
- [x] 7.2 Publish self-contained builds for a desktop RID (e.g. `osx-arm64`) and verify no new trim warnings and the app starts and opens an export
- [x] 7.3 Verify large-table scrolling performance is preserved (paging window stays bounded, wheel-step responsiveness comparable to pre-change baseline)
