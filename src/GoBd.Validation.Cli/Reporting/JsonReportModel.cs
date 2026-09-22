using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoBd.Validation.Cli.Reporting;

/// <summary>One finding, as the JSON report expresses it.</summary>
/// <param name="Code">Stable finding code — the machine interface, never localised.</param>
/// <param name="Severity">Language-neutral severity: <c>error</c>, <c>warning</c> or <c>info</c>.</param>
/// <param name="Scope">What the finding concerns, or null when it concerns the export itself.</param>
/// <param name="Line">Line in <c>index.xml</c>, or null when the finding has no position.</param>
/// <param name="Column">Column in <c>index.xml</c>, or null when the finding has no position.</param>
/// <param name="Message">Rendered message — the only localised field.</param>
public sealed record JsonFinding(
    string Code,
    string Severity,
    string? Scope,
    int? Line,
    int? Column,
    string Message);

/// <summary>Per-severity totals.</summary>
/// <param name="Error">Number of errors.</param>
/// <param name="Warning">Number of warnings.</param>
/// <param name="Info">Number of notes.</param>
public sealed record JsonCounts(int Error, int Warning, int Info);

/// <summary>The JSON report document.</summary>
/// <param name="SchemaVersion">Increments when this shape changes, so consumers can detect it.</param>
/// <param name="Export">Path that was validated.</param>
/// <param name="Verdict">Language-neutral verdict: <c>conformant</c> or <c>nonConformant</c>.</param>
/// <param name="Strict">Whether warnings counted against the verdict.</param>
/// <param name="Language">Language the messages were rendered in.</param>
/// <param name="ContentsExamined">Whether the data files were read, or only what describes them.</param>
/// <param name="Counts">Per-severity totals.</param>
/// <param name="Findings">Every finding; empty rather than absent on a clean run.</param>
public sealed record JsonReport(
    int SchemaVersion,
    string Export,
    string Verdict,
    bool Strict,
    string Language,
    bool ContentsExamined,
    JsonCounts Counts,
    IReadOnlyList<JsonFinding> Findings);

/// <summary>
/// Source-generated serialisation for the report.
/// </summary>
/// <remarks>
/// No reflection-based serialisation anywhere, so the CLI publishes NativeAOT without trim
/// warnings. See design.md D5.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(JsonReport))]
public sealed partial class JsonReportContext : JsonSerializerContext;
