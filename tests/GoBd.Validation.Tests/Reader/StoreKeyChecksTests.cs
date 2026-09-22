using GoBd.Reader.Data;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The store's key checks, held to the streaming engine's.
/// </summary>
/// <remarks>
/// Each fixture is run through both engines and the findings compared as findings — code,
/// arguments and order — rather than by counting them. Two engines that agree on how many
/// defects there are and disagree on which have not agreed about anything.
/// </remarks>
public sealed class StoreKeyChecksTests
{
    private const int Bound = ContentOptions.DefaultMaximumFindingsPerTable;

    private const string Kunden = """
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private const string Bestellungen = """
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
    """;

    private static void BothEnginesAgree(StoreHarness harness)
    {
        var streaming = harness.Streaming(new ContentOptions(Bound), new KeyIntegrityCheck());
        var store = StoreKeyChecks.Run(Imported(harness), harness.DataSet, Bound);

        // Order is part of the agreement: a bound that kept different findings in each engine
        // would make one report unciteable against the other.
        store.ShouldBe(streaming);
    }

    private static ExportStore Imported(StoreHarness harness)
    {
        var store = harness.Store();
        foreach (var table in harness.DataSet.Tables)
        {
            store.Import(table, Bound);
        }

        return store;
    }

    [Fact]
    public void AnExportWhoseKeysAreSoundProducesNothingFromEitherEngine()
    {
        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", "K1;Meier\r\nK2;Lang\r\n"),
            ("bestellungen.csv", "B1;K1\r\nB2;K2\r\nB3;K1\r\n"));

        harness.Streaming(new ContentOptions(Bound), new KeyIntegrityCheck()).ShouldBeEmpty();
        BothEnginesAgree(harness);
    }

    [Fact]
    public void ADuplicatePrimaryKeyIsFoundIdenticallyByBoth()
    {
        using var harness = StoreHarness.Create(
            Kunden,
            ("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK1;Bode\r\n"));

        harness.Streaming(new ContentOptions(Bound), new KeyIntegrityCheck())
            .ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.PrimaryKeyDuplicated);
        BothEnginesAgree(harness);
    }

    [Fact]
    public void AnEmptyPrimaryKeyIsFoundIdenticallyByBoth()
    {
        using var harness = StoreHarness.Create(Kunden, ("kunden.csv", "K1;Meier\r\n;Lang\r\n"));

        BothEnginesAgree(harness);
    }

    [Fact]
    public void ADanglingReferenceIsFoundIdenticallyByBoth()
    {
        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", "K1;Meier\r\n"),
            ("bestellungen.csv", "B1;K1\r\nB2;K9\r\nB3;\r\n"));

        harness.Streaming(new ContentOptions(Bound), new KeyIntegrityCheck())
            .ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.ForeignKeyValueUnresolved);
        BothEnginesAgree(harness);
    }

    [Fact]
    public void ACompositeKeyIsMatchedAsATupleByBoth()
    {
        const string Belege = """
                <Table>
                  <URL>belege.csv</URL>
                  <Name>Belege</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Jahr</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

        const string Zeilen = """
                <Table>
                  <URL>zeilen.csv</URL>
                  <Name>Zeilen</Name>
                  <VariableLength>
                    <VariableColumn><Name>BelegJahr</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>BelegNummer</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey>
                      <Name>BelegJahr</Name>
                      <Name>BelegNummer</Name>
                      <References>Belege</References>
                    </ForeignKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Belege + "\n" + Zeilen,
            ("belege.csv", "2024;2\r\n2023;1\r\n"),
            ("zeilen.csv", "2024;1\r\n2024;2\r\n"));

        harness.Streaming(new ContentOptions(Bound), new KeyIntegrityCheck())
            .ShouldHaveSingleItem().Arguments[2].ShouldBe("2024|1");
        BothEnginesAgree(harness);
    }

    [Fact]
    public void AReferenceToATableThatCouldNotBeReadIsUncheckedInBoth()
    {
        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("bestellungen.csv", "B1;K1\r\nB2;K9\r\n"));

        harness.Streaming(new ContentOptions(Bound), new KeyIntegrityCheck())
            .ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.ReferenceNotChecked);
        BothEnginesAgree(harness);
    }

    [Fact]
    public void ASeparatorInsideAKeyDoesNotMakeTwoKeysEqual()
    {
        // ("a|b", "c") and ("a", "b|c") join to the same text and are different keys. Both
        // engines have to compare tuples rather than the text a finding quotes.
        const string Belege = """
                <Table>
                  <URL>belege.csv</URL>
                  <Name>Belege</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Teil1</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariablePrimaryKey><Name>Teil2</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Belege, ("belege.csv", "a|b;c\r\na;b|c\r\n"));

        harness.Streaming(new ContentOptions(Bound), new KeyIntegrityCheck()).ShouldBeEmpty();
        BothEnginesAgree(harness);
    }

    [Fact]
    public void TheBoundKeepsTheSameFindingsInBoth()
    {
        var children = string.Concat(Enumerable.Range(1, 40).Select(number => $"B{number};K9\r\n"));

        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", "K1;Meier\r\n"),
            ("bestellungen.csv", children));

        var streaming = harness.Streaming(new ContentOptions(5), new KeyIntegrityCheck());
        var store = StoreKeyChecks.Run(Imported(harness), harness.DataSet, 5);

        streaming.Count(finding => finding.Code == FindingCodes.ForeignKeyValueUnresolved).ShouldBe(5);
        store.ShouldBe(streaming);
    }
}
