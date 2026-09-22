using GoBd.Validation.Content;

namespace GoBd.Validation.Tests.Content;

/// <summary>
/// Interpreting a value as its column declares it.
/// </summary>
/// <remarks>
/// The declaration decides everything: the datatype, the mask, the table's symbols, the epoch,
/// the accuracy, the maximum length and any redefinition. Nothing here may consult the machine's
/// culture, which is why the same bytes are read twice under different declarations and expected
/// to mean different things.
/// </remarks>
public sealed class ValueInterpreterTests
{
    private static string Numeric(string accuracy = "", string symbols = "") => $"""
            <Table>
              <URL>buchungen.csv</URL>
              <Name>Buchungen</Name>
              {symbols}
              <VariableLength>
                <VariableColumn><Name>Betrag</Name><Numeric>{accuracy}</Numeric></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private static string Date(string format = "", string epoch = "") => $"""
            <Table>
              <URL>buchungen.csv</URL>
              <Name>Buchungen</Name>
              {epoch}
              <VariableLength>
                <VariableColumn><Name>Datum</Name><Date>{format}</Date></VariableColumn>
              </VariableLength>
            </Table>
    """;

    // ---- 2.5 declared types ------------------------------------------------------------------

    [Fact]
    public void ANumericValueIsReadUnderTheTablesDeclaredSymbols() =>
        ContentHarness.Interpret(Numeric(), "1.234,56").Number.ShouldBe(1234.56m);

    [Fact]
    public void TheSameBytesMeanSomethingElseUnderDifferentDeclaredSymbols()
    {
        const string English = "<DecimalSymbol>.</DecimalSymbol><DigitGroupingSymbol>,</DigitGroupingSymbol>";

        ContentHarness.Interpret(Numeric(), "1.234").Number.ShouldBe(1234m);
        ContentHarness.Interpret(Numeric(symbols: English), "1.234").Number.ShouldBe(1.234m);
    }

    [Fact]
    public void MisplacedDigitGroupingIsNotANumber() =>
        // Stripping the separators would read this as 123. That is the "silently read as
        // something else" outcome the check exists to prevent, so the grouping is checked.
        ContentHarness.Interpret(Numeric(), "1.2.3").Defect.ShouldNotBeNull();

    [Fact]
    public void ProperlyGroupedDigitsAreANumber() =>
        ContentHarness.Interpret(Numeric(), "-1.234.567,89").Number.ShouldBe(-1234567.89m);

    [Fact]
    public void ANegativeValueIsANumber() =>
        ContentHarness.Interpret(Numeric(), "-42,5").Number.ShouldBe(-42.5m);

    [Fact]
    public void AMinusAfterTheDigitsIsANegativeNumber() =>
        // The standard's own example: a negative figure may carry its sign behind it.
        ContentHarness.Interpret(Numeric(), "1782,90-").Number.ShouldBe(-1782.90m);

    [Fact]
    public void APlusAfterTheDigitsIsAPositiveNumber() =>
        ContentHarness.Interpret(Numeric(), "1782,90+").Number.ShouldBe(1782.90m);

    [Fact]
    public void GroupedDigitsCarryASignAfterThem() =>
        ContentHarness.Interpret(Numeric(), "1.782,90-").Number.ShouldBe(-1782.90m);

    [Fact]
    public void ASignAfterImpliedDecimalsIsNotCountedAsADigit() =>
        ContentHarness.Interpret(Numeric("<ImpliedAccuracy>2</ImpliedAccuracy>"), "178290-").Number.ShouldBe(-1782.90m);

    [Theory]
    [InlineData("1782,90 -")]
    [InlineData("- 1782,90")]
    [InlineData("-1782,90-")]
    [InlineData("1782,90--")]
    public void ASignApartFromTheDigitsOrTwiceIsNotANumber(string value) =>
        ContentHarness.Interpret(Numeric(), value).Defect.ShouldNotBeNull().Kind.ShouldBe(ContentDefectKind.TypeOrFormatMismatch);

    [Fact]
    public void AValueThatIsNotANumberIsReportedAgainstItsColumn()
    {
        var defect = ContentHarness.Interpret(Numeric(), "n/a").Defect.ShouldNotBeNull();

        defect.Kind.ShouldBe(ContentDefectKind.TypeOrFormatMismatch);
        defect.ColumnIndex.ShouldBe(0);
        defect.Value.ShouldBe("n/a");
    }

    [Fact]
    public void MoreDecimalsThanTheDeclaredAccuracyIsReported()
    {
        var defect = ContentHarness.Interpret(Numeric("<Accuracy>2</Accuracy>"), "1,234").Defect.ShouldNotBeNull();

        defect.Kind.ShouldBe(ContentDefectKind.AccuracyExceeded);
        defect.Value.ShouldBe("1,234");
        defect.Expected.ShouldBe("2");
    }

    [Fact]
    public void TheDeclaredAccuracyIsAMaximumRatherThanAnExactCount() =>
        ContentHarness.Interpret(Numeric("<Accuracy>2</Accuracy>"), "1,2").Defect.ShouldBeNull();

    [Fact]
    public void AnImpliedAccuracyPutsThePointWhereTheDeclarationSaysItBelongs()
    {
        // The file writes whole digits and the declaration says the last two are decimals, so
        // there is no separator to be wrong about and no accuracy to exceed.
        var value = ContentHarness.Interpret(Numeric("<ImpliedAccuracy>2</ImpliedAccuracy>"), "12345");

        value.Number.ShouldBe(123.45m);
        value.Defect.ShouldBeNull();
    }

    [Fact]
    public void ADateIsReadUnderTheStandardsDefaultMaskWhenNoneIsDeclared() =>
        ContentHarness.Interpret(Date(), "31.12.2024").Date.ShouldBe(new DateOnly(2024, 12, 31));

    [Fact]
    public void ADateIsReadUnderItsColumnsDeclaredMask() =>
        ContentHarness.Interpret(Date("<Format>YYYY-MM-DD</Format>"), "2024-12-31")
            .Date.ShouldBe(new DateOnly(2024, 12, 31));

    [Fact]
    public void ADateThatDoesNotMatchItsMaskIsReportedWithTheMask()
    {
        var defect = ContentHarness.Interpret(Date(), "2024-12-31").Defect.ShouldNotBeNull();

        defect.Kind.ShouldBe(ContentDefectKind.TypeOrFormatMismatch);
        defect.Value.ShouldBe("2024-12-31");
        defect.Expected.ShouldBe("DD.MM.YYYY");
    }

    [Fact]
    public void ADateThatMatchesItsMaskButNamesNoDayIsStillReported() =>
        // The shape is right and the day is not: 31 February passes any mask-shaped check and
        // is nonetheless not a date.
        ContentHarness.Interpret(Date(), "31.02.2024").Defect.ShouldNotBeNull();

    [Fact]
    public void ATwoDigitYearBelowTheEpochBelongsToThisCentury() =>
        ContentHarness.Interpret(Date("<Format>DD.MM.YY</Format>"), "01.01.29")
            .Date.ShouldBe(new DateOnly(2029, 1, 1));

    [Fact]
    public void ATwoDigitYearAtOrAboveTheEpochBelongsToTheLast() =>
        ContentHarness.Interpret(Date("<Format>DD.MM.YY</Format>"), "01.01.30")
            .Date.ShouldBe(new DateOnly(1930, 1, 1));

    [Fact]
    public void TheDeclaredEpochMovesTheWindow() =>
        ContentHarness.Interpret(Date("<Format>DD.MM.YY</Format>", "<Epoch>50</Epoch>"), "01.01.40")
            .Date.ShouldBe(new DateOnly(2040, 1, 1));

    [Fact]
    public void AValueLongerThanTheDeclaredMaximumIsReported()
    {
        const string Bounded = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariableColumn><Name>Kurz</Name><AlphaNumeric/><MaxLength>3</MaxLength></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        ContentHarness.Interpret(Bounded, "abc").Defect.ShouldBeNull();
        var defect = ContentHarness.Interpret(Bounded, "abcd").Defect.ShouldNotBeNull();
        defect.Kind.ShouldBe(ContentDefectKind.MaxLengthExceeded);
        defect.Value.ShouldBe("abcd");
        defect.Expected.ShouldBe("3");
    }

    [Fact]
    public void AnEmptyValueIsAnAbsentValueRatherThanADefect()
    {
        // A GoBD export writes an empty field for "no value". Reporting each one would bury the
        // defects that matter under the ordinary shape of the data.
        ContentHarness.Interpret(Numeric(), string.Empty).Defect.ShouldBeNull();
        ContentHarness.Interpret(Date(), string.Empty).Defect.ShouldBeNull();
    }

    // ---- 2.6 declared redefinitions ----------------------------------------------------------

    [Fact]
    public void AMappedValueIsPresentedAsItsTargetAndStillCarriesWhatIsStored()
    {
        const string Mapped = """
                <Table>
                  <URL>belege.csv</URL>
                  <Name>Belege</Name>
                  <VariableLength>
                    <VariableColumn>
                      <Name>Art</Name>
                      <AlphaNumeric/>
                      <Map><From>R</From><To>Rechnung</To></Map>
                      <Map><From>G</From><To>Gutschrift</To></Map>
                    </VariableColumn>
                  </VariableLength>
                </Table>
        """;

        var value = ContentHarness.Interpret(Mapped, "R");

        value.Presented.ShouldBe("Rechnung");
        value.Stored.ShouldBe("R");
        ContentHarness.Interpret(Mapped, "G").Presented.ShouldBe("Gutschrift");
    }

    [Fact]
    public void AValueNoMapNamesIsPresentedAsItIs() =>
        ContentHarness.Interpret("""
                <Table>
                  <URL>belege.csv</URL>
                  <Name>Belege</Name>
                  <VariableLength>
                    <VariableColumn>
                      <Name>Art</Name>
                      <AlphaNumeric/>
                      <Map><From>R</From><To>Rechnung</To></Map>
                    </VariableColumn>
                  </VariableLength>
                </Table>
        """, "S").Presented.ShouldBe("S");
}
