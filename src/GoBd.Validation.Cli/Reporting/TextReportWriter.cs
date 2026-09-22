using System.Globalization;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Cli.Reporting;

/// <summary>Renders a report for a person to read.</summary>
public sealed class TextReportWriter : IReportWriter
{
    /// <inheritdoc />
    public void Write(TextWriter output, ValidationReport report, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(report);

        var verdict = report.Verdict == Verdict.Conformant
            ? ReportText.Conformant(language)
            : ReportText.NonConformant(language);

        // Verdict first: it is the answer, and everything after it is evidence.
        output.WriteLine($"{ReportText.Title(language)}: {verdict}");
        output.WriteLine($"{ReportText.Export(language)}: {report.ExportPath}");
        output.WriteLine(ReportText.Counts(language, report.ErrorCount, report.WarningCount, report.InfoCount));
        if (report.Strict)
        {
            output.WriteLine(ReportText.StrictNote(language));
        }

        foreach (var group in FindingGrouping.Arrange(report.Findings))
        {
            output.WriteLine();
            output.WriteLine(group.Key ?? ReportText.General(language));
            foreach (var finding in group)
            {
                output.WriteLine(Line(finding, language));
            }
        }

        output.WriteLine();
        // Always stated, not only on a clean run: a report with warnings can mislead just as
        // easily as an empty one if the reader assumes the data files were read.
        output.WriteLine(ReportText.Scope(language, report.ContentsExamined));
    }

    private static string Line(Finding finding, ReportLanguage language)
    {
        var severity = ReportText.Severity(language, finding.Severity);
        var position = finding.Location is { } location
            ? string.Create(CultureInfo.InvariantCulture, $"{location.Line}:{location.Column}")
            : "-";
        var message = MessageCatalogue.Render(finding, language);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"  {severity,-8} {finding.Code}  {position,-8} {message}");
    }
}
