using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Checks;

public sealed class MaskTests
{
    [Theory]
    [InlineData("HHMM")] [InlineData("HH:MM")] [InlineData("HHMMSS")] [InlineData("HHMMTT")]
    [InlineData("HH:MMTT")] [InlineData("HH:MM:SS")] [InlineData("HHMMSSTT")] [InlineData("HH:MM:SSTT")]
    [InlineData("HHMM TT")] [InlineData("HH:MM TT")] [InlineData("HHMMSS TT")] [InlineData("HH:MM:SS TT")]
    public void EveryTimeMaskTheStandardListsIsAccepted(string mask) =>
        // These are exactly the twelve examples given in the 1.6 document.
        Masks.IsTimeMask(mask).ShouldBeTrue(mask);

    [Theory]
    [InlineData("HH")] [InlineData("MMSS")] [InlineData("YYYYMMDD")] [InlineData("")] [InlineData("Uhrzeit")]
    public void NonTimeMasksAreRejected(string mask) => Masks.IsTimeMask(mask).ShouldBeFalse(mask);

    [Theory]
    [InlineData("DD.MM.YYYY")] [InlineData("MM/DD/YY")] [InlineData("YYYY-MM-DD")]
    [InlineData("DDMMYY")] [InlineData("DDMMYYYY")] [InlineData("MM/DD/YYYY")]
    public void UsableDateMasksAreAccepted(string mask) =>
        Masks.IsUsableDateMask(mask).ShouldBeTrue(mask);

    [Theory]
    [InlineData("DD.MM")]        // no year
    [InlineData("YYYY")]         // no month or day
    [InlineData("DDDDMMYY")]     // day twice
    [InlineData("DD.MM.YYYY.YY")]// year twice
    [InlineData("TT.MM.YYYY")]   // stray placeholder letters
    [InlineData("")]
    public void UnusableDateMasksAreRejected(string mask) =>
        Masks.IsUsableDateMask(mask).ShouldBeFalse(mask);

    [Fact]
    public void FourDigitYearIsNotMistakenForTwoTwoDigitYears() =>
        // YYYY must be consumed before YY, or DDMMYYYY looks like two years.
        Masks.IsUsableDateMask("DDMMYYYY").ShouldBeTrue();
}

public sealed class MapCheckTests
{
    private static string Table(string columnType, string maps) => CheckHarness.Medium($"""
        <Table>
          <URL>kunden.csv</URL>
          <Name>Kunden</Name>
          <VariableLength>
            <VariableColumn>
              <Name>Erfassungszeit</Name>
              {columnType}
              {maps}
            </VariableColumn>
          </VariableLength>
        </Table>
        """);

    [Fact]
    public void EqualTimeMasksDeclareATimeColumnAndAreAccepted() =>
        CheckHarness.Codes(new MapCheck(), Table("<AlphaNumeric/>",
            "<Map><From>HHMMSS</From><To>HHMMSS</To></Map>")).ShouldBeEmpty();

    [Fact]
    public void DifferingTimeMasksAreAWarning()
    {
        var finding = CheckHarness
            .Run(new MapCheck(), Table("<AlphaNumeric/>", "<Map><From>HHMMSS</From><To>HH:MM:SS</To></Map>"))
            .ShouldHaveSingleItem();

        // This reads as a Time declaration but silently behaves as a value substitution.
        finding.Code.ShouldBe(FindingCodes.TimeMapMasksDiffer);
        finding.Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void OrdinaryValueSubstitutionIsNotMistakenForATimeDeclaration() =>
        CheckHarness.Codes(new MapCheck(), Table("<AlphaNumeric/>",
            "<Map><From>1</From><To>True</To></Map>")).ShouldBeEmpty();

    [Fact]
    public void MapOnANumericColumnIsReported() =>
        CheckHarness.Codes(new MapCheck(), Table("<Numeric/>",
            "<Map><From>1</From><To>True</To></Map>")).ShouldBe([FindingCodes.MapOnNonAlphanumeric]);

    [Fact]
    public void MapOnADateColumnIsReported() =>
        CheckHarness.Codes(new MapCheck(), Table("<Date><Format>YYYYMMDD</Format></Date>",
            "<Map><From>1</From><To>True</To></Map>")).ShouldBe([FindingCodes.MapOnNonAlphanumeric]);
}

public sealed class MediaTablesCheckTests
{
    [Fact]
    public void EmptyMediumWithoutTheMarkerIsAWarning()
    {
        var finding = CheckHarness.Run(new MediaTablesCheck(), """
              <Version>1.0</Version>
              <Media><Name>Disk 1</Name></Media>
            """).ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.MediaWithoutTables);
        finding.Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void EmptyMediumWithTheMarkerIsMerelyNoted()
    {
        var finding = CheckHarness.Run(new MediaTablesCheck(), """
              <Version>1.0</Version>
              <Media><Name>Disk 1</Name><AcceptNoTables>true</AcceptNoTables></Media>
            """).ShouldHaveSingleItem();

        // Legal since 1.4, but only as a deliberate choice.
        finding.Code.ShouldBe(FindingCodes.MediaWithoutTablesAccepted);
        finding.Severity.ShouldBe(Severity.Info);
    }

    [Fact]
    public void MediumWithTablesRaisesNothing() =>
        CheckHarness.Codes(new MediaTablesCheck(),
            CheckHarness.Medium(CheckHarness.MasterTable("Kunden"))).ShouldBeEmpty();
}
