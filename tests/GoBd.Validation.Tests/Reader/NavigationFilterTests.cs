using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Following a reference into a table a person has filtered.
/// </summary>
/// <remarks>
/// A filter is something a person set; the record a reference names is something the export says.
/// When the two disagree the filter gives way, because a navigation that landed anywhere but the
/// record it names would be wrong. The sort stays: it hides nothing.
/// </remarks>
public sealed class NavigationFilterTests
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
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private const int Code = 0;
    private const int Kunde = 1;

    private static StoreHarness Export() => StoreHarness.Create(
        Shop,
        ("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK3;Wolf\r\n"),
        ("bestellungen.csv", "N1;K1\r\nN2;K2\r\nN3;K1\r\n"));

    private static ReaderSession Read(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    private static TableNode Table(ReaderSession session, string identity) =>
        session.DataSet.Tables.First(table => string.Equals(table.Identity, identity, StringComparison.Ordinal));

    /// <summary>Puts a filter on a table's tab, as a person would.</summary>
    private static async Task<TableTab> FilterAsync(
        ReaderSession session,
        ReaderTabs tabs,
        ViewPreparation preparation,
        TableNode table,
        ColumnFilter filter)
    {
        var tab = tabs.Open(table);
        var query = new TableQuery([filter], []);
        var prepared = await preparation.PrepareAsync(table, query, tab.View);

        tab.Query = query;
        tab.View = prepared.Rows.ShouldNotBeNull().View;
        tab.Rows = prepared.Rows.Records;
        return tab;
    }

    [Fact]
    public async Task AFilterThatHidesTheRecordAReferenceNamesIsLifted()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        using var preparation = new ViewPreparation(session, 0, TimeSpan.Zero);

        // The customers tab shows only K2, and the order being followed belongs to K1.
        var customerTab = await FilterAsync(
            session,
            tabs,
            preparation,
            customers,
            new ColumnFilter(Code, FilterComparison.Equals, ["K2"]));
        customerTab.Rows.ShouldNotBeNull().Count.ShouldBe(1);

        var key = session.KeysAt(orders, Kunde).ShouldHaveSingleItem();
        var navigation = session.Follow(orders, 1, key);

        var landed = (await tabs.ApplyAsync(navigation, null, preparation)).ShouldNotBeNull();

        landed.Query.Filters.ShouldBeEmpty();
        landed.Rows.ShouldNotBeNull().Count.ShouldBe(3);
        landed.Rows[landed.Position].Values[Code].ShouldBe("K1");
        landed.Notice.ShouldContain("Filter removed");
    }

    [Fact]
    public async Task AReferenceFollowedIntoASortedTabLandsOnTheRecordItNames()
    {
        // A sort hides nothing, so there is no filter to lift — but it renumbers every position,
        // and the index the session worked out against the file's order names a different record
        // in the view. So the view is asked where the record sits, sort or no sort.
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        using var preparation = new ViewPreparation(session, 0, TimeSpan.Zero);

        // By code descending the customers run K3, K2, K1: the record the reference names is last
        // here and first in the file.
        var tab = tabs.Open(customers);
        var query = new TableQuery([], [new ColumnSort(Code, Ordering.Descending)]);
        var prepared = await preparation.PrepareAsync(customers, query, tab.View);
        tab.Query = query;
        tab.View = prepared.Rows.ShouldNotBeNull().View;
        tab.Rows = prepared.Rows.Records;

        var key = session.KeysAt(orders, Kunde).ShouldHaveSingleItem();
        var landed = (await tabs.ApplyAsync(session.Follow(orders, 1, key), null, preparation)).ShouldNotBeNull();

        landed.Query.Sorts.ShouldHaveSingleItem();
        landed.Position.ShouldBe(2);
        landed.Rows.ShouldNotBeNull()[landed.Position].Values[Code].ShouldBe("K1");
    }

    [Fact]
    public async Task WhatIsSaidAboutALiftedFilterStopsBeingSaid()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        using var preparation = new ViewPreparation(session, 0, TimeSpan.Zero);

        await FilterAsync(session, tabs, preparation, customers, new ColumnFilter(Code, FilterComparison.Equals, ["K2"]));
        var key = session.KeysAt(orders, Kunde).ShouldHaveSingleItem();
        var landed = (await tabs.ApplyAsync(session.Follow(orders, 1, key), null, preparation)).ShouldNotBeNull();

        var now = DateTimeOffset.UtcNow;
        landed.NoticeAt(now).ShouldContain("Filter removed");
        landed.NoticeAt(now + TableTab.NoticeLife + TimeSpan.FromSeconds(1)).ShouldBeEmpty();

        // What the tab is showing is said by the banner, which does not expire.
        landed.Query.IsFileOrder.ShouldBeTrue();
    }

    [Fact]
    public async Task AFilterThatShowsTheRecordIsLeftAsItWas()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        using var preparation = new ViewPreparation(session, 0, TimeSpan.Zero);

        var customerTab = await FilterAsync(
            session,
            tabs,
            preparation,
            customers,
            new ColumnFilter(Code, FilterComparison.Equals, ["K1"]));
        var view = customerTab.View;

        var key = session.KeysAt(orders, Kunde).ShouldHaveSingleItem();
        var landed = (await tabs.ApplyAsync(session.Follow(orders, 1, key), null, preparation)).ShouldNotBeNull();

        landed.Query.Filters.ShouldHaveSingleItem();
        landed.View.ShouldBe(view);
        landed.Rows.ShouldNotBeNull().Count.ShouldBe(1);
        landed.Rows[landed.Position].Values[Code].ShouldBe("K1");
        landed.Notice.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheSortSurvivesAFilterBeingLifted()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        using var preparation = new ViewPreparation(session, 0, TimeSpan.Zero);

        var tab = tabs.Open(customers);
        var query = new TableQuery(
            [new ColumnFilter(Code, FilterComparison.Equals, ["K3"])],
            [new ColumnSort(Code, Ordering.Descending)]);
        var prepared = await preparation.PrepareAsync(customers, query, null);
        tab.Query = query;
        tab.View = prepared.Rows.ShouldNotBeNull().View;
        tab.Rows = prepared.Rows.Records;

        var key = session.KeysAt(orders, Kunde).ShouldHaveSingleItem();
        var landed = (await tabs.ApplyAsync(session.Follow(orders, 1, key), null, preparation)).ShouldNotBeNull();

        landed.Query.Filters.ShouldBeEmpty();
        landed.Query.Sorts.ShouldHaveSingleItem().Direction.ShouldBe(Ordering.Descending);

        // Still sorted: K3, K2, K1 — and positioned on the K1 the reference named.
        landed.Rows.ShouldNotBeNull()[0].Values[Code].ShouldBe("K3");
        landed.Rows[landed.Position].Values[Code].ShouldBe("K1");
    }

    [Fact]
    public async Task SteppingBackwardsIntoAHiddenReferringRecordLiftsTheFilterToo()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        using var preparation = new ViewPreparation(session, 0, TimeSpan.Zero);

        // The orders tab shows only K2's order; stepping back from K1 names one it hides.
        await FilterAsync(session, tabs, preparation, orders, new ColumnFilter(Kunde, FilterComparison.Equals, ["K2"]));

        var key = session.KeysAt(orders, Kunde).ShouldHaveSingleItem();
        var navigation = session.FollowBack(customers, 1, key);
        var walk = new Walk(key, customers, 1, navigation.MatchIndex, navigation.Matches);

        var landed = (await tabs.ApplyAsync(navigation, walk, preparation)).ShouldNotBeNull();

        landed.Query.Filters.ShouldBeEmpty();
        landed.Walk.ShouldNotBeNull().Matches.ShouldBe(2);
        landed.Rows.ShouldNotBeNull()[landed.Position].Values[Kunde].ShouldBe("K1");
    }
}
