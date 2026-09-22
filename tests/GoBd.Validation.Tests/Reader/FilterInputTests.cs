using GoBd.Reader.Data;
using GoBd.Validation.Content;
using GoBd.Validation.Tests.Content;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Reading what a person types into a filter, under the declaration the grid shows values by.
/// </summary>
/// <remarks>
/// The same bytes mean different numbers under different declarations, and the reader must agree
/// with the file rather than with the machine it happens to run on. What cannot be read is
/// refused with the form expected, because a bound quietly dropped would show records nobody
/// asked for.
/// </remarks>
public sealed class FilterInputTests
{
    private static string Table(string columns, string symbols = "", string epoch = "") => $"""
            <Table>
              <URL>t.csv</URL>
              <Name>T</Name>
              {symbols}
              {epoch}
              <VariableLength>
        {columns}
              </VariableLength>
            </Table>
    """;

    private const string German = "<DecimalSymbol>,</DecimalSymbol><DigitGroupingSymbol>.</DigitGroupingSymbol>";
    private const string English = "<DecimalSymbol>.</DecimalSymbol><DigitGroupingSymbol>,</DigitGroupingSymbol>";

    private const string Amount =
        "            <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>";

    private static FilterReading Read(string tableXml, ColumnQueryKind kind, string typed, int scale = 2)
    {
        var layout = ContentHarness.Layout(tableXml);
        var capability = new ColumnCapability(0, kind, scale, 0, ColumnLimitation.None);
        return FilterInput.For(capability, layout.Columns[0], layout, typed);
    }

    // ---- numbers -------------------------------------------------------------------------------

    [Theory]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("-42,5", "-42.5")]
    [InlineData("1782,90-", "-1782.90")]
    [InlineData("100", "100")]
    public void ANumberIsReadUnderTheTablesDeclaredSymbols(string typed, string expected)
    {
        var reading = Read(Table(Amount, German), ColumnQueryKind.Number, typed);

        reading.Read.ShouldBeTrue();
        reading.Value.ShouldBe(expected);
    }

    [Fact]
    public void TheSameCharactersMeanDifferentNumbersUnderDifferentDeclarations()
    {
        Read(Table(Amount, German), ColumnQueryKind.Number, "1.234").Value.ShouldBe("1234");
        Read(Table(Amount, English), ColumnQueryKind.Number, "1.234").Value.ShouldBe("1.234");
    }

    [Fact]
    public void WhatIsNotANumberIsRefusedWithTheFormExpected()
    {
        var reading = Read(Table(Amount, German), ColumnQueryKind.Number, "ungefähr 100");

        reading.Read.ShouldBeFalse();
        reading.Value.ShouldBeEmpty();
        reading.Expected.ShouldContain("','");
    }

    [Fact]
    public void ANumberOfMoreDigitsThanADecimalHoldsSurvivesBeingRead()
    {
        // The column may hold it, so a bound must be able to name it.
        const string Huge = "123456789012345678901234567890123456";

        Read(Table(Amount, German), ColumnQueryKind.Number, Huge).Value.ShouldBe(Huge);
    }

    // ---- dates and times -------------------------------------------------------------------------

    [Fact]
    public void ADateIsReadUnderItsMaskAndTheTablesYearWindow()
    {
        const string Datum =
            "            <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YY</Format></Date></VariableColumn>";

        var reading = Read(Table(Datum, German, "<Epoch>30</Epoch>"), ColumnQueryKind.Date, "01.02.35");

        reading.Value.ShouldBe("1935-02-01");
    }

    [Fact]
    public void ADateThatDoesNotMatchItsMaskIsRefusedWithTheMask()
    {
        const string Datum =
            "            <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>";

        var reading = Read(Table(Datum, German), ColumnQueryKind.Date, "2025-02-01");

        reading.Read.ShouldBeFalse();
        reading.Expected.ShouldBe("DD.MM.YYYY");
    }

    [Theory]
    [InlineData("HHMMSS", "081500", "08:15:00")]
    [InlineData("HH:MM", "23:59", "23:59:00")]
    [InlineData("HH:MM TT", "01:05 PM", "13:05:00")]
    [InlineData("HH:MM TT", "12:30 AM", "00:30:00")]
    public void ATimeIsReadUnderTheMaskItsColumnDeclares(string mask, string typed, string expected)
    {
        var column = $"""
                    <VariableColumn>
                      <Name>Zeit</Name><AlphaNumeric/>
                      <Map><From>{mask}</From><To>{mask}</To></Map>
                    </VariableColumn>
        """;

        var reading = Read(Table(column, German), ColumnQueryKind.Time, typed);

        reading.Read.ShouldBeTrue();
        reading.Value.ShouldBe(expected);
    }

    [Fact]
    public void WhatIsNotATimeIsRefusedWithTheMask()
    {
        const string Zeit = """
                    <VariableColumn>
                      <Name>Zeit</Name><AlphaNumeric/>
                      <Map><From>HHMMSS</From><To>HHMMSS</To></Map>
                    </VariableColumn>
        """;

        var reading = Read(Table(Zeit, German), ColumnQueryKind.Time, "kurz nach acht");

        reading.Read.ShouldBeFalse();
        reading.Expected.ShouldBe("HHMMSS");
    }

    // ---- text and open bounds ----------------------------------------------------------------------

    [Fact]
    public void TextIsTakenAsItWasTyped()
    {
        const string Text = "            <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>";

        Read(Table(Text, German), ColumnQueryKind.Text, "Meier & Söhne").Value.ShouldBe("Meier & Söhne");
    }

    [Fact]
    public void NothingTypedIsABoundLeftOpenRatherThanAValueThatFailedToRead()
    {
        var reading = Read(Table(Amount, German), ColumnQueryKind.Number, string.Empty);

        reading.Read.ShouldBeTrue();
        reading.Value.ShouldBeEmpty();
    }
}
