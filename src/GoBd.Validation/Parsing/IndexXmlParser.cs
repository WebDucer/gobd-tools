using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;
using GoBd.Validation.Dtd;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Parsing;

/// <summary>Result of parsing and grammar-checking <c>index.xml</c>.</summary>
/// <param name="DataSet">The model, present whenever the document was well formed.</param>
/// <param name="Findings">Findings raised while parsing.</param>
public sealed record ParseOutcome(DataSetNode? DataSet, ImmutableArray<Finding> Findings)
{
    /// <summary>
    /// True when a model exists and the semantic checks can run. A grammar violation does not
    /// stop the run — the document parsed, so there is a tree to reason about. Only a
    /// well-formedness failure does. See design.md D3.
    /// </summary>
    public bool CanProceed => DataSet is not null;
}

/// <summary>
/// Parses <c>index.xml</c> into an immutable model while validating it against the canonical
/// Beschreibungsstandard 1.6 grammar.
/// </summary>
public static class IndexXmlParser
{
    /// <summary>
    /// Budget for entity expansion. The canonical grammar declares no entities at all, so any
    /// expansion comes from an internal subset the document declared for itself.
    /// </summary>
    private const long MaxCharactersFromEntities = 10_000_000;

    /// <summary>Parses and grammar-checks the given <c>index.xml</c> stream.</summary>
    public static ParseOutcome Parse(Stream indexXml)
    {
        ArgumentNullException.ThrowIfNull(indexXml);

        var findings = new List<Finding>();
        var resolver = new EmbeddedDtdResolver();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.DTD,
            XmlResolver = resolver,
            MaxCharactersFromEntities = MaxCharactersFromEntities,
            CloseInput = false,
        };

        // Grammar violations are recoverable: the handler records them and the reader carries on,
        // so one run reports every violation rather than only the first.
        settings.ValidationEventHandler += (_, args) =>
            findings.Add(Finding.Create(
                FindingCodes.GrammarViolation,
                new SourceLocation(args.Exception?.LineNumber ?? 0, args.Exception?.LinePosition ?? 0),
                args.Message));

        XDocument document;
        try
        {
            using var reader = XmlReader.Create(indexXml, settings, EmbeddedDtdResolver.BaseUri.ToString());
            document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (ExternalReferenceRefusedException refused)
        {
            findings.Add(Finding.Create(FindingCodes.ExternalReferenceBlocked, null, refused.SystemId));
            return new ParseOutcome(null, [.. findings]);
        }
        catch (XmlException exception)
        {
            var location = new SourceLocation(exception.LineNumber, exception.LinePosition);
            if (exception.InnerException is ExternalReferenceRefusedException inner)
            {
                findings.Add(Finding.Create(FindingCodes.ExternalReferenceBlocked, location, inner.SystemId));
            }
            else if (resolver.RefusedReferences.Count > 0)
            {
                findings.Add(Finding.Create(FindingCodes.ExternalReferenceBlocked, location, resolver.RefusedReferences[0]));
            }
            else if (IsEntityBudgetFailure(exception))
            {
                findings.Add(Finding.Create(FindingCodes.EntityExpansionExceeded, location, exception.Message));
            }
            else
            {
                findings.Add(Finding.Create(FindingCodes.XmlNotWellFormed, location, exception.Message));
            }

            return new ParseOutcome(null, [.. findings]);
        }

        var systemId = document.DocumentType?.SystemId;
        if (systemId is not null
            && string.Equals(systemId, CanonicalDtd.LegacyFileName, StringComparison.Ordinal))
        {
            // The 1.6 document's own Examples 1 and 3 declare the 2002 name, so this is an
            // observation rather than a defect. Validation used the canonical 1.6 grammar either way.
            findings.Add(Finding.Create(
                FindingCodes.DtdLegacySystemIdentifier,
                LocationOf(document.Root),
                systemId));
        }

        var dataSet = document.Root is null ? null : IndexXmlBinder.BindDataSet(document.Root, systemId);
        return new ParseOutcome(dataSet, [.. findings]);
    }

    /// <summary>
    /// Distinguishes an entity-budget refusal from an ordinary malformed document.
    /// </summary>
    /// <remarks>
    /// The runtime reports this through <see cref="XmlException"/> like any other parse failure,
    /// so the setting's own name is matched rather than the surrounding prose — an identifier is
    /// far more stable than a sentence. Should the wording ever stop carrying it, the failure
    /// degrades to <see cref="FindingCodes.XmlNotWellFormed"/>: a less precise code, but still an
    /// error that stops the run, so the document is never accepted either way.
    /// </remarks>
    private static bool IsEntityBudgetFailure(XmlException exception) =>
        exception.Message.Contains(nameof(XmlReaderSettings.MaxCharactersFromEntities), StringComparison.Ordinal);

    internal static SourceLocation LocationOf(XObject? node) =>
        node is IXmlLineInfo info && info.HasLineInfo()
            ? new SourceLocation(info.LineNumber, info.LinePosition)
            : new SourceLocation(0, 0);
}
