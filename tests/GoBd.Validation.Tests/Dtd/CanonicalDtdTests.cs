using System.Text;
using GoBd.Validation.Dtd;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Dtd;

public sealed class CanonicalDtdTests
{
    // Fingerprint of the authoritative file obtained from Audicon/Caseware.
    //
    // This literal deliberately duplicates CanonicalDtd.ExpectedSha256, and the duplication is
    // the whole point. Comparing the computed hash against the library's own constant would be
    // circular: replace the resource, regenerate the constant, and the test stays green while
    // proving nothing. Keeping an independent copy here means changing the grammar requires two
    // edits in two files, both visible in one diff. Do not replace this with a reference to
    // CanonicalDtd.ExpectedSha256. See design.md D2.
    private const string ExpectedSha256 =
        "691051c9828ec2bbef71527c4aa77554c09ecd49e862fc6f2bc4b14507c72a0e";

    [Fact]
    public void EmbeddedGrammarHasTheCanonicalLength() =>
        CanonicalDtd.Bytes.Length.ShouldBe(10_646);

    [Fact]
    public void EmbeddedGrammarHasTheCanonicalHash() =>
        CanonicalDtd.Sha256.ShouldBe(ExpectedSha256);

    [Fact]
    public void OpenReadYieldsAnIndependentStreamEachTime()
    {
        using var first = CanonicalDtd.OpenRead();
        using var second = CanonicalDtd.OpenRead();

        first.ReadByte();

        first.Position.ShouldBe(1);
        second.Position.ShouldBe(0);
        second.Length.ShouldBe(CanonicalDtd.Bytes.Length);
    }

    [Fact]
    public void TheLibrarysPinnedFingerprintMatchesThisTestsIndependentCopy() =>
        // Guards the constant itself. If someone regenerates CanonicalDtd.ExpectedSha256 to match
        // a substituted resource, this fails -- which is exactly what the duplication buys.
        CanonicalDtd.ExpectedSha256.ShouldBe(ExpectedSha256);

    [Fact]
    public void TheEmbeddedGrammarPassesItsOwnSelfCheck() => CanonicalDtd.IsCanonical.ShouldBeTrue();

    [Fact]
    public void VerifyRejectsBytesThatAreNotTheCanonicalGrammar()
    {
        // A check that has only ever been seen to pass proves nothing.
        CanonicalDtd.Verify("<!ELEMENT DataSet EMPTY>"u8).ShouldBeFalse();
        CanonicalDtd.Verify(ReadOnlySpan<byte>.Empty).ShouldBeFalse();
    }

    [Fact]
    public void VerifyRejectsAGrammarThatDiffersOnlyInLineEndings()
    {
        // The drift this whole mechanism exists to catch: same text, re-encoded.
        var asLf = Encoding.ASCII.GetBytes(
            Encoding.ASCII.GetString(CanonicalDtd.Bytes).Replace("\r\n", "\n", StringComparison.Ordinal));

        CanonicalDtd.Verify(asLf).ShouldBeFalse();
    }

    [Fact]
    public void GrammarResolvesTheTwoConstructsThePdfGetsWrong()
    {
        var text = Encoding.ASCII.GetString(CanonicalDtd.Bytes);

        // Figure 5 of the specification says Table*; its prose section says Table+. The file wins.
        text.ShouldContain("<!ELEMENT Media (Name, Command*, Table*, Command*, AcceptNoTables?)>");
        // Likewise the Table content model is mandatory, not optional.
        text.ShouldContain("(VariableLength | FixedLength))>");
    }
}
