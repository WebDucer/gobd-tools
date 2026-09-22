using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Checks;

public sealed class FixedRangeCheckTests
{
    private static string Sales(string columns, string? recordLength = null) => CheckHarness.Medium($"""
        <Table>
          <URL>sales.csv</URL>
          <Name>Sales</Name>
          <FixedLength>
            {(recordLength is null ? string.Empty : $"<Length>{recordLength}</Length>")}
            {columns}
          </FixedLength>
        </Table>
        """);

    private static string Column(string name, string range) => $"""
        <FixedColumn><Name>{name}</Name><AlphaNumeric/><FixedRange>{range}</FixedRange></FixedColumn>
        """;

    [Fact]
    public void ContiguousSpansAreAccepted() =>
        CheckHarness.Codes(new FixedRangeCheck(), Sales(
            Column("A", "<From>1</From><To>10</To>") +
            Column("B", "<From>11</From><Length>10</Length>"))).ShouldBeEmpty();

    [Fact]
    public void EndBeforeStartIsInvalid() =>
        CheckHarness.Codes(new FixedRangeCheck(), Sales(
            Column("A", "<From>10</From><To>5</To>"))).ShouldBe([FindingCodes.FixedRangeInvalid]);

    [Fact]
    public void ZeroLengthIsInvalid() =>
        CheckHarness.Codes(new FixedRangeCheck(), Sales(
            Column("A", "<From>1</From><Length>0</Length>"))).ShouldBe([FindingCodes.FixedRangeInvalid]);

    [Fact]
    public void NonNumericPositionIsInvalid() =>
        CheckHarness.Codes(new FixedRangeCheck(), Sales(
            Column("A", "<From>eins</From><To>10</To>"))).ShouldBe([FindingCodes.FixedRangeInvalid]);

    [Fact]
    public void PositionsAreOneBasedSoZeroIsInvalid() =>
        CheckHarness.Codes(new FixedRangeCheck(), Sales(
            Column("A", "<From>0</From><To>10</To>"))).ShouldBe([FindingCodes.FixedRangeInvalid]);

    [Fact]
    public void OverlappingSpansAreAWarning()
    {
        var finding = CheckHarness.Run(new FixedRangeCheck(), Sales(
            Column("A", "<From>1</From><To>10</To>") +
            Column("B", "<From>5</From><To>15</To>"))).ShouldHaveSingleItem();

        // A composite field exposed both whole and in parts is a real pattern, so not an error.
        finding.Code.ShouldBe(FindingCodes.FixedRangeOverlap);
        finding.Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void GapsAreMerelyNoted()
    {
        var finding = CheckHarness.Run(new FixedRangeCheck(), Sales(
            Column("A", "<From>1</From><To>10</To>") +
            Column("B", "<From>21</From><To>30</To>"))).ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.FixedRangeGap);
        finding.Severity.ShouldBe(Severity.Info);
        finding.Arguments.ShouldContain("11");
        finding.Arguments.ShouldContain("20");
    }

    [Fact]
    public void SpanBeyondTheDeclaredRecordLengthIsAnError() =>
        CheckHarness.Codes(new FixedRangeCheck(), Sales(
            Column("A", "<From>1</From><To>10</To>"), recordLength: "8"))
            .ShouldContain(FindingCodes.FixedRangeExceedsRecordLength);

    [Fact]
    public void VariableLengthTablesAreNotSubjectToSpanChecks() =>
        CheckHarness.Codes(new FixedRangeCheck(),
            CheckHarness.Medium(CheckHarness.MasterTable("Kunden"))).ShouldBeEmpty();
}

public sealed class MetadataLintCheckTests
{
    private static string Column(string name, string type) =>
        $"<VariableColumn><Name>{name}</Name>{type}</VariableColumn>";

    private static string Table(string body, string columns) => CheckHarness.Medium($"""
        <Table>
          <URL>kunden.csv</URL>
          <Name>Kunden</Name>
          {body}
          <VariableLength>
            {columns}
          </VariableLength>
        </Table>
        """);

    [Fact]
    public void CleanTableRaisesNothing() =>
        CheckHarness.Codes(new MetadataLintCheck(), Table(
            "<DecimalSymbol>,</DecimalSymbol><DigitGroupingSymbol>.</DigitGroupingSymbol>",
            Column("Betrag", "<Numeric><Accuracy>2</Accuracy></Numeric>"))).ShouldBeEmpty();

    [Theory]
    [InlineData("<Numeric><Accuracy>zwei</Accuracy></Numeric>")]
    [InlineData("<Numeric><ImpliedAccuracy>-3</ImpliedAccuracy></Numeric>")]
    [InlineData("<AlphaNumeric/><MaxLength>0</MaxLength>")]
    [InlineData("<AlphaNumeric/><MaxLength>viele</MaxLength>")]
    public void UnusableNumericSettingsAreReported(string type) =>
        CheckHarness.Codes(new MetadataLintCheck(), Table(string.Empty, Column("Feld", type)))
            .ShouldContain(FindingCodes.NumericSettingInvalid);

    [Theory]
    [InlineData("0")]
    [InlineData("acht")]
    public void AnUnusableFixedRecordLengthIsReported(string length) =>
        CheckHarness.Codes(new MetadataLintCheck(), CheckHarness.Medium($"""
            <Table>
              <URL>konten.txt</URL>
              <Name>Konten</Name>
              <FixedLength>
                <Length>{length}</Length>
                <FixedColumn>
                  <Name>Konto</Name><AlphaNumeric/>
                  <FixedRange><From>1</From><To>8</To></FixedRange>
                </FixedColumn>
              </FixedLength>
            </Table>
            """)).ShouldContain(FindingCodes.NumericSettingInvalid);

    [Fact]
    public void IdenticalDecimalAndGroupingSymbolsAreReported() =>
        CheckHarness.Codes(new MetadataLintCheck(), Table(
            "<DecimalSymbol>,</DecimalSymbol><DigitGroupingSymbol>,</DigitGroupingSymbol>",
            Column("Betrag", "<Numeric/>"))).ShouldContain(FindingCodes.SymbolCollision);

    [Fact]
    public void CollidingDelimitersAreReported() =>
        CheckHarness.Codes(new MetadataLintCheck(), CheckHarness.Medium("""
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <ColumnDelimiter>;</ColumnDelimiter>
                <RecordDelimiter>;</RecordDelimiter>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
            """)).ShouldContain(FindingCodes.DelimiterCollision);

    [Fact]
    public void EmptyTextEncapsulatorMeansNoneAndCannotCollide() =>
        CheckHarness.Codes(new MetadataLintCheck(), CheckHarness.Medium("""
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <ColumnDelimiter>;</ColumnDelimiter>
                <TextEncapsulator></TextEncapsulator>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();

    [Fact]
    public void UnusableDateMaskIsReported() =>
        CheckHarness.Codes(new MetadataLintCheck(), Table(string.Empty,
            Column("Datum", "<Date><Format>DD.MM</Format></Date>")))
            .ShouldContain(FindingCodes.DateMaskUnusable);

    [Fact]
    public void RecordRangeStartingBelowOneIsReported() =>
        CheckHarness.Codes(new MetadataLintCheck(), Table(
            "<Range><From>0</From></Range>", Column("Id", "<AlphaNumeric/>")))
            .ShouldContain(FindingCodes.RecordRangeInvalid);

    [Fact]
    public void RangeUsedToSkipAHeaderRowIsAccepted() =>
        // <Range><From>2</From></Range> is the documented way to skip a header record.
        CheckHarness.Codes(new MetadataLintCheck(), Table(
            "<Range><From>2</From></Range>", Column("Id", "<AlphaNumeric/>"))).ShouldBeEmpty();

    [Fact]
    public void UnusableEpochAndSkipNumBytesAreReported() =>
        CheckHarness.Codes(new MetadataLintCheck(), Table(
            "<SkipNumBytes>viele</SkipNumBytes><Epoch>dreissig</Epoch>", Column("Id", "<AlphaNumeric/>")))
            .Count(code => code == FindingCodes.NumericSettingInvalid).ShouldBe(2);
}
