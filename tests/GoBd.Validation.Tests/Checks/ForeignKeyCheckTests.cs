using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Checks;

public sealed class ForeignKeyColumnDeclarationCheckTests
{
    private static IReadOnlyList<string> Run(string body) =>
        CheckHarness.Codes(new ForeignKeyColumnDeclarationCheck(), body);

    [Fact]
    public void ForeignKeyNamingAnUndeclaredColumnIsReported()
    {
        var codes = Run(CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>Menge</Name><Numeric/></VariableColumn>
                <ForeignKey><Name>KundenCode</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
            """));

        // A ForeignKey never introduces a column; KundenCode was never declared.
        codes.ShouldBe([FindingCodes.ForeignKeyColumnUndeclared]);
    }

    [Fact]
    public void ForeignKeyOverDeclaredColumnsIsAccepted() =>
        Run(CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>KundenCode</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>KundenCode</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();

    [Fact]
    public void APrimaryKeyCountsAsADeclaredColumn() =>
        Run(CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>KundenCode</Name><AlphaNumeric/></VariablePrimaryKey>
                <ForeignKey><Name>KundenCode</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();
}

public sealed class ReferenceResolutionCheckTests
{
    private static IReadOnlyList<string> Run(string body) =>
        CheckHarness.Codes(new ReferenceResolutionCheck(), body);

    [Fact]
    public void ReferenceToAnAbsentTableIsDangling() =>
        Run(CheckHarness.Medium("""
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>KundenCode</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>KundenCode</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBe([FindingCodes.ReferenceDangling]);

    [Fact]
    public void ReferenceMatchingATableIdentifiedOnlyByUrlResolves() =>
        // Name is optional; the standard says the URL then serves as the table's identity.
        Run(CheckHarness.Medium(
            """
            <Table>
              <URL>region.csv</URL>
              <VariableLength>
                <VariablePrimaryKey><Name>RegionId</Name><AlphaNumeric/></VariablePrimaryKey>
              </VariableLength>
            </Table>
            """,
            """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>RegionId</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>RegionId</Name><References>region.csv</References></ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();

    [Fact]
    public void ReferenceMatchingSeveralTablesIsAmbiguous()
    {
        var findings = CheckHarness.Run(new ReferenceResolutionCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            """
            <Table>
              <URL>kunden-alt.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
              </VariableLength>
            </Table>
            """,
            """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>Id</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
            """));

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(FindingCodes.ReferenceAmbiguous);
        // The candidates must be named, or the producer cannot tell which two collided.
        finding.Arguments.ShouldContain(argument => argument.Contains("kunden-alt.csv", StringComparison.Ordinal));
    }

    [Fact]
    public void ReferenceAcrossMediaResolves() =>
        // References are scoped to the DataSet, not to a single medium.
        Run($"""
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
            {CheckHarness.MasterTable("Kunden")}
              </Media>
              <Media>
                <Name>Disk 2</Name>
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Id</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
              </Media>
            """).ShouldBeEmpty();
}
