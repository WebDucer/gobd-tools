using GoBd.Validation.Findings;

namespace GoBd.Validation.Cli;

/// <summary>
/// What the process tells a pipeline.
/// </summary>
/// <remarks>
/// "Your export is bad" and "the validator could not run" need different humans, so they get
/// different codes. Collapsing them into a single non-zero code is the most common way a
/// validation gate becomes untrustworthy. See design.md D7.
/// </remarks>
public enum ExitCode
{
    /// <summary>Nothing above a note.</summary>
    Conformant = 0,

    /// <summary>Warnings, but no errors.</summary>
    Warnings = 1,

    /// <summary>At least one error, or a warning under strict mode.</summary>
    Errors = 2,

    /// <summary>The validator could not complete, independent of the export's quality.</summary>
    ToolFailure = 3,
}

/// <summary>Derives the process exit code from a report.</summary>
public static class ExitCodes
{
    /// <summary>Maps a report onto the code the process should return.</summary>
    public static ExitCode For(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        // A tool failure outranks everything: the report cannot be trusted to be complete.
        if (report.Findings.Any(finding =>
                finding.Severity == Severity.Error
                && FindingCodes.Describe(finding.Code).Category == FindingCategory.Tool))
        {
            return ExitCode.ToolFailure;
        }

        if (report.ErrorCount > 0 || (report.Strict && report.WarningCount > 0))
        {
            return ExitCode.Errors;
        }

        return report.WarningCount > 0 ? ExitCode.Warnings : ExitCode.Conformant;
    }
}
