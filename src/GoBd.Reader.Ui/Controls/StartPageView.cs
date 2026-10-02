using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Controls;

/// <summary>
/// The start page: how far reading the export has got, and what it found.
/// </summary>
/// <remarks>
/// Someone handed a medium first needs to know what condition it is in, and only then which
/// table to read. So this is the first tab, it is open on arrival, and it cannot be closed.
/// <para>
/// It is rebuilt from one <see cref="ExportReading"/> — the same value the pipeline reports
/// progress with — so the verdict, a table's state and the findings can never disagree with what
/// the tabs show. Rebuilt whole rather than patched, at most ten times a second, which for a
/// list of tables costs nothing and removes a class of stale-fragment bug entirely.
/// </para>
/// </remarks>
internal sealed class StartPageView : ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    /// <summary>A fallback stack, because a finding code is read as a code on whichever platform.</summary>
    internal static readonly FontFamily CodeFont =
        FontFamily.Parse("Menlo, Consolas, DejaVu Sans Mono, monospace");

    private ReportLanguage language = ReportLanguage.English;

    /// <summary>Shows again whatever is shown, so a change of language reaches all of it.</summary>
    private Action? redraw;

    private readonly TextBlock verdict = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock progressText = new() { Classes = { ReaderTheme.MutedClass }, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 1, Height = 6 };
    private readonly StackPanel tables = new() { Orientation = Orientation.Vertical };
    private readonly StackPanel findings = new() { Orientation = Orientation.Vertical, Spacing = 16 };
    private readonly StackPanel notices = new() { Orientation = Orientation.Vertical, Spacing = 4 };
    private readonly ReadingAnnouncements announcements = new();
    private readonly TextBlock tablesHeading = Section();
    private readonly TextBlock findingsHeading = Section();
    private readonly TextBlock noticesHeading = Section();

    /// <summary>Creates the start page, empty until an export is read.</summary>
    public StartPageView()
    {
        // Takes focus when its tab is brought to the front from the keyboard, so the summary can
        // be scrolled with the keys rather than only with a wheel.
        Focusable = true;
        Content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Margin = new Thickness(20, 16, 20, 24),
            Children =
            {
                verdict,
                progressText,
                progress,
                tablesHeading,
                tables,
                findingsHeading,
                findings,
                noticesHeading,
                notices,
            },
        };

        SetLanguage(language);
    }

    /// <summary>Told what to announce to assistive technology, and whether it interrupts.</summary>
    public Action<string, bool>? Announce { get; set; }

    /// <summary>Starts announcing the reading of another export from its beginning.</summary>
    public void BeginReading() => announcements.Reset();

    /// <summary>Updates the display language and refreshes all text.</summary>
    public void SetLanguage(ReportLanguage newLanguage)
    {
        language = newLanguage;
        AutomationProperties.SetName(progress, UiText.ReadingProgressName(language));
        AutomationProperties.SetName(this, UiText.SummaryTab(language));
        tablesHeading.Text = UiText.TablesHeading(language);
        findingsHeading.Text = UiText.FindingsHeading(language);
        noticesHeading.Text = UiText.NoticesHeading(language);
        redraw?.Invoke();
    }

    /// <summary>Presents the export as reading it has left it.</summary>
    public void Show(ExportReading reading, string? exportPath)
    {
        ArgumentNullException.ThrowIfNull(reading);
        redraw = () => Show(reading, exportPath);

        verdict.Text = Verdict(reading, exportPath, language);
        if (reading.Complete)
        {
            var brushKey = reading.Verdict == GoBd.Validation.Findings.Verdict.Conformant
                ? ReaderTheme.Conformant
                : ReaderTheme.Defective;
            verdict[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(brushKey);
        }
        else
        {
            verdict.ClearValue(TextBlock.ForegroundProperty);
        }

        progress.Value = reading.Fraction;
        progress.IsVisible = !reading.Complete;
        progressText.Text = Progress(reading, language);

        ShowTables(reading);
        ShowFindings(reading);
        ShowNotices(reading);

        // What changed enough to matter, not every tick: the display is told ten times a second.
        foreach (var message in announcements.Next(reading, language))
        {
            Announce?.Invoke(message, false);
        }
    }

    /// <summary>Says that nothing is open, which is what the reader starts with.</summary>
    public void ShowNothingOpened()
    {
        redraw = ShowNothingOpened;
        Nothing(UiText.NoExportOpened(language), UiText.OpenPrompt(language));
    }

    /// <summary>Says why what was chosen could not be opened, in whichever language is chosen.</summary>
    public void ShowRefusal(Func<ReportLanguage, string> reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        redraw = () => ShowRefusal(reason);
        Nothing(UiText.RefusalTitle(language), reason(language));
    }

    private void Nothing(string heading, string message)
    {
        verdict.Text = heading;
        verdict.ClearValue(TextBlock.ForegroundProperty);
        progressText.Text = message;
        progress.IsVisible = false;
        tables.Children.Clear();
        tablesHeading.IsVisible = false;
        findings.Children.Clear();
        findingsHeading.IsVisible = false;
        notices.Children.Clear();
        noticesHeading.IsVisible = false;
    }

    /// <summary>
    /// What this reader cannot do with the export's columns.
    /// </summary>
    /// <remarks>
    /// Grouped by table, like the findings, and headed as the reader's own. Nothing here carries a
    /// code, because nothing here is a defect: a number longer than this reader computes with and
    /// a time column holding something that is not a time are both within the standard, and the
    /// validator reports neither.
    /// </remarks>
    private void ShowNotices(ExportReading reading)
    {
        notices.Children.Clear();
        noticesHeading.IsVisible = reading.Notices.Count > 0;

        foreach (var group in reading.Notices.GroupBy(notice => notice.Table.Identity, StringComparer.Ordinal))
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                Margin = new Thickness(0, 8, 0, 0),
            };

            panel.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
            });

            foreach (var notice in group)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = notice.Text(language),
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            notices.Children.Add(Card(panel));
        }
    }

    /// <summary>
    /// Every table with its state, its record count and how much was found about it.
    /// </summary>
    /// <remarks>
    /// A table rather than a list of sentences. What a person does here is compare tables — which
    /// are done, which is big, which carries findings — and a column of numbers can be compared
    /// at a glance where a column of prose has to be read line by line.
    /// </remarks>
    private void ShowTables(ExportReading reading)
    {
        tables.Children.Clear();
        tablesHeading.IsVisible = reading.Tables.Count > 0;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
            Margin = new Thickness(0, 4, 0, 0),
        };

        Row(grid, 0);
        Cell(grid, 0, 0, UiText.ColTable(language), header: true);
        Cell(grid, 0, 1, UiText.ColState(language), header: true);
        Cell(grid, 0, 2, UiText.ColRecords(language), header: true, right: true);
        Cell(grid, 0, 3, UiText.ColFindings(language), header: true, right: true);
        Rule(grid, 0);

        var row = 1;
        foreach (var table in reading.Tables)
        {
            Row(grid, row);
            Cell(grid, row, 0, table.Table.Identity);
            Cell(grid, row, 1, State(table, language));
            Cell(grid, row, 2, Records(table, language), right: true);
            Cell(grid, row, 3, table.Findings.Count == 0
                ? "—"
                : UiText.Count(language, table.Findings.Count), right: true);
            row++;
        }

        tables.Children.Add(Card(grid));
    }

    /// <summary>What was found, grouped by the table it concerns.</summary>
    private void ShowFindings(ExportReading reading)
    {
        findings.Children.Clear();

        // Findings about the export rather than about any one table — the description, the
        // grammar it ships — belong to the export, and dropping them would let a non-conformant
        // verdict appear with nothing under it to explain itself.
        var loose = reading.Report.Findings
            .Where(finding => finding.Scope is not { Kind: FindingScopeKind.Table })
            .ToArray();
        if (loose.Length > 0)
        {
            findings.Children.Add(Group(UiText.ExportGroup(language), loose, truncated: false));
        }

        foreach (var table in reading.Tables.Where(table => table.Findings.Count > 0))
        {
            findings.Children.Add(Group(table.Table.Identity, table.Findings, table.Truncated));
        }

        findingsHeading.IsVisible = findings.Children.Count > 0;
    }

    private Control Group(string heading, IReadOnlyList<Finding> group, bool truncated)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = heading,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
        });

        // Code and message in their own columns, so the codes line up and can be read down
        // rather than hunted for at the start of a wrapped paragraph.
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        for (var index = 0; index < group.Count; index++)
        {
            Row(grid, index);
            Cell(grid, index, 0, group[index].Code, code: true);
            Cell(grid, index, 1, MessageCatalogue.Render(group[index], language));
        }

        panel.Children.Add(grid);

        if (truncated)
        {
            panel.Children.Add(new TextBlock
            {
                Text = UiText.TruncatedNotice(language),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
                Classes = { ReaderTheme.MutedClass },
            });
        }

        return Card(panel);
    }

    private static TextBlock Section() =>
        new()
        {
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 20, 0, 0),
            IsVisible = false,
        };

    private static Control Card(Control content)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Margin = new Thickness(0, 6, 0, 0),
            Child = content,
        };
        border[!Border.BackgroundProperty] = new DynamicResourceExtension(ReaderTheme.Surface);
        border[!Border.BorderBrushProperty] = new DynamicResourceExtension(ReaderTheme.SurfaceBorder);
        return border;
    }

    private static void Row(Grid grid, int row)
    {
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
    }

    private static void Cell(
        Grid grid,
        int row,
        int column,
        string text,
        bool header = false,
        bool right = false,
        bool code = false)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = code ? TextWrapping.NoWrap : TextWrapping.Wrap,
            FontWeight = header ? FontWeight.SemiBold : FontWeight.Normal,
            FontFamily = code ? CodeFont : FontFamily.Default,
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(0, 3, column == 0 ? 24 : 20, 3),
        };

        // Headers and codes say less than the text beside them, but must still be read.
        if (header || code)
        {
            block.Classes.Add(ReaderTheme.MutedClass);
        }

        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    /// <summary>A hairline under the header row, so the columns read as columns.</summary>
    private static void Rule(Grid grid, int row)
    {
        var rule = new Border
        {
            Height = 1,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -1),
        };
        rule[!Border.BackgroundProperty] = new DynamicResourceExtension(ReaderTheme.SurfaceBorder);

        Grid.SetRow(rule, row);
        Grid.SetColumn(rule, 0);
        Grid.SetColumnSpan(rule, grid.ColumnDefinitions.Count);
        grid.Children.Add(rule);
    }

    private static string Verdict(ExportReading reading, string? exportPath, ReportLanguage lang)
    {
        var name = exportPath is null ? string.Empty : " — " + Path.GetFileName(exportPath);
        if (!reading.Complete)
        {
            return UiText.ReadingExport(lang) + name;
        }

        return (reading.Verdict == GoBd.Validation.Findings.Verdict.Conformant
            ? UiText.Conformant(lang)
            : UiText.NonConformant(lang)) + name;
    }

    private static string Progress(ExportReading reading, ReportLanguage lang)
    {
        var report = reading.Report;
        return reading.Complete
            ? UiText.ReadSummary(lang, reading.Tables.Count, Size(reading.Bytes, lang), report.ErrorCount, report.WarningCount)
            : UiText.ReadProgress(lang, reading.Current?.Table.Identity, Size(reading.BytesRead, lang), Size(reading.Bytes, lang));
    }

    /// <summary>
    /// A size in the unit that says something about it.
    /// </summary>
    /// <remarks>
    /// Rounded to whole megabytes, a small export reads "0 MB", which looks like nothing was read
    /// at all. The point of measuring progress rather than guessing it is lost if the measurement
    /// cannot express what it measured.
    /// </remarks>
    private static string Size(long bytes, ReportLanguage lang) => bytes switch
    {
        < 1024 => string.Create(UiText.Numbers(lang), $"{bytes} B"),
        < 1024 * 1024 => string.Create(UiText.Numbers(lang), $"{bytes / 1024d:F0} KB"),
        < 1024L * 1024 * 1024 => string.Create(UiText.Numbers(lang), $"{bytes / (1024d * 1024):F1} MB"),
        _ => string.Create(UiText.Numbers(lang), $"{bytes / (1024d * 1024 * 1024):F2} GB"),
    };

    private static string State(TableReading table, ReportLanguage lang) => table.State switch
    {
        TableReadingState.Waiting => UiText.StateWaiting(lang),
        TableReadingState.Reading => UiText.StateReading(lang, table.Fraction),
        TableReadingState.Ready => UiText.StateRead(lang),
        TableReadingState.Defective => UiText.StateDefective(lang),
        _ => UiText.StateUnreadable(lang),
    };

    /// <summary>
    /// A table's record count, once there is one to report.
    /// </summary>
    /// <remarks>
    /// Only for a table that conforms. A defective one is presented as its findings, and a count
    /// of records that do not conform to their declaration is not what a person looks to it for.
    /// </remarks>
    private static string Records(TableReading table, ReportLanguage lang) =>
        table.State == TableReadingState.Ready ? UiText.Count(lang, table.Records) : "—";
}
