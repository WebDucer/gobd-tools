using System.Globalization;
using GoBd.Reader.Data;
using GoBd.Validation.Content;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// What a table's columns can be queried as, and the values the query view reads them as.
/// </summary>
/// <remarks>
/// The store holds text, because a value that fails its declaration still has to be quoted and
/// the grid shows what the file stores. Filtering, sorting and totalling need the values the
/// declaration says those characters mean, so the view states them — measured once, when the
/// table is imported. See the change's design.md D1, D2 and D3.
/// </remarks>
public sealed class StoreQueryViewTests
{
    private static string Table(string columns, string extra = "") => $"""
            <Table>
              <URL>t.csv</URL>
              <Name>T</Name>
            {extra}
              <VariableLength>
                <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
        {columns}
              </VariableLength>
            </Table>
    """;

    private static (ExportStore Store, TableNode Table, TableCapabilities Capabilities) Import(
        StoreHarness harness)
    {
        var store = harness.Store();
        var table = harness.Table("T");
        store.Import(table);
        return (store, table, store.Capabilities(table).ShouldNotBeNull());
    }

    /// <summary>One value of the query view, for the record with that number.</summary>
    private static object? Value(ExportStore store, TableNode table, string expression, long ordinal)
    {
        object? value = null;
        store.Query(
            $"SELECT {expression} FROM {QueryView.NameOf(store.NameOf(table))}"
            + $" WHERE {TableExtraction.OrdinalColumn} = {ordinal.ToString(CultureInfo.InvariantCulture)}",
            row => value = row.IsDBNull(0) ? null : row.GetValue(0));

        return value;
    }

    /// <summary>One value as text.</summary>
    private static string? Read(ExportStore store, TableNode table, string expression, long ordinal) =>
        Convert.ToString(Value(store, table, expression, ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// One value as the number it denotes.
    /// </summary>
    /// <remarks>
    /// Compared as a number rather than as text: how many trailing zeros a store writes back is
    /// its own business, and this is about what the value means.
    /// </remarks>
    private static decimal? Number(ExportStore store, TableNode table, int column, long ordinal) =>
        Value(store, table, QueryView.Column(column), ordinal) is { } value
            ? Convert.ToDecimal(value, CultureInfo.InvariantCulture)
            : null;

    private static string Column(int index) => QueryView.Column(index);

    // ---- 2.1 what each column can be queried as ----------------------------------------------

    [Fact]
    public void AColumnDeclaringNoAccuracyIsReadAtTheDecimalsItsValuesCarry()
    {
        // The standard's default accuracy is zero, and the validator does not hold a column
        // without one to it. Reading 3,456 at scale zero would round a value the file states
        // exactly, so the scale is measured instead.
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric/></VariableColumn>"),
            ("t.csv", "A;12,50\r\nB;3,456\r\n"));

        var (store, table, capabilities) = Import(harness);

        capabilities[1].Kind.ShouldBe(ColumnQueryKind.Number);
        capabilities[1].Scale.ShouldBe(3);
        Number(store, table, 1, 2).ShouldBe(3.456m);
    }

    [Fact]
    public void ADeclaredAccuracyIsTheScale()
    {
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>"),
            ("t.csv", "A;1.234,56\r\n"));

        var (store, table, capabilities) = Import(harness);

        capabilities[1].Scale.ShouldBe(2);
        Number(store, table, 1, 1).ShouldBe(1234.56m);
    }

    [Fact]
    public void AColumnHoldingANumberTooWideForTheReaderIsTurnedOffWhole()
    {
        // 39 digits is a number the standard permits and this reader cannot hold exactly. Dropping
        // the value alone would leave a sum quietly short of it, so the column offers nothing —
        // and still shows every value it holds.
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric/></VariableColumn>"),
            ("t.csv", "A;123456789012345678901234567890123456789\r\nB;12,50\r\n"));

        var (store, table, capabilities) = Import(harness);

        capabilities[1].Limitation.ShouldBe(ColumnLimitation.NumberTooLarge);
        capabilities[1].IsQueryable.ShouldBeFalse();
        capabilities.HasLimitations.ShouldBeTrue();

        // The value is still there to read, as the file wrote it.
        Read(store, table, ExportStore.Column(1), 1).ShouldBe("123456789012345678901234567890123456789");
    }

    [Fact]
    public void ATableOfNoRecordsIsMeasuredAndGetsItsViewAllTheSame()
    {
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>"),
            ("t.csv", string.Empty));

        var (store, table, capabilities) = Import(harness);

        capabilities[1].IsQueryable.ShouldBeTrue();

        var records = 0L;
        store.Query(
            "SELECT count(*) FROM " + QueryView.NameOf(store.NameOf(table)),
            row => records = row.GetInt64(0));
        records.ShouldBe(0);
    }

    // ---- 2.2 the value the declaration says --------------------------------------------------

    [Theory]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("-42,50", "-42.50")]
    [InlineData("1782,90-", "-1782.90")]
    [InlineData("1782,90+", "1782.90")]
    [InlineData("", null)]
    public void ANumberIsReadUnderTheDeclaredSymbolsAndSign(string value, string? expected)
    {
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>"),
            ("t.csv", $"A;{value}\r\n"));

        var (store, table, _) = Import(harness);

        Number(store, table, 1, 1).ShouldBe(
            expected is null ? null : decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ImpliedDecimalsArePlacedRatherThanDividedIn()
    {
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric><ImpliedAccuracy>3</ImpliedAccuracy></Numeric></VariableColumn>"),
            ("t.csv", "A;100\r\nB;6587890\r\nC;12-\r\n"));

        var (store, table, capabilities) = Import(harness);

        capabilities[1].Scale.ShouldBe(3);
        Number(store, table, 1, 1).ShouldBe(0.1m);
        Number(store, table, 1, 2).ShouldBe(6587.89m);
        Number(store, table, 1, 3).ShouldBe(-0.012m);
    }

    [Fact]
    public void ImpliedDecimalsAreCountedOnTopOfTheOnesAValueWrites()
    {
        // A value that also writes decimals of its own carries both: the declaration says where
        // the point belongs relative to the digits, so the implied places move it further left.
        // The written decimal symbol has to become a point before any of that, or the view is
        // built out of a comma and the engines disagree about what the file says.
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric><ImpliedAccuracy>2</ImpliedAccuracy></Numeric></VariableColumn>"),
            ("t.csv", "A;12,34\r\nB;1.234,5\r\n"));

        var (store, table, capabilities) = Import(harness);
        var layout = RecordLayout.For(table);

        capabilities[1].Scale.ShouldBe(4);
        Number(store, table, 1, 1).ShouldBe(0.1234m);
        Number(store, table, 1, 2).ShouldBe(12.345m);

        // The same numbers the streaming engine reads, which divides what it parsed by the
        // declared power of ten.
        ValueInterpreter.Interpret("12,34", layout.Columns[1], layout).Number.ShouldBe(0.1234m);
        ValueInterpreter.Interpret("1.234,5", layout.Columns[1], layout).Number.ShouldBe(12.345m);
    }

    [Fact]
    public void ATwoDigitYearTakesItsCenturyFromTheDeclaredEpoch()
    {
        // The store's own two-digit-year pivot is not the declared one. Under an epoch of 30, a
        // year of 35 belongs to the twentieth century, and the streaming engine reads it so.
        const string Epoch = "  <Epoch>30</Epoch>";
        using var harness = StoreHarness.Create(
            Table(
                "            <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YY</Format></Date></VariableColumn>",
                Epoch),
            ("t.csv", "A;01.02.35\r\nB;01.02.05\r\n"));

        var (store, table, capabilities) = Import(harness);
        capabilities[1].Kind.ShouldBe(ColumnQueryKind.Date);

        Read(store, table, $"strftime({Column(1)}, '%Y-%m-%d')", 1).ShouldBe("1935-02-01");
        Read(store, table, $"strftime({Column(1)}, '%Y-%m-%d')", 2).ShouldBe("2005-02-01");

        // The same century the engine that produces the findings reads.
        var layout = RecordLayout.For(table);
        ValueInterpreter.Interpret("01.02.35", layout.Columns[1], layout).Date.ShouldBe(new DateOnly(1935, 2, 1));
    }

    [Fact]
    public void ATimeColumnIsReadUnderItsDeclaredMaskAndCountsWhatIsNotATime()
    {
        // An alphanumeric column whose Map names the same time mask on both sides declares a Time
        // column. Nothing checks its values, so the ones that are not times are counted and
        // treated as absent.
        using var harness = StoreHarness.Create(
            Table("""
                            <VariableColumn>
                              <Name>Zeit</Name><AlphaNumeric/>
                              <Map><From>HHMMSS</From><To>HHMMSS</To></Map>
                            </VariableColumn>
            """),
            ("t.csv", "A;081500\r\nB;keine Zeit\r\nC;235959\r\n"));

        var (store, table, capabilities) = Import(harness);

        capabilities[1].Kind.ShouldBe(ColumnQueryKind.Time);
        capabilities[1].ValuesNotTimes.ShouldBe(1);
        Read(store, table, $"CAST({Column(1)} AS VARCHAR)", 1).ShouldBe("08:15:00");
        Value(store, table, Column(1), 2).ShouldBeNull();
        Read(store, table, $"CAST({Column(1)} AS VARCHAR)", 3).ShouldBe("23:59:59");
    }

    [Fact]
    public void TextIsReadAsTheGridShowsIt()
    {
        // A declared Map redefines a value, and a person filters what they can see.
        using var harness = StoreHarness.Create(
            Table("""
                            <VariableColumn>
                              <Name>Status</Name><AlphaNumeric/>
                              <Map><From>S</From><To>Storniert</To></Map>
                            </VariableColumn>
            """),
            ("t.csv", "A;S\r\nB;offen\r\n"));

        var (store, table, capabilities) = Import(harness);

        capabilities[1].Kind.ShouldBe(ColumnQueryKind.Text);
        Read(store, table, Column(1), 1).ShouldBe("Storniert");
        Read(store, table, Column(1), 2).ShouldBe("offen");
        Read(store, table, ExportStore.Column(1), 1).ShouldBe("S");
    }

    // ---- 2.3 a table that is withheld has no view --------------------------------------------

    [Fact]
    public void ATableThatDoesNotConformIsNeitherMeasuredNorGivenAView()
    {
        // Nothing can filter a table whose data is withheld, and measuring one would read values
        // the reader has already declared unreadable.
        using var harness = StoreHarness.Create(
            Table("            <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>"),
            ("t.csv", "A;keine Zahl\r\n"));

        var store = harness.Store();
        var table = harness.Table("T");
        store.Import(table).Conforms.ShouldBeFalse();

        store.Capabilities(table).ShouldBeNull();
    }
}
