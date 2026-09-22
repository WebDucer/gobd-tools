using GoBd.Validation.Dtd;
using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Parsing;

/// <summary>
/// The resolver is the security boundary: the export never influences the grammar it is judged
/// against, and no reference in index.xml reaches the filesystem or the network.
/// </summary>
public sealed class EmbeddedDtdResolverTests
{
    [Theory]
    [InlineData("gdpdu-01-03-2019.dtd")]
    [InlineData("gdpdu-01-08-2002.dtd")]
    public void RelativeSystemIdentifiersYieldTheEmbeddedGrammar(string systemId)
    {
        var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Minimal()));

        outcome.CanProceed.ShouldBeTrue();
        outcome.DataSet!.DoctypeSystemId.ShouldNotBeNull();

        // Whichever of the two names the producer declared, the canonical grammar was used.
        var declared = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
              <Version>1.0</Version>
              <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
            """, systemId)));

        declared.CanProceed.ShouldBeTrue();
        declared.DataSet!.DoctypeSystemId.ShouldBe(systemId);
        declared.Findings.ShouldNotContain(finding => finding.Code == FindingCodes.GrammarViolation);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("file://localhost/etc/passwd")]
    [InlineData("http://example.invalid/evil.dtd")]
    [InlineData("ftp://example.invalid/evil.dtd")]
    public void ExternalSystemIdentifiersAreRefused(string systemId)
    {
        var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
              <Version>1.0</Version>
              <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
            """, systemId)));

        outcome.CanProceed.ShouldBeFalse();
        outcome.Findings.ShouldContain(finding => finding.Code == FindingCodes.ExternalReferenceBlocked);
    }

    [Fact]
    public void RefusingAnExternalReferenceReadsNoFile()
    {
        // The refusal must happen before any I/O: point the DOCTYPE at a file that exists and
        // assert its content never reaches the parse.
        var probe = Path.Combine(Path.GetTempPath(), $"gobd-probe-{Guid.NewGuid():N}.dtd");
        File.WriteAllText(probe, "<!ELEMENT DataSet EMPTY>");
        try
        {
            var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
                  <Version>1.0</Version>
                  <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
                """, new Uri(probe).AbsoluteUri)));

            outcome.CanProceed.ShouldBeFalse();
            outcome.Findings.ShouldContain(finding => finding.Code == FindingCodes.ExternalReferenceBlocked);
            // Had the probe DTD been honoured, DataSet EMPTY would have produced grammar
            // violations for the Version and Media children instead.
            outcome.Findings.ShouldNotContain(finding => finding.Code == FindingCodes.GrammarViolation);
        }
        finally
        {
            File.Delete(probe);
        }
    }

    [Fact]
    public void CanonicalGrammarIsUsedEvenWhenNoDtdFileIsPresentAnywhere()
    {
        // Nothing on disk, no export at all -- the grammar travels inside the assembly.
        var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Minimal()));

        outcome.CanProceed.ShouldBeTrue();
        CanonicalDtd.Bytes.Length.ShouldBe(10_646);
    }
}
