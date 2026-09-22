using System.Globalization;
using System.Reflection;
using System.Text;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// What the reader shows: the declared structure, a table's relationships, and the gate that
/// decides whether a table is presented as data or as a report.
/// </summary>
public sealed class ReaderSessionTests
{
    private static string Table(string name, string url, string extra = "") => $"""
                <Table>
                  <URL>{url}</URL>
                  <Name>{name}</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Wert</Name><AlphaNumeric/></VariableColumn>
        {extra}
                  </VariableLength>
                </Table>
        """;

    /// <summary>
    /// Opens an export and reads it to the end, inline.
    /// </summary>
    /// <remarks>
    /// Opening is fast and reads no data; everything a table shows comes from the pipeline, which
    /// the window runs on a worker. A test that wants the finished state runs it inline, which is
    /// exactly what <see cref="ReaderSession.Read"/> being synchronous is for.
    /// </remarks>
    private static ReaderSession Open(StoreHarness harness)
    {
        var result = ReaderSession.Open(harness.ExportPath, harness.Options);
        var session = result.Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    /// <summary>Opens an export without reading it, for what a table looks like before its turn.</summary>
    private static ReaderSession OpenOnly(StoreHarness harness)
    {
        var result = ReaderSession.Open(harness.ExportPath, harness.Options);
        return result.Session.ShouldNotBeNull();
    }

    // ---- 9.1 the navigator -------------------------------------------------------------------

    [Fact]
    public void TheNavigatorPresentsEveryMediumAndTheTablesItCarries()
    {
        // Two media, written as one document: the harness wraps its tables in a single medium,
        // so this fixture closes that medium and opens another.
        const string TwoMedia = """
                <Table>
                  <URL>a.csv</URL>
                  <Name>A</Name>
                  <VariableLength>
                    <VariableColumn><Name>Wert</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
              </Media>
              <Media>
                <Name>Disk 2</Name>
                <Table>
                  <URL>b.csv</URL>
                  <Name>B</Name>
                  <VariableLength>
                    <VariableColumn><Name>Wert</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(TwoMedia, ("a.csv", "x\r\n"), ("b.csv", "y\r\n"));
        using var session = Open(harness);

        session.Navigator.Count.ShouldBe(2);
        session.Navigator[0].Name.ShouldBe("Disk 1");
        session.Navigator[0].Tables.Select(table => table.Identity).ShouldBe(["A"]);
        session.Navigator[1].Name.ShouldBe("Disk 2");
        session.Navigator[1].Tables.Select(table => table.Identity).ShouldBe(["B"]);
    }

    [Fact]
    public void TheNavigatorStopsAtTables()
    {
        // Columns are not in the navigator. A table of three hundred columns would bury the
        // structure a person is navigating by, and the columns are the grid's headers anyway.
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", "1;x\r\n"));
        using var session = Open(harness);

        typeof(NavigatorMedium).GetProperties()
            .Select(property => property.Name)
            .ShouldBe(["Name", "Tables"], ignoreOrder: true);
    }

    // ---- 9.2 relationships -------------------------------------------------------------------

    [Fact]
    public void ATablesReferencesAndReferrersArePresentedPerTable()
    {
        const string Graph = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>artikel.csv</URL>
                  <Name>Artikel</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>positionen.csv</URL>
                  <Name>Positionen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Artikel</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                    <ForeignKey><Name>Artikel</Name><References>Artikel</References></ForeignKey>
                  </VariableLength>
                </Table>
              </Media>
              <Media>
                <Name>Disk 2</Name>
                <Table>
                  <URL>notizen.csv</URL>
                  <Name>Notizen</Name>
                  <VariableLength>
                    <VariableColumn><Name>Position</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Position</Name><References>Positionen</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Graph,
            ("kunden.csv", "K1\r\n"),
            ("artikel.csv", "A1\r\n"),
            ("positionen.csv", "P1;K1;A1\r\n"),
            ("notizen.csv", "P1\r\n"));
        using var session = Open(harness);

        var positionen = session.RelationshipsOf(harness.Table("Positionen"));

        positionen.References.Select(entry => entry.Other.Identity).ShouldBe(["Kunden", "Artikel"]);

        // The reference from Notizen crosses a medium boundary, which a Media-shaped tree could
        // not express and which this presentation shows without difficulty.
        positionen.ReferencedBy.Select(entry => entry.Other.Identity).ShouldBe(["Notizen"]);
    }

    [Fact]
    public void ATableThatReferencesItselfIsItsOwnReferrer()
    {
        const string Mitarbeiter = """
                <Table>
                  <URL>m.csv</URL>
                  <Name>Mitarbeiter</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Chef</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey>
                      <Name>Chef</Name>
                      <References>Mitarbeiter</References>
                      <Alias><From>Chef</From><To>Id</To></Alias>
                    </ForeignKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Mitarbeiter, ("m.csv", "P1;\r\nP2;P1\r\n"));
        using var session = Open(harness);

        var related = session.RelationshipsOf(harness.Table("Mitarbeiter"));

        related.References.ShouldHaveSingleItem().Other.Identity.ShouldBe("Mitarbeiter");
        related.ReferencedBy.ShouldHaveSingleItem().Other.Identity.ShouldBe("Mitarbeiter");
    }

    // ---- 9.3 the gate ------------------------------------------------------------------------

    [Fact]
    public void AConsistentTableIsPresentedAsData()
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", "1;x\r\n2;y\r\n"));
        using var session = Open(harness);

        var view = session.View(harness.Table("A"));

        view.Kind.ShouldBe(TableViewKind.Data);
        view.Columns.ShouldBe(["Id", "Wert"]);
        view.Rows.ShouldNotBeNull().Count.ShouldBe(2);
        view.Rows[0].Values.ShouldBe(["1", "x"]);
        view.Rows[0].Ordinal.ShouldBe(1);
    }

    [Fact]
    public void ATableThatDoesNotConformIsPresentedAsItsFindings()
    {
        const string Dated = """
                <Table>
                  <URL>a.csv</URL>
                  <Name>A</Name>
                  <VariableLength>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Dated, ("a.csv", "nicht-ein-datum\r\n"));
        using var session = Open(harness);

        var view = session.View(harness.Table("A"));

        view.Kind.ShouldBe(TableViewKind.Findings);
        view.Rows.ShouldBeNull();
        view.Findings.ShouldContain(finding => finding.Code == FindingCodes.RecordValueTypeMismatch);
    }

    [Fact]
    public void OneDefectiveTableDoesNotWithholdTheOthers()
    {
        const string Two = """
                <Table>
                  <URL>gut.csv</URL>
                  <Name>Gut</Name>
                  <VariableLength>
                    <VariableColumn><Name>Wert</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>schlecht.csv</URL>
                  <Name>Schlecht</Name>
                  <VariableLength>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Two,
            ("gut.csv", "x\r\ny\r\n"),
            ("schlecht.csv", "nicht-ein-datum\r\n"));
        using var session = Open(harness);

        session.View(harness.Table("Schlecht")).Kind.ShouldBe(TableViewKind.Findings);
        session.View(harness.Table("Gut")).Kind.ShouldBe(TableViewKind.Data);
        session.View(harness.Table("Gut")).Rows.ShouldNotBeNull().Count.ShouldBe(2);
    }

    // ---- 9.4 a truncated list says so --------------------------------------------------------

    [Fact]
    public void ATruncatedListOfFindingsSaysThatAnalysisStopped()
    {
        const string Dated = """
                <Table>
                  <URL>a.csv</URL>
                  <Name>A</Name>
                  <VariableLength>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        var content = new StringBuilder();
        for (var number = 0; number < 100; number++)
        {
            content.Append("nicht-ein-datum\r\n");
        }

        using var harness = StoreHarness.Create(Dated, ("a.csv", content.ToString()));
        var result = ReaderSession.Open(harness.ExportPath, harness.Options, bound: 5);
        using var session = result.Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);

        var view = session.View(harness.Table("A"));

        view.Truncated.ShouldBeTrue();
        view.Findings.Count(finding => finding.Code == FindingCodes.RecordValueTypeMismatch).ShouldBe(5);
    }

    // ---- 9.5 one view per table, paged -------------------------------------------------------

    [Fact]
    public void ReachingATableAgainReusesItsView()
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", "1;x\r\n"));
        using var session = Open(harness);
        var table = harness.Table("A");

        var first = session.View(table);
        for (var again = 0; again < 10; again++)
        {
            session.View(table).ShouldBeSameAs(first);
        }

        session.OpenViews.Count.ShouldBe(1);
    }

    [Fact]
    public void TheGridHoldsAWindowRatherThanTheWholeTable()
    {
        var content = new StringBuilder();
        for (var number = 1; number <= 5_000; number++)
        {
            content.Append(CultureInfo.InvariantCulture, $"{number};w{number}\r\n");
        }

        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", content.ToString()));
        using var session = Open(harness);

        var rows = session.View(harness.Table("A")).Rows.ShouldNotBeNull();
        rows.Count.ShouldBe(5_000);

        // Reading two rows four thousand apart must not materialise what lies between them.
        rows[0].Values[0].ShouldBe("1");
        rows[4_000].Values[0].ShouldBe("4001");
        rows.PeakResident.ShouldBeLessThan(5_000);
        rows.PageLoads.ShouldBe(2);
    }

    [Fact]
    public void ADeclaredValueRedefinitionReachesTheGrid()
    {
        const string Mapped = """
                <Table>
                  <URL>a.csv</URL>
                  <Name>A</Name>
                  <VariableLength>
                    <VariableColumn>
                      <Name>Art</Name>
                      <AlphaNumeric/>
                      <Map><From>R</From><To>Rechnung</To></Map>
                    </VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Mapped, ("a.csv", "R\r\n"));
        using var session = Open(harness);

        session.View(harness.Table("A")).Rows.ShouldNotBeNull()[0].Values[0].ShouldBe("Rechnung");
    }

    // ---- 9.6 what the reader does not offer --------------------------------------------------

    [Fact]
    public void NothingInTheReaderOffersSortingFilteringGroupingOrAggregation()
    {
        // Record order is what makes a record citable in an audit, so it is not the reader's to
        // rearrange. This holds the surface to that rather than trusting it.
        var forbidden = new[] { "Sort", "Filter", "Group", "Aggregate", "OrderBy", "Where" };
        var offered = new[] { typeof(ReaderSession), typeof(TablePresentation), typeof(TablePageList) }
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(member => member.Name)
            .Where(name => forbidden.Any(word => name.Contains(word, StringComparison.Ordinal)))
            .ToArray();

        offered.ShouldBeEmpty($"the reader offers: {string.Join(", ", offered)}");
    }

    [Fact]
    public void ReadingAnExportLeavesItExactlyAsItWas()
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", "1;x\r\n2;y\r\n"));
        var before = harness.ExportContents()
            .Select(name => (name, File.Exists(Path.Combine(harness.ExportPath, name))
                ? File.ReadAllBytes(Path.Combine(harness.ExportPath, name)).Length
                : -1))
            .ToArray();

        using (var session = Open(harness))
        {
            var view = session.View(harness.Table("A"));
            view.Rows.ShouldNotBeNull()[0].Values.ShouldBe(["1", "x"]);
        }

        harness.ExportContents()
            .Select(name => (name, File.Exists(Path.Combine(harness.ExportPath, name))
                ? File.ReadAllBytes(Path.Combine(harness.ExportPath, name)).Length
                : -1))
            .ShouldBe(before);
    }

    // ---- 14.3 an empty table -----------------------------------------------------------------

    [Fact]
    public void ATableWithNoRecordsIsPresentedAsAnEmptyTableUnderItsColumns()
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", string.Empty));
        using var session = Open(harness);

        var view = session.View(harness.Table("A"));

        view.Kind.ShouldBe(TableViewKind.Data);
        view.Columns.ShouldBe(["Id", "Wert"]);
        view.Rows.ShouldNotBeNull().Count.ShouldBe(0);
        view.Findings.ShouldBeEmpty();
    }

    // ---- 14.6 the reported case --------------------------------------------------------------

    private static string EmptyBesideFilled() =>
        Table("Kunden", "kunden.csv")
        + Table("Bestellungen", "bestellungen.csv", "<ForeignKey><Name>Wert</Name><References>Kunden</References></ForeignKey>");

    [Fact]
    public void SelectingTheEmptyTableFirstLeavesTheReaderUsable()
    {
        // The crash reported from a real export, reduced: one table with no records beside one
        // with records. Selecting the empty table ended the session; the reader has to survive it
        // and carry on.
        using var harness = StoreHarness.Create(EmptyBesideFilled(), ("kunden.csv", string.Empty), ("bestellungen.csv", "B1;K1\r\n"));
        using var session = Open(harness);

        session.View(harness.Table("Kunden")).Rows.ShouldNotBeNull().Count.ShouldBe(0);
        session.View(harness.Table("Bestellungen")).Rows.ShouldNotBeNull().Count.ShouldBe(1);
    }

    [Fact]
    public void SelectingTheTableWithRecordsFirstLeavesTheReaderUsable()
    {
        using var harness = StoreHarness.Create(EmptyBesideFilled(), ("kunden.csv", string.Empty), ("bestellungen.csv", "B1;K1\r\n"));
        using var session = Open(harness);

        session.View(harness.Table("Bestellungen")).Rows.ShouldNotBeNull().Count.ShouldBe(1);
        session.View(harness.Table("Kunden")).Rows.ShouldNotBeNull().Count.ShouldBe(0);
    }

    [Fact]
    public void FollowingAReferenceIntoTheEmptyTableSaysItResolvesToNothing()
    {
        // The key points at a table with no records, so it cannot resolve. That is a finding to
        // show, not a failure to survive.
        using var harness = StoreHarness.Create(EmptyBesideFilled(), ("kunden.csv", string.Empty), ("bestellungen.csv", "B1;K1\r\n"));
        using var session = Open(harness);
        var orders = harness.Table("Bestellungen");

        var navigation = session.Follow(orders, 1, session.KeysAt(orders, 1).ShouldHaveSingleItem());

        navigation.Kind.ShouldBe(NavigationKind.Unresolved);
        navigation.Value.ShouldBe("K1");
    }

    // ---- 14.5 a failure stays with its table -------------------------------------------------

    [Fact]
    public void ATableThatCannotBePresentedShowsWhyAndTheSessionCarriesOn()
    {
        // A failure no finding code anticipates: something already occupies the path this table
        // is extracted to, so writing the extract throws. It has to stay with that table.
        using var harness = StoreHarness.Create(
            Table("A", "a.csv") + Table("B", "b.csv") + Table("C", "c.csv"),
            ("a.csv", "1;x\r\n"),
            ("b.csv", "2;y\r\n"),
            ("c.csv", "3;z\r\n"));
        using var session = OpenOnly(harness);
        var broken = harness.Table("B");

        // Put in place before the export is read, because reading it is when the extract is
        // written. Nothing about the failure is lazy any more.
        Directory.CreateDirectory(Path.Combine(session.Store.Directory, session.Store.NameOf(broken) + ".csv"));
        session.Read(cancellationToken: TestContext.Current.CancellationToken);

        var view = session.View(broken);

        view.Kind.ShouldBe(TableViewKind.Failed);
        view.Rows.ShouldBeNull();
        var finding = view.Findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(FindingCodes.CheckFailed);
        finding.Scope!.Name.ShouldBe("B");

        // Neither the table already open nor one opened afterwards is affected.
        session.View(harness.Table("A")).Kind.ShouldBe(TableViewKind.Data);
        session.View(harness.Table("C")).Rows.ShouldNotBeNull().Count.ShouldBe(1);
    }
}
