using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
    private static readonly IBrush ConformantBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x82, 0x3B));
    private static readonly IBrush NonConformantBrush = new SolidColorBrush(Color.FromRgb(0xA5, 0x1D, 0x2D));

    /// <summary>Grey rather than a theme colour, so a rule is faint on a light theme and on a dark one.</summary>
    private static readonly IBrush RuleBrush = Brushes.Gray;

    /// <summary>A fallback stack, because a finding code is read as a code on whichever platform.</summary>
    internal static readonly FontFamily CodeFont =
        FontFamily.Parse("Menlo, Consolas, DejaVu Sans Mono, monospace");

    private readonly TextBlock verdict = new() { FontSize = 22, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock progressText = new() { Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 1, Height = 6 };
    private readonly StackPanel tables = new() { Orientation = Orientation.Vertical };
    private readonly StackPanel findings = new() { Orientation = Orientation.Vertical, Spacing = 16 };
    private readonly StackPanel notices = new() { Orientation = Orientation.Vertical, Spacing = 4 };
    private readonly TextBlock tablesHeading = Section("Tables");
    private readonly TextBlock findingsHeading = Section("Findings");

    /// <summary>
    /// The reader's own limits, headed as its own rather than the export's.
    /// </summary>
    /// <remarks>
    /// Under the findings and named for what it is. What stands here is within the standard and
    /// carries no finding code — the validator reports none of it — so a person comparing the two
    /// tools must not have to work out which of these is a defect and which is this reader
    /// admitting what it cannot do.
    /// </remarks>
    private readonly TextBlock noticesHeading = Section("What this reader cannot do with it");

    /// <summary>Creates the start page, empty until an export is read.</summary>
    public StartPageView()
    {
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
    }

    /// <summary>Presents the export as reading it has left it.</summary>
    public void Show(ExportReading reading, string? exportPath)
    {
        ArgumentNullException.ThrowIfNull(reading);

        verdict.Text = Verdict(reading, exportPath);
        verdict.Foreground = reading.Complete
            ? reading.Verdict == GoBd.Validation.Findings.Verdict.Conformant
                ? ConformantBrush
                : NonConformantBrush
            : null;

        progress.Value = reading.Fraction;
        progress.IsVisible = !reading.Complete;
        progressText.Text = Progress(reading);

        ShowTables(reading);
        ShowFindings(reading);
        ShowNotices(reading);
    }

    /// <summary>Says that nothing is open, which is what the reader starts with.</summary>
    public void ShowNothingOpened(string message) => Nothing("No export opened", message);

    /// <summary>Says why what was chosen could not be opened.</summary>
    public void ShowRefusal(string reason) => Nothing("This export could not be opened", reason);

    private void Nothing(string heading, string message)
    {
        verdict.Text = heading;
        verdict.Foreground = null;
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
                    Text = notice.Text,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.9,
                });
            }

            notices.Children.Add(panel);
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
        Cell(grid, 0, 0, "Table", header: true);
        Cell(grid, 0, 1, "State", header: true);
        Cell(grid, 0, 2, "Records", header: true, right: true);
        Cell(grid, 0, 3, "Findings", header: true, right: true);
        Rule(grid, 0);

        var row = 1;
        foreach (var table in reading.Tables)
        {
            Row(grid, row);
            Cell(grid, row, 0, table.Table.Identity);
            Cell(grid, row, 1, State(table));
            Cell(grid, row, 2, Records(table), right: true);
            Cell(grid, row, 3, table.Findings.Count == 0
                ? "—"
                : table.Findings.Count.ToString(CultureInfo.InvariantCulture), right: true);
            row++;
        }

        tables.Children.Add(grid);
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
            findings.Children.Add(Group("The export", loose, truncated: false));
        }

        foreach (var table in reading.Tables.Where(table => table.Findings.Count > 0))
        {
            findings.Children.Add(Group(table.Table.Identity, table.Findings, table.Truncated));
        }

        findingsHeading.IsVisible = findings.Children.Count > 0;
    }

    private static Control Group(string heading, IReadOnlyList<Finding> group, bool truncated)
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
            Cell(grid, index, 1, MessageCatalogue.Render(group[index], ReportLanguage.English));
        }

        panel.Children.Add(grid);

        if (truncated)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Analysis of this table stopped at its limit. It may hold further defects "
                    + "that were not looked for.",
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
                Opacity = 0.75,
            });
        }

        return panel;
    }

    private static TextBlock Section(string text) =>
        new()
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 20, 0, 0),
            IsVisible = false,
        };

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
            Opacity = header || code ? 0.8 : 1,
            FontFamily = code ? CodeFont : FontFamily.Default,
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(0, 3, column == 0 ? 24 : 20, 3),
        };

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
            Background = RuleBrush,
            Opacity = 0.35,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -1),
        };

        Grid.SetRow(rule, row);
        Grid.SetColumn(rule, 0);
        Grid.SetColumnSpan(rule, grid.ColumnDefinitions.Count);
        grid.Children.Add(rule);
    }

    private static string Verdict(ExportReading reading, string? exportPath)
    {
        var name = exportPath is null ? string.Empty : " — " + Path.GetFileName(exportPath);
        if (!reading.Complete)
        {
            return "Reading the export" + name;
        }

        return (reading.Verdict == GoBd.Validation.Findings.Verdict.Conformant
            ? "Conformant"
            : "Not conformant") + name;
    }

    private static string Progress(ExportReading reading)
    {
        var report = reading.Report;
        if (reading.Complete)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{reading.Tables.Count} table(s), {Size(reading.Bytes)} read. "
                + $"{report.ErrorCount} error(s), {report.WarningCount} warning(s).");
        }

        var current = reading.Current is { } table
            ? $"Reading '{table.Table.Identity}'. "
            : "Preparing. ";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{current}{Size(reading.BytesRead)} of {Size(reading.Bytes)} read.");
    }

    /// <summary>
    /// A size in the unit that says something about it.
    /// </summary>
    /// <remarks>
    /// Rounded to whole megabytes, a small export reads "0 MB", which looks like nothing was read
    /// at all. The point of measuring progress rather than guessing it is lost if the measurement
    /// cannot express what it measured.
    /// </remarks>
    private static string Size(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024d:F0} KB"),
        < 1024L * 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024):F1} MB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024 * 1024):F2} GB"),
    };

    private static string State(TableReading table) => table.State switch
    {
        TableReadingState.Waiting => "waiting",
        TableReadingState.Reading => string.Create(CultureInfo.InvariantCulture, $"reading {table.Fraction * 100:F0}%"),
        TableReadingState.Ready => "read",
        TableReadingState.Defective => "does not conform",
        _ => "could not be read",
    };

    /// <summary>
    /// A table's record count, once there is one to report.
    /// </summary>
    /// <remarks>
    /// Only for a table that conforms. A defective one is presented as its findings, and a count
    /// of records that do not conform to their declaration is not what a person looks to it for.
    /// </remarks>
    private static string Records(TableReading table) =>
        table.State == TableReadingState.Ready
            ? string.Create(CultureInfo.InvariantCulture, $"{table.Records:N0}")
            : "—";
}
