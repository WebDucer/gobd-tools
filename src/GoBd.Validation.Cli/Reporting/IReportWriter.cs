using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Cli.Reporting;

/// <summary>Renders a validation report to a stream.</summary>
/// <remarks>
/// Rendering lives in the CLI rather than the library: report <i>content</i> is specified
/// behaviour, but report <i>presentation</i> is not, and keeping it out of the library leaves
/// the deferred UI free to consume findings directly. See design.md D6.
/// </remarks>
public interface IReportWriter
{
    /// <summary>Writes the report in the given language.</summary>
    void Write(TextWriter output, ValidationReport report, ReportLanguage language);
}

/// <summary>Report formats the CLI can produce.</summary>
public enum ReportFormat
{
    /// <summary>Human-readable text.</summary>
    Text,

    /// <summary>Machine-readable JSON, for CI pipelines and agents.</summary>
    Json,
}
