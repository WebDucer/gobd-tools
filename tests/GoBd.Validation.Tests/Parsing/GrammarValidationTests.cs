using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Parsing;

public sealed class GrammarValidationTests
{
    private static ParseOutcome Parse(string body) =>
        IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document(body)));

    [Fact]
    public void GrammarViolationIsReportedWithItsPosition()
    {
        var outcome = Parse("""
              <Version>1.0</Version>
              <Media>
                <Name>D</Name>
                <Table><URL>a.csv</URL></Table>
              </Media>
            """);

        var violation = outcome.Findings.First(finding => finding.Code == FindingCodes.GrammarViolation);

        violation.Severity.ShouldBe(Severity.Error);
        violation.Location.ShouldNotBeNull().Line.ShouldBeGreaterThan(0);
        violation.Arguments.ShouldNotBeEmpty();
    }

    [Fact]
    public void EveryRecoverableViolationIsReportedNotJustTheFirst()
    {
        var outcome = Parse("""
              <Version>1.0</Version>
              <Media>
                <Name>D</Name>
                <Table><URL>a.csv</URL></Table>
                <Table><URL>b.csv</URL></Table>
                <Table><URL>c.csv</URL></Table>
              </Media>
            """);

        outcome.Findings.Count(finding => finding.Code == FindingCodes.GrammarViolation).ShouldBe(3);
    }

    // ---- Task 4.4: the two constructs the specification PDF gets wrong -------------------
    // Figure 5 and the prose "Description of elements" section disagree. The authoritative
    // .dtd file sides with Figure 5. These pin it, so that "correcting" the grammar from the
    // PDF fails the build rather than silently changing what the tool accepts.

    [Fact]
    public void TableWithoutAContentModelIsRejected()
    {
        // The PDF's prose prints (VariableLength | FixedLength)? -- optional. The file says
        // mandatory, so a Table declaring neither is a grammar violation.
        var outcome = Parse("""
              <Version>1.0</Version>
              <Media>
                <Name>D</Name>
                <Table><URL>a.csv</URL></Table>
              </Media>
            """);

        outcome.Findings.ShouldContain(finding => finding.Code == FindingCodes.GrammarViolation);
    }

    [Fact]
    public void EmptyMediaWithAcceptNoTablesIsAccepted()
    {
        // The PDF's prose prints Table+ -- at least one. The file says Table*, so a medium
        // carrying no table is legal since 1.4.
        var outcome = Parse("""
              <Version>1.0</Version>
              <Media>
                <Name>D</Name>
                <AcceptNoTables>true</AcceptNoTables>
              </Media>
            """);

        outcome.CanProceed.ShouldBeTrue();
        outcome.Findings.ShouldNotContain(finding => finding.Code == FindingCodes.GrammarViolation);
    }

    // ---- Task 4.5: 1.6 is a strict superset of 1.1 ---------------------------------------

    [Fact]
    public void DocumentWrittenToElevenConventionsValidatesUnderSixteen()
    {
        // No Extension, Alias or AcceptNoTables anywhere -- exactly what a 1.1-era exporter
        // emits. It must validate, because 1.6 only relaxes and extends 1.1.
        var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
              <Version>1.0</Version>
              <DataSupplier>
                <Name>Beispiel AG</Name>
                <Location>Musterstadt</Location>
                <Comment>1.1-era export</Comment>
              </DataSupplier>
              <Media>
                <Name>CD Nummer 1</Name>
                <Table>
                  <URL>Account.csv</URL>
                  <Name>Account</Name>
                  <DecimalSymbol>.</DecimalSymbol>
                  <DigitGroupingSymbol>,</DigitGroupingSymbol>
                  <VariableLength>
                    <VariablePrimaryKey><Name>RegionId</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Balance</Name><Numeric/></VariableColumn>
                  </VariableLength>
                </Table>
              </Media>
            """, "gdpdu-01-08-2002.dtd")));

        outcome.CanProceed.ShouldBeTrue();
        outcome.Findings.ShouldNotContain(finding => finding.Code == FindingCodes.GrammarViolation);
    }

    [Fact]
    public void LegacySystemIdentifierIsAnObservationNotAnError()
    {
        var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
              <Version>1.0</Version>
              <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
            """, "gdpdu-01-08-2002.dtd")));

        var finding = outcome.Findings.Single(candidate => candidate.Code == FindingCodes.DtdLegacySystemIdentifier);
        // The 1.6 document's own Examples 1 and 3 declare this name, so it cannot be an error.
        finding.Severity.ShouldBe(Severity.Info);
        finding.Arguments.ShouldContain("gdpdu-01-08-2002.dtd");
    }

    [Fact]
    public void CanonicalSystemIdentifierRaisesNoObservation() =>
        Parse("""
              <Version>1.0</Version>
              <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
            """)
            .Findings.ShouldNotContain(finding => finding.Code == FindingCodes.DtdLegacySystemIdentifier);

    // ---- Task 4.6: the gate ---------------------------------------------------------------

    [Fact]
    public void WellFormednessFailureStopsTheRun()
    {
        var outcome = IndexXmlParser.Parse(IndexXml.Stream("""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE DataSet SYSTEM "gdpdu-01-03-2019.dtd">
            <DataSet><Version>1.0</Version>
            """));

        // No tree means nothing for the semantic checks to reason about.
        outcome.CanProceed.ShouldBeFalse();
        outcome.DataSet.ShouldBeNull();
        outcome.Findings.ShouldContain(finding => finding.Code == FindingCodes.XmlNotWellFormed);
    }

    [Fact]
    public void GrammarFailureDoesNotStopTheRun()
    {
        var outcome = Parse("""
              <Version>1.0</Version>
              <Media>
                <Name>D</Name>
                <Table><URL>a.csv</URL></Table>
              </Media>
            """);

        // The document parsed, so the model exists and the semantic catalogue still runs.
        outcome.CanProceed.ShouldBeTrue();
        outcome.DataSet.ShouldNotBeNull().Tables.Single().Url.Value.ShouldBe("a.csv");
        outcome.Findings.ShouldContain(finding => finding.Code == FindingCodes.GrammarViolation);
    }

    [Fact]
    public void EntityExpansionIsBounded()
    {
        // A billion-laughs style internal subset. The canonical grammar declares no entities,
        // so anything expanding here was declared by the document for itself.
        var bomb = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE DataSet SYSTEM "gdpdu-01-03-2019.dtd" [
              <!ENTITY a "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa">
              <!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">
              <!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">
              <!ENTITY d "&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;">
              <!ENTITY e "&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;">
              <!ENTITY f "&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;">
            ]>
            <DataSet><Version>&f;</Version><Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media></DataSet>
            """;

        var outcome = IndexXmlParser.Parse(IndexXml.Stream(bomb));

        // It must be refused rather than exhausting memory, and reported as the budget failure
        // it is rather than as a generic malformed-document error.
        outcome.CanProceed.ShouldBeFalse();
        outcome.Findings.Select(finding => finding.Code)
            .ShouldContain(FindingCodes.EntityExpansionExceeded);
    }
}
