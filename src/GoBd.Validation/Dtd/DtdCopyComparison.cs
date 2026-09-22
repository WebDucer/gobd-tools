using System.Security.Cryptography;

namespace GoBd.Validation.Dtd;

/// <summary>How the DTD file shipped inside an export compares to the canonical grammar.</summary>
public enum DtdCopyOutcome
{
    /// <summary>The export carries no DTD file at all.</summary>
    Absent,

    /// <summary>Byte-for-byte identical to the canonical grammar.</summary>
    Identical,

    /// <summary>
    /// Identical once a byte-order mark, line-ending convention and trailing whitespace are
    /// normalised away — a packaging artefact rather than an edit.
    /// </summary>
    NormalisationDifference,

    /// <summary>Different in substance; the standard forbids modifying the grammar.</summary>
    Altered,
}

/// <summary>Encoding artefacts that can account for a normalisation difference.</summary>
[Flags]
public enum EncodingArtefacts
{
    /// <summary>Nothing differs.</summary>
    None = 0,

    /// <summary>A leading UTF-8 byte-order mark.</summary>
    ByteOrderMark = 1,

    /// <summary>The line-ending convention.</summary>
    LineEndings = 2,

    /// <summary>Whitespace at the end of the file.</summary>
    TrailingWhitespace = 4,
}

/// <summary>Result of comparing an export's DTD copy to the canonical grammar.</summary>
/// <param name="Outcome">How the two compare.</param>
/// <param name="EntryName">Entry that was compared, when one was found.</param>
/// <param name="Sha256">Lowercase hex hash of the export's copy, when one was found.</param>
/// <param name="Artefacts">
/// Which encoding artefacts account for a normalisation difference. Naming them turns "one of
/// these three things differs" into an answer.
/// </param>
public sealed record DtdCopyComparison(
    DtdCopyOutcome Outcome,
    string? EntryName,
    string? Sha256,
    EncodingArtefacts Artefacts = EncodingArtefacts.None)
{
    /// <summary>Compares the given bytes to the canonical grammar.</summary>
    /// <remarks>
    /// Grading matters here. A strict byte comparison alone would condemn an otherwise perfect
    /// export that a <c>.gitattributes</c> rule or a Linux unzip/rezip cycle had converted to LF
    /// — a false accusation of tampering aimed at exactly the audience this tool serves. See
    /// design.md D1.
    /// </remarks>
    public static DtdCopyComparison Compare(string entryName, ReadOnlySpan<byte> shipped)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(shipped));
        if (shipped.SequenceEqual(CanonicalDtd.Bytes))
        {
            return new DtdCopyComparison(DtdCopyOutcome.Identical, entryName, hash);
        }

        return Normalise(shipped).SequenceEqual(Normalise(CanonicalDtd.Bytes))
            ? new DtdCopyComparison(
                DtdCopyOutcome.NormalisationDifference, entryName, hash, Classify(shipped))
            : new DtdCopyComparison(DtdCopyOutcome.Altered, entryName, hash);
    }

    /// <summary>The result for an export that carries no DTD file.</summary>
    public static DtdCopyComparison Missing() => new(DtdCopyOutcome.Absent, null, null);

    /// <summary>
    /// Works out which artefacts differ, by applying each normalisation on its own.
    /// </summary>
    /// <remarks>
    /// A file can exhibit more than one at once — re-encoded <i>and</i> BOM-prefixed — so every
    /// step that changes the bytes is reported rather than the first one found.
    /// </remarks>
    private static EncodingArtefacts Classify(ReadOnlySpan<byte> shipped)
    {
        var artefacts = EncodingArtefacts.None;

        if (StartsWithBom(shipped) != StartsWithBom(CanonicalDtd.Bytes))
        {
            artefacts |= EncodingArtefacts.ByteOrderMark;
        }

        if (CountCarriageReturns(shipped) != CountCarriageReturns(CanonicalDtd.Bytes))
        {
            artefacts |= EncodingArtefacts.LineEndings;
        }

        if (!TrailingWhitespace(shipped).SequenceEqual(TrailingWhitespace(CanonicalDtd.Bytes)))
        {
            artefacts |= EncodingArtefacts.TrailingWhitespace;
        }

        return artefacts;
    }

    private static bool StartsWithBom(ReadOnlySpan<byte> content)
    {
        ReadOnlySpan<byte> bom = [0xEF, 0xBB, 0xBF];
        return content.StartsWith(bom);
    }

    private static int CountCarriageReturns(ReadOnlySpan<byte> content)
    {
        var count = 0;
        foreach (var value in content)
        {
            if (value == (byte)'\r')
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// The whitespace at the end of the file, with line endings normalised first.
    /// </summary>
    /// <remarks>
    /// Normalising first is essential: re-encoding CRLF to LF changes the trailing bytes without
    /// changing how much trailing whitespace there is, and comparing raw bytes would report
    /// every re-encoded file as also differing in trailing whitespace.
    /// </remarks>
    private static byte[] TrailingWhitespace(ReadOnlySpan<byte> content)
    {
        var normalised = NormaliseLineEndings(content);
        return [.. normalised.Skip(EndOfContent(normalised))];
    }

    /// <summary>
    /// Index just past the last non-whitespace byte.
    /// </summary>
    /// <remarks>
    /// Callers pass line-ending-normalised content, so carriage returns are already gone and
    /// only space, tab and newline can appear here.
    /// </remarks>
    private static int EndOfContent(List<byte> normalised)
    {
        var end = normalised.Count;
        while (end > 0 && normalised[end - 1] is (byte)' ' or (byte)'\t' or (byte)'\n')
        {
            end--;
        }

        return end;
    }

    /// <summary>Collapses CRLF and lone CR to LF, so only line content is compared.</summary>
    private static List<byte> NormaliseLineEndings(ReadOnlySpan<byte> content)
    {
        var normalised = new List<byte>(content.Length);
        for (var index = 0; index < content.Length; index++)
        {
            var value = content[index];
            if (value == (byte)'\r')
            {
                normalised.Add((byte)'\n');
                if (index + 1 < content.Length && content[index + 1] == (byte)'\n')
                {
                    index++;
                }

                continue;
            }

            normalised.Add(value);
        }

        return normalised;
    }

    private static byte[] Normalise(ReadOnlySpan<byte> content)
    {
        // Strip a UTF-8 byte-order mark.
        ReadOnlySpan<byte> bom = [0xEF, 0xBB, 0xBF];
        if (content.StartsWith(bom))
        {
            content = content[bom.Length..];
        }

        var normalised = NormaliseLineEndings(content);

        // Trailing whitespace at the end of the file is a packaging artefact, not an edit.
        // Whitespace elsewhere is left alone, so an actual change to the grammar still shows.
        return [.. normalised.Take(EndOfContent(normalised))];
    }
}
