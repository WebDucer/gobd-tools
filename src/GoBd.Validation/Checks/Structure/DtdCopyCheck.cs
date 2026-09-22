using GoBd.Validation.Dtd;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Reports on the DTD file the export ships alongside <c>index.xml</c>.
/// </summary>
/// <remarks>
/// The standard requires the data carrier to carry the grammar and forbids modifying it. This
/// check is purely comparative — validation itself always used the canonical grammar embedded in
/// the application, so nothing here can influence the verdict on <c>index.xml</c>. See design.md D1.
/// </remarks>
public sealed class DtdCopyCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.DtdMissing,
        FindingCodes.DtdNormalisationDifference,
        FindingCodes.DtdAltered,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var comparison = DtdCopyLocator.Inspect(context.Source);

        switch (comparison.Outcome)
        {
            case DtdCopyOutcome.Absent:
                yield return Finding.Create(FindingCodes.DtdMissing, null, CanonicalDtd.FileName);
                break;

            case DtdCopyOutcome.NormalisationDifference:
                yield return Finding.Create(
                    FindingCodes.DtdNormalisationDifference, null,
                    comparison.EntryName!,
                    Describe(comparison.Artefacts),
                    CanonicalDtd.ExpectedSha256,
                    comparison.Sha256!).About(FindingScope.Dtd(comparison.EntryName!));
                break;

            case DtdCopyOutcome.Altered:
                yield return Finding.Create(
                    FindingCodes.DtdAltered, null,
                    comparison.EntryName!,
                    CanonicalDtd.ExpectedSha256,
                    comparison.Sha256!).About(FindingScope.Dtd(comparison.EntryName!));
                break;

            case DtdCopyOutcome.Identical:
            default:
                break;
        }
    }

    /// <summary>
    /// Names the artefacts that differ, as catalogue tokens in a stable order.
    /// </summary>
    /// <remarks>
    /// Tokens, not prose: a check has no business knowing the report language, so the wording
    /// lives in the message catalogue. See design.md D6c.
    /// </remarks>
    private static string Describe(EncodingArtefacts artefacts)
    {
        var named = new List<string>(3);
        if (artefacts.HasFlag(EncodingArtefacts.LineEndings))
        {
            named.Add("lineEndings");
        }

        if (artefacts.HasFlag(EncodingArtefacts.ByteOrderMark))
        {
            named.Add("byteOrderMark");
        }

        if (artefacts.HasFlag(EncodingArtefacts.TrailingWhitespace))
        {
            named.Add("trailingWhitespace");
        }

        // Only reached when normalisation resolved the difference, so something always differs;
        // the fallback keeps the message sensible rather than empty if that ever changes.
        return Localisation.MessageCatalogue.Token(named.Count == 0 ? ["encoding"] : [.. named]);
    }
}
