using System.Globalization;
using System.Text;
using System.Text.Json;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Cli.Reporting;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Tests.Content;

/// <summary>
/// Reporting the records that do not conform to what their table declares.
/// </summary>
public sealed class RecordConformanceCheckTests
{
    private static readonly RecordConformanceCheck Check = new();

    /// <summary>A table of a key, a date and an amount — enough for every value defect class.</summary>
    private const string Buchungen = """
            <Table>
              <URL>buchungen.csv</URL>
              <Name>Buchungen</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/><MaxLength>4</MaxLength></VariablePrimaryKey>
                <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private static IReadOnlyList<Finding> Run(string content, ContentOptions? options = null) =>
        ContentCheckHarness.Run(Check, Buchungen, "buchungen.csv", content, options);

    // ---- 3.1 one finding per defect class ----------------------------------------------------

    [Fact]
    public void AConformingTableProducesNothing() =>
        Run("B001;31.12.2024;1,50\r\nB002;01.01.2025;-99,99\r\n").ShouldBeEmpty();

    [Fact]
    public void EveryCodeTheCheckDeclaresIsInTheCatalogue() =>
        Check.Codes.ShouldAllBe(code => FindingCodes.IsKnown(code));

    [Fact]
    public void AValueThatDoesNotMatchItsDeclaredFormatIsReported()
    {
        var finding = Run("B001;2024-12-31;1,50\r\n").ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.RecordValueTypeMismatch);
        finding.Arguments.ShouldBe(["Buchungen", "1", "Datum", "2024-12-31", "DD.MM.YYYY"]);
    }

    [Fact]
    public void AValueBeyondTheDeclaredAccuracyIsReported() =>
        Run("B001;31.12.2024;1,505\r\n").ShouldHaveSingleItem()
            .Code.ShouldBe(FindingCodes.RecordValueAccuracyExceeded);

    [Fact]
    public void AValueBeyondTheDeclaredMaximumLengthIsReported() =>
        Run("B00012;31.12.2024;1,50\r\n").ShouldHaveSingleItem()
            .Code.ShouldBe(FindingCodes.RecordValueTooLong);

    [Fact]
    public void ARecordWithTheWrongNumberOfColumnsIsReportedWithBothCounts()
    {
        var finding = Run("B001;31.12.2024\r\n").ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.RecordColumnCountMismatch);
        finding.Arguments.ShouldBe(["Buchungen", "1", "2", "3"]);
    }

    [Fact]
    public void BytesTheDeclaredCodepageCannotRepresentAreReported()
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

        var findings = ContentCheckHarness.Run(
            Check,
            Utf8Table,
            [("kunden.csv", [0xC3, 0x28, (byte)'\r', (byte)'\n'])]);

        findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.RecordBytesUndecodable);
    }

    [Fact]
    public void AnUnclosedEncapsulatorIsReported() =>
        ContentCheckHarness.Codes(Run("B001;31.12.2024;\"1,50\r\n"))
            .ShouldContain(FindingCodes.RecordEncapsulatorUnterminated);

    [Fact]
    public void AFixedRecordOfTheWrongLengthIsReported()
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

        var findings = ContentCheckHarness.Run(Check, Konten, "konten.txt", "10000000200");

        findings.ShouldContain(finding => finding.Code == FindingCodes.RecordLengthMismatch);
    }

    // ---- 3.2 what a finding is about ---------------------------------------------------------

    [Fact]
    public void EveryContentFindingNamesItsTableAsItsSubject() =>
        Run("B00012;2024-12-31;1,505\r\n")
            .ShouldAllBe(finding => finding.Scope!.Kind == FindingScopeKind.Table
                && finding.Scope.Name == "Buchungen");

    [Fact]
    public void EveryContentFindingNamesTheRecordAndTheColumn()
    {
        var findings = Run("B001;31.12.2024;1,50\r\nB002;2024-01-01;1,50\r\n");

        var finding = findings.ShouldHaveSingleItem();
        finding.Arguments[1].ShouldBe("2");
        finding.Arguments[2].ShouldBe("Datum");
    }

    [Fact]
    public void TheSubjectDoesNotDependOnArgumentOrder()
    {
        // The scope is stated by the check rather than recovered from the arguments, so a
        // reworded message cannot move it. This is the same guard the structural checks carry.
        var reworded = Finding.Create(
            FindingCodes.RecordValueTypeMismatch, null, "1", "Datum", "2024-12-31", "DD.MM.YYYY", "Buchungen")
            .About(FindingScope.Table("Buchungen"));

        reworded.Scope!.Name.ShouldBe("Buchungen");
    }

    // ---- 3.3 what a finding must not carry ---------------------------------------------------

    [Fact]
    public void AFindingCarriesTheOffendingValueAndNothingElseFromTheRecord()
    {
        // The canary sits in the columns either side of the defect. A report that quoted the
        // record rather than the cell would carry it, in either format.
        const string Canary = "GEHEIMNIS-4711";
        const string Table = """
                <Table>
                  <URL>personal.csv</URL>
                  <Name>Personal</Name>
                  <VariableLength>
                    <VariableColumn><Name>Name</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Eintritt</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                    <VariableColumn><Name>Bemerkung</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        var findings = ContentCheckHarness.Run(
            Check, Table, "personal.csv", $"{Canary};nicht-ein-datum;{Canary}\r\n");

        var report = new ValidationReport("/exports/personal.zip", findings, strict: false);
        findings.ShouldHaveSingleItem().Arguments.ShouldContain("nicht-ein-datum");

        foreach (var language in new[] { ReportLanguage.English, ReportLanguage.German })
        {
            Render(new TextReportWriter(), report, language).ShouldNotContain(Canary);
            Render(new JsonReportWriter(), report, language).ShouldNotContain(Canary);
        }
    }

    private static string Render(IReportWriter writer, ValidationReport report, ReportLanguage language)
    {
        var output = new StringWriter();
        writer.Write(output, report, language);
        return output.ToString();
    }

    // ---- 3.4 to 3.6 the bound ----------------------------------------------------------------

    private static string ManyBadDates(int count)
    {
        var builder = new StringBuilder();
        for (var index = 1; index <= count; index++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"B{index:000};nicht-ein-datum;1,50\r\n");
        }

        return builder.ToString();
    }

    [Fact]
    public void AnalysisStopsAtTheBoundAndSaysSo()
    {
        var findings = Run(ManyBadDates(200), new ContentOptions(MaximumFindingsPerTable: 5));

        findings.Count(finding => finding.Code == FindingCodes.RecordValueTypeMismatch).ShouldBe(5);
        findings.ShouldContain(finding => finding.Code == FindingCodes.TableAnalysisStopped);
    }

    [Fact]
    public void TheFindingsReportedAreTheFirstByRecordOrder()
    {
        var findings = Run(ManyBadDates(200), new ContentOptions(MaximumFindingsPerTable: 5));

        findings
            .Where(finding => finding.Code == FindingCodes.RecordValueTypeMismatch)
            .Select(finding => finding.Arguments[1])
            .ShouldBe(["1", "2", "3", "4", "5"]);
    }

    [Fact]
    public void ThePerTableBoundDefaultsToFifty() =>
        Run(ManyBadDates(200))
            .Count(finding => finding.Code == FindingCodes.RecordValueTypeMismatch)
            .ShouldBe(ContentOptions.DefaultMaximumFindingsPerTable);

    [Fact]
    public void ALargerBoundYieldsASupersetOfASmallerOne()
    {
        var content = ManyBadDates(200);
        var few = Run(content, new ContentOptions(5));
        var many = Run(content, new ContentOptions(20));

        var narrow = few.Where(finding => finding.Code != FindingCodes.TableAnalysisStopped).ToArray();
        var wide = many.Where(finding => finding.Code != FindingCodes.TableAnalysisStopped).ToArray();

        wide.Length.ShouldBe(20);
        wide.Take(narrow.Length).ShouldBe(narrow);
    }

    [Fact]
    public void TheVerdictIsTheSameUnderEitherBound()
    {
        var content = ManyBadDates(200);

        new ValidationReport("x", Run(content, new ContentOptions(5)), strict: false).Verdict
            .ShouldBe(new ValidationReport("x", Run(content, new ContentOptions(20)), strict: false).Verdict);
    }

    [Fact]
    public void ATableWithFewerDefectsThanTheBoundIsReadToItsEnd()
    {
        // The last record is defective: reaching it proves the file was not abandoned early, and
        // no truncation notice must appear for a list that is in fact complete.
        var findings = Run("B001;31.12.2024;1,50\r\nB002;31.12.2024;1,50\r\nB003;nicht-ein-datum;1,50\r\n");

        findings.ShouldHaveSingleItem().Arguments[1].ShouldBe("3");
        findings.ShouldNotContain(finding => finding.Code == FindingCodes.TableAnalysisStopped);
    }

    [Fact]
    public void TheBoundBelongsToTheTableRatherThanToEachPassOverIt()
    {
        // One table with both kinds of defect: a bad date in every record, and a primary key
        // repeated. Two passes each keeping their own count would let it report twice the bound.
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

        var findings = ContentCheckHarness.Run(
            new GoBd.Validation.Checks.Content.RecordConformanceCheck(),
            Both,
            "b.csv",
            content.ToString(),
            new ContentOptions(MaximumFindingsPerTable: 6));

        findings.Count(finding => finding.Code != FindingCodes.TableAnalysisStopped).ShouldBe(6);
        findings.Count(finding => finding.Code == FindingCodes.TableAnalysisStopped).ShouldBe(1);
    }

    [Fact]
    public void ARecordExcludedByTheDeclarationIsNotCheckedAsData()
    {
        const string Skipping = """
                <Table>
                  <URL>buchungen.csv</URL>
                  <Name>Buchungen</Name>
                  <Range><From>2</From></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        ContentCheckHarness.Run(Check, Skipping, "buchungen.csv", "Beleg;Datum\r\nB001;31.12.2024\r\n")
            .ShouldBeEmpty();
    }

    [Fact]
    public void ATableWhoseFileIsAbsentIsPassedOverRatherThanReportedTwice() =>
        // GOBD1003 already says the file is missing. A content code repeating it would make the
        // report longer without making it more useful.
        ContentCheckHarness.Run(Check, Buchungen, []).ShouldBeEmpty();

    [Fact]
    public void JsonRendersTheOffendingValueAsWritten()
    {
        var findings = Run("B001;31.12.2024;1.2.3\r\n");
        var report = new ValidationReport("/exports/x.zip", findings, strict: false);
        using var document = JsonDocument.Parse(Render(new JsonReportWriter(), report, ReportLanguage.German));

        document.RootElement.GetProperty("findings")[0].GetProperty("message").GetString()
            .ShouldNotBeNull()
            .ShouldContain("1.2.3");
    }
}
