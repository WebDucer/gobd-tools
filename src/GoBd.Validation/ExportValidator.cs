using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;

namespace GoBd.Validation;

/// <summary>
/// Validates a GoBD export end to end: open the source, parse and grammar-check
/// <c>index.xml</c>, then run the Tier 0 catalogue over the result.
/// </summary>
/// <remarks>
/// The three phases are gated as design.md D3 describes. A failure to open the export, or an
/// <c>index.xml</c> that is not well formed, stops the run — there is nothing to reason about.
/// A grammar violation does not: the document parsed, so the semantic catalogue still runs and
/// still produces useful findings.
/// </remarks>
public static class ExportValidator
{
    /// <summary>Validates the export at the given path.</summary>
    /// <param name="exportPath">A ZIP archive or an unpacked folder.</param>
    /// <param name="strict">When true, warnings count against the verdict.</param>
    /// <param name="additionalFindings">Findings raised before validation began, such as an unsupported report language.</param>
    /// <param name="contents">How to check the data files, or null to leave them unopened.</param>
    public static ValidationReport Validate(
        string exportPath,
        bool strict = false,
        IEnumerable<Finding>? additionalFindings = null,
        ContentOptions? contents = null) =>
        Validate(exportPath, strict, additionalFindings, Dtd.CanonicalDtd.IsCanonical, contents);

    /// <summary>
    /// Validates with the outcome of the grammar self-check supplied explicitly.
    /// </summary>
    /// <remarks>
    /// A seam for tests only. The embedded grammar cannot be corrupted at run time, so without
    /// this the refusal path could never be exercised — and a guard that has only been seen to
    /// pass proves nothing.
    /// </remarks>
    internal static ValidationReport Validate(
        string exportPath,
        bool strict,
        IEnumerable<Finding>? additionalFindings,
        bool grammarIsCanonical,
        ContentOptions? contents = null)
    {
        ArgumentNullException.ThrowIfNull(exportPath);

        var findings = new List<Finding>(additionalFindings ?? []);

        // Before anything is read: a validator whose own grammar is wrong produces wrong
        // verdicts, and a refusal is far cheaper to notice than a false pass. See design.md D3.
        if (!grammarIsCanonical)
        {
            findings.Add(Finding.Create(
                FindingCodes.EmbeddedGrammarNotCanonical,
                null,
                Dtd.CanonicalDtd.ExpectedSha256,
                Dtd.CanonicalDtd.Sha256));
            return new ValidationReport(exportPath, findings, strict, contents is not null);
        }

        var opened = ExportSourceFactory.Open(exportPath);
        findings.AddRange(opened.Findings);
        if (opened.Source is not { } source)
        {
            return new ValidationReport(exportPath, findings, strict, contents is not null);
        }

        using (source)
        {
            ParseOutcome parsed;
            using (var indexXml = source.OpenRead(ExportSourceFactory.IndexFileName))
            {
                parsed = IndexXmlParser.Parse(indexXml);
            }

            findings.AddRange(parsed.Findings);
            if (parsed.DataSet is { } dataSet)
            {
                var context = new CheckContext(dataSet, source, contents);
                findings.AddRange(CheckEngine.Run(CheckRegistry.Tier0, context));

                // The data files are opened only when the caller asked for it, so that the
                // default run keeps costing a listing and one small document.
                if (contents is not null)
                {
                    findings.AddRange(CheckEngine.Run(CheckRegistry.Content, context));
                }
            }
        }

        return new ValidationReport(exportPath, findings, strict, contents is not null);
    }
}
