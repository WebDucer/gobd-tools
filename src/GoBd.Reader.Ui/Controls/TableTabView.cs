using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Content;
using GoBd.Validation.Localisation;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.Controls;

/// <summary>
/// One table's tab: what a person has asked to see of it, its grid or its report, what was last
/// said about it, and the walk being stepped through in it.
/// </summary>
/// <remarks>
/// A tab owns everything that belongs to its table. Leaving it cannot leave a stale message, a
/// stale Previous/Next bar or another table's filter behind, because none of it was ever shared.
/// <para>
/// The filters, the sort and the figures are chosen above the grid rather than on its column
/// headers: the control the grid is built on has no sorting, no header event and no footer row,
/// and choices hidden inside column headers would be invisible until their column was scrolled
/// into view. See the change's design.md D10.
/// </para>
/// </remarks>
internal sealed class TableTabView : DockPanel
{
    /// <summary>
    /// Columns the reader puts before the declared ones: the position in the view, and the
    /// record number the file gave the record.
    /// </summary>
    /// <remarks>
    /// Stated once, because two places have to agree about it: the grid that adds them and the
    /// click that maps a cell back to the column its declaration describes. They disagreed once,
    /// and following a key then took the reader to whatever the neighbouring column pointed at.
    /// </remarks>
    private const int LeadingColumns = 2;

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private ReportLanguage language;

    private readonly ReaderSession session;
    private readonly TableTab tab;
    private readonly Action<TableNode, RecordRow, int> follow;
    private readonly Action<TableNode, RecordRow, Control, bool> offerRecordActions;
    private readonly Action<int> step;

    private readonly TextBlock subtitle = new() { Classes = { ReaderTheme.MutedClass }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel walkBar;
    private readonly Button previous = new();
    private readonly Button next = new();
    private readonly ContentControl body = new();

    private readonly TextBlock filtersRowLabel = new() { Classes = { ReaderTheme.MutedClass }, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock sortRowLabel = new() { Classes = { ReaderTheme.MutedClass }, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock figuresRowLabel = new() { Classes = { ReaderTheme.MutedClass }, VerticalAlignment = VerticalAlignment.Center };

    private readonly Button addFilterButton;
    private readonly Button addSortButton;
    private readonly Button addFigureButton;

    private readonly WrapPanel filters = new() { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 4 };
    private readonly WrapPanel sorts = new() { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 4 };
    private readonly WrapPanel figures = new() { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 4 };
    private readonly TextBlock banner = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button reset = new();
    private readonly TextBlock preparing = new() { Classes = { ReaderTheme.MutedClass }, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock notice = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false, FontStyle = FontStyle.Italic };
    private readonly StackPanel figuresPanel = new() { Orientation = Orientation.Horizontal, Spacing = 18 };
    private readonly Border figuresStrip;

    private TableView? grid;

    /// <summary>The strip above the records: what is in force, what was said, the walk.</summary>
    private readonly StackPanel toolbar;

    /// <summary>Whether this tab has announced that its table is still being read.</summary>
    private bool saidNotReady;

    /// <summary>The figures last shown, so a change of language can say them again.</summary>
    private IReadOnlyList<FigureReading> figureReadings = [];

    /// <summary>Creates the tab for one table.</summary>
    /// <param name="session">The open export.</param>
    /// <param name="tab">This table's place in the workspace.</param>
    /// <param name="follow">Called with the table, record and column a reference was followed from.</param>
    /// <param name="offerRecordActions">
    /// Called to offer what can be done with a record — the keys it can follow, the tables that
    /// refer to it, copying it — next to the control given, and whether it was asked for from the
    /// keyboard.
    /// </param>
    /// <param name="step">Called to move one referring record backwards or forwards.</param>
    /// <param name="language">The language the tab speaks until told otherwise.</param>
    public TableTabView(
        ReaderSession session,
        TableTab tab,
        Action<TableNode, RecordRow, int> follow,
        Action<TableNode, RecordRow, Control, bool> offerRecordActions,
        Action<int> step,
        ReportLanguage language = ReportLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(tab);

        this.session = session;
        this.tab = tab;
        this.follow = follow;
        this.offerRecordActions = offerRecordActions;
        this.step = step;
        this.language = language;

        previous.Click += (_, _) => this.step(-1);
        next.Click += (_, _) => this.step(1);
        reset.Click += (_, _) => Ask(TableQuery.FileOrder);

        walkBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            IsVisible = false,
            Children = { previous, next },
        };

        figuresStrip = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6, 12, 8),
            Child = figuresPanel,
            IsVisible = false,
        };
        figuresStrip[!Border.BorderBrushProperty] = new DynamicResourceExtension(ReaderTheme.SurfaceBorder);
        figuresStrip[!Border.BackgroundProperty] = new DynamicResourceExtension(ReaderTheme.Background);

        addFilterButton = Adds(() => FilterEditor());
        addSortButton = Adds(() => SortEditor());
        addFigureButton = Adds(() => FigureEditor());

        var header = toolbar = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(12, 8, 12, 8),
            Children =
            {
                subtitle,
                Row(filtersRowLabel, filters, addFilterButton),
                Row(sortRowLabel, sorts, addSortButton),
                Row(figuresRowLabel, figures, addFigureButton),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { banner, reset, preparing },
                },
                notice,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { walkBar, status },
                },
            },
        };

        Grid.SetIsSharedSizeScope(header, true);
        SetDock(header, Dock.Top);
        SetDock(figuresStrip, Dock.Bottom);
        Children.Add(header);
        Children.Add(figuresStrip);
        Children.Add(body);

        ApplyTexts();
        Refresh();
    }

    /// <summary>The table this tab shows.</summary>
    public TableNode Table => tab.Table;

    /// <summary>The strip above the records, which F6 moves focus to.</summary>
    internal Control Toolbar => toolbar;

    /// <summary>Where the records, or what stands instead of them, are shown.</summary>
    internal Control RecordsArea => body;

    /// <summary>Whether the table shows its data, so that something can be asked of it.</summary>
    internal bool HasData => session.View(tab.Table).Kind == TableViewKind.Data;

    /// <summary>Whether the table is shown as the file delivers it, with nothing in force.</summary>
    internal bool InFileOrder => tab.Query.IsFileOrder;

    /// <summary>Moves keyboard focus to the strip above the records.</summary>
    internal void FocusToolbar() => addFilterButton.Focus(NavigationMethod.Directional);

    /// <summary>Opens the filter editor, as its button does.</summary>
    internal void OpenFilterEditor() => Open(addFilterButton);

    /// <summary>Opens the sort editor, as its button does.</summary>
    internal void OpenSortEditor() => Open(addSortButton);

    /// <summary>Opens the figure editor, as its button does.</summary>
    internal void OpenFigureEditor() => Open(addFigureButton);

    /// <summary>The record the grid is positioned at, or none.</summary>
    internal long? CurrentOrdinal => (grid?.SelectedItem as RecordRow)?.Ordinal;

    /// <summary>
    /// Asks for a record number and hands it to whoever goes there, which says whether the table
    /// holds such a record.
    /// </summary>
    /// <remarks>
    /// The number is read as the display language writes numbers, so "1.234" is a thousand and
    /// more in German. A number the table does not hold is said where it was typed, and the table
    /// stays where it was. See the improve-reader-accessibility change's design.md D12.
    /// </remarks>
    internal void AskForRecord(Func<long, bool> goTo)
    {
        ArgumentNullException.ThrowIfNull(goTo);

        var count = session.View(tab.Table).Rows?.Count ?? 0;
        var field = new TextBox { Width = 200 };
        AutomationProperties.SetName(field, UiText.RecordNumberField(language));
        var complaint = Complaint();
        var go = new Button { Content = UiText.GoTo(language), Classes = { ApplyClass } };
        var content = Editor(
            UiText.GoToRecordHeading(language),
            field,
            new TextBlock { Text = UiText.RecordRange(language, count), Classes = { ReaderTheme.MutedClass }, TextWrapping = TextWrapping.Wrap },
            complaint,
            go);

        var flyout = OpenEditor(subtitle, content, () => FocusRecords());
        go.Click += (_, _) =>
        {
            var typed = (field.Text ?? string.Empty).Trim();
            if (ReadRecordNumber(typed, language) is { } ordinal && goTo(ordinal))
            {
                complaint.IsVisible = false;
                flyout.Hide();
                return;
            }

            Complain(complaint, UiText.NoSuchRecord(language, typed, count));
        };
    }

    /// <summary>A record number as the display language writes it, or none when it is not one.</summary>
    internal static long? ReadRecordNumber(string typed, ReportLanguage language)
    {
        var groups = NumberFormatInfo.GetInstance(UiText.Numbers(language)).NumberGroupSeparator;
        var digits = typed.Replace(groups, string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>Shows the table as the file delivers it again, as the button beside the banner does.</summary>
    internal void ReturnToFileOrder() => Ask(TableQuery.FileOrder);

    private static void Open(Button adder) => adder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Asked to show this table under a different query, and told what came of it.
    /// </summary>
    /// <remarks>
    /// The tab says what a person asked for; preparing it belongs to whoever owns the worker and
    /// the store, which is the window. Set after construction, so a tab can be built without one.
    /// </remarks>
    public Func<TableQuery, Task>? Asked { get; set; }

    /// <summary>Told what to announce to assistive technology, and whether it interrupts.</summary>
    public Action<string, bool>? Announce { get; set; }

    /// <summary>Asked to compute the figures a person has chosen for this table.</summary>
    public Func<IReadOnlyList<ColumnFigure>, Task>? Totalled { get; set; }

    /// <summary>Says everything in the given language from now on.</summary>
    /// <remarks>
    /// The grid is built afresh, positioned where it was: its column headers and the tooltips of
    /// the cells that lead elsewhere are set as cells are realised, and a realised cell keeps
    /// what it was given.
    /// </remarks>
    public void SetLanguage(ReportLanguage newLanguage)
    {
        language = newLanguage;
        Remember();
        grid = null;
        ApplyTexts();
        Refresh();
        ShowFigures(figureReadings);
    }

    /// <summary>Says what this tab says of its own accord, in the current language.</summary>
    private void ApplyTexts()
    {
        filtersRowLabel.Text = UiText.FiltersLabel(language);
        sortRowLabel.Text = UiText.SortLabel(language);
        figuresRowLabel.Text = UiText.FiguresLabel(language);
        Caption(addFilterButton, UiText.AddFilter(language));
        Caption(addSortButton, UiText.AddColumn(language));
        Caption(addFigureButton, UiText.AddColumn(language));
        reset.Content = UiText.BackToFileOrder(language);
        preparing.Text = UiText.Preparing(language);
        previous.Content = UiText.PreviousRecord(language);
        next.Content = UiText.NextRecord(language);
        AutomationProperties.SetName(previous, UiText.PreviousReferring(language));
        AutomationProperties.SetName(next, UiText.NextReferring(language));
        AutomationProperties.SetName(reset, UiText.ReturnToFileOrder(language));
    }

    private static void Caption(Button button, string text)
    {
        button.Content = text;
        AutomationProperties.SetName(button, text);
    }

    /// <summary>
    /// Rebuilds whatever has changed: the presentation, what is in force, the status and the walk.
    /// </summary>
    /// <remarks>
    /// The grid itself is built once and kept, so returning to a tab finds it where it was left;
    /// a table that shows its data never stops showing it. A new view replaces what the grid is
    /// bound to rather than the grid, so the columns, their widths and the cell templates survive
    /// a filter.
    /// <para>
    /// A refresh does not move the grid, except to place one it has just built. Every progress
    /// tick refreshes every tab, and positioning on each pulled the grid back to the record a
    /// navigation landed on ten times a second while a person was scrolling away from it.
    /// </para>
    /// </remarks>
    public void Refresh()
    {
        var view = session.View(tab.Table);
        var building = grid is null;

        // Said once while the tab shows it, not on every progress tick that refreshes the tab.
        if (view.Kind == TableViewKind.NotReady && !saidNotReady && Announce is { } announce)
        {
            saidNotReady = true;
            announce(UiText.TableNotReady(language), false);
        }

        if (building)
        {
            body.Content = view.Kind switch
            {
                TableViewKind.Data => Data(view),
                TableViewKind.NotReady => NotReady(),
                _ => Report(view),
            };
        }
        else if (grid is not null && tab.Rows is { } rows && !ReferenceEquals(grid.ItemsSource, rows))
        {
            grid.ItemsSource = rows;
        }

        subtitle.Text = view.Kind switch
        {
            TableViewKind.Data => UiText.DataSubtitle(language, Shown(view), view.Columns.Count),
            TableViewKind.NotReady => UiText.StillBeingRead(language),
            _ => UiText.FindingsCount(language, view.Findings.Count),
        } + " · " + tab.Table.Url.Value;

        ShowQuery(view);
        status.Text = tab.Status(language);

        var current = tab.NoticeAt(DateTimeOffset.UtcNow);
        notice.Text = current;
        notice.IsVisible = current.Length > 0;

        // Created only while a backwards walk is in progress, and gone the moment it is not. A
        // forward navigation presents no stepping controls at all.
        walkBar.IsVisible = tab.Walk is not null;
        if (tab.Walk is { } walk)
        {
            previous.IsEnabled = walk.HasPrevious;
            next.IsEnabled = walk.HasNext;
        }

        if (building && grid is not null)
        {
            Position();
        }
    }

    /// <summary>Says that a view is being prepared, so an empty grid is never mistaken for one.</summary>
    public void Preparing(bool preparing_) => preparing.IsVisible = preparing_;

    /// <summary>Shows the figures a person chose, beneath the records they were computed over.</summary>
    public void ShowFigures(IReadOnlyList<FigureReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        figureReadings = readings;
        figuresPanel.Children.Clear();
        var columns = session.View(tab.Table).Columns;
        var layout = RecordLayout.For(tab.Table);

        foreach (var reading in readings)
        {
            var at = reading.Asked.Column;
            var name = at < columns.Count ? columns[at] : "?";
            var column = at < layout.Columns.Count ? layout.Columns[at] : null;

            figuresPanel.Children.Add(new TextBlock
            {
                Text = $"{name} {Named(reading.Asked.Figure)} {Stated(reading, layout, column)}",
            });
        }

        figuresStrip.IsVisible = readings.Count > 0;
    }

    /// <summary>
    /// What a figure came to, said as exactly as it was computed.
    /// </summary>
    /// <remarks>
    /// Written by <see cref="DeclaredText"/>, which owns what the declaration makes of a value in
    /// both directions. A control formatting numbers and masks itself would be a third place that
    /// knows what the export's symbols mean, and a total could then disagree with the column
    /// above it.
    /// </remarks>
    private string Stated(FigureReading reading, RecordLayout layout, ColumnLayout? column)
    {
        if (!reading.Exact)
        {
            return UiText.FigureInexact(language);
        }

        if (reading.Value is null)
        {
            return "—";
        }

        var rounded = reading.Rounded ? " " + UiText.Rounded(language) : string.Empty;
        var missing = reading.Missing > 0 ? ", " + UiText.WithoutValue(language, reading.Missing) : string.Empty;

        return DeclaredText.Write(reading.Value, column, layout) + rounded + missing;
    }

    private string Named(Figure figure) => figure switch
    {
        Figure.Count => UiText.FigCount(language),
        Figure.DistinctCount => UiText.FigDistinct(language),
        Figure.Sum => UiText.FigSum(language),
        Figure.Minimum => UiText.FigMin(language),
        Figure.Maximum => UiText.FigMax(language),
        _ => UiText.FigAvg(language),
    };

    /// <summary>Records the view is showing, of however many the table holds.</summary>
    private long Shown(TablePresentation view) => tab.Rows?.Count ?? view.Rows?.Count ?? 0;

    /// <summary>What was last drawn, so that a tick that changed nothing redraws nothing.</summary>
    /// <remarks>
    /// Every progress tick refreshes every open tab. Rebuilding the chips each time discarded and
    /// re-added a control per filter, sort and figure — ten times a second, invalidating the
    /// layout each time — to arrive at what was already on screen.
    /// </remarks>
    private (TableQuery Query, IReadOnlyList<ColumnFigure> Figures, long Held, long Whole, ReportLanguage Language) drawn;

    /// <summary>
    /// What is in force: every filter and sorted column, each removable, and what it leaves.
    /// </summary>
    private void ShowQuery(TablePresentation view)
    {
        var showing = (
            tab.Query,
            tab.Figures,
            Held: tab.Rows?.Count ?? view.Rows?.Count ?? 0,
            Whole: (long)(view.Rows?.Count ?? tab.Rows?.Count ?? 0),
            Language: language);

        if (ReferenceEquals(showing.Query, drawn.Query)
            && ReferenceEquals(showing.Figures, drawn.Figures)
            && showing.Held == drawn.Held
            && showing.Whole == drawn.Whole
            && showing.Language == drawn.Language)
        {
            return;
        }

        drawn = showing;

        var columns = view.Columns;
        filters.Children.Clear();
        sorts.Children.Clear();
        figures.Children.Clear();

        foreach (var filter in tab.Query.Filters)
        {
            var without = tab.Query with { Filters = [.. tab.Query.Filters.Where(other => other != filter)] };
            filters.Children.Add(Chip(Describe(filter, columns), () => Ask(without)));
        }

        for (var index = 0; index < tab.Query.Sorts.Count; index++)
        {
            var sort = tab.Query.Sorts[index];
            var without = tab.Query with { Sorts = [.. tab.Query.Sorts.Where(other => other != sort)] };
            var name = sort.Column < columns.Count ? columns[sort.Column] : "?";
            var direction = sort.Direction == Ordering.Descending ? UiText.Descending(language) : UiText.Ascending(language);
            sorts.Children.Add(Chip(
                string.Create(CultureInfo.InvariantCulture, $"{index + 1} {name} {direction}"),
                () => Ask(without)));
        }

        foreach (var figure in tab.Figures)
        {
            var name = figure.Column < columns.Count ? columns[figure.Column] : "?";
            var without = tab.Figures.Where(other => other != figure).ToArray();
            figures.Children.Add(Chip($"{name} {Named(figure.Figure)}", () => Total(without)));
        }

        filters.IsVisible = filters.Children.Count > 0;
        sorts.IsVisible = sorts.Children.Count > 0;
        figures.IsVisible = figures.Children.Count > 0;

        var held = tab.Rows?.Count ?? view.Rows?.Count ?? 0;
        var whole = view.Rows?.Count ?? held;
        banner.Text = tab.Query.IsFileOrder
            ? UiText.FileOrderBanner(language, whole)
            : held == 0
                ? UiText.NoRecordMatches(language)
                : UiText.ShowingBanner(language, held, whole);

        reset.IsVisible = !tab.Query.IsFileOrder;
    }

    /// <summary>One filter, as the banner names it.</summary>
    private string Describe(ColumnFilter filter, IReadOnlyList<string> columns)
    {
        var name = filter.Column < columns.Count ? columns[filter.Column] : "?";
        var comparison = filter.Comparison switch
        {
            FilterComparison.Equals => "=",
            FilterComparison.NotEquals => "≠",
            FilterComparison.LessThan => "<",
            FilterComparison.AtMost => "≤",
            FilterComparison.GreaterThan => ">",
            FilterComparison.AtLeast => "≥",

            // Shorter than the editor's names, which explain how to type the value.
            FilterComparison.Matches => UiText.MatchesChip(language),
            FilterComparison.OneOf => UiText.OneOfChip(language),
            _ => Named(filter.Comparison),
        };

        var values = filter.Values.Count == 0 ? string.Empty : " " + string.Join(" … ", filter.Values);
        var case_ = filter.IgnoreCase ? " " + UiText.AnyCase(language) : string.Empty;
        return $"{name} {comparison}{values}{case_}";
    }

    /// <summary>Something in force, with a way to take it off again.</summary>
    private Control Chip(string text, Action remove)
    {
        var close = ReaderTheme.Glyph(new Button
        {
            Content = "✕",
            FontSize = 10,
            Padding = new Thickness(4, 1),
            MinWidth = 0,
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
        });
        AutomationProperties.SetName(close, UiText.RemoveChip(language, text));

        close.Click += (_, _) => remove();

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 2),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = text,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    close,
                },
            },
        };
        border[!Border.BackgroundProperty] = new DynamicResourceExtension(ReaderTheme.Chip);
        border[!Border.BorderBrushProperty] = new DynamicResourceExtension(ReaderTheme.SurfaceBorder);
        return border;
    }

    /// <summary>
    /// One row of the control strip: its label, what is in force, and the button that adds another.
    /// </summary>
    /// <remarks>
    /// The labels share a column across the rows, so the strip lines up in whichever language
    /// labels it. Spaced by margins rather than by the grid, so a row with nothing in force has no
    /// gap where its chips would be.
    /// </remarks>
    private static Grid Row(TextBlock label, Control content, Control add)
    {
        label.Margin = new Thickness(0, 0, 8, 0);
        content.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(content, 1);
        Grid.SetColumn(add, 2);

        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "RowLabel" },
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            Children = { label, content, add },
        };
    }

    /// <summary>
    /// A button that opens an editor in a flyout of its own, drawn at the reader's zoom.
    /// </summary>
    /// <remarks>
    /// The editor's first input takes focus when it opens, so it can be filled in from the keyboard
    /// at once, and Enter applies it. Closed — Escape closes it — it gives focus back to the button
    /// that opened it, unless the person has put focus somewhere else in the meantime. See the
    /// improve-reader-accessibility change's design.md D17.
    /// </remarks>
    /// <summary>The class an editor's Apply button carries, which Enter presses.</summary>
    private const string ApplyClass = "apply";

    private static Button Adds(Func<Control> editor)
    {
        var button = new Button { Padding = new Thickness(8, 2) };
        button.Click += (_, _) =>
            button.Flyout = OpenEditor(button, editor(), () => button.Focus(NavigationMethod.Directional));

        return button;
    }

    /// <summary>
    /// Opens an editor in a flyout of its own beside a control, drawn at the reader's zoom.
    /// </summary>
    /// <remarks>
    /// The editor's first input takes focus when it opens, so it can be filled in from the keyboard
    /// at once, and Enter applies it — once the picker or field it was pressed in has not taken it
    /// itself; a default button does not reach a flyout. Closed — Escape closes it — it gives focus
    /// back, unless the person has put focus somewhere else in the meantime.
    /// </remarks>
    /// <param name="anchor">What the flyout opens beside.</param>
    /// <param name="content">The editor.</param>
    /// <param name="giveFocusBack">Where focus goes when the editor closes.</param>
    private static Flyout OpenEditor(Control anchor, Control content, Action giveFocusBack)
    {
        // Drawn at the reader's zoom: a popup is not inside the window that zooms.
        var flyout = new Flyout { Content = new Zoomed(content), Placement = PlacementMode.BottomEdgeAlignedLeft };

        // The keyboard stays in the editor: back on a picker once its list closes, and Tab going
        // round its own controls rather than out of it.
        ReaderWindows.KeepFocusOnPickers(content);
        KeyboardNavigation.SetTabNavigation(content, KeyboardNavigationMode.Cycle);

        content.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Enter
                && args.KeyModifiers == KeyModifiers.None
                && content.GetLogicalDescendants().OfType<Button>().FirstOrDefault(candidate => candidate.Classes.Contains(ApplyClass)) is { IsEffectivelyEnabled: true } apply)
            {
                args.Handled = true;
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        });

        flyout.Opened += (_, _) => Dispatcher.UIThread.Post(
            () => content.GetVisualDescendants()
                .OfType<InputElement>()
                .FirstOrDefault(input => input.Focusable && input.IsEffectivelyVisible && input.IsEffectivelyEnabled)
                ?.Focus(NavigationMethod.Tab),
            DispatcherPriority.Loaded);

        flyout.Closed += (_, _) =>
        {
            var focused = TopLevel.GetTopLevel(anchor)?.FocusManager?.GetFocusedElement() as Visual;
            if (focused is null || TopLevel.GetTopLevel(focused) is null || content.IsVisualAncestorOf(focused))
            {
                giveFocusBack();
            }
        };

        flyout.ShowAt(anchor);
        return flyout;
    }

    /// <summary>
    /// The columns a person may choose from, with what the reader cannot do said where it is
    /// chosen.
    /// </summary>
    private ComboBox Columns(Func<ColumnCapability, bool> offered)
    {
        var names = session.View(tab.Table).Columns;
        var capabilities = session.Capabilities(tab.Table);
        var picker = new ComboBox { MinWidth = 160 };
        AutomationProperties.SetName(picker, UiText.ColumnField(language));

        for (var index = 0; index < names.Count; index++)
        {
            var capability = capabilities?[index];
            var allowed = capability is not null && capability.IsQueryable && offered(capability);
            picker.Items.Add(new ComboBoxItem
            {
                Content = names[index],
                Tag = index,
                IsEnabled = allowed,
                [ToolTip.TipProperty] = allowed ? null : UiText.ColumnNotQueryable(language),
            });
        }

        return picker;
    }

    private static int? Chosen(ComboBox picker) =>
        (picker.SelectedItem as ComboBoxItem)?.Tag as int?;

    /// <summary>
    /// Says why an editor refused what it was given, where it was typed and to assistive
    /// technology at once, which interrupts: the person is waiting on the answer.
    /// </summary>
    private void Complain(TextBlock complaint, string text)
    {
        complaint.Text = text;
        complaint.IsVisible = true;
        Announce?.Invoke(text, true);
    }

    /// <summary>Where an editor says why it refused what it was given.</summary>
    private static TextBlock Complaint()
    {
        var complaint = new TextBlock { TextWrapping = TextWrapping.Wrap, IsVisible = false };
        complaint[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ReaderTheme.Defective);
        return complaint;
    }

    /// <summary>
    /// The filter editor for whichever column is chosen, in the terms that column declares.
    /// </summary>
    /// <remarks>
    /// What a person types is read under the table's declared symbols and the column's mask, and
    /// refused with the form expected rather than approximated. The comparisons offered follow
    /// the column's kind: a range means nothing on text, and a pattern means nothing on a number.
    /// </remarks>
    private Control FilterEditor()
    {
        var columns = Columns(_ => true);
        var comparison = new ComboBox { MinWidth = 140 };
        var first = new TextBox { Width = 160, PlaceholderText = UiText.ValuePlaceholder(language) };
        var second = new TextBox { Width = 160, PlaceholderText = UiText.AndPlaceholder(language), IsVisible = false };
        AutomationProperties.SetName(comparison, UiText.ComparisonField(language));
        AutomationProperties.SetName(first, UiText.ValueField(language));
        AutomationProperties.SetName(second, UiText.SecondValueField(language));
        var ignoreCase = new CheckBox { Content = UiText.IgnoreCase(language), IsVisible = false };
        var expected = new TextBlock { Classes = { ReaderTheme.MutedClass }, TextWrapping = TextWrapping.Wrap };
        var complaint = Complaint();
        var apply = new Button { Content = UiText.Apply(language), IsEnabled = false, Classes = { ApplyClass } };

        var layout = RecordLayout.For(tab.Table);
        var capabilities = session.Capabilities(tab.Table);

        columns.SelectionChanged += (_, _) =>
        {
            if (Chosen(columns) is not { } index || capabilities?[index] is not { } capability)
            {
                return;
            }

            comparison.Items.Clear();
            foreach (var offered in Comparisons(capability.Kind))
            {
                comparison.Items.Add(new ComboBoxItem { Content = Named(offered), Tag = offered });
            }

            comparison.SelectedIndex = 0;
            ignoreCase.IsVisible = capability.Kind == ColumnQueryKind.Text;
            expected.Text = UiText.Expects(language, FilterInput.Expected(capability, layout.Columns[index], layout, language));
            apply.IsEnabled = true;
        };

        comparison.SelectionChanged += (_, _) =>
        {
            var chosen = (comparison.SelectedItem as ComboBoxItem)?.Tag as FilterComparison?;
            second.IsVisible = chosen == FilterComparison.Between;
            first.IsVisible = chosen is not (FilterComparison.IsEmpty or FilterComparison.IsNotEmpty);
        };

        apply.Click += (_, _) =>
        {
            if (Chosen(columns) is not { } index
                || capabilities?[index] is not { } capability
                || (comparison.SelectedItem as ComboBoxItem)?.Tag is not FilterComparison chosen)
            {
                return;
            }

            var values = new List<string>();
            foreach (var typed in Typed(chosen, first, second))
            {
                var reading = FilterInput.For(capability, layout.Columns[index], layout, typed, language);
                if (!reading.Read)
                {
                    Complain(complaint, UiText.IsNot(language, typed, reading.Expected));
                    return;
                }

                values.Add(reading.Value);
            }

            var filter = new ColumnFilter(index, chosen, values, ignoreCase.IsChecked == true);
            if (!filter.NamesValue)
            {
                // Refused rather than applied as nothing: a filter naming no value would either
                // show every record or none, and the banner would name it either way.
                Complain(complaint, chosen == FilterComparison.OneOf
                    ? UiText.ExpectsValues(language)
                    : UiText.ExpectsValue(language));
                return;
            }

            complaint.IsVisible = false;
            Ask(tab.Query with { Filters = [.. tab.Query.Filters, filter] });
        };

        return Editor(UiText.FilterHeading(language), columns, comparison, first, second, ignoreCase, expected, complaint, apply);
    }

    private static IEnumerable<string> Typed(FilterComparison comparison, TextBox first, TextBox second)
    {
        if (comparison is FilterComparison.IsEmpty or FilterComparison.IsNotEmpty)
        {
            yield break;
        }

        if (comparison == FilterComparison.OneOf)
        {
            foreach (var entry in (first.Text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return entry;
            }

            yield break;
        }

        yield return first.Text ?? string.Empty;

        if (comparison == FilterComparison.Between)
        {
            yield return second.Text ?? string.Empty;
        }
    }

    /// <summary>What a column's kind can be asked.</summary>
    private static IEnumerable<FilterComparison> Comparisons(ColumnQueryKind kind) => kind switch
    {
        ColumnQueryKind.Text =>
        [
            FilterComparison.Contains,
            FilterComparison.Equals,
            FilterComparison.NotEquals,
            FilterComparison.StartsWith,
            FilterComparison.EndsWith,
            FilterComparison.Matches,
            FilterComparison.OneOf,
            FilterComparison.IsEmpty,
            FilterComparison.IsNotEmpty,
        ],
        _ =>
        [
            FilterComparison.Between,
            FilterComparison.Equals,
            FilterComparison.NotEquals,
            FilterComparison.LessThan,
            FilterComparison.AtMost,
            FilterComparison.GreaterThan,
            FilterComparison.AtLeast,
            FilterComparison.OneOf,
            FilterComparison.IsEmpty,
            FilterComparison.IsNotEmpty,
        ],
    };

    private string Named(FilterComparison comparison) => comparison switch
    {
        FilterComparison.Equals => UiText.EqualsOp(language),
        FilterComparison.NotEquals => UiText.NotEqualsOp(language),
        FilterComparison.LessThan => UiText.LessThanOp(language),
        FilterComparison.AtMost => UiText.AtMostOp(language),
        FilterComparison.GreaterThan => UiText.GreaterThanOp(language),
        FilterComparison.AtLeast => UiText.AtLeastOp(language),
        FilterComparison.Between => UiText.BetweenOp(language),
        FilterComparison.Contains => UiText.ContainsOp(language),
        FilterComparison.StartsWith => UiText.StartsWithOp(language),
        FilterComparison.EndsWith => UiText.EndsWithOp(language),
        FilterComparison.Matches => UiText.MatchesOp(language),
        FilterComparison.OneOf => UiText.OneOfOp(language),
        FilterComparison.IsEmpty => UiText.IsEmptyOp(language),
        _ => UiText.IsNotEmptyOp(language),
    };

    /// <summary>The sort editor: a column, a direction, and no more than three at once.</summary>
    private Control SortEditor()
    {
        var columns = Columns(_ => true);
        var direction = new ComboBox { MinWidth = 140 };
        AutomationProperties.SetName(direction, UiText.DirectionField(language));
        direction.Items.Add(new ComboBoxItem { Content = UiText.Ascending(language), Tag = Ordering.Ascending });
        direction.Items.Add(new ComboBoxItem { Content = UiText.Descending(language), Tag = Ordering.Descending });
        direction.SelectedIndex = 0;

        var complaint = Complaint();
        var apply = new Button { Content = UiText.Apply(language), Classes = { ApplyClass } };

        apply.Click += (_, _) =>
        {
            if (Chosen(columns) is not { } index)
            {
                return;
            }

            if (tab.Query.IsSortFull)
            {
                // Refused rather than dropping one silently: a person who cannot see which sort
                // went cannot tell what they are looking at.
                Complain(complaint, UiText.SortsFull(language, TableQuery.MaximumSorts));
                return;
            }

            var chosen = (direction.SelectedItem as ComboBoxItem)?.Tag as Ordering? ?? Ordering.Ascending;
            complaint.IsVisible = false;
            Ask(tab.Query with { Sorts = [.. tab.Query.Sorts, new ColumnSort(index, chosen)] });
        };

        return Editor(UiText.SortHeading(language), columns, direction, complaint, apply);
    }

    /// <summary>The figure editor: a column, and what to compute over the records in view.</summary>
    private Control FigureEditor()
    {
        var columns = Columns(_ => true);
        var figure = new ComboBox { MinWidth = 140 };
        AutomationProperties.SetName(figure, UiText.FigureField(language));
        var apply = new Button { Content = UiText.Apply(language), IsEnabled = false, Classes = { ApplyClass } };
        var capabilities = session.Capabilities(tab.Table);

        columns.SelectionChanged += (_, _) =>
        {
            if (Chosen(columns) is not { } index || capabilities?[index] is not { } capability)
            {
                return;
            }

            figure.Items.Clear();
            foreach (var offered in Offered(capability.Kind))
            {
                figure.Items.Add(new ComboBoxItem { Content = Named(offered), Tag = offered });
            }

            figure.SelectedIndex = 0;
            apply.IsEnabled = true;
        };

        apply.Click += (_, _) =>
        {
            if (Chosen(columns) is not { } index
                || (figure.SelectedItem as ComboBoxItem)?.Tag is not Figure chosen)
            {
                return;
            }

            Total([.. tab.Figures, new ColumnFigure(index, chosen)]);
        };

        return Editor(UiText.FigureHeading(language), columns, figure, apply);
    }

    /// <summary>
    /// What a column's kind can be asked for.
    /// </summary>
    /// <remarks>
    /// Whether a figure means anything is the person's to judge: the sum of amounts is a figure
    /// and the sum of account numbers is not, and the declaration cannot tell them apart. So a
    /// number offers a sum whether or not it is a key.
    /// </remarks>
    private static IEnumerable<Figure> Offered(ColumnQueryKind kind) => kind switch
    {
        ColumnQueryKind.Number => [Figure.Sum, Figure.Minimum, Figure.Maximum, Figure.Average, Figure.Count, Figure.DistinctCount],
        ColumnQueryKind.Date or ColumnQueryKind.Time => [Figure.Minimum, Figure.Maximum, Figure.Count, Figure.DistinctCount],
        _ => [Figure.Count, Figure.DistinctCount],
    };

    private static Control Editor(string heading, params Control[] contents)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, MinWidth = 260 };
        panel.Children.Add(new TextBlock { Text = heading, FontWeight = FontWeight.SemiBold });
        foreach (var content in contents)
        {
            panel.Children.Add(content);
        }

        return panel;
    }

    private void Ask(TableQuery query)
    {
        if (Asked is { } asked)
        {
            _ = asked(query);
        }
    }

    private void Total(IReadOnlyList<ColumnFigure> chosen)
    {
        if (Totalled is { } totalled)
        {
            _ = totalled(chosen);
        }
    }

    /// <summary>Brings the record this tab is positioned at back into view.</summary>
    /// <param name="focus">
    /// Whether the record should also take keyboard focus, as it should when a navigation reached
    /// it: someone following a reference from the keyboard has to arrive where the navigation did.
    /// </param>
    public void Position(bool focus = false)
    {
        if (grid is not null && tab.Position >= 0)
        {
            grid.SelectedIndex = tab.Position;
            grid.ScrollIntoView(tab.Position);
        }

        if (focus)
        {
            FocusRecords();
        }
    }

    /// <summary>
    /// Moves keyboard focus to the record this tab is positioned at, or to what it shows instead
    /// of records.
    /// </summary>
    /// <remarks>
    /// Posted, because the row has to be realised before it can take focus, and the grid realises
    /// it only once it has been laid out at that position.
    /// </remarks>
    public void FocusRecords()
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                if (grid is null)
                {
                    (body.Content as Control)?.Focus(NavigationMethod.Directional);
                    return;
                }

                var index = tab.Position >= 0 ? tab.Position : Math.Max(grid.SelectedIndex, 0);
                if (grid.ItemCount == 0)
                {
                    grid.Focus(NavigationMethod.Directional);
                    return;
                }

                // Selected as well as focused: the selection is what the tab remembers its place by,
                // and a focused row that is not selected would be forgotten on leaving the tab.
                grid.SelectedIndex = index;
                grid.ScrollIntoView(index);
                grid.UpdateLayout();
                if (grid.ContainerFromIndex(index) is { } row)
                {
                    row.Focus(NavigationMethod.Directional);
                }
                else
                {
                    grid.Focus(NavigationMethod.Directional);
                }
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>Remembers where the grid is, so returning to this tab finds the same record.</summary>
    public void Remember()
    {
        if (grid is { SelectedIndex: >= 0 } current)
        {
            tab.Position = current.SelectedIndex;
        }
    }

    private Control NotReady() =>
        new TextBlock
        {
            Text = UiText.TableNotReady(language),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12),
        };

    /// <summary>
    /// The grid: read-only, showing the records this tab has asked for, with the declared columns.
    /// </summary>
    /// <remarks>
    /// Two numbers lead every row — the position in what is shown, and the record number the file
    /// gave it — because they answer different questions and a filter changes only the first. The
    /// control is a <see cref="TableView"/>, which is read-only by design and virtualises rows and
    /// cells.
    /// </remarks>
    private Control Data(TablePresentation view)
    {
        // The tab's own records when it is showing a view of the table, and the table's own when
        // it is not.
        var table = new TableView { ItemsSource = tab.Rows ?? view.Rows };
        table.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        table.AddHandler(KeyDownEvent, OnRecordsKey, RoutingStrategies.Tunnel);
        table.ContainerPrepared += (_, args) => NameRecord(args.Container, view.Columns);
        table.ContextRequested += OnContextRequested;

        // Compiled bindings rather than property paths: a path is resolved by reflection when the
        // cell is realised, which trimming cannot see and may break. See the change's design.md D4.
        table.Columns.Add(new TableViewColumn
        {
            Header = "#",
            Width = new GridLength(70),
            Binding = CompiledBinding.Create((RecordRow row) => row.Position),
        });
        table.Columns.Add(new TableViewColumn
        {
            Header = UiText.RecordColumn(language),
            Width = new GridLength(90),
            Binding = CompiledBinding.Create((RecordRow row) => row.Ordinal),
        });

        // Styled by column rather than by cell: a column either takes part in a declared foreign
        // key or it does not, and that is known before a single cell is realised. A composite key
        // styles all of its columns, because acting on any of them follows the whole key.
        var links = session.LinksOf(tab.Table).ToDictionary(link => link.Column, link => link.LeadsTo);

        for (var index = 0; index < view.Columns.Count; index++)
        {
            var column = new TableViewColumn
            {
                Header = view.Columns[index],
                Width = new GridLength(160),
            };

            if (links.TryGetValue(index, out var leadsTo))
            {
                column.CellTemplate = CellTemplate(index, leadsTo);
            }
            else
            {
                // Copied per column: the loop has one variable, and a cell reads the index when it
                // is realised, long after the loop has moved on.
                var declared = index;
                column.Binding = CompiledBinding.Create((RecordRow row) => row.Values[declared]);
            }

            table.Columns.Add(column);
        }

        grid = table;
        return table;
    }

    /// <summary>
    /// Names a row for assistive technology: its record number, and each value under its column's
    /// name, with a value that refers to nothing marked as the grid marks it.
    /// </summary>
    /// <remarks>
    /// The grid is a list to a screen reader rather than a table, so without this a row announced
    /// nothing, or the name of a type. Set as each row is prepared, which is also when a row is
    /// reused for another record. See the improve-reader-accessibility change's design.md D15.
    /// </remarks>
    private void NameRecord(Control container, IReadOnlyList<string> columns)
    {
        if (container.DataContext is not RecordRow row)
        {
            return;
        }

        var values = new List<string>(row.Values.Count);
        for (var index = 0; index < row.Values.Count; index++)
        {
            var name = index < columns.Count ? columns[index] : string.Empty;
            var value = row.Values[index];
            values.Add(row.RefersToNothing(index)
                ? $"{name} {value} ({UiText.RefersToNothingMark(language)})"
                : $"{name} {value}");
        }

        AutomationProperties.SetName(container, UiText.RecordName(language, row.Ordinal, string.Join(", ", values)));
    }

    /// <summary>
    /// A followable cell: link coloured, a hand cursor, and the table it leads to in its tooltip.
    /// </summary>
    /// <remarks>
    /// A value that matches no record of that table is marked where it appears, from what the
    /// page asked the store rather than from the findings — the report is bounded and the table
    /// is not, so marking from findings would stop at the fiftieth and leave the fifty-first
    /// looking sound. See the change's design.md D7.
    /// </remarks>
    private IDataTemplate CellTemplate(int index, IReadOnlyList<string> leadsTo)
    {
        var leads = string.Join(", ", leadsTo);
        return new FuncDataTemplate<RecordRow>(
            (row, _) =>
            {
                if (row is null)
                {
                    return new TextBlock();
                }

                // Coloured by class, so the row's selection can recolour it. See ReaderTheme.
                var dangling = row.RefersToNothing(index);
                return new TextBlock
                {
                    Text = row.Values[index],
                    Cursor = HandCursor,
                    TextDecorations = dangling ? TextDecorations.Strikethrough : TextDecorations.Underline,
                    VerticalAlignment = VerticalAlignment.Center,
                    Classes = { dangling ? ReaderTheme.DanglingClass : ReaderTheme.LinkClass },
                    [ToolTip.TipProperty] = dangling
                        ? UiText.RefersToNothing(language, leads)
                        : UiText.FollowsTo(language, leads),
                };
            },
            supportsRecycling: true);
    }

    private Control Report(TablePresentation view)
    {
        var list = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, Margin = new Thickness(12) };
        list.Children.Add(new TextBlock
        {
            Text = view.Kind == TableViewKind.Failed
                ? UiText.TableUnreadable(language)
                : UiText.TableNotConformant(language),
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var finding in view.Findings)
        {
            list.Children.Add(new TextBlock
            {
                Text = finding.Code + "  " + MessageCatalogue.Render(finding, language),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (view.Truncated)
        {
            // The list is not exhaustive, and a reader who assumed it was would conclude the
            // table had been fully described.
            list.Children.Add(new TextBlock
            {
                Text = UiText.TruncatedNotice(language),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
            });
        }

        var report = new ScrollViewer { Content = list, Focusable = true };
        AutomationProperties.SetName(report, UiText.FindingsOf(language, tab.Table.Identity));
        return report;
    }

    /// <summary>
    /// Follows a reference from the cell that was acted on.
    /// </summary>
    /// <remarks>
    /// The unit of navigation is the foreign key, not the cell: acting on any of a composite
    /// key's values follows the whole key.
    /// </remarks>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        // A right click asks for the record's context, which OnContextRequested answers.
        if (sender is not TableView table
            || args.Source is not Visual source
            || args.InitialPressMouseButton == MouseButton.Right)
        {
            return;
        }

        var cell = source.GetSelfAndVisualAncestors().OfType<TableViewCell>().FirstOrDefault();
        if (cell?.Column is not { } column || cell.DataContext is not RecordRow row)
        {
            return;
        }

        // The first two columns are the reader's own — the position in the view and the file's
        // record number — and belong to no declaration. Acting on either follows nothing.
        var index = table.Columns.IndexOf(column) - LeadingColumns;
        if (index < 0)
        {
            return;
        }

        follow(tab.Table, row, index);
    }

    /// <summary>Offers what can be done with the record a context was asked for on.</summary>
    /// <remarks>
    /// Found from the row rather than the cell, so asking beside a record's values offers that
    /// record's actions as surely as asking on them: a click there lands on the row, and once
    /// went unanswered here while the grid reopened a menu built for another record.
    /// <para>
    /// The context keys — the Menu key, and Shift+F10 — arrive here too, as a request without a
    /// pointer position, and are offered at the record rather than wherever the pointer was left.
    /// </para>
    /// </remarks>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs args)
    {
        if (sender is not TableView table
            || args.Source is not Visual source
            || source.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault() is not { DataContext: RecordRow row } container)
        {
            return;
        }

        args.Handled = true;
        var fromKeyboard = !args.TryGetPosition(table, out _);
        offerRecordActions(tab.Table, row, fromKeyboard ? container : table, fromKeyboard);
    }

    /// <summary>
    /// The keys the records answer themselves: Enter offers the record's actions, copy copies it,
    /// and Left and Right scroll sideways.
    /// </summary>
    /// <remarks>
    /// Enter rather than following straight away, because a record may hold several keys and be
    /// referred to by several tables, and the person should see where each leads before going.
    /// Left and Right scroll, because a cell cannot take focus and a table may declare fifty
    /// columns: without them, what lies beyond the window's edge could not be reached from the
    /// keyboard at all. See the improve-reader-accessibility change's design.md D8 and D14.
    /// </remarks>
    private void OnRecordsKey(object? sender, KeyEventArgs args)
    {
        if (sender is not TableView table)
        {
            return;
        }

        if (ReaderKeys.ScrollLeft.Matches(args) || ReaderKeys.ScrollRight.Matches(args))
        {
            args.Handled = true;
            ScrollSideways(table, ReaderKeys.ScrollLeft.Matches(args) ? -1 : 1);
            return;
        }

        if (Focused(table) is not { DataContext: RecordRow row } container)
        {
            return;
        }

        if (ReaderKeys.CopyRecord.Matches(args))
        {
            args.Handled = true;
            _ = CopyRecordAsync(row);
        }
        else if (args.Key == Key.Enter && args.KeyModifiers == KeyModifiers.None)
        {
            args.Handled = true;
            offerRecordActions(tab.Table, row, container, true);
        }
    }

    /// <summary>How far one press of Left or Right scrolls the records sideways.</summary>
    internal const double SidewaysStep = 64;

    private static void ScrollSideways(TableView table, int direction)
    {
        if (table.Scroll is not { } scroll)
        {
            return;
        }

        var furthest = Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width);
        var x = Math.Clamp(scroll.Offset.X + (direction * SidewaysStep), 0, furthest);
        scroll.Offset = new Vector(x, scroll.Offset.Y);
    }

    /// <summary>
    /// Puts a record on the clipboard as a spreadsheet pastes it: its column names, and its values
    /// as shown.
    /// </summary>
    internal async Task CopyRecordAsync(RecordRow row)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var text = RecordCopy.Text(UiText.RecordColumn(language), session.View(tab.Table).Columns, row);
        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A clipboard the platform refuses leaves the record on the screen, where it was being
            // read; nothing here is worth taking the reader down for.
        }
    }

    /// <summary>The row that has keyboard focus, or the selected one when the grid itself has it.</summary>
    private static TableViewRow? Focused(TableView table)
    {
        var focused = TopLevel.GetTopLevel(table)?.FocusManager?.GetFocusedElement() as Visual;
        return focused?.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()
            ?? (table.SelectedIndex >= 0 ? table.ContainerFromIndex(table.SelectedIndex) as TableViewRow : null);
    }
}
