using System.Reflection;
using System.Security.Cryptography;

namespace GoBd.Validation.Dtd;

/// <summary>
/// The Beschreibungsstandard 1.6 DTD the application carries itself.
/// Validation always resolves the DOCTYPE to these bytes; a DTD found inside an export is
/// evidence to be compared, never input that steers the parse. See design.md D1.
/// </summary>
public static class CanonicalDtd
{
    /// <summary>File name the standard gives the 1.6 grammar.</summary>
    public const string FileName = "gdpdu-01-03-2019.dtd";

    /// <summary>File name of the 1.1 grammar, still declared by the 1.6 document's own examples.</summary>
    public const string LegacyFileName = "gdpdu-01-08-2002.dtd";

    /// <summary>
    /// SHA-256 the embedded grammar must have, as published by Audicon/Caseware.
    /// </summary>
    /// <remarks>
    /// This constant, not the embedded file, defines what "canonical" means. The resource is an
    /// artefact that must satisfy it, so a mangled, substituted or wrong-version grammar fails
    /// verification instead of quietly redefining the standard.
    /// <para>
    /// The same literal is repeated in the test suite <b>on purpose</b>. It looks like
    /// duplication and it is not: if the test compared the computed hash against this constant,
    /// then replacing the resource and regenerating the constant would keep the test green, and
    /// the guard would prove only that the constant matches itself. Two literals in two files
    /// mean changing the grammar takes two deliberate edits, visible together in one diff. Do
    /// not "simplify" either away.
    /// </para>
    /// <para>
    /// This is drift protection, not tamper-proofing: anyone able to edit the resource can edit
    /// this line beside it. It defends against accidents — a normalising editor, a bad merge, a
    /// wrong file copied in, a corrupted build — and that is all it claims.
    /// </para>
    /// </remarks>
    public const string ExpectedSha256 = "691051c9828ec2bbef71527c4aa77554c09ecd49e862fc6f2bc4b14507c72a0e";

    private const string ResourceName = "GoBd.Validation.gdpdu-01-03-2019.dtd";

    private static readonly byte[] BytesField = LoadBytes();

    /// <summary>The canonical grammar as shipped by Audicon/Caseware.</summary>
    public static ReadOnlySpan<byte> Bytes => BytesField;

    /// <summary>Lowercase hex SHA-256 of <see cref="Bytes"/>.</summary>
    public static string Sha256 { get; } = Convert.ToHexStringLower(SHA256.HashData(BytesField));

    /// <summary>
    /// True when the embedded grammar matches <see cref="ExpectedSha256"/>.
    /// </summary>
    /// <remarks>
    /// Checked once per run before an export is opened. A validator whose own grammar is wrong
    /// cannot say anything trustworthy about an export, so the run stops rather than reporting a
    /// verdict.
    /// </remarks>
    public static bool IsCanonical { get; } = Verify(BytesField);

    /// <summary>Whether the given bytes are the canonical grammar.</summary>
    public static bool Verify(ReadOnlySpan<byte> candidate) =>
        string.Equals(
            Convert.ToHexStringLower(SHA256.HashData(candidate)),
            ExpectedSha256,
            StringComparison.Ordinal);

    /// <summary>A fresh forward-only stream over the canonical grammar.</summary>
    public static Stream OpenRead() => new MemoryStream(BytesField, writable: false);

    /// <summary>True when <paramref name="fileName"/> names a DTD file this tool recognises.</summary>
    public static bool IsDtdFileName(string fileName) =>
        fileName.EndsWith(".dtd", StringComparison.OrdinalIgnoreCase);

    private static byte[] LoadBytes()
    {
        using var stream = typeof(CanonicalDtd).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
