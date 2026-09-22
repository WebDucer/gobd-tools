using System.Globalization;
using System.Text;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// A value that can be followed looks different from one that cannot, and a value that refers to
/// nothing is marked where it appears.
/// </summary>
/// <remarks>
/// The marks come from asking the store about the page on screen, not from the findings. The
/// report stops at the per-table bound and a table does not, so marking from findings would
/// silently stop at the fiftieth dangling value and leave the fifty-first looking sound. See the
/// change's design.md D7.
/// </remarks>
public sealed class FollowableCellTests
{
    private const string Composite = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Land</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Land</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Betrag</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey>
                      <Name>Land</Name>
                      <Name>Kunde</Name>
                      <References>Kunden</References>
                    </ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private const string Simple = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
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

    private static ReaderSession Read(StoreHarness harness, int bound = ContentOptions.DefaultMaximumFindingsPerTable)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options, bound).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    private static TableNode Table(ReaderSession session, string identity) =>
        session.DataSet.Tables.First(table =>
            string.Equals(table.Identity, identity, StringComparison.Ordinal));

    // ---- 8.1 the column model marks exactly the foreign key columns -----------------------------

    [Fact]
    public void EveryColumnOfACompositeKeyIsFollowableAndNoOtherColumnIs()
    {
        using var harness = StoreHarness.Create(
            Composite,
            ("kunden.csv", "DE;K1;Meier\r\n"),
            ("bestellungen.csv", "N1;DE;K1;12,00\r\n"));
        using var session = Read(harness);

        var links = session.LinksOf(Table(session, "Bestellungen"));

        // Columns 1 and 2 form the key; 0 is the primary key of this table and 3 is an ordinary
        // value, and neither leads anywhere.
        links.Select(link => link.Column).ShouldBe([1, 2]);
        links.ShouldAllBe(link => link.LeadsTo.Count == 1 && link.LeadsTo[0] == "Kunden");
    }

    [Fact]
    public void ATableWithNoForeignKeysHasNoFollowableColumns()
    {
        using var harness = StoreHarness.Create(
            Simple,
            ("kunden.csv", "K1\r\n"),
            ("bestellungen.csv", "N1;K1\r\n"));
        using var session = Read(harness);

        session.LinksOf(Table(session, "Kunden")).ShouldBeEmpty();
        session.LinksOf(Table(session, "Bestellungen")).Select(link => link.Column).ShouldBe([1]);
    }

    [Fact]
    public void AColumnTakingPartInTwoKeysNamesBothTablesItLeadsTo()
    {
        const string TwoKeys = """
                    <Table>
                      <URL>kunden.csv</URL>
                      <Name>Kunden</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                      </VariableLength>
                    </Table>
                    <Table>
                      <URL>haendler.csv</URL>
                      <Name>Haendler</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                      </VariableLength>
                    </Table>
                    <Table>
                      <URL>bestellungen.csv</URL>
                      <Name>Bestellungen</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                        <VariableColumn><Name>Partner</Name><AlphaNumeric/></VariableColumn>
                        <ForeignKey><Name>Partner</Name><References>Kunden</References></ForeignKey>
                        <ForeignKey><Name>Partner</Name><References>Haendler</References></ForeignKey>
                      </VariableLength>
                    </Table>
            """;

        using var harness = StoreHarness.Create(
            TwoKeys,
            ("kunden.csv", "K1\r\n"),
            ("haendler.csv", "K1\r\n"),
            ("bestellungen.csv", "N1;K1\r\n"));
        using var session = Read(harness);

        var link = session.LinksOf(Table(session, "Bestellungen")).ShouldHaveSingleItem();
        link.Column.ShouldBe(1);
        link.LeadsTo.ShouldBe(["Kunden", "Haendler"]);
    }

    // ---- 8.2 values that refer to nothing, marked per page --------------------------------------

    [Fact]
    public void AValueThatRefersToNothingIsMarkedAndOneThatResolvesIsNot()
    {
        using var harness = StoreHarness.Create(
            Simple,
            ("kunden.csv", "K1\r\n"),
            ("bestellungen.csv", "N1;K1\r\nN2;K9\r\nN3;\r\n"));
        using var session = Read(harness);

        var rows = session.View(Table(session, "Bestellungen")).Rows.ShouldNotBeNull();

        rows[0].RefersToNothing(1).ShouldBeFalse();
        rows[1].RefersToNothing(1).ShouldBeTrue();

        // An empty value refers to nothing on purpose, which is not the same as referring to
        // something that is not there.
        rows[2].RefersToNothing(1).ShouldBeFalse();

        // And the column that is not part of the key is never marked.
        rows.ShouldAllBe(row => !row.RefersToNothing(0));
    }

    [Fact]
    public void EveryColumnOfACompositeKeyIsMarkedTogether()
    {
        using var harness = StoreHarness.Create(
            Composite,
            ("kunden.csv", "DE;K1;Meier\r\n"),
            ("bestellungen.csv", "N1;DE;K1;12,00\r\nN2;AT;K1;13,00\r\n"));
        using var session = Read(harness);

        var rows = session.View(Table(session, "Bestellungen")).Rows.ShouldNotBeNull();

        rows[0].Unresolved.ShouldBeEmpty();
        rows[1].Unresolved.ShouldBe([1, 2]);
    }

    [Fact]
    public void ATableWithMoreDanglingValuesThanTheBoundMarksEveryOneOfThem()
    {
        const int Bound = 50;
        const int Records = 120;

        var orders = new StringBuilder();
        for (var number = 1; number <= Records; number++)
        {
            // Every record refers to a customer that is not in the export.
            orders.Append(string.Create(CultureInfo.InvariantCulture, $"N{number:0000};K{number:0000}\r\n"));
        }

        using var harness = StoreHarness.Create(
            Simple,
            ("kunden.csv", "K0001\r\n"),
            ("bestellungen.csv", orders.ToString()));
        using var session = Read(harness, Bound);
        var table = Table(session, "Bestellungen");

        // The report stopped where it was told to.
        var reported = session.Reading.Tables
            .Single(entry => ReferenceEquals(entry.Table, table))
            .Findings
            .Count(finding => finding.Code == FindingCodes.ForeignKeyValueUnresolved);
        reported.ShouldBe(Bound);

        // The table did not. The fifty-first is marked, and so is the last.
        var rows = session.View(table).Rows.ShouldNotBeNull();
        rows.Count.ShouldBe(Records);
        rows[50].RefersToNothing(1).ShouldBeTrue();
        rows[Records - 1].RefersToNothing(1).ShouldBeTrue();
        rows.Count(row => row.RefersToNothing(1)).ShouldBe(Records - 1);
        rows[0].RefersToNothing(1).ShouldBeFalse();
    }

    [Fact]
    public void MarksAreAskedForPerPageRatherThanForTheWholeTable()
    {
        var orders = new StringBuilder();
        for (var number = 1; number <= 2_000; number++)
        {
            orders.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"N{number:0000};K{(number % 2 == 0 ? "0001" : number.ToString("0000", CultureInfo.InvariantCulture))}\r\n"));
        }

        using var harness = StoreHarness.Create(
            Simple,
            ("kunden.csv", "K0001\r\n"),
            ("bestellungen.csv", orders.ToString()));
        using var session = Read(harness);

        var rows = session.View(Table(session, "Bestellungen")).Rows.ShouldNotBeNull();

        // A record deep in the table is marked on the page it is on, not because the whole table
        // was scanned for the first page.
        rows[1_500].RefersToNothing(1).ShouldBeTrue();
        rows[1_501].RefersToNothing(1).ShouldBeFalse();
        rows.PageLoads.ShouldBeLessThan(rows.Count);
    }

    // ---- 9.1 the gate is unchanged: keys do not withhold a table -------------------------------

    [Fact]
    public void ATableWhoseReferencesDoNotResolveStillShowsItsDataAndReportsThem()
    {
        // Checking keys on opening makes dangling references known before any table is chosen.
        // It does not make them a reason to withhold a table: a table of a million records with
        // one reference that does not resolve is still a table someone needs to read. The gate
        // stays on record conformance. See the change's design.md D8.
        using var harness = StoreHarness.Create(
            Simple,
            ("kunden.csv", "K1\r\n"),
            ("bestellungen.csv", "N1;K1\r\nN2;K9\r\n"));
        using var session = Read(harness);
        var table = Table(session, "Bestellungen");

        var view = session.View(table);
        view.Kind.ShouldBe(TableViewKind.Data);
        view.Rows.ShouldNotBeNull().Count.ShouldBe(2);

        var reading = session.Reading.Tables.Single(entry => ReferenceEquals(entry.Table, table));
        reading.State.ShouldBe(TableReadingState.Ready);
        reading.Findings.Select(finding => finding.Code)
            .ShouldBe([FindingCodes.ForeignKeyValueUnresolved]);

        // And the record that carries it is marked where it appears.
        view.Rows![1].RefersToNothing(1).ShouldBeTrue();
    }

    [Fact]
    public void ATableWhoseRecordsDoNotConformStillWithholdsItsData()
    {
        const string Dated = """
                    <Table>
                      <URL>a.csv</URL>
                      <Name>A</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                        <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                      </VariableLength>
                    </Table>
            """;

        using var harness = StoreHarness.Create(Dated, ("a.csv", "A1;nicht-ein-datum\r\n"));
        using var session = Read(harness);

        var view = session.View(Table(session, "A"));
        view.Kind.ShouldBe(TableViewKind.Findings);
        view.Rows.ShouldBeNull();
    }

    [Fact]
    public void ATableIsNotMarkedFromATableThatHasNotBeenReadYet()
    {
        // Tables are read in document order, so a table opened early may be paged before the
        // table it points at is in the store. Nothing was compared, so nothing may be marked.
        using var harness = StoreHarness.Create(
            """
                    <Table>
                      <URL>bestellungen.csv</URL>
                      <Name>Bestellungen</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                        <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                        <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                      </VariableLength>
                    </Table>
                    <Table>
                      <URL>kunden.csv</URL>
                      <Name>Kunden</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                      </VariableLength>
                    </Table>
            """,
            ("bestellungen.csv", "N1;K9\r\n"),
            ("kunden.csv", "K1\r\n"));
        using var session = Read(harness);

        // Once everything has been read the mark is there, and the page that was cached without
        // it is not the page anyone sees.
        session.View(Table(session, "Bestellungen")).Rows.ShouldNotBeNull()[0]
            .RefersToNothing(1).ShouldBeTrue();
    }
}
