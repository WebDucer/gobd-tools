using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Cli.Reporting;

/// <summary>Renders a report for CI pipelines and agents.</summary>
/// <remarks>
/// Everything except <c>message</c> is language-neutral, so a pipeline parsing this behaves
/// identically on a German and an English machine. See design.md D8.
/// </remarks>
public sealed class JsonReportWriter : IReportWriter
{
    /// <summary>
    /// Version of the JSON shape this writer emits.
    /// </summary>
    /// <remarks>
    /// Raised to 2 when <c>contentsExamined</c> was added. Every field version 1 promised is
    /// still present and still means the same thing, so a consumer written against 1 keeps
    /// working — but a consumer that decides what a clean report means now has a field it must
    /// read, and the version is how it learns that the field is there to read.
    /// </remarks>
    public const int SchemaVersion = 2;

    /// <summary>
    /// Serialisation options that escape only what the JSON grammar requires.
    /// </summary>
    /// <remarks>
    /// The default encoder escapes everything outside basic Latin plus every HTML-sensitive
    /// character, which turns <c>'</c> into <c>\u0027</c>, every German umlaut into a numeric
    /// escape, and the finding that reports the forbidden characters <c>&amp;</c>, <c>&lt;</c>
    /// and <c>&gt;</c> into a row of escapes — a message about characters, with the characters
    /// hidden. Messages are written for people to read, most often in a CI log.
    /// <para>
    /// "Unsafe" in the encoder's name is a real constraint, not a formality: this output must
    /// never be emitted raw into an HTML page or a <c>&lt;script&gt;</c> element. It goes to
    /// standard output or a file for a pipeline to parse, which is the documented-safe use; a
    /// consumer rendering it in a web page owns the HTML encoding.
    /// </para>
    /// <para>
    /// <c>Encoder</c> cannot be set through <c>[JsonSourceGenerationOptions]</c>, so it is set
    /// here with the source-generated resolver attached. Serialisation stays source-generated
    /// and NativeAOT-safe. See design.md D6b.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Supplying options to the context's constructor <b>replaces</b> everything the
    /// <c>[JsonSourceGenerationOptions]</c> attribute declared, so the naming policy and
    /// indentation are restated here. Omitting either would silently change the field names the
    /// report promises.
    /// </remarks>
    private static readonly JsonReportContext Context = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true,
    });

    /// <inheritdoc />
    public void Write(TextWriter output, ValidationReport report, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(report);

        var document = new JsonReport(
            SchemaVersion,
            report.ExportPath,
            report.Verdict == Verdict.Conformant ? "conformant" : "nonConformant",
            report.Strict,
            language == ReportLanguage.German ? "de" : "en",
            report.ContentsExamined,
            new JsonCounts(report.ErrorCount, report.WarningCount, report.InfoCount),
            [.. report.Findings.Select(finding => Convert(finding, language))]);

        output.Write(JsonSerializer.Serialize(document, Context.JsonReport));
        output.WriteLine();
    }

    private static JsonFinding Convert(Finding finding, ReportLanguage language) => new(
        finding.Code,
        finding.Severity switch
        {
            Severity.Error => "error",
            Severity.Warning => "warning",
            _ => "info",
        },
        FindingGrouping.ScopeOf(finding),
        finding.Location?.Line,
        finding.Location?.Column,
        MessageCatalogue.Render(finding, language));
}
