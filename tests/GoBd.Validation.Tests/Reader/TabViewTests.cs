using System.Globalization;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// A tab's view of its table: what it holds, what it keeps, what it takes with it when it closes,
/// and what the reader makes of the figures asked over it.
/// </summary>
/// <remarks>
/// A filter is part of where a person is in a table, like the record they are positioned at. It
/// therefore lives with the tab and not beside it, which is what keeps one table's filter from
/// changing what another table shows.
/// </remarks>
public sealed class TabViewTests
{
    private const string Shop = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private const int Kunde = 1;
    private const int Betrag = 2;

    private static StoreHarness Export() => StoreHarness.Create(
        Shop,
        ("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK3;Wolf\r\n"),
        ("bestellungen.csv", "N1;K1;10,00\r\nN2;K2;20,00\r\nN3;K1;25,00\r\n"));

    private static ReaderSession Read(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    private static TableNode Table(ReaderSession session, string identity) =>
        session.DataSet.Tables.First(table => string.Equals(table.Identity, identity, StringComparison.Ordinal));

    /// <summary>Filters a tab's table to the orders of one customer, as the reader will.</summary>
    private static TableTab Filter(ReaderSession session, ReaderTabs tabs, TableNode table, string customer)
    {
        var tab = tabs.Open(table);
        var query = new TableQuery([new ColumnFilter(Kunde, FilterComparison.Equals, [customer])], []);
        var rows = session.Build(table, query, tabs.Tables.Count, TestContext.Current.CancellationToken);

        tab.Query = query;
        tab.View = rows.View;
        tab.Rows = rows.Records;
        return tab;
    }

    // ---- a view belongs to its tab -------------------------------------------------------------

    [Fact]
    public void AFilteredTableIsStillFilteredWhenItIsComeBackTo()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");

        var filtered = Filter(session, tabs, orders, "K1");
        filtered.Rows.ShouldNotBeNull().Count.ShouldBe(2);

        tabs.Open(Table(session, "Kunden"));
        tabs.Open(orders);

        var returned = tabs.For(orders).ShouldNotBeNull();
        returned.Query.Filters.ShouldHaveSingleItem().Values.ShouldBe(["K1"]);
        returned.View.ShouldBe(filtered.View);
        returned.Rows.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public void ClosingATabTakesItsViewWithIt()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");

        var view = Filter(session, tabs, orders, "K1").View.ShouldNotBeNull();
        tabs.Close(orders);

        // The view's records are a copy of a table nobody is looking at any more.
        Should.Throw<Exception>(() => session.Store.CountView(view));

        var reopened = tabs.Open(orders);
        reopened.Query.IsFileOrder.ShouldBeTrue();
        reopened.View.ShouldBeNull();
        reopened.Rows.ShouldBeNull();
    }

    [Fact]
    public void OneTablesFilterLeavesAnotherTableAsItWas()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");
        var customers = Table(session, "Kunden");

        var untouched = tabs.Open(customers);
        Filter(session, tabs, orders, "K1");

        untouched.Query.IsFileOrder.ShouldBeTrue();
        untouched.View.ShouldBeNull();
        tabs.For(customers).ShouldNotBeNull().Query.IsFileOrder.ShouldBeTrue();
    }

    [Fact]
    public void ATableInFileOrderBuildsNoView()
    {
        // An export nobody filters costs nothing: there is no second copy of anything.
        using var harness = Export();
        using var session = Read(harness);
        var orders = Table(session, "Bestellungen");

        var rows = session.Build(orders, TableQuery.FileOrder, 0, TestContext.Current.CancellationToken);

        rows.View.ShouldBeNull();
        rows.Count.ShouldBe(3);
        rows.Records.Count.ShouldBe(3);
    }

    // ---- the figures the reader states ---------------------------------------------------------

    [Fact]
    public void AnAverageIsRoundedToTheColumnsDecimalsAndSaysSo()
    {
        // 55,00 over three orders is 18,3333…, which no number of decimal places states exactly.
        // Shown at the column's own accuracy and marked as rounded, rather than offered as though
        // it were the figure itself.
        using var harness = Export();
        using var session = Read(harness);
        var orders = Table(session, "Bestellungen");

        var reading = session.Figures(
            orders,
            null,
            [new ColumnFigure(Betrag, Figure.Average)],
            0,
            TestContext.Current.CancellationToken).ShouldHaveSingleItem();

        Convert.ToDecimal(reading.Value, CultureInfo.InvariantCulture).ShouldBe(18.33m);
        reading.Rounded.ShouldBeTrue();
        reading.Exact.ShouldBeTrue();
        reading.Missing.ShouldBe(0);
    }

    [Fact]
    public void AnAverageThatComesOutExactlyIsNotMarkedAsRounded()
    {
        using var harness = StoreHarness.Create(
            Shop,
            ("kunden.csv", "K1;Meier\r\n"),
            ("bestellungen.csv", "N1;K1;10,00\r\nN2;K1;20,00\r\n"));
        using var session = Read(harness);
        var orders = Table(session, "Bestellungen");

        var reading = session.Figures(
            orders,
            null,
            [new ColumnFigure(Betrag, Figure.Average)],
            0,
            TestContext.Current.CancellationToken).ShouldHaveSingleItem();

        Convert.ToDecimal(reading.Value, CultureInfo.InvariantCulture).ShouldBe(15m);
        reading.Rounded.ShouldBeFalse();
    }

    [Fact]
    public void FiguresFollowTheViewRatherThanTheTable()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");

        var tab = Filter(session, tabs, orders, "K1");

        var whole = session.Figures(
            orders,
            null,
            [new ColumnFigure(Betrag, Figure.Sum)],
            tab.Lane,
            TestContext.Current.CancellationToken);
        var filtered = session.Figures(
            orders,
            tab.View,
            [new ColumnFigure(Betrag, Figure.Sum)],
            tab.Lane,
            TestContext.Current.CancellationToken);

        Convert.ToDecimal(whole[0].Value, CultureInfo.InvariantCulture).ShouldBe(55m);
        Convert.ToDecimal(filtered[0].Value, CultureInfo.InvariantCulture).ShouldBe(35m);
    }
}
