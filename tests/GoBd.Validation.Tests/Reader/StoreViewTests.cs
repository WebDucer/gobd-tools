using System.Globalization;
using GoBd.Reader.Data;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Views as the store builds them: which records they hold, in what order, and what is computed
/// over them.
/// </summary>
/// <remarks>
/// The statements are held to their meaning in <see cref="ViewStatementsTests"/>. These hold the
/// store to the records that come back, because a filter that reads well and selects the wrong
/// records is the failure that matters.
/// </remarks>
public sealed class StoreViewTests
{
    private const string Tables = """
            <Table>
              <URL>t.csv</URL>
              <Name>T</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                <VariableColumn><Name>Text</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    /// <summary>Eight records, covering ties, empty values and an alphabet that needs collating.</summary>
    private const string Records =
        "A;100,00;01.01.2025;Miete Januar\r\n"
        + "B;-50,50;15.02.2025;Porto\r\n"
        + "C;;01.03.2025;miete März\r\n"
        + "D;2.000,00;;Äpfel\r\n"
        + "E;100,00;01.01.2025;Zebra\r\n"
        + "F;75,25;31.12.2024;apfel\r\n"
        + "G;100,00;01.06.2025;\r\n"
        + "H;10,00;01.01.2026;Bewirtung\r\n";

    private const int Id = 0;
    private const int Betrag = 1;
    private const int Datum = 2;
    private const int Text = 3;

    private static (ExportStore Store, TableNode Table) Open(StoreHarness harness)
    {
        var store = harness.Store();
        var table = harness.Table("T");
        store.Import(table).Conforms.ShouldBeTrue();
        return (store, table);
    }

    private static string View(ExportStore store, TableNode table, TableQuery query, int tab = 0) =>
        store.CreateView(table, query, tab, TestContext.Current.CancellationToken);

    private static IReadOnlyList<FigureResult> Figures(
        ExportStore store,
        TableNode table,
        string? view,
        params ColumnFigure[] figures) =>
        store.Figures(table, view, figures, 0, TestContext.Current.CancellationToken);

    /// <summary>The record numbers a view holds, in the order it holds them.</summary>
    private static List<long> Ordinals(ExportStore store, TableNode table, TableQuery query, int tab = 0) =>
        [.. store.PageView(table, View(store, table, query, tab), 0, 100).Select(record => record.Ordinal)];

    private static TableQuery Filtered(params ColumnFilter[] filters) => new(filters, []);

    private static TableQuery Sorted(params ColumnSort[] sorts) => new([], sorts);

    private static decimal Amount(FigureResult figure) =>
        Convert.ToDecimal(figure.Value, CultureInfo.InvariantCulture);

    private static long Whole(FigureResult figure) =>
        Convert.ToInt64(figure.Value, CultureInfo.InvariantCulture);

    // ---- which records a view holds ----------------------------------------------------------

    [Fact]
    public void ANumberFilterSelectsByWhatTheValuesDenote()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        Ordinals(store, table, Filtered(new ColumnFilter(Betrag, FilterComparison.Between, ["50", "200"])))
            .ShouldBe([1, 5, 6, 7]);
    }

    [Fact]
    public void ADateFilterSelectsByTheDatesTheMaskDescribes()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        Ordinals(store, table, Filtered(new ColumnFilter(Datum, FilterComparison.Between, ["2025-01-01", "2025-12-31"])))
            .ShouldBe([1, 2, 3, 5, 7]);
    }

    [Fact]
    public void TextIsMatchedAsTypedUnlessCaseIsToBeIgnored()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        Ordinals(store, table, Filtered(new ColumnFilter(Text, FilterComparison.Contains, ["Miete"])))
            .ShouldBe([1]);
        Ordinals(store, table, Filtered(new ColumnFilter(Text, FilterComparison.Contains, ["miete"], IgnoreCase: true)), 1)
            .ShouldBe([1, 3]);
    }

    [Fact]
    public void OneOfSeveralValuesSelectsAnyOfThem()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        Ordinals(store, table, Filtered(new ColumnFilter(Id, FilterComparison.OneOf, ["A", "F"])))
            .ShouldBe([1, 6]);
    }

    [Fact]
    public void EmptySelectsTheRecordsThatHoldNoValueThere()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        Ordinals(store, table, Filtered(new ColumnFilter(Betrag, FilterComparison.IsEmpty, []))).ShouldBe([3]);
        Ordinals(store, table, Filtered(new ColumnFilter(Datum, FilterComparison.IsEmpty, [])), 1).ShouldBe([4]);
        Ordinals(store, table, Filtered(new ColumnFilter(Text, FilterComparison.IsEmpty, [])), 2).ShouldBe([7]);
    }

    [Fact]
    public void EveryFilterMustHold()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        Ordinals(store, table, Filtered(
            new ColumnFilter(Betrag, FilterComparison.Equals, ["100.00"]),
            new ColumnFilter(Datum, FilterComparison.Equals, ["2025-01-01"])))
            .ShouldBe([1, 5]);
    }

    // ---- in what order ------------------------------------------------------------------------

    [Fact]
    public void NumbersSortAsNumbersAndEmptyValuesComeLast()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        // 2000, then the three hundreds in file order, then 75.25, 10, -50.50, and the record
        // holding no amount at all.
        Ordinals(store, table, Sorted(new ColumnSort(Betrag, Ordering.Descending)))
            .ShouldBe([4, 1, 5, 7, 6, 8, 2, 3]);
    }

    [Fact]
    public void AscendingPutsEmptyValuesLastToo()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        Ordinals(store, table, Sorted(new ColumnSort(Betrag, Ordering.Ascending)))
            .ShouldBe([2, 8, 6, 1, 5, 7, 4, 3]);
    }

    [Fact]
    public void TextSortsIntoAnAlphabetAPersonRecognises()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        var order = Ordinals(store, table, Sorted(new ColumnSort(Text, Ordering.Ascending)));

        // apfel, Äpfel, Bewirtung — not the engine's own order, which puts every capital before
        // every small letter and Ä after Z.
        order.IndexOf(6).ShouldBeLessThan(order.IndexOf(4));
        order.IndexOf(4).ShouldBeLessThan(order.IndexOf(8));
        order[^1].ShouldBe(7);
    }

    [Fact]
    public void SeveralColumnsSortInTheOrderTheyWereChosen()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        // The three hundreds, ordered among themselves by date: June first going down, then the
        // two of 1 January in file order.
        Ordinals(store, table, Sorted(
            new ColumnSort(Betrag, Ordering.Descending),
            new ColumnSort(Datum, Ordering.Descending)))
            .ShouldBe([4, 7, 1, 5, 6, 8, 2, 3]);
    }

    // ---- reading a view -----------------------------------------------------------------------

    [Fact]
    public void APageIsReadByPositionInTheViewsOwnOrder()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);
        var view = View(store, table, Sorted(new ColumnSort(Betrag, Ordering.Descending)));

        var page = store.PageView(table, view, 2, 3);

        page.Select(record => record.Ordinal).ShouldBe([5, 7, 6]);
        page[0].Values[Id].ShouldBe("E");
    }

    [Fact]
    public void ARecordTheViewHidesHasNoPositionInIt()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);
        var view = View(store, table, Filtered(new ColumnFilter(Betrag, FilterComparison.Between, ["50", "200"])));

        store.CountView(view).ShouldBe(4);
        store.PositionInView(view, 5).ShouldBe(1);
        store.PositionInView(view, 8).ShouldBe(-1);
    }

    [Fact]
    public void ATabsNextViewIsATableOfItsOwn()
    {
        // Never the same name twice, so that building the next view cannot disturb the one the
        // grid is paging. Retiring the old one is the tab's to do, once it has swapped.
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        var first = View(store, table, Filtered(new ColumnFilter(Betrag, FilterComparison.IsEmpty, [])));
        var second = View(store, table, Filtered(new ColumnFilter(Betrag, FilterComparison.IsNotEmpty, [])));

        second.ShouldNotBe(first);
        store.CountView(first).ShouldBe(1);
        store.CountView(second).ShouldBe(7);
    }

    [Fact]
    public void AViewIsGoneOnceItIsDropped()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);
        var view = View(store, table, TableQuery.FileOrder);

        store.DropView(view, 0);

        Should.Throw<Exception>(() => store.CountView(view));
    }

    [Fact]
    public void AViewNameTheStoreDidNotMakeIsRefused()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, _) = Open(harness);

        Should.Throw<ArgumentException>(() => store.CountView("v_0; DROP TABLE t_0"));
    }

    // ---- what is computed over it --------------------------------------------------------------

    [Fact]
    public void FiguresAreComputedOverTheRecordsTheViewHolds()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);
        var view = View(store, table, Filtered(new ColumnFilter(Betrag, FilterComparison.Between, ["50", "200"])));

        var figures = Figures(store, table, view, new ColumnFigure(Betrag, Figure.Sum));

        Amount(figures[0]).ShouldBe(375.25m);
        figures[0].Missing.ShouldBe(0);
    }

    [Fact]
    public void ASumIsExactAndSaysHowManyValuesItHadNothingToRead()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        var figures = Figures(
            store,
            table,
            null,
            new ColumnFigure(Betrag, Figure.Sum),
            new ColumnFigure(Betrag, Figure.Minimum),
            new ColumnFigure(Betrag, Figure.Maximum),
            new ColumnFigure(Betrag, Figure.DistinctCount),
            new ColumnFigure(Text, Figure.Count));

        Amount(figures[0]).ShouldBe(2334.75m);
        figures[0].Missing.ShouldBe(1);
        Amount(figures[1]).ShouldBe(-50.50m);
        Amount(figures[2]).ShouldBe(2000m);
        Whole(figures[3]).ShouldBe(5);
        Whole(figures[4]).ShouldBe(7);
        figures[4].Missing.ShouldBe(1);
    }

    [Fact]
    public void AnAverageComesBackAsItsSumAndItsCount()
    {
        // So that the division and the rounding happen where they can be stated as rounded.
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        var figures = Figures(store, table, null, new ColumnFigure(Betrag, Figure.Average));

        Amount(figures[0]).ShouldBe(2334.75m);
        figures[0].Counted.ShouldBe(7);
        figures[0].Missing.ShouldBe(1);
    }

    // ---- marks on a page whose record numbers are scattered ------------------------------------

    [Fact]
    public void ReferencesAreCheckedForThePagesOwnRecordsHoweverTheyAreOrdered()
    {
        // A sorted or filtered page draws its records from anywhere in the table, so the range a
        // page in file order occupies says nothing about it. Asked of the page's own record
        // numbers, the answer is the same claim the key check makes.
        const string Related = """
                <Table>
                  <URL>k.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>b.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>KundenCode</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>KundenCode</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Related,
            ("k.csv", "K1\r\nK2\r\n"),
            ("b.csv", "B1;K1\r\nB2;K9\r\nB3;K2\r\nB4;K8\r\n"));

        var store = harness.Store();
        var kunden = harness.Table("Kunden");
        var bestellungen = harness.Table("Bestellungen");
        store.Import(kunden).Conforms.ShouldBeTrue();
        store.Import(bestellungen).Conforms.ShouldBeTrue();

        IReadOnlyList<long> Among(params long[] ordinals) =>
            store.UnresolvedAmong(bestellungen, [1], kunden, [0], ordinals);

        Among(2, 4).ShouldBe([2, 4]);
        Among(1, 3).ShouldBeEmpty();
        Among(4, 1).ShouldBe([4]);
        Among().ShouldBeEmpty();

        // The same records the range asks about, when the page happens to be one.
        store.UnresolvedBetween(bestellungen, [1], kunden, [0], 1, 4).ShouldBe([2, 4]);
    }

    [Fact]
    public void ValuesThatDenoteTheSameNumberCountAsOne()
    {
        // 1,5 and 1,50 are one number written two ways. Counted as text they would be two, which
        // would make a distinct count of amounts depend on how the exporting system padded them.
        using var harness = StoreHarness.Create(
            Tables,
            ("t.csv", "A;1,5;01.01.2025;x\r\nB;1,50;01.01.2025;y\r\nC;1,05;01.01.2025;z\r\n"));
        var (store, table) = Open(harness);

        Whole(Figures(store, table, null, new ColumnFigure(Betrag, Figure.DistinctCount))[0]).ShouldBe(2);
    }

    [Fact]
    public void NoViewIsLeftBehindOnceItsTabIsDone()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", Records));
        var (store, table) = Open(harness);

        // Every view a tab is given is one it must give back: a store that kept them would carry a
        // copy of the table per filter anyone had ever tried. Dropping one takes that one and
        // leaves the rest, which is what lets a tab retire the view it replaced.
        var first = View(store, table, Sorted(new ColumnSort(Betrag, Ordering.Descending)));
        var replacing = View(store, table, Filtered(new ColumnFilter(Betrag, FilterComparison.IsNotEmpty, [])));
        var second = View(store, table, Sorted(new ColumnSort(Datum, Ordering.Ascending)), 1);

        Views(store).Count.ShouldBe(3);

        store.DropView(first, 0);
        Views(store).Count.ShouldBe(2);

        store.DropView(replacing, 0);
        store.DropView(second, 1);

        Views(store).ShouldBeEmpty();
    }

    /// <summary>The view tables the store is holding.</summary>
    private static IReadOnlyList<string> Views(ExportStore store)
    {
        var tables = new List<string>();
        store.Query("SELECT table_name FROM duckdb_tables()", row => tables.Add(row.GetString(0)));
        return [.. tables.Where(name => name.Contains("_v", StringComparison.Ordinal))];
    }

    [Fact]
    public void ASumTooLargeToComputeExactlySaysSoAndLeavesTheOtherFiguresExact()
    {
        // Each value fits what the reader holds exactly; their total does not. An approximation
        // offered as a total would be a guess presented as a fact, so that figure says it cannot
        // be computed — and the figures asked alongside it are still answered.
        const string Wide = """
                <Table>
                  <URL>w.csv</URL>
                  <Name>W</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Betrag</Name><Numeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        const string Widest = "99999999999999999999999999999999999999";
        using var harness = StoreHarness.Create(Wide, ("w.csv", $"A;{Widest}\r\nB;{Widest}\r\n"));
        var store = harness.Store();
        var table = harness.Table("W");
        store.Import(table).Conforms.ShouldBeTrue();

        var figures = Figures(
            store,
            table,
            null,
            new ColumnFigure(1, Figure.Sum),
            new ColumnFigure(1, Figure.Count));

        figures[0].Exact.ShouldBeFalse();
        figures[0].Value.ShouldBeNull();
        figures[1].Exact.ShouldBeTrue();
        Whole(figures[1]).ShouldBe(2);
    }
}
