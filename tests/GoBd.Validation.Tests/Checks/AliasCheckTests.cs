using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Checks;

/// <summary>
/// Alias remaps a foreign key column onto a target key column whose name differs. Without one,
/// mapping is positional.
/// </summary>
public sealed class AliasCheckTests
{
    private const string Orders = """
        <Table>
          <URL>orders.csv</URL>
          <Name>Orders</Name>
          <VariableLength>
            <VariablePrimaryKey><Name>OrderId</Name><AlphaNumeric/></VariablePrimaryKey>
            <VariablePrimaryKey><Name>CustomerId</Name><AlphaNumeric/></VariablePrimaryKey>
          </VariableLength>
        </Table>
        """;

    private static string Accounts(string foreignKey) => $"""
        <Table>
          <URL>accounts.csv</URL>
          <Name>Accounts</Name>
          <VariableLength>
            <VariableColumn><Name>Order</Name><AlphaNumeric/></VariableColumn>
            <VariableColumn><Name>Customer</Name><AlphaNumeric/></VariableColumn>
            {foreignKey}
          </VariableLength>
        </Table>
        """;

    private static IReadOnlyList<string> Run(string foreignKey) =>
        CheckHarness.Codes(new AliasCheck(), CheckHarness.Medium(Orders, Accounts(foreignKey)));

    [Fact]
    public void FullyAliasedCompositeKeyIsAccepted() =>
        // The specification's own example: Order -> OrderId, Customer -> CustomerId.
        Run("""
            <ForeignKey>
              <Name>Order</Name><Name>Customer</Name>
              <References>Orders</References>
              <Alias><From>Order</From><To>OrderId</To></Alias>
              <Alias><From>Customer</From><To>CustomerId</To></Alias>
            </ForeignKey>
            """).ShouldBeEmpty();

    [Fact]
    public void AliasFromOutsideTheKeyIsReported() =>
        Run("""
            <ForeignKey>
              <Name>Order</Name><Name>Customer</Name>
              <References>Orders</References>
              <Alias><From>Nonsense</From><To>OrderId</To></Alias>
              <Alias><From>Customer</From><To>CustomerId</To></Alias>
            </ForeignKey>
            """).ShouldContain(FindingCodes.AliasFromUnknown);

    [Fact]
    public void AliasOntoANonKeyColumnIsReported() =>
        Run("""
            <ForeignKey>
              <Name>Order</Name><Name>Customer</Name>
              <References>Orders</References>
              <Alias><From>Order</From><To>OrderId</To></Alias>
              <Alias><From>Customer</From><To>NotAKey</To></Alias>
            </ForeignKey>
            """).ShouldContain(FindingCodes.AliasToNotPrimaryKey);

    [Fact]
    public void TwoAliasesOntoTheSameTargetAreReported() =>
        Run("""
            <ForeignKey>
              <Name>Order</Name><Name>Customer</Name>
              <References>Orders</References>
              <Alias><From>Order</From><To>OrderId</To></Alias>
              <Alias><From>Customer</From><To>OrderId</To></Alias>
            </ForeignKey>
            """).ShouldContain(FindingCodes.AliasTargetDuplicated);

    [Fact]
    public void PartiallyAliasedCompositeKeyIsAWarning()
    {
        var finding = CheckHarness
            .Run(new AliasCheck(), CheckHarness.Medium(Orders, Accounts("""
                <ForeignKey>
                  <Name>Order</Name><Name>Customer</Name>
                  <References>Orders</References>
                  <Alias><From>Order</From><To>OrderId</To></Alias>
                </ForeignKey>
                """)))
            .ShouldHaveSingleItem();

        // The standard does not say what the unaliased columns fall back to, so this is
        // reported as producer-facing ambiguity rather than decided by guessing.
        finding.Code.ShouldBe(FindingCodes.AliasPartial);
        finding.Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void KeyWithNoAliasesIsNotJudged() =>
        // Positional mapping is the documented default and needs no alias.
        Run("""
            <ForeignKey>
              <Name>Order</Name><Name>Customer</Name>
              <References>Orders</References>
            </ForeignKey>
            """).ShouldBeEmpty();

    [Fact]
    public void SingleColumnKeyWithOneAliasIsNotPartial() =>
        CheckHarness.Codes(new AliasCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariableColumn><Name>KundenCode</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey>
                  <Name>KundenCode</Name>
                  <References>Kunden</References>
                  <Alias><From>KundenCode</From><To>Id</To></Alias>
                </ForeignKey>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();

    [Fact]
    public void AliasRemapsTheDatatypeComparisonTarget() =>
        // Order aliases onto OrderId, not onto the positionally-first key column. If the
        // resolver ignored the alias this would compare against the wrong column.
        CheckHarness.Codes(new ForeignKeyDatatypeCheck(), CheckHarness.Medium(
            """
            <Table>
              <URL>orders.csv</URL>
              <Name>Orders</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>OrderId</Name><Numeric/></VariablePrimaryKey>
                <VariablePrimaryKey><Name>CustomerId</Name><AlphaNumeric/></VariablePrimaryKey>
              </VariableLength>
            </Table>
            """,
            Accounts("""
                <ForeignKey>
                  <Name>Order</Name><Name>Customer</Name>
                  <References>Orders</References>
                  <Alias><From>Order</From><To>CustomerId</To></Alias>
                  <Alias><From>Customer</From><To>CustomerId</To></Alias>
                </ForeignKey>
                """)))
            .ShouldBeEmpty();
}
