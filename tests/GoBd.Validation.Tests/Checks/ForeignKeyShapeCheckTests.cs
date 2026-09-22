using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Checks;

public sealed class ReferencedPrimaryKeyCheckTests
{
    [Fact]
    public void ReferenceToATableWithoutAPrimaryKeyIsReported() =>
        CheckHarness.Codes(new ReferencedPrimaryKeyCheck(), CheckHarness.Medium(
            """
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
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
            """))
            // The DTD allows a table with no primary key, but nothing can join to it.
            .ShouldBe([FindingCodes.ReferencedTableHasNoPrimaryKey]);

    [Fact]
    public void ReferenceToATableWithAPrimaryKeyIsAccepted() =>
        CheckHarness.Codes(new ReferencedPrimaryKeyCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>Id</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();

    [Fact]
    public void DanglingReferenceIsNotPiledOnWith() =>
        // The dangling reference has its own finding; this check must stay quiet.
        CheckHarness.Codes(new ReferencedPrimaryKeyCheck(), CheckHarness.Medium("""
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>Id</Name><References>Nirgends</References></ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();
}

public sealed class ForeignKeyArityCheckTests
{
    private const string CompositeMaster = """
        <Table>
          <URL>account.csv</URL>
          <Name>Account</Name>
          <VariableLength>
            <VariablePrimaryKey><Name>RegionId</Name><AlphaNumeric/></VariablePrimaryKey>
            <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
          </VariableLength>
        </Table>
        """;

    [Fact]
    public void ArityMismatchIsReported()
    {
        var finding = CheckHarness.Run(new ForeignKeyArityCheck(), CheckHarness.Medium(
            CompositeMaster,
            """
            <Table>
              <URL>sales.csv</URL>
              <Name>Sales</Name>
              <VariableLength>
                <VariableColumn><Name>RegionId</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>RegionId</Name><References>Account</References></ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.ForeignKeyArityMismatch);
        // Both counts must be stated or the producer cannot see what to change.
        finding.Arguments.ShouldContain("1");
        finding.Arguments.ShouldContain("2");
    }

    [Fact]
    public void MatchingCompositeArityIsAccepted() =>
        CheckHarness.Codes(new ForeignKeyArityCheck(), CheckHarness.Medium(
            CompositeMaster,
            """
            <Table>
              <URL>sales.csv</URL>
              <Name>Sales</Name>
              <VariableLength>
                <VariableColumn><Name>RegionId</Name><AlphaNumeric/></VariableColumn>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey>
                  <Name>RegionId</Name><Name>Id</Name>
                  <References>Account</References>
                </ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();
}

public sealed class ForeignKeyDatatypeCheckTests
{
    private static string Bestellungen(string columnType) => $"""
        <Table>
          <URL>bestellungen.csv</URL>
          <Name>Bestellungen</Name>
          <VariableLength>
            <VariableColumn><Name>Id</Name>{columnType}</VariableColumn>
            <ForeignKey><Name>Id</Name><References>Kunden</References></ForeignKey>
          </VariableLength>
        </Table>
        """;

    [Fact]
    public void DatatypeMismatchIsReported()
    {
        var finding = CheckHarness.Run(new ForeignKeyDatatypeCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            Bestellungen("<Numeric/>"))).ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.ForeignKeyDatatypeMismatch);
        finding.Arguments.ShouldContain("Numeric");
        finding.Arguments.ShouldContain("AlphaNumeric");
    }

    [Fact]
    public void MatchingDatatypesAreAccepted() =>
        CheckHarness.Codes(new ForeignKeyDatatypeCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            Bestellungen("<AlphaNumeric/>"))).ShouldBeEmpty();

    [Fact]
    public void AccuracyAndMaxLengthDoNotAffectDatatypeIdentity() =>
        // Numeric with accuracy is still Numeric; only the datatype kind is compared.
        CheckHarness.Codes(new ForeignKeyDatatypeCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden", keyType: "<Numeric><Accuracy>2</Accuracy></Numeric>"),
            Bestellungen("<Numeric/>"))).ShouldBeEmpty();

    [Fact]
    public void DateAgainstAlphaNumericIsAMismatch() =>
        CheckHarness.Codes(new ForeignKeyDatatypeCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            Bestellungen("<Date><Format>YYYYMMDD</Format></Date>")))
            .ShouldBe([FindingCodes.ForeignKeyDatatypeMismatch]);
}
