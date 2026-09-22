using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// One tab per table, and everything that belongs to a table living in that table's tab.
/// </summary>
/// <remarks>
/// The stale Previous/Next bar, the status message about another table and the navigator
/// selection that no longer matched what was shown were all one fault: state that belongs to a
/// table was shared between them. These hold the structure that fixes it.
/// </remarks>
public sealed class ReaderTabsTests
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
                    <VariableColumn><Name>ProductLine</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private static StoreHarness Export() => StoreHarness.Create(
        Shop,
        ("kunden.csv", "K1;Meier\r\nK2;Lang\r\nK3;Wolf\r\n"),
        ("bestellungen.csv", "N1;K1;\r\nN2;K2;Kaffee\r\nN3;K1;\r\n"));

    private static ReaderSession Read(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    /// <summary>
    /// The session's own node for a table.
    /// </summary>
    /// <remarks>
    /// The harness parsed the description once and the session parsed it again, so the two hold
    /// different nodes for the same table. Relationships are resolved by reference within one
    /// description, which is what makes two tables sharing a name distinguishable.
    /// </remarks>
    private static TableNode Table(ReaderSession session, string identity) =>
        session.DataSet.Tables.First(table =>
            string.Equals(table.Identity, identity, StringComparison.Ordinal));

    // ---- 5.1 one tab per table -----------------------------------------------------------------

    [Fact]
    public void TenNavigationsAcrossTwoTablesLeaveExactlyTwoTabs()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");
        var key = session.KeysAt(orders, 1).ShouldHaveSingleItem();

        for (var round = 0; round < 5; round++)
        {
            tabs.Open(orders);
            tabs.Apply(session.Follow(orders, (round % 3) + 1, key));
        }

        tabs.Tables.Select(tab => tab.Table.Identity).ShouldBe(["Bestellungen", "Kunden"]);
    }

    [Fact]
    public void TheStartPageIsWhatIsInFrontUntilATableIsChosen()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);

        tabs.StartPageActive.ShouldBeTrue();
        tabs.ActiveTable.ShouldBeNull();
        tabs.Tables.ShouldBeEmpty();
    }

    // ---- 4.4 the summary is the first tab, and it cannot be closed -----------------------------

    [Fact]
    public void TheSummaryIsInFrontOnArrivalAndSurvivesEveryTableTabBeingClosed()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");
        var customers = Table(session, "Kunden");

        // On arrival, before any table is chosen.
        tabs.StartPageActive.ShouldBeTrue();

        tabs.Open(orders);
        tabs.Open(customers);
        tabs.StartPageActive.ShouldBeFalse();

        tabs.Close(orders);
        tabs.Close(customers);

        // There is no table left, and the one tab that is not a table's is still there. Closing
        // is something only a table's tab offers: an export's condition is what someone handed a
        // medium needs, so it is never taken away from them.
        tabs.Tables.ShouldBeEmpty();
        tabs.StartPageActive.ShouldBeTrue();

        tabs.ShowStartPage();
        tabs.StartPageActive.ShouldBeTrue();
    }

    // ---- 5.2 the target tab comes to the front, and the navigator follows it --------------------

    [Fact]
    public void TheTableInFrontIsTheOneJustReachedWhicheverWayItWasReached()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");
        var customers = Table(session, "Kunden");

        // Chosen in the navigator.
        tabs.Open(orders);
        tabs.ActiveTable.ShouldBe(orders);

        // Reached by following a reference: the navigator's selection is set from the tab, not
        // from wherever the navigation started.
        tabs.Apply(session.Follow(orders, 1, session.KeysAt(orders, 1).ShouldHaveSingleItem()));
        tabs.ActiveTable.ShouldBe(customers);

        // And back, without a second tab for either.
        tabs.Open(orders);
        tabs.ActiveTable.ShouldBe(orders);
        tabs.Tables.Count.ShouldBe(2);
    }

    // ---- 5.3 a tab keeps its position when it is left -------------------------------------------

    [Fact]
    public void ATabLeftByFollowingAReferenceIsFoundWhereItWasLeft()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");

        var order = tabs.Open(orders);
        order.Position = 2;

        tabs.Apply(session.Follow(orders, 3, session.KeysAt(orders, 1).ShouldHaveSingleItem()));
        tabs.ActiveTable.ShouldBe(Table(session, "Kunden"));

        tabs.Open(orders).Position.ShouldBe(2);
    }

    // ---- 5.4 closing one tab leaves the others alone --------------------------------------------

    [Fact]
    public void ClosingATabLeavesTheOthersAsTheyWereAndTheTableStillOpenable()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");
        var customers = Table(session, "Kunden");

        tabs.Open(orders).Position = 2;
        tabs.Open(customers).Position = 1;

        tabs.Close(customers);

        tabs.Tables.Select(tab => tab.Table.Identity).ShouldBe(["Bestellungen"]);
        tabs.For(orders).ShouldNotBeNull().Position.ShouldBe(2);

        // Opened again, it is a fresh tab rather than the closed one brought back.
        tabs.Open(customers).Position.ShouldBe(-1);
        tabs.Tables.Count.ShouldBe(2);
        tabs.For(orders).ShouldNotBeNull().Position.ShouldBe(2);
    }

    [Fact]
    public void ClosingTheLastTabPutsTheStartPageInFront()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");

        tabs.Open(orders);
        tabs.Close(orders);

        tabs.StartPageActive.ShouldBeTrue();
        tabs.Tables.ShouldBeEmpty();
    }

    // ---- 6.1 the walk bar exists only during a walk ---------------------------------------------

    [Fact]
    public void AForwardNavigationPresentsNoSteppingControls()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");

        var tab = tabs.Apply(session.Follow(orders, 1, session.KeysAt(orders, 1).ShouldHaveSingleItem()));

        tab.ShouldNotBeNull().Walk.ShouldBeNull();
    }

    [Fact]
    public void ABackwardsWalkLivesInTheReferringTablesTabAndEndsWhenThatTabIsLeft()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        var key = Backwards(session, orders, customers);

        var navigation = session.FollowBack(customers, 1, key);
        var walking = tabs.Apply(navigation, new Walk(key, customers, 1, navigation.MatchIndex, navigation.Matches));

        // Two orders refer to K1, so the walk has somewhere to step to, and it sits in the tab of
        // the table being stepped through.
        walking.ShouldNotBeNull().Table.ShouldBe(orders);
        var walk = walking.Walk.ShouldNotBeNull();
        walk.Matches.ShouldBe(2);
        walk.HasPrevious.ShouldBeFalse();
        walk.HasNext.ShouldBeTrue();

        // Leaving the table being stepped through ends the walk.
        tabs.Open(customers);
        tabs.For(orders).ShouldNotBeNull().Walk.ShouldBeNull();
    }

    [Fact]
    public void StartingAnotherWalkReplacesTheOneTheTabWasHolding()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        var key = Backwards(session, orders, customers);

        // K1 is referred to by two orders, K2 by one. Starting the second walk must not leave the
        // first walk's count behind, which is what one shared bar did.
        var first = session.FollowBack(customers, 1, key);
        tabs.Apply(first, new Walk(key, customers, 1, first.MatchIndex, first.Matches));
        tabs.For(orders).ShouldNotBeNull().Walk.ShouldNotBeNull().Matches.ShouldBe(2);

        var second = session.FollowBack(customers, 2, key);
        tabs.Apply(second, new Walk(key, customers, 2, second.MatchIndex, second.Matches));

        var walk = tabs.For(orders).ShouldNotBeNull().Walk.ShouldNotBeNull();
        walk.Matches.ShouldBe(1);
        walk.Ordinal.ShouldBe(2);
        walk.HasNext.ShouldBeFalse();
        walk.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public void ClosingATabTakesItsWalkWithIt()
    {
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var customers = Table(session, "Kunden");
        var orders = Table(session, "Bestellungen");
        var key = Backwards(session, orders, customers);

        var navigation = session.FollowBack(customers, 1, key);
        tabs.Apply(navigation, new Walk(key, customers, 1, navigation.MatchIndex, navigation.Matches));

        tabs.Close(orders);

        tabs.For(orders).ShouldBeNull();
        tabs.Open(orders).Walk.ShouldBeNull();
    }

    // ---- 6.2 a navigation's message stays with the table it concerns ----------------------------

    [Fact]
    public void SwitchingTabsNeverShowsAnotherTablesMessage()
    {
        // The reported screen: "Record 13 for '33592539'" stayed in view beside a table that had
        // nothing to do with it, because one status line served every table. Here the message is
        // the tab's, so leaving the tab leaves the message.
        using var harness = Export();
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");
        var customers = Table(session, "Kunden");

        var landed = tabs.Apply(session.Follow(orders, 1, session.KeysAt(orders, 1).ShouldHaveSingleItem()));
        landed.ShouldNotBeNull().Table.ShouldBe(customers);
        landed.Status.ShouldContain("K1");

        // The table the navigation started from says nothing about it.
        tabs.Open(orders).Status.ShouldBeEmpty();

        // And the message is still there when its own table is returned to, unchanged.
        tabs.Open(customers).Status.ShouldContain("K1");
    }

    [Fact]
    public void AValueThatRefersToNothingIsReportedInTheTableItPointsAt()
    {
        using var harness = StoreHarness.Create(
            Shop,
            ("kunden.csv", "K1;Meier\r\n"),
            ("bestellungen.csv", "N1;K9;\r\n"));
        using var session = Read(harness);
        var tabs = new ReaderTabs(session);
        var orders = Table(session, "Bestellungen");

        var tab = tabs.Apply(session.Follow(orders, 1, session.KeysAt(orders, 1).ShouldHaveSingleItem()));

        tab.ShouldNotBeNull().Status.ShouldContain("does not resolve");
        tab.Walk.ShouldBeNull();
    }

    [Fact]
    public void ATableStillBeingReadIsSaidToBeRatherThanCalledUnreadable()
    {
        using var harness = Export();
        using var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        var orders = Table(session, "Bestellungen");

        var navigation = session.Follow(orders, 1, session.KeysAt(orders, 1).ShouldHaveSingleItem());

        navigation.Kind.ShouldBe(NavigationKind.NotReady);
        ReaderTabs.Describe(navigation).ShouldContain("still being read");
    }

    private static ResolvedForeignKey Backwards(ReaderSession session, TableNode referring, TableNode referenced)
    {
        var relationship = session.RelationshipsOf(referenced).ReferencedBy
            .Single(entry => ReferenceEquals(entry.Other, referring));
        return ResolvedForeignKey.Resolve(relationship.ForeignKey, referring, referenced).ShouldNotBeNull();
    }
}
