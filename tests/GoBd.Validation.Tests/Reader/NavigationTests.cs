using System.Globalization;
using System.Text;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Following declared references from the record in front of the reader.
/// </summary>
/// <remarks>
/// Navigation positions; it never filters. The records between the matches stay present, because
/// record order is what makes a record citable in an audit — the same reason the grid offers no
/// sorting.
/// </remarks>
public sealed class NavigationTests
{
    private const string Kunden = """
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private const string Bestellungen = """
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

    /// <summary>
    /// Opens an export and reads it to the end, inline.
    /// </summary>
    /// <remarks>
    /// Every table is read when the export is opened, on a worker the window owns. Navigation
    /// needs the tables at both ends in the store, so a test runs that read inline.
    /// </remarks>
    private static ReaderSession Open(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    private static StoreHarness Shop()
    {
        var customers = new StringBuilder();
        for (var number = 1; number <= 500; number++)
        {
            customers.Append(CultureInfo.InvariantCulture, $"K{number:0000};Firma {number}\r\n");
        }

        var orders = new StringBuilder();
        for (var number = 1; number <= 900; number++)
        {
            // Every third order belongs to K0400 and no other order does, so backwards
            // navigation has several matches spread across the table rather than adjacent ones.
            var customer = number % 3 == 0 ? "K0400" : $"K{(number % 399) + 1:0000}";
            orders.Append(CultureInfo.InvariantCulture, $"B{number:0000};{customer}\r\n");
        }

        return StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", customers.ToString()),
            ("bestellungen.csv", orders.ToString()));
    }

    // ---- 10.1 following a reference ----------------------------------------------------------

    [Fact]
    public void FollowingAKeyPositionsTheReferencedTableAtTheRecordItNames()
    {
        using var harness = Shop();
        using var session = Open(harness);
        var orders = harness.Table("Bestellungen");

        var key = session.KeysAt(orders, 1).ShouldHaveSingleItem();
        var navigation = session.Follow(orders, ordinal: 3, key);

        navigation.Kind.ShouldBe(NavigationKind.Positioned);
        navigation.Table!.Identity.ShouldBe("Kunden");
        navigation.Value.ShouldBe("K0400");

        // Record 400 of the customers file, and position 399 in a list counting from zero.
        navigation.Ordinal.ShouldBe(400);
        navigation.Position.ShouldBe(399);

        var rows = session.View(harness.Table("Kunden")).Rows.ShouldNotBeNull();
        rows[navigation.Position].Values[0].ShouldBe("K0400");
    }

    [Fact]
    public void TheColumnsToIndicateAreTheOnesFormingTheKey()
    {
        using var harness = Shop();
        using var session = Open(harness);

        var key = session.KeysAt(harness.Table("Bestellungen"), 1).ShouldHaveSingleItem();

        session.Follow(harness.Table("Bestellungen"), 3, key).Columns.ShouldBe([0]);
    }

    [Fact]
    public void TheRecordsAroundTheLandingRecordAreStillThere() =>
        // Positioning, not filtering: a filtered view would answer "which customer is this?" by
        // hiding every other customer, and the record numbers would stop meaning anything.
        RowsAround().ShouldBe(["K0399", "K0400", "K0401"]);

    private static IReadOnlyList<string> RowsAround()
    {
        using var harness = Shop();
        using var session = Open(harness);
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];
        var navigation = session.Follow(harness.Table("Bestellungen"), 3, key);
        var rows = session.View(harness.Table("Kunden")).Rows!;

        return
        [
            rows[navigation.Position - 1].Values[0],
            rows[navigation.Position].Values[0],
            rows[navigation.Position + 1].Values[0],
        ];
    }

    // ---- 10.2 a composite key is one key -----------------------------------------------------

    [Fact]
    public void ACompositeKeyIsFollowedWholeFromAnyOfItsColumns()
    {
        const string Belege = """
                <Table>
                  <URL>belege.csv</URL>
                  <Name>Belege</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Jahr</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Text</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        const string Zeilen = """
                <Table>
                  <URL>zeilen.csv</URL>
                  <Name>Zeilen</Name>
                  <VariableLength>
                    <VariableColumn><Name>BelegJahr</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>BelegNummer</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey>
                      <Name>BelegJahr</Name>
                      <Name>BelegNummer</Name>
                      <References>Belege</References>
                    </ForeignKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Belege + "\n" + Zeilen,
            ("belege.csv", "2023;1;alt\r\n2024;1;neu\r\n2024;2;neuer\r\n"),
            ("zeilen.csv", "2024;2\r\n"));
        using var session = Open(harness);
        var lines = harness.Table("Zeilen");

        // Acting on either column follows the same key and lands in the same place.
        foreach (var column in new[] { 0, 1 })
        {
            var key = session.KeysAt(lines, column).ShouldHaveSingleItem();
            var navigation = session.Follow(lines, 1, key);

            navigation.Kind.ShouldBe(NavigationKind.Positioned);
            navigation.Ordinal.ShouldBe(3);
            navigation.Value.ShouldBe("2024|2");
            navigation.Columns.ShouldBe([0, 1]);
        }
    }

    // ---- 10.3 following a reference backwards ------------------------------------------------

    [Fact]
    public void FollowingBackwardsLandsOnTheFirstReferringRecordAndCountsThemAll()
    {
        using var harness = Shop();
        using var session = Open(harness);
        var customers = harness.Table("Kunden");
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];

        var navigation = session.FollowBack(customers, ordinal: 400, key);

        navigation.Kind.ShouldBe(NavigationKind.Positioned);
        navigation.Table!.Identity.ShouldBe("Bestellungen");
        navigation.Matches.ShouldBe(300);
        navigation.MatchIndex.ShouldBe(0);
        navigation.Ordinal.ShouldBe(3);
    }

    [Fact]
    public void MovingBetweenReferringRecordsWalksThemInFileOrder()
    {
        using var harness = Shop();
        using var session = Open(harness);
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];

        var ordinals = new List<long>();
        for (var match = 0; match < 4; match++)
        {
            ordinals.Add(session.FollowBack(harness.Table("Kunden"), 400, key, match).Ordinal);
        }

        ordinals.ShouldBe([3L, 6L, 9L, 12L]);
    }

    [Fact]
    public void AskingBeyondTheLastMatchStaysOnTheLastMatch()
    {
        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", "K1;Meier\r\n"),
            ("bestellungen.csv", "B1;K1\r\nB2;K1\r\n"));
        using var session = Open(harness);
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];

        var navigation = session.FollowBack(harness.Table("Kunden"), 1, key, matchIndex: 99);

        navigation.Matches.ShouldBe(2);
        navigation.MatchIndex.ShouldBe(1);
        navigation.Ordinal.ShouldBe(2);
    }

    [Fact]
    public void TheRecordsBetweenReferringRecordsRemainPresent()
    {
        using var harness = Shop();
        using var session = Open(harness);
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];

        var first = session.FollowBack(harness.Table("Kunden"), 400, key, 0);
        var second = session.FollowBack(harness.Table("Kunden"), 400, key, 1);
        var rows = session.View(harness.Table("Bestellungen")).Rows.ShouldNotBeNull();

        // Records 4 and 5 belong to other customers and are still between the two matches.
        (second.Position - first.Position).ShouldBe(3);
        rows[first.Position + 1].Values[1].ShouldNotBe("K0400");
    }

    // ---- 10.4 a value that refers to nothing -------------------------------------------------

    [Fact]
    public void AValueThatRefersToNothingIsReportedRatherThanShownAsAnEmptyResult()
    {
        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", "K1;Meier\r\n"),
            ("bestellungen.csv", "B1;K9\r\n"));
        using var session = Open(harness);
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];

        var navigation = session.Follow(harness.Table("Bestellungen"), 1, key);

        navigation.Kind.ShouldBe(NavigationKind.Unresolved);
        navigation.Value.ShouldBe("K9");
        navigation.Position.ShouldBe(-1);
    }

    [Fact]
    public void AnEmptyForeignKeyIsAnAbsentReferenceRatherThanADanglingOne()
    {
        using var harness = StoreHarness.Create(
            Kunden + "\n" + Bestellungen,
            ("kunden.csv", "K1;Meier\r\n"),
            ("bestellungen.csv", "B1;\r\n"));
        using var session = Open(harness);
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];

        session.Follow(harness.Table("Bestellungen"), 1, key).Kind.ShouldBe(NavigationKind.NoReference);
    }

    [Fact]
    public void AColumnThatTakesPartInNoKeyOffersNoNavigation()
    {
        using var harness = Shop();
        using var session = Open(harness);

        session.KeysAt(harness.Table("Bestellungen"), 0).ShouldBeEmpty();
    }

    // ---- 10.5 a table that could not be read -------------------------------------------------

    [Fact]
    public void NavigationIntoATableThatCouldNotBeReadIsRefusedWithAReason()
    {
        const string Defekt = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Defekt + "\n" + Bestellungen,
            ("kunden.csv", "K1;nicht-ein-datum\r\n"),
            ("bestellungen.csv", "B1;K1\r\n"));
        using var session = Open(harness);
        var key = session.KeysAt(harness.Table("Bestellungen"), 1)[0];

        var navigation = session.Follow(harness.Table("Bestellungen"), 1, key);

        navigation.Kind.ShouldBe(NavigationKind.Unreadable);
        navigation.Table!.Identity.ShouldBe("Kunden");
        session.View(harness.Table("Kunden")).Kind.ShouldBe(TableViewKind.Findings);
    }

    // ---- 10.6 one view per table -------------------------------------------------------------

    [Fact]
    public void TenNavigationsProduceNoDuplicateViews()
    {
        using var harness = Shop();
        using var session = Open(harness);
        var orders = harness.Table("Bestellungen");
        var key = session.KeysAt(orders, 1)[0];

        for (var round = 0; round < 10; round++)
        {
            session.Follow(orders, (round * 3) + 3, key).Kind.ShouldBe(NavigationKind.Positioned);
            session.FollowBack(harness.Table("Kunden"), 400, key, round).Kind.ShouldBe(NavigationKind.Positioned);
        }

        session.OpenViews.Count.ShouldBe(2);
    }
}
