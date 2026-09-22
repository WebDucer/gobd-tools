using System.Globalization;
using System.Text;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Content;

/// <summary>
/// Checking the keys a declaration promises: that a primary key identifies one record, and that
/// every reference finds one.
/// </summary>
public sealed class KeyIntegrityCheckTests
{
    private static readonly KeyIntegrityCheck Check = new();

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

    private static (string Name, byte[] Bytes) File(string name, string content) =>
        (name, Encoding.Latin1.GetBytes(content));

    // ---- 5.1 the codes -----------------------------------------------------------------------

    [Fact]
    public void EveryCodeTheCheckDeclaresIsInTheCatalogue() =>
        Check.Codes.ShouldAllBe(code => FindingCodes.IsKnown(code));

    // ---- 5.2 eight bytes a record ------------------------------------------------------------

    [Fact]
    public void TheIndexHoldsEightBytesPerKeyAndNoMore()
    {
        // Measured rather than asserted: the claim that a table of tens of millions of records
        // fits in memory rests on this number and on nothing else.
        const int Keys = 1_000_000;
        using var index = new KeyIndex(ContentOptions.DefaultMaximumKeyRecords);
        for (var key = 0; key < Keys; key++)
        {
            index.Add(KeyHash.Of([key.ToString(CultureInfo.InvariantCulture)])).ShouldBeTrue();
        }

        index.Seal();

        index.Count.ShouldBe(Keys);
        index.BytesHeld.ShouldBe(Keys * 8L);
    }

    [Fact]
    public void TheSameKeyHashesTheSameWayInEveryRun() =>
        // .NET randomises string hashing per process. Two runs of the same export must agree, so
        // the key hash cannot be built on it.
        KeyHash.Of(["K1", "2024"]).ShouldBe(KeyHash.Of(["K1", "2024"]));

    [Fact]
    public void ACompositeKeyIsHashedAsATupleRatherThanAsConcatenatedText() =>
        KeyHash.Of(["AB", "C"]).ShouldNotBe(KeyHash.Of(["A", "BC"]));

    // ---- 5.3 duplicate primary keys ----------------------------------------------------------

    [Fact]
    public void ADuplicatePrimaryKeyIsReportedWithTheValueAndTheRecords()
    {
        var findings = ContentCheckHarness.Run(
            Check,
            Kunden,
            [File("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK1;Meier GmbH\r\n")]);

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(FindingCodes.PrimaryKeyDuplicated);
        finding.Arguments[0].ShouldBe("Kunden");
        finding.Arguments[1].ShouldBe("K1");
        finding.Arguments[2].ShouldBe("1, 3");
    }

    [Fact]
    public void ATableOfDistinctKeysReportsNothing() =>
        ContentCheckHarness.Run(Check, Kunden, [File("kunden.csv", "K1;Meier\r\nK2;Lang\r\n")])
            .ShouldBeEmpty();

    [Fact]
    public void AHashCollisionDoesNotProduceAFalseDuplicate()
    {
        // Every key hashes to the same value here, which is the worst collision there is. Only
        // keys that are actually equal may be reported, because a candidate is confirmed against
        // the values themselves.
        var colliding = new KeyIntegrityCheck(_ => 0);

        ContentCheckHarness.Run(colliding, Kunden, [File("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK3;Bode\r\n")])
            .ShouldBeEmpty();

        ContentCheckHarness.Run(colliding, Kunden, [File("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK1;Bode\r\n")])
            .ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.PrimaryKeyDuplicated);
    }

    // ---- 5.4 keys that are not there ---------------------------------------------------------

    [Fact]
    public void AnEmptyPrimaryKeyIsReported()
    {
        var findings = ContentCheckHarness.Run(Check, Kunden, [File("kunden.csv", "K1;Meier\r\n;Lang\r\n")]);

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(FindingCodes.PrimaryKeyIncomplete);
        finding.Arguments[1].ShouldBe("2");
    }

    [Fact]
    public void ACompositeKeyThatIsOnlyPartlyPresentIsReported()
    {
        const string Positionen = """
                <Table>
                  <URL>positionen.csv</URL>
                  <Name>Positionen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariablePrimaryKey><Name>Zeile</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

        var findings = ContentCheckHarness.Run(
            Check, Positionen, [File("positionen.csv", "B1;1\r\nB2;\r\n")]);

        findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.PrimaryKeyIncomplete);
    }

    [Fact]
    public void ARecordWithNoKeyIsNotCountedAsADuplicateOfAnotherOne() =>
        // Two empty keys are two absent keys, not one repeated key. Reporting them as duplicates
        // would name a value that is not in the file.
        ContentCheckHarness.Run(Check, Kunden, [File("kunden.csv", ";Meier\r\n;Lang\r\n")])
            .ShouldAllBe(finding => finding.Code == FindingCodes.PrimaryKeyIncomplete);

    // ---- 5.5 references resolve --------------------------------------------------------------

    [Fact]
    public void AForeignKeyValueWithNoMatchingRecordIsReported()
    {
        var findings = ContentCheckHarness.Run(
            Check,
            Kunden + "\n" + Bestellungen,
            [
                File("kunden.csv", "K1;Meier\r\n"),
                File("bestellungen.csv", "B1;K1\r\nB2;K9\r\n"),
            ]);

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(FindingCodes.ForeignKeyValueUnresolved);
        finding.Arguments[0].ShouldBe("Bestellungen");
        finding.Arguments[1].ShouldBe("2");
        finding.Arguments[2].ShouldBe("K9");
        finding.Arguments[3].ShouldBe("Kunden");
        finding.Scope!.Name.ShouldBe("Bestellungen");
    }

    [Fact]
    public void EveryReferenceResolvingReportsNothing() =>
        ContentCheckHarness.Run(
            Check,
            Kunden + "\n" + Bestellungen,
            [
                File("kunden.csv", "K1;Meier\r\nK2;Lang\r\n"),
                File("bestellungen.csv", "B1;K1\r\nB2;K2\r\nB3;K1\r\n"),
            ]).ShouldBeEmpty();

    [Fact]
    public void AnEmptyForeignKeyIsAnAbsentReferenceRatherThanADanglingOne() =>
        ContentCheckHarness.Run(
            Check,
            Kunden + "\n" + Bestellungen,
            [
                File("kunden.csv", "K1;Meier\r\n"),
                File("bestellungen.csv", "B1;\r\n"),
            ]).ShouldBeEmpty();

    [Fact]
    public void ACompositeForeignKeyIsMatchedAsATupleRatherThanColumnByColumn()
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

        // Both parts exist in the parent, but not together: 2024/2 and 2023/1 are declared, and
        // the child names 2024/1. A column-by-column check would find each half and say nothing.
        var findings = ContentCheckHarness.Run(
            Check,
            Belege + "\n" + Zeilen,
            [
                File("belege.csv", "2024;2\r\n2023;1\r\n"),
                File("zeilen.csv", "2024;1\r\n"),
            ]);

        findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.ForeignKeyValueUnresolved);
    }

    [Fact]
    public void AnAliasedForeignKeyJoinsToTheColumnTheAliasNames()
    {
        const string Mitarbeiter = """
                <Table>
                  <URL>mitarbeiter.csv</URL>
                  <Name>Mitarbeiter</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Personalnummer</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Name</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Vorgesetzter</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey>
                      <Name>Vorgesetzter</Name>
                      <References>Mitarbeiter</References>
                      <Alias><From>Vorgesetzter</From><To>Personalnummer</To></Alias>
                    </ForeignKey>
                  </VariableLength>
                </Table>
        """;

        var findings = ContentCheckHarness.Run(
            Check, Mitarbeiter, [File("mitarbeiter.csv", "P1;Meier;\r\nP2;Lang;P1\r\nP3;Bode;P9\r\n")]);

        findings.ShouldHaveSingleItem().Arguments[2].ShouldBe("P9");
    }

    // ---- 5.6 one index at a time -------------------------------------------------------------

    [Fact]
    public void OnlyOneTablesKeysAreHeldAtATime()
    {
        // Peak memory is the largest table rather than the sum of them, and that is a claim about
        // how many indexes exist at once. Three referenced tables, one index.
        var tables = string.Join("\n", Enumerable.Range(1, 3).Select(number => $"""
                <Table>
                  <URL>t{number}.csv</URL>
                  <Name>T{number}</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Ref</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Ref</Name><References>T{(number % 3) + 1}</References></ForeignKey>
                  </VariableLength>
                </Table>
        """));

        var files = Enumerable.Range(1, 3)
            .Select(number => File($"t{number}.csv", "A;A\r\nB;B\r\n"))
            .ToArray();

        KeyIndex.ResetPeak();
        ContentCheckHarness.Run(Check, tables, files).ShouldBeEmpty();

        KeyIndex.PeakLive.ShouldBe(1);
    }

    // ---- 5.7 what could not be checked -------------------------------------------------------

    [Fact]
    public void AReferenceToATableThatCouldNotBeReadIsReportedAsUnchecked()
    {
        // The parent's file is absent. Its values are not reported as unresolved, because nothing
        // was compared with anything.
        var findings = ContentCheckHarness.Run(
            Check,
            Kunden + "\n" + Bestellungen,
            [File("bestellungen.csv", "B1;K1\r\nB2;K9\r\n")]);

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(FindingCodes.ReferenceNotChecked);
        finding.Arguments.ShouldBe(["Bestellungen", "Kunden"]);
        findings.ShouldNotContain(item => item.Code == FindingCodes.ForeignKeyValueUnresolved);
    }

    // ---- 5.8 the engine's ceiling ------------------------------------------------------------

    [Fact]
    public void ATableBeyondTheEnginesCapacityIsReportedAsUnchecked()
    {
        // The budget is lowered rather than the fixture enlarged: the behaviour under the limit
        // is the point, and a 64-million-record fixture would prove the same thing far slower.
        var findings = ContentCheckHarness.Run(
            Check,
            Kunden,
            [File("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK1;Bode\r\n")],
            new ContentOptions(MaximumKeyRecords: 2));

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(FindingCodes.KeyCheckCapacityExceeded);
        finding.Arguments.ShouldBe(["Kunden", "2"]);
    }

    [Fact]
    public void AnUncheckedTableDoesNotLeaveTheExportConformant()
    {
        var findings = ContentCheckHarness.Run(
            Check,
            Kunden,
            [File("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK3;Bode\r\n")],
            new ContentOptions(MaximumKeyRecords: 2));

        new ValidationReport("x", findings, strict: false).Verdict.ShouldBe(Verdict.NonConformant);
    }

    [Fact]
    public void TheDuplicateItWouldHaveFoundIsNotReportedAsIfItHadBeenChecked() =>
        ContentCheckHarness.Run(
            Check,
            Kunden,
            [File("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK1;Bode\r\n")],
            new ContentOptions(MaximumKeyRecords: 2))
            .ShouldNotContain(finding => finding.Code == FindingCodes.PrimaryKeyDuplicated);

    // ---- the bound applies here too ----------------------------------------------------------

    [Fact]
    public void DanglingReferencesStopAtTheBound()
    {
        var child = new StringBuilder();
        for (var number = 1; number <= 40; number++)
        {
            child.Append(CultureInfo.InvariantCulture, $"B{number};K9\r\n");
        }

        var findings = ContentCheckHarness.Run(
            Check,
            Kunden + "\n" + Bestellungen,
            [File("kunden.csv", "K1;Meier\r\n"), File("bestellungen.csv", child.ToString())],
            new ContentOptions(MaximumFindingsPerTable: 5));

        findings.Count(finding => finding.Code == FindingCodes.ForeignKeyValueUnresolved).ShouldBe(5);
        findings.ShouldContain(finding => finding.Code == FindingCodes.TableAnalysisStopped);
    }
}
