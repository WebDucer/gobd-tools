using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The navigator shows each table's relationships as information about that table.
/// </summary>
/// <remarks>
/// The reference graph is not a tree — a table may be referenced by several others, references
/// cross media, and a table may reference itself — so the navigator navigates the hierarchy
/// <c>index.xml</c> declares and lists the relationships beneath each table as leaves. One level
/// deep by construction, so cycles and self-references cannot recurse. See the change's
/// design.md D6.
/// </remarks>
public sealed class NavigatorRelationshipTests
{
    /// <summary>
    /// Two parents and one child, a reference that crosses media, and a table referencing itself.
    /// </summary>
    private const string Graph = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>artikel.csv</URL>
                  <Name>Artikel</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
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
                <Table>
                  <URL>konten.csv</URL>
                  <Name>Konten</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Konto</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Oberkonto</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Oberkonto</Name><References>Konten</References></ForeignKey>
                  </VariableLength>
                </Table>
              </Media>
              <Media>
                <Name>Disk 2</Name>
                <Table>
                  <URL>notizen.csv</URL>
                  <Name>Notizen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private static StoreHarness Export() => StoreHarness.Create(
        Graph,
        ("kunden.csv", "K1\r\n"),
        ("artikel.csv", "A1\r\n"),
        ("positionen.csv", "P1;K1;A1\r\n"),
        ("konten.csv", "1000;\r\n1100;1000\r\n"),
        ("notizen.csv", "N1;K1\r\n"));

    private static ReaderSession Open(StoreHarness harness) =>
        ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();

    private static NavigatorTable Entry(ReaderSession session, string identity) =>
        session.Navigator
            .SelectMany(medium => medium.Tables)
            .Single(table => string.Equals(table.Identity, identity, StringComparison.Ordinal));

    // ---- 7.1 relationships listed beneath each table --------------------------------------------

    [Fact]
    public void ATableWithTwoParentsListsBothBeneathIt()
    {
        using var harness = Export();
        using var session = Open(harness);

        Entry(session, "Positionen").Relationships.References
            .Select(relationship => relationship.Label)
            .ShouldBe(["Kunden (Kunde)", "Artikel (Artikel)"]);
    }

    [Fact]
    public void ATableWithAChildListsItAmongItsReferrers()
    {
        using var harness = Export();
        using var session = Open(harness);

        Entry(session, "Artikel").Relationships.ReferencedBy
            .Select(relationship => relationship.Label)
            .ShouldBe(["Positionen (Artikel)"]);
    }

    [Fact]
    public void AReferenceThatCrossesMediaIsListedLikeAnyOther()
    {
        using var harness = Export();
        using var session = Open(harness);

        // Notizen sits on Disk 2 and points at Kunden on Disk 1. The navigator's tree is the
        // declared hierarchy, so the relationship is listed rather than nested across media.
        session.Navigator[1].Name.ShouldBe("Disk 2");
        Entry(session, "Notizen").Relationships.References
            .Select(relationship => relationship.Label)
            .ShouldBe(["Kunden (Kunde)"]);

        Entry(session, "Kunden").Relationships.ReferencedBy
            .Select(relationship => relationship.Label)
            .ShouldBe(["Positionen (Kunde)", "Notizen (Kunde)"]);
    }

    [Fact]
    public void ATableThatReferencesItselfIsListedAmongItsOwnReferencesAndReferrers()
    {
        using var harness = Export();
        using var session = Open(harness);

        var konten = Entry(session, "Konten").Relationships;

        konten.References.Select(relationship => relationship.Label).ShouldBe(["Konten (Oberkonto)"]);
        konten.ReferencedBy.Select(relationship => relationship.Label).ShouldBe(["Konten (Oberkonto)"]);
    }

    [Fact]
    public void ATableWithNoRelationshipsListsNone()
    {
        using var harness = StoreHarness.Create(
            """
                    <Table>
                      <URL>a.csv</URL>
                      <Name>A</Name>
                      <VariableLength>
                        <VariableColumn><Name>Wert</Name><AlphaNumeric/></VariableColumn>
                      </VariableLength>
                    </Table>
            """,
            ("a.csv", "x\r\n"));
        using var session = Open(harness);

        var relationships = Entry(session, "A").Relationships;
        relationships.References.ShouldBeEmpty();
        relationships.ReferencedBy.ShouldBeEmpty();
    }

    // ---- 7.2 a relationship entry is information, not navigation --------------------------------

    [Fact]
    public void ChoosingARelationshipEntryOpensNothingAndMovesNothing()
    {
        using var harness = Export();
        using var session = Open(harness);
        session.Read(cancellationToken: TestContext.Current.CancellationToken);

        var tabs = new ReaderTabs(session);
        var positions = Entry(session, "Positionen");
        var tab = tabs.Open(positions.Table);
        tab.Position = 0;

        var relationship = positions.Relationships.References[0];
        ReaderTabs.Opens(relationship).ShouldBeNull();
        ReaderTabs.Opens(relationship.Label).ShouldBeNull();

        // Nothing about the workspace changed: the same tab is in front, at the same record, and
        // the table at the other end was not opened.
        tabs.ActiveTable.ShouldBe(positions.Table);
        tabs.Tables.Count.ShouldBe(1);
        tabs.For(positions.Table).ShouldNotBeNull().Position.ShouldBe(0);
        tabs.For(relationship.Other).ShouldBeNull();
    }

    [Fact]
    public void ChoosingATableEntryOpensThatTable()
    {
        using var harness = Export();
        using var session = Open(harness);
        var tabs = new ReaderTabs(session);
        var customers = Entry(session, "Kunden");

        var table = ReaderTabs.Opens(customers.Table).ShouldNotBeNull();
        tabs.Open(table);

        tabs.ActiveTable.ShouldBe(customers.Table);
    }
}
