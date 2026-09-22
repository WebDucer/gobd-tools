using System.Text;
using GoBd.Validation.Dtd;
using GoBd.Validation.Sources;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Dtd;

public sealed class DtdCopyComparisonTests
{
    private static byte[] Canonical => CanonicalDtd.Bytes.ToArray();

    [Fact]
    public void ByteIdenticalCopyPasses()
    {
        var comparison = DtdCopyComparison.Compare(CanonicalDtd.FileName, Canonical);

        comparison.Outcome.ShouldBe(DtdCopyOutcome.Identical);
        comparison.Sha256.ShouldBe(CanonicalDtd.Sha256);
    }

    [Fact]
    public void LineEndingConversionIsAPackagingArtefactNotTampering()
    {
        // The canonical file is CRLF. A .gitattributes rule or a Linux unzip/rezip cycle turns
        // it into LF, which must not be reported as a modified grammar.
        var asLf = Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(Canonical).Replace("\r\n", "\n", StringComparison.Ordinal));

        asLf.ShouldNotBe(Canonical);
        DtdCopyComparison.Compare(CanonicalDtd.FileName, asLf)
            .Outcome.ShouldBe(DtdCopyOutcome.NormalisationDifference);
    }

    [Fact]
    public void ByteOrderMarkIsAPackagingArtefact()
    {
        var withBom = (byte[])[0xEF, 0xBB, 0xBF, .. Canonical];

        DtdCopyComparison.Compare(CanonicalDtd.FileName, withBom)
            .Outcome.ShouldBe(DtdCopyOutcome.NormalisationDifference);
    }

    [Fact]
    public void TrailingWhitespaceIsAPackagingArtefact()
    {
        var padded = (byte[])[.. Canonical, (byte)'\n', (byte)'\n', (byte)' '];

        DtdCopyComparison.Compare(CanonicalDtd.FileName, padded)
            .Outcome.ShouldBe(DtdCopyOutcome.NormalisationDifference);
    }

    [Fact]
    public void EditedGrammarIsAltered()
    {
        // Relaxing Table* back to Table+ is exactly the kind of edit the standard forbids.
        var edited = Encoding.ASCII.GetBytes(
            Encoding.ASCII.GetString(Canonical).Replace("Table*, Command*", "Table+, Command*", StringComparison.Ordinal));

        edited.ShouldNotBe(Canonical);
        DtdCopyComparison.Compare(CanonicalDtd.FileName, edited)
            .Outcome.ShouldBe(DtdCopyOutcome.Altered);
    }

    [Fact]
    public void WhitespaceChangeInsideTheGrammarIsStillAltered()
    {
        // Normalisation is deliberately conservative: only end-of-file whitespace is forgiven,
        // so an edit that happens to be whitespace still shows.
        var edited = Encoding.ASCII.GetBytes(
            Encoding.ASCII.GetString(Canonical).Replace("<!ELEMENT Version", "<!ELEMENT  Version", StringComparison.Ordinal));

        DtdCopyComparison.Compare(CanonicalDtd.FileName, edited)
            .Outcome.ShouldBe(DtdCopyOutcome.Altered);
    }

    [Fact]
    public void MissingCopyIsReportedAsAbsent()
    {
        var comparison = DtdCopyComparison.Missing();

        comparison.Outcome.ShouldBe(DtdCopyOutcome.Absent);
        comparison.EntryName.ShouldBeNull();
    }
}

public sealed class DtdCopyLocatorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"gobd-dtd-{Guid.NewGuid():N}");

    public DtdCopyLocatorTests()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
    }

    private IExportSource Source() => new FolderExportSource(directory);

    [Fact]
    public void ExportWithoutADtdReportsAbsent()
    {
        using var source = Source();

        DtdCopyLocator.Inspect(source).Outcome.ShouldBe(DtdCopyOutcome.Absent);
    }

    [Fact]
    public void CanonicalCopyIsFoundAndMatched()
    {
        File.WriteAllBytes(Path.Combine(directory, CanonicalDtd.FileName), CanonicalDtd.Bytes.ToArray());
        using var source = Source();

        var comparison = DtdCopyLocator.Inspect(source);

        comparison.Outcome.ShouldBe(DtdCopyOutcome.Identical);
        comparison.EntryName.ShouldBe(CanonicalDtd.FileName);
    }

    [Fact]
    public void CanonicallyNamedFileWinsWhenSeveralArePresent()
    {
        File.WriteAllBytes(Path.Combine(directory, CanonicalDtd.FileName), CanonicalDtd.Bytes.ToArray());
        File.WriteAllText(Path.Combine(directory, CanonicalDtd.LegacyFileName), "<!ELEMENT DataSet EMPTY>");
        File.WriteAllText(Path.Combine(directory, "aaa-other.dtd"), "<!ELEMENT DataSet EMPTY>");
        using var source = Source();

        // Selection must be deterministic, not "whichever the listing happened to yield first".
        DtdCopyLocator.Inspect(source).EntryName.ShouldBe(CanonicalDtd.FileName);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}

/// <summary>
/// A normalisation difference must say which artefact accounts for it. "One of these three
/// things" is a list of possibilities, not an answer.
/// </summary>
public sealed class DtdArtefactClassificationTests
{
    private static byte[] Canonical => CanonicalDtd.Bytes.ToArray();

    private static byte[] AsLf() => Encoding.ASCII.GetBytes(
        Encoding.ASCII.GetString(Canonical).Replace("\r\n", "\n", StringComparison.Ordinal));

    [Fact]
    public void LineEndingsAloneAreNamedAlone() =>
        DtdCopyComparison.Compare(CanonicalDtd.FileName, AsLf())
            .Artefacts.ShouldBe(EncodingArtefacts.LineEndings);

    [Fact]
    public void ByteOrderMarkAloneIsNamedAlone() =>
        DtdCopyComparison.Compare(CanonicalDtd.FileName, [0xEF, 0xBB, 0xBF, .. Canonical])
            .Artefacts.ShouldBe(EncodingArtefacts.ByteOrderMark);

    [Fact]
    public void TrailingWhitespaceAloneIsNamedAlone() =>
        DtdCopyComparison.Compare(CanonicalDtd.FileName, [.. Canonical, (byte)'\n', (byte)' '])
            .Artefacts.ShouldBe(EncodingArtefacts.TrailingWhitespace);

    [Fact]
    public void TwoArtefactsAtOnceAreBothNamed()
    {
        // A file can be re-encoded AND BOM-prefixed. Reporting only the first found would send
        // the producer to fix half the problem.
        var comparison = DtdCopyComparison.Compare(CanonicalDtd.FileName, [0xEF, 0xBB, 0xBF, .. AsLf()]);

        comparison.Artefacts.ShouldBe(EncodingArtefacts.LineEndings | EncodingArtefacts.ByteOrderMark);
    }

    [Fact]
    public void AnIdenticalCopyNamesNothing() =>
        DtdCopyComparison.Compare(CanonicalDtd.FileName, Canonical)
            .Artefacts.ShouldBe(EncodingArtefacts.None);
}
