using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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

    private static readonly IBrush LinkBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x5F, 0xB4));
    private static readonly IBrush DanglingBrush = new SolidColorBrush(Color.FromRgb(0xA5, 0x1D, 0x2D));
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly ReaderSession session;
    private readonly TableTab tab;
    private readonly Action<TableNode, RecordRow, int> follow;
    private readonly Action<TableNode, RecordRow, Control> offerReferrers;
    private readonly Action<int> step;

    private readonly TextBlock subtitle = new() { Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel walkBar;
    private readonly Button previous = new() { Content = "Previous" };
    private readonly Button next = new() { Content = "Next" };
    private readonly ContentControl body = new();

    private readonly WrapPanel filters = new() { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 4 };
    private readonly WrapPanel sorts = new() { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 4 };
    private readonly WrapPanel figures = new() { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 4 };
    private readonly TextBlock banner = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button reset = new() { Content = "Back to file order" };
    private readonly TextBlock preparing = new() { Opacity = 0.75, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock notice = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false, FontStyle = FontStyle.Italic };
    private readonly StackPanel figuresStrip = new() { Orientation = Orientation.Horizontal, Spacing = 18, Margin = new Thickness(12, 6, 12, 8) };

    private TableView? grid;

    /// <summary>Creates the tab for one table.</summary>
    /// <param name="session">The open export.</param>
    /// <param name="tab">This table's place in the workspace.</param>
    /// <param name="follow">Called with the table, record and column a reference was followed from.</param>
    /// <param name="offerReferrers">Called to offer the tables referring to a record.</param>
    /// <param name="step">Called to move one referring record backwards or forwards.</param>
    public TableTabView(
        ReaderSession session,
        TableTab tab,
        Action<TableNode, RecordRow, int> follow,
        Action<TableNode, RecordRow, Control> offerReferrers,
        Action<int> step)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(tab);

        this.session = session;
        this.tab = tab;
        this.follow = follow;
        this.offerReferrers = offerReferrers;
        this.step = step;

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

        var header = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(12, 8, 12, 8),
            Children =
            {
                subtitle,
                Row("Filters", filters, Adds("+ Filter", () => FilterEditor())),
                Row("Sort", sorts, Adds("+ Column", () => SortEditor())),
                Row("Figures", figures, Adds("+ Column", () => FigureEditor())),
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

        SetDock(header, Dock.Top);
        SetDock(figuresStrip, Dock.Bottom);
        Children.Add(header);
        Children.Add(figuresStrip);
        Children.Add(body);

        Refresh();
    }

    /// <summary>The table this tab shows.</summary>
    public TableNode Table => tab.Table;

    /// <summary>
    /// Asked to show this table under a different query, and told what came of it.
    /// </summary>
    /// <remarks>
    /// The tab says what a person asked for; preparing it belongs to whoever owns the worker and
    /// the store, which is the window. Set after construction, so a tab can be built without one.
    /// </remarks>
    public Func<TableQuery, Task>? Asked { get; set; }

    /// <summary>Asked to compute the figures a person has chosen for this table.</summary>
    public Func<IReadOnlyList<ColumnFigure>, Task>? Totalled { get; set; }

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
            TableViewKind.Data => string.Create(
                CultureInfo.InvariantCulture,
                $"{Shown(view):N0} records · {view.Columns.Count} columns · {tab.Table.Url.Value}"),
            TableViewKind.NotReady => "Still being read · " + tab.Table.Url.Value,
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"{view.Findings.Count} finding(s) · {tab.Table.Url.Value}"),
        };

        ShowQuery(view);
        status.Text = tab.Status;

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
    public void Preparing(bool preparing_)
    {
        preparing.Text = "Preparing…";
        preparing.IsVisible = preparing_;
    }

    /// <summary>Shows the figures a person chose, beneath the records they were computed over.</summary>
    public void ShowFigures(IReadOnlyList<FigureReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        figuresStrip.Children.Clear();
        var columns = session.View(tab.Table).Columns;
        var layout = RecordLayout.For(tab.Table);

        foreach (var reading in readings)
        {
            var at = reading.Asked.Column;
            var name = at < columns.Count ? columns[at] : "?";
            var column = at < layout.Columns.Count ? layout.Columns[at] : null;

            figuresStrip.Children.Add(new TextBlock
            {
                Text = $"{name} {Named(reading.Asked.Figure)} {Stated(reading, layout, column)}",
                Opacity = 0.9,
            });
        }
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
    private static string Stated(FigureReading reading, RecordLayout layout, ColumnLayout? column)
    {
        if (!reading.Exact)
        {
            return "cannot be computed exactly";
        }

        if (reading.Value is null)
        {
            return "—";
        }

        var rounded = reading.Rounded ? " (rounded)" : string.Empty;
        var missing = reading.Missing > 0
            ? string.Create(CultureInfo.InvariantCulture, $", {reading.Missing} without a value")
            : string.Empty;

        return DeclaredText.Write(reading.Value, column, layout) + rounded + missing;
    }

    private static string Named(Figure figure) => figure switch
    {
        Figure.Count => "count",
        Figure.DistinctCount => "distinct",
        Figure.Sum => "sum",
        Figure.Minimum => "min",
        Figure.Maximum => "max",
        _ => "average",
    };

    /// <summary>Records the view is showing, of however many the table holds.</summary>
    private long Shown(TablePresentation view) => tab.Rows?.Count ?? view.Rows?.Count ?? 0;

    /// <summary>What was last drawn, so that a tick that changed nothing redraws nothing.</summary>
    /// <remarks>
    /// Every progress tick refreshes every open tab. Rebuilding the chips each time discarded and
    /// re-added a control per filter, sort and figure — ten times a second, invalidating the
    /// layout each time — to arrive at what was already on screen.
    /// </remarks>
    private (TableQuery Query, IReadOnlyList<ColumnFigure> Figures, long Held, long Whole) drawn;

    /// <summary>
    /// What is in force: every filter and sorted column, each removable, and what it leaves.
    /// </summary>
    private void ShowQuery(TablePresentation view)
    {
        var showing = (
            tab.Query,
            tab.Figures,
            Held: tab.Rows?.Count ?? view.Rows?.Count ?? 0,
            Whole: (long)(view.Rows?.Count ?? tab.Rows?.Count ?? 0));

        if (ReferenceEquals(showing.Query, drawn.Query)
            && ReferenceEquals(showing.Figures, drawn.Figures)
            && showing.Held == drawn.Held
            && showing.Whole == drawn.Whole)
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
            var direction = sort.Direction == Ordering.Descending ? "descending" : "ascending";
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

        var held = tab.Rows?.Count ?? view.Rows?.Count ?? 0;
        var whole = view.Rows?.Count ?? held;
        banner.Text = tab.Query.IsFileOrder
            ? string.Create(CultureInfo.InvariantCulture, $"{whole:N0} records, in file order")
            : held == 0
                ? "No record matches"
                : string.Create(CultureInfo.InvariantCulture, $"Showing {held:N0} of {whole:N0} records");

        reset.IsVisible = !tab.Query.IsFileOrder;
    }

    /// <summary>One filter, as the banner names it.</summary>
    private static string Describe(ColumnFilter filter, IReadOnlyList<string> columns)
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
            FilterComparison.Between => "between",
            FilterComparison.Contains => "contains",
            FilterComparison.StartsWith => "starts with",
            FilterComparison.EndsWith => "ends with",
            FilterComparison.Matches => "matches",
            FilterComparison.OneOf => "one of",
            FilterComparison.IsEmpty => "is empty",
            _ => "is not empty",
        };

        var values = filter.Values.Count == 0 ? string.Empty : " " + string.Join(" … ", filter.Values);
        var case_ = filter.IgnoreCase ? " (any case)" : string.Empty;
        return $"{name} {comparison}{values}{case_}";
    }

    /// <summary>Something in force, with a way to take it off again.</summary>
    private static Control Chip(string text, Action remove)
    {
        var close = new Button
        {
            Content = "✕",
            FontSize = 11,
            Padding = new Thickness(4, 0),
            MinWidth = 0,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        };

        close.Click += (_, _) => remove();

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center },
                    close,
                },
            },
        };
    }

    private static Control Row(string label, Control content, Control add) =>
        new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Width = 56,
                    Opacity = 0.75,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                content,
                add,
            },
        };

    private static Button Adds(string label, Func<Control> editor)
    {
        var button = new Button { Content = label, Padding = new Thickness(8, 2) };
        button.Click += (_, _) =>
        {
            var flyout = new Flyout { Content = editor() };
            button.Flyout = flyout;
            flyout.ShowAt(button);
        };

        return button;
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

        for (var index = 0; index < names.Count; index++)
        {
            var capability = capabilities?[index];
            var allowed = capability is not null && capability.IsQueryable && offered(capability);
            picker.Items.Add(new ComboBoxItem
            {
                Content = names[index],
                Tag = index,
                IsEnabled = allowed,
                [ToolTip.TipProperty] = allowed
                    ? null
                    : "This reader cannot filter, sort or total this column. Its values are still shown.",
            });
        }

        return picker;
    }

    private static int? Chosen(ComboBox picker) =>
        (picker.SelectedItem as ComboBoxItem)?.Tag as int?;

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
        var first = new TextBox { Width = 160, PlaceholderText = "value" };
        var second = new TextBox { Width = 160, PlaceholderText = "and", IsVisible = false };
        var ignoreCase = new CheckBox { Content = "ignore case", IsVisible = false };
        var expected = new TextBlock { Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
        var complaint = new TextBlock { Foreground = DanglingBrush, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var apply = new Button { Content = "Apply", IsEnabled = false };

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
            expected.Text = "Expects " + FilterInput.Expected(capability, layout.Columns[index], layout) + ".";
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
                var reading = FilterInput.For(capability, layout.Columns[index], layout, typed);
                if (!reading.Read)
                {
                    complaint.Text = $"'{typed}' is not {reading.Expected}.";
                    complaint.IsVisible = true;
                    return;
                }

                values.Add(reading.Value);
            }

            var filter = new ColumnFilter(index, chosen, values, ignoreCase.IsChecked == true);
            if (!filter.NamesValue)
            {
                // Refused rather than applied as nothing: a filter naming no value would either
                // show every record or none, and the banner would name it either way.
                complaint.Text = chosen == FilterComparison.OneOf
                    ? "Expects one or more values, separated by commas."
                    : "Expects a value to compare against.";
                complaint.IsVisible = true;
                return;
            }

            complaint.IsVisible = false;
            Ask(tab.Query with { Filters = [.. tab.Query.Filters, filter] });
        };

        return Editor("Filter", columns, comparison, first, second, ignoreCase, expected, complaint, apply);
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

    private static string Named(FilterComparison comparison) => comparison switch
    {
        FilterComparison.Equals => "equals",
        FilterComparison.NotEquals => "does not equal",
        FilterComparison.LessThan => "less than",
        FilterComparison.AtMost => "at most",
        FilterComparison.GreaterThan => "greater than",
        FilterComparison.AtLeast => "at least",
        FilterComparison.Between => "between",
        FilterComparison.Contains => "contains",
        FilterComparison.StartsWith => "starts with",
        FilterComparison.EndsWith => "ends with",
        FilterComparison.Matches => "matches (* and ?)",
        FilterComparison.OneOf => "one of (comma separated)",
        FilterComparison.IsEmpty => "is empty",
        _ => "is not empty",
    };

    /// <summary>The sort editor: a column, a direction, and no more than three at once.</summary>
    private Control SortEditor()
    {
        var columns = Columns(_ => true);
        var direction = new ComboBox { MinWidth = 140 };
        direction.Items.Add(new ComboBoxItem { Content = "ascending", Tag = Ordering.Ascending });
        direction.Items.Add(new ComboBoxItem { Content = "descending", Tag = Ordering.Descending });
        direction.SelectedIndex = 0;

        var complaint = new TextBlock { Foreground = DanglingBrush, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var apply = new Button { Content = "Apply" };

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
                complaint.Text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Already sorted by {TableQuery.MaximumSorts} columns. Remove one first.");
                complaint.IsVisible = true;
                return;
            }

            var chosen = (direction.SelectedItem as ComboBoxItem)?.Tag as Ordering? ?? Ordering.Ascending;
            complaint.IsVisible = false;
            Ask(tab.Query with { Sorts = [.. tab.Query.Sorts, new ColumnSort(index, chosen)] });
        };

        return Editor("Sort by", columns, direction, complaint, apply);
    }

    /// <summary>The figure editor: a column, and what to compute over the records in view.</summary>
    private Control FigureEditor()
    {
        var columns = Columns(_ => true);
        var figure = new ComboBox { MinWidth = 140 };
        var apply = new Button { Content = "Apply", IsEnabled = false };
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

        return Editor("Figure", columns, figure, apply);
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
    public void Position()
    {
        if (grid is null || tab.Position < 0)
        {
            return;
        }

        grid.SelectedIndex = tab.Position;
        grid.ScrollIntoView(tab.Position);
    }

    /// <summary>Remembers where the grid is, so returning to this tab finds the same record.</summary>
    public void Remember()
    {
        if (grid is { SelectedIndex: >= 0 } current)
        {
            tab.Position = current.SelectedIndex;
        }
    }

    private static Control NotReady() =>
        new TextBlock
        {
            Text = "This table is still being read. It will be shown as soon as it has been.",
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
            Header = "Record",
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
    /// A followable cell: link coloured, a hand cursor, and the table it leads to in its tooltip.
    /// </summary>
    /// <remarks>
    /// A value that matches no record of that table is marked where it appears, from what the
    /// page asked the store rather than from the findings — the report is bounded and the table
    /// is not, so marking from findings would stop at the fiftieth and leave the fifty-first
    /// looking sound. See the change's design.md D7.
    /// </remarks>
    private static IDataTemplate CellTemplate(int index, IReadOnlyList<string> leadsTo)
    {
        var leads = string.Join(", ", leadsTo);
        return new FuncDataTemplate<RecordRow>(
            (row, _) =>
            {
                if (row is null)
                {
                    return new TextBlock();
                }

                var dangling = row.RefersToNothing(index);
                return new TextBlock
                {
                    Text = row.Values[index],
                    Foreground = dangling ? DanglingBrush : LinkBrush,
                    Cursor = HandCursor,
                    TextDecorations = dangling ? TextDecorations.Strikethrough : TextDecorations.Underline,
                    VerticalAlignment = VerticalAlignment.Center,
                    [ToolTip.TipProperty] = dangling
                        ? $"Refers to '{leads}', which holds no such record."
                        : $"Follows to '{leads}'.",
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
                ? "This table could not be read, so its data is not shown. The rest of the export is unaffected."
                : "This table does not conform to its declaration, so its data is not shown.",
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var finding in view.Findings)
        {
            list.Children.Add(new TextBlock
            {
                Text = finding.Code + "  " + MessageCatalogue.Render(finding, ReportLanguage.English),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (view.Truncated)
        {
            // The list is not exhaustive, and a reader who assumed it was would conclude the
            // table had been fully described.
            list.Children.Add(new TextBlock
            {
                Text = "Analysis of this table stopped at its limit. It may hold further defects "
                    + "that were not looked for.",
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
            });
        }

        return new ScrollViewer { Content = list };
    }

    /// <summary>
    /// Follows a reference from the cell that was acted on, or offers the ones that lead back.
    /// </summary>
    /// <remarks>
    /// The unit of navigation is the foreign key, not the cell: acting on any of a composite
    /// key's values follows the whole key.
    /// </remarks>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (sender is not TableView table || args.Source is not Visual source)
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

        if (args.InitialPressMouseButton == MouseButton.Right)
        {
            offerReferrers(tab.Table, row, table);
            return;
        }

        follow(tab.Table, row, index);
    }
}
