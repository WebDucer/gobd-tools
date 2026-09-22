using System.Globalization;
using System.Text;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The two engines, held to each other on one fixture per defect class.
/// </summary>
/// <remarks>
/// Content validation is implemented twice on purpose — streaming so the CLI stays one
/// self-contained file, and over the store so the reader stays interactive. The cost of that
/// decision is drift, and this is what turns drift into a test failure rather than a support
/// call.
/// </remarks>
public sealed class BothEnginesTests
{
    private const string Buchungen = """
            <Table>
              <URL>buchungen.csv</URL>
              <Name>Buchungen</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/><MaxLength>6</MaxLength></VariablePrimaryKey>
                <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
              </VariableLength>
            </Table>
    """;

    /// <summary>Every code a fixture below is expected to produce, checked for completeness.</summary>
    private static readonly List<string> Covered = [];

    private static IReadOnlyList<Finding> Agree(StoreHarness harness, int bound = 50)
    {
        var findings = EngineComparison.Agree(harness, bound);
        lock (Covered)
        {
            Covered.AddRange(findings.Select(finding => finding.Code));
        }

        return findings;
    }

    // ---- 8.1 the same defects, in the same order ---------------------------------------------

    [Fact]
    public void ACleanExportProducesNothingFromEitherEngine()
    {
        using var harness = StoreHarness.Create(
            Buchungen,
            ("buchungen.csv", "B0001;31.12.2024;1,50\r\nB0002;01.01.2025;-2,25\r\n"));

        Agree(harness).ShouldBeEmpty();
    }

    [Fact]
    public void ManyDefectsInOneFileAreReportedInTheSameOrderByBoth()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv",
            "B0001;2024-12-31;1,50\r\n"
            + "B0002;31.12.2024;1,505\r\n"
            + "B0000003;31.12.2024;1,50\r\n"
            + "B0004;nicht-ein-datum;keine-zahl\r\n"));

        var findings = Agree(harness);

        // Records in file order, and within a record its columns from left to right.
        findings.Select(finding => (finding.Code, finding.Arguments[1])).ShouldBe(
        [
            (FindingCodes.RecordValueTypeMismatch, "1"),
            (FindingCodes.RecordValueAccuracyExceeded, "2"),
            (FindingCodes.RecordValueTooLong, "3"),
            (FindingCodes.RecordValueTypeMismatch, "4"),
            (FindingCodes.RecordValueTypeMismatch, "4"),
        ]);
    }

    [Fact]
    public void TheComparisonNoticesWhenOnlyOneEngineChanges()
    {
        // A guard on the guard. If dropping a single finding from one side still compared equal,
        // every assertion in this class would be worthless.
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "B0001;2024-12-31;1,50\r\n"));

        var streaming = EngineComparison.Streaming(harness, 50);
        var store = EngineComparison.Store(harness, 50);

        streaming.ShouldBe(store);
        Should.Throw<Shouldly.ShouldAssertException>(() => streaming.Skip(1).ToList().ShouldBe(store));
    }

    // ---- 8.3 one fixture per defect class ----------------------------------------------------

    [Fact]
    public void AColumnCountMismatchIsReportedByBoth()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "B0001;31.12.2024\r\n"));

        Agree(harness).ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.RecordColumnCountMismatch);
    }

    [Fact]
    public void UndecodableBytesAreReportedByBoth()
    {
        const string Utf8Table = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <UTF8/>
                  <VariableLength>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.CreateRaw(
            Utf8Table,
            ("kunden.csv", [0xC3, 0x28, (byte)'\r', (byte)'\n']));

        Agree(harness).ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.RecordBytesUndecodable);
    }

    [Fact]
    public void AnUnterminatedEncapsulatorIsReportedByBoth()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "B0001;31.12.2024;\"1,50\r\n"));

        Agree(harness).ShouldContain(finding => finding.Code == FindingCodes.RecordEncapsulatorUnterminated);
    }

    [Fact]
    public void AFixedRecordOfTheWrongLengthIsReportedByBoth()
    {
        const string Konten = """
                <Table>
                  <URL>konten.txt</URL>
                  <Name>Konten</Name>
                  <FixedLength>
                    <Length>8</Length>
                    <FixedColumn>
                      <Name>Konto</Name><AlphaNumeric/>
                      <FixedRange><From>1</From><To>8</To></FixedRange>
                    </FixedColumn>
                  </FixedLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Konten, ("konten.txt", "10000000200"));

        Agree(harness).ShouldContain(finding => finding.Code == FindingCodes.RecordLengthMismatch);
    }

    [Fact]
    public void TheBoundStopsBothEnginesAtTheSamePlace()
    {
        var content = new StringBuilder();
        for (var number = 1; number <= 200; number++)
        {
            content.Append(CultureInfo.InvariantCulture, $"B{number:0000};nicht-ein-datum;1,50\r\n");
        }

        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", content.ToString()));

        var findings = Agree(harness, bound: 5);

        findings.Count(finding => finding.Code == FindingCodes.RecordValueTypeMismatch).ShouldBe(5);
        findings.ShouldContain(finding => finding.Code == FindingCodes.TableAnalysisStopped);
    }

    [Fact]
    public void AHeaderRowIsReportedByBoth()
    {
        const string Kunden = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <Range><From>2</From></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var reordered = StoreHarness.Create(Kunden, ("kunden.csv", "Firma;Code\r\nMeier;K1\r\n"));
        Agree(reordered).ShouldContain(finding => finding.Code == FindingCodes.HeaderOrderDiffers);

        using var undeclared = StoreHarness.Create(
            Kunden.Replace("<Range><From>2</From></Range>", string.Empty, StringComparison.Ordinal),
            ("kunden.csv", "Code;Firma\r\nK1;Meier\r\n"));
        Agree(undeclared).ShouldContain(finding => finding.Code == FindingCodes.HeaderUndeclared);
    }

    [Fact]
    public void ATableThatCannotBeReadIsReportedByBoth()
    {
        const string Unusable = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <ColumnDelimiter></ColumnDelimiter>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Unusable, ("kunden.csv", "Meier\r\n"));

        Agree(harness).ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.TableNotReadable);
    }

    [Fact]
    public void AFixedRecordLengthOfZeroIsATableNeitherEngineCanRead()
    {
        // With no record delimiter, a length of zero separates nothing from nothing. Reading it
        // never advanced, so this used to run until the volume was full or the process was killed.
        const string Konten = """
                <Table>
                  <URL>konten.txt</URL>
                  <Name>Konten</Name>
                  <FixedLength>
                    <Length>0</Length>
                    <FixedColumn>
                      <Name>Konto</Name><AlphaNumeric/>
                      <FixedRange><From>1</From><To>8</To></FixedRange>
                    </FixedColumn>
                  </FixedLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Konten, ("konten.txt", "1000000020000000"));

        Agree(harness).ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.TableNotReadable);
    }

    [Theory]
    [InlineData("0,5", true)]
    [InlineData("-0,5", true)]
    [InlineData("+12", true)]
    [InlineData("123456789012345678901234567890,12", true)]
    [InlineData(",5", false)]
    [InlineData("-,5", false)]
    [InlineData("1,", false)]
    [InlineData("+", false)]
    [InlineData("1.5", false)]
    [InlineData("1782,90-", true)]
    [InlineData("1782,90+", true)]
    [InlineData("1.782,90-", true)]
    [InlineData("   1782,90-", true)]
    [InlineData("1782,90 -", false)]
    [InlineData("- 1782,90", false)]
    [InlineData("-1782,90-", false)]
    [InlineData("1782,90--", false)]
    [InlineData("+-5", false)]
    public void ANumberIsANumberToBothOrToNeither(string value, bool conforms)
    {
        // One grammar, decided by the characters: a digit on each side of the decimal symbol, and
        // no limit on how many. The streaming engine used to accept ",5" and refuse a value too
        // long for a decimal, each the opposite of what the store decided. A sign stands directly
        // before the digits or directly after them — the standard permits "1782,90-" — but only
        // once, and never apart from them.
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", $"B0001;31.12.2024;{value}\r\n"));

        var findings = Agree(harness);

        if (conforms)
        {
            findings.ShouldBeEmpty();
        }
        else
        {
            findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.RecordValueTypeMismatch);
        }
    }

    [Fact]
    public void ASignAfterTheDigitsHidesNoExcessDecimalsFromEitherEngine()
    {
        // The accuracy check looks for digits at the end of the value, and a trailing sign stands
        // in the way. Widening the grammar alone would have let this through as a number with
        // nothing wrong with it.
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "B0001;31.12.2024;1,234-\r\n"));

        Agree(harness).ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.RecordValueAccuracyExceeded);
    }

    [Theory]
    [InlineData("1-5", true)]
    [InlineData("15-", false)]
    public void ADeclaredMinusDecimalSymbolIsNeverReadAsATrailingSign(string value, bool conforms)
    {
        // Under a declared decimal symbol of "-", "15-" could be a trailing sign or a decimal
        // symbol with nothing after it. Neither engine guesses: the value is read as it always was.
        const string MinusDecimals = """
                <Table>
                  <URL>buchungen.csv</URL>
                  <Name>Buchungen</Name>
                  <DecimalSymbol>-</DecimalSymbol>
                  <DigitGroupingSymbol>.</DigitGroupingSymbol>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Betrag</Name><Numeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(MinusDecimals, ("buchungen.csv", $"B0001;{value}\r\n"));

        var findings = Agree(harness);

        if (conforms)
        {
            findings.ShouldBeEmpty();
        }
        else
        {
            findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.RecordValueTypeMismatch);
        }
    }

    [Fact]
    public void AReferenceIntoATableWhoseAnalysisStoppedStillResolvesForBoth()
    {
        // The referenced table breaks its declaration often enough near the top to exhaust its
        // bound. Its later records are records all the same, and a reference to one resolves: the
        // store stopped reading at the bound, so it held only the first records and reported this
        // reference as unresolved where the streaming engine did not.
        const string Kunden = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        const string Bestellungen = """
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

        var kunden = new StringBuilder();
        for (var number = 1; number <= 20; number++)
        {
            kunden.Append(string.Create(CultureInfo.InvariantCulture, $"K{number};Meier;zu viel\r\n"));
        }

        kunden.Append("K100;Lang\r\n");

        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", kunden.ToString()),
            ("bestellungen.csv", "K100\r\n"));

        var findings = Agree(harness, bound: 5);

        findings.ShouldContain(finding => finding.Code == FindingCodes.TableAnalysisStopped);
        findings.ShouldNotContain(finding => finding.Code == FindingCodes.ForeignKeyValueUnresolved);
    }

    [Fact]
    public void KeyDefectsAreReportedByBoth()
    {
        const string Kunden = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        const string Bestellungen = """
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", "K1;Meier\r\nK1;Lang\r\n;Bode\r\n"),
            ("bestellungen.csv", "K9\r\n"));

        var codes = Agree(harness).Select(finding => finding.Code);

        codes.ShouldContain(FindingCodes.PrimaryKeyDuplicated);
        codes.ShouldContain(FindingCodes.PrimaryKeyIncomplete);
        codes.ShouldContain(FindingCodes.ForeignKeyValueUnresolved);
    }

    [Fact]
    public void AnUncheckableReferenceIsReportedByBoth()
    {
        const string Kunden = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

        const string Bestellungen = """
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("bestellungen.csv", "K9\r\n"));

        Agree(harness).ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.ReferenceNotChecked);
    }

    [Fact]
    public void OneTableSpendsOneBudgetAcrossBothKindsOfDefect()
    {
        // Records that fail their declaration and a key that repeats, in one table. The bound is
        // the table's, not each pass's, and both engines have to spend it the same way.
        const string Both = """
                <Table>
                  <URL>b.csv</URL>
                  <Name>B</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        var content = new StringBuilder();
        for (var number = 1; number <= 40; number++)
        {
            content.Append("B1;nicht-ein-datum\r\n");
        }

        using var harness = StoreHarness.Create(Both, ("b.csv", content.ToString()));

        var findings = Agree(harness, bound: 6);

        findings.Count(finding => finding.Code != FindingCodes.TableAnalysisStopped).ShouldBe(6);
        findings.Count(finding => finding.Code == FindingCodes.TableAnalysisStopped).ShouldBe(1);
    }

    // ---- 8.3 the coverage assertion ----------------------------------------------------------

    [Fact]
    public void EveryContentAndKeyCodeHasAFixtureInThisComparison()
    {
        // Runs the fixtures rather than relying on the other tests having run: a coverage
        // assertion that depends on test ordering proves nothing on a parallel runner.
        ACleanExportProducesNothingFromEitherEngine();
        ManyDefectsInOneFileAreReportedInTheSameOrderByBoth();
        AColumnCountMismatchIsReportedByBoth();
        UndecodableBytesAreReportedByBoth();
        AnUnterminatedEncapsulatorIsReportedByBoth();
        AFixedRecordOfTheWrongLengthIsReportedByBoth();
        TheBoundStopsBothEnginesAtTheSamePlace();
        AHeaderRowIsReportedByBoth();
        ATableThatCannotBeReadIsReportedByBoth();
        KeyDefectsAreReportedByBoth();
        AnUncheckableReferenceIsReportedByBoth();
        OneTableSpendsOneBudgetAcrossBothKindsOfDefect();

        string[] covered;
        lock (Covered)
        {
            covered = [.. Covered.Distinct(StringComparer.Ordinal)];
        }

        var expected = FindingCodes.All
            .Where(info => info.Category is FindingCategory.Content or FindingCategory.Integrity)
            .Select(info => info.Code);

        var missing = expected.Where(code => !covered.Contains(code, StringComparer.Ordinal)).ToArray();

        missing.ShouldBeEmpty($"no fixture holds the two engines to each other for: {string.Join(", ", missing)}");
    }

    // ---- 14.4 a table with no records --------------------------------------------------------

    [Fact]
    public void ATableWithNoRecordsIsReportedByNeitherEngine() =>
        // The streaming side is exactly what the CLI runs, so this is also the CLI's answer: an
        // empty table is not a defect.
        Agree(StoreHarness.Create(Buchungen, ("buchungen.csv", string.Empty))).ShouldBeEmpty();
}
