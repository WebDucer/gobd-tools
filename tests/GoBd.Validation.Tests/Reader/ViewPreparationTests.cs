using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Preparing a tab's view: off the thread the window draws on, superseded when a person changes
/// their mind, and never leaving a tab with less than it had.
/// </summary>
public sealed class ViewPreparationTests
{
    private const string Tables = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>k.csv</URL>
                  <Name>Kaputt</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
        """;

    private const int Betrag = 1;

    private static StoreHarness Export() => StoreHarness.Create(
        Tables,
        ("t.csv", "A;10,00\r\nB;20,00\r\nC;30,00\r\n"),
        ("k.csv", "A;keine Zahl\r\n"));

    private static ReaderSession Read(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    private static TableNode Table(ReaderSession session, string identity) =>
        session.DataSet.Tables.First(table => string.Equals(table.Identity, identity, StringComparison.Ordinal));

    private static TableQuery Above(string bound) =>
        new([new ColumnFilter(Betrag, FilterComparison.GreaterThan, [bound])], []);

    [Fact]
    public async Task AViewIsPreparedAwayFromTheThreadTheWindowDrawsOn()
    {
        using var harness = Export();
        using var session = Read(harness);
        using var preparing = new ViewPreparation(session, 0, TimeSpan.Zero);

        var prepared = await preparing.PrepareAsync(Table(session, "T"), Above("15"), null);

        prepared.IsReady.ShouldBeTrue();
        prepared.Failure.ShouldBeNull();
        prepared.Rows.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public async Task ANewerFilterReplacesOneThatHasNotFinished()
    {
        // Typing settles before the store is asked, so a person who keeps typing is not waiting
        // on answers to what they have already replaced.
        using var harness = Export();
        using var session = Read(harness);
        using var preparing = new ViewPreparation(session, 0, TimeSpan.FromMilliseconds(400));
        var table = Table(session, "T");

        var abandoned = preparing.PrepareAsync(table, Above("5"), null);
        var wanted = preparing.PrepareAsync(table, Above("25"), null);

        (await abandoned).Superseded.ShouldBeTrue();
        var prepared = await wanted;
        prepared.IsReady.ShouldBeTrue();
        prepared.Rows.ShouldNotBeNull().Count.ShouldBe(1);
        preparing.Superseded.ShouldBe(1);
    }

    [Fact]
    public async Task ATabKeepsWhatItHadWhenAViewCannotBePrepared()
    {
        using var harness = Export();
        using var session = Read(harness);
        using var preparing = new ViewPreparation(session, 0, TimeSpan.Zero);

        // A table whose records do not conform shows its findings, so there is nothing to filter.
        var prepared = await preparing.PrepareAsync(Table(session, "Kaputt"), Above("5"), null);

        prepared.IsReady.ShouldBeFalse();
        prepared.Superseded.ShouldBeFalse();
        prepared.Failure.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturningToFileOrderTakesTheViewAwayWithIt()
    {
        using var harness = Export();
        using var session = Read(harness);
        using var preparing = new ViewPreparation(session, 0, TimeSpan.Zero);
        var table = Table(session, "T");

        var filtered = await preparing.PrepareAsync(table, Above("15"), null);
        var view = filtered.Rows.ShouldNotBeNull().View.ShouldNotBeNull();

        var whole = await preparing.PrepareAsync(table, TableQuery.FileOrder, view);

        whole.Rows.ShouldNotBeNull().View.ShouldBeNull();
        whole.Rows.Count.ShouldBe(3);
        Should.Throw<Exception>(() => session.Store.CountView(view));
    }

    [Fact]
    public async Task ReplacingAFilterRetiresTheViewItReplaces()
    {
        // A tab's next view is a table of its own, built while the grid may still be paging the
        // one it has. Only once the new one is ready does the old one go — replacing it in place
        // would have changed what a page was being read out of, halfway through reading it.
        using var harness = Export();
        using var session = Read(harness);
        using var preparing = new ViewPreparation(session, 0, TimeSpan.Zero);
        var table = Table(session, "T");

        var first = await preparing.PrepareAsync(table, Above("15"), null);
        var view = first.Rows.ShouldNotBeNull().View.ShouldNotBeNull();

        var second = await preparing.PrepareAsync(table, Above("5"), view);

        var replacing = second.Rows.ShouldNotBeNull().View.ShouldNotBeNull();
        replacing.ShouldNotBe(view);
        second.Rows.Count.ShouldBe(3);
        session.Store.CountView(replacing).ShouldBe(3);
        Should.Throw<Exception>(() => session.Store.CountView(view));
    }

    [Fact]
    public async Task OneTabsFilterDoesNotStopAnothersView()
    {
        // Each tab queries on a connection of its own, so superseding one tab's view leaves every
        // other tab's alone: a shared connection would have taken them all down with it.
        using var harness = Export();
        using var session = Read(harness);
        using var one = new ViewPreparation(session, 0, TimeSpan.Zero);
        using var another = new ViewPreparation(session, 1, TimeSpan.FromMilliseconds(400));
        var table = Table(session, "T");

        var abandoned = another.PrepareAsync(table, Above("5"), null);
        var wanted = await one.PrepareAsync(table, Above("25"), null);

        wanted.IsReady.ShouldBeTrue();
        wanted.Rows.ShouldNotBeNull().Count.ShouldBe(1);

        var finished = await abandoned;
        finished.IsReady.ShouldBeTrue();
        finished.Rows.ShouldNotBeNull().Count.ShouldBe(3);
        another.Superseded.ShouldBe(0);
    }
}
