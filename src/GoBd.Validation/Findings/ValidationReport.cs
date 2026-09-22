using System.Collections.Immutable;

namespace GoBd.Validation.Findings;

/// <summary>
/// The result of one validation run: every finding, the counts, and the verdict derived from
/// them.
/// </summary>
/// <remarks>
/// Strict mode changes only the verdict. Findings keep the severity their check assigned, so a
/// warning still reports as a warning even when it is what made the run non-conformant. See
/// design.md D7 and the validation-reporting spec.
/// </remarks>
public sealed class ValidationReport
{
    /// <summary>Builds a report over the given findings.</summary>
    /// <param name="exportPath">The export that was validated.</param>
    /// <param name="findings">Findings produced by the run, in the order they were produced.</param>
    /// <param name="strict">When true, warnings count against the verdict as errors do.</param>
    /// <param name="contentsExamined">When true, the data files were read as well as described.</param>
    public ValidationReport(
        string exportPath,
        IEnumerable<Finding> findings,
        bool strict = false,
        bool contentsExamined = false)
    {
        ArgumentNullException.ThrowIfNull(exportPath);
        ArgumentNullException.ThrowIfNull(findings);

        ExportPath = exportPath;
        Findings = [.. findings];
        Strict = strict;
        ContentsExamined = contentsExamined;

        var info = 0;
        var warnings = 0;
        var errors = 0;
        foreach (var finding in Findings)
        {
            switch (finding.Severity)
            {
                case Severity.Error: errors++; break;
                case Severity.Warning: warnings++; break;
                case Severity.Info: info++; break;
                default: break;
            }
        }

        InfoCount = info;
        WarningCount = warnings;
        ErrorCount = errors;
    }

    /// <summary>The export this report describes.</summary>
    public string ExportPath { get; }

    /// <summary>Every finding, in production order.</summary>
    public ImmutableArray<Finding> Findings { get; }

    /// <summary>Whether warnings were counted against the verdict.</summary>
    public bool Strict { get; }

    /// <summary>
    /// Whether this run read the data files, or only what describes them.
    /// </summary>
    /// <remarks>
    /// Published rather than inferred. The likeliest way a report misleads is a clean result
    /// being read as "the export is fine" when no data file was opened, and a reader cannot tell
    /// the two apart from the findings: both produce none.
    /// </remarks>
    public bool ContentsExamined { get; }

    /// <summary>Number of <see cref="Severity.Info"/> findings.</summary>
    public int InfoCount { get; }

    /// <summary>Number of <see cref="Severity.Warning"/> findings.</summary>
    public int WarningCount { get; }

    /// <summary>Number of <see cref="Severity.Error"/> findings.</summary>
    public int ErrorCount { get; }

    /// <summary>True when the run produced no findings at all above <see cref="Severity.Info"/>.</summary>
    public bool IsClean => ErrorCount == 0 && WarningCount == 0;

    /// <summary>
    /// Conformant unless an error was produced — or, in strict mode, unless a warning was.
    /// </summary>
    public Verdict Verdict =>
        ErrorCount > 0 || (Strict && WarningCount > 0)
            ? Verdict.NonConformant
            : Verdict.Conformant;

    /// <summary>Findings carrying the given severity, in production order.</summary>
    public IEnumerable<Finding> OfSeverity(Severity severity) =>
        Findings.Where(finding => finding.Severity == severity);

    /// <summary>The same report re-evaluated under a different strictness.</summary>
    public ValidationReport WithStrict(bool strict) =>
        strict == Strict ? this : new ValidationReport(ExportPath, Findings, strict, ContentsExamined);
}
