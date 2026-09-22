using Avalonia.Controls;
using Avalonia.LogicalTree;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.Controls;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// What the banner says a table is showing, and the one click back to the table itself.
/// </summary>
/// <remarks>
/// A filtered table that looked like the table would invite a person to cite a part as the whole.
/// So the banner says how much of it is in view, and returning to file order is one action rather
/// than the undoing of every filter in turn.
/// </remarks>
public sealed class ViewBannerTests : HeadlessTest
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
        """;

    private const string Records = "A;10,00\r\nB;20,00\r\nC;30,00\r\n";

    private const int Betrag = 1;

    private static Button Reset(TableTabView view) =>
        view.GetLogicalDescendants()
            .OfType<Button>()
            .First(button => (string?)button.Content == "Back to file order");

    [Fact]
    public Task AFilterThatMatchesNothingSaysSoRatherThanShowingAnEmptyTable()
    {
        // Taken before the body, because the dispatcher the body runs on is not where xUnit put
        // the test's context.
        var token = TestContext.Current.CancellationToken;

        return Ui(() =>
        {
            using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
            var (view, tab, session) = Driver.Open(harness);

            // Built the way the window builds it, so the banner is reading a real view.
            var query = new TableQuery([new ColumnFilter(Betrag, FilterComparison.GreaterThan, ["1000"])], []);
            var rows = session.Build(tab.Table, query, 0, token);

            rows.Count.ShouldBe(0);
            tab.Query = query;
            tab.View = rows.View;
            tab.Rows = rows.Records;
            view.Refresh();

            Driver.Banner(view).ShouldBe("No record matches");
        });
    }

    [Fact]
    public Task TheBannerCountsWhatAViewHoldsOfWhatTheTableHolds()
    {
        var token = TestContext.Current.CancellationToken;

        return Ui(() =>
        {
            using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
            var (view, tab, session) = Driver.Open(harness);

            var query = new TableQuery([new ColumnFilter(Betrag, FilterComparison.GreaterThan, ["15"])], []);
            var rows = session.Build(tab.Table, query, 0, token);

            tab.Query = query;
            tab.View = rows.View;
            tab.Rows = rows.Records;
            view.Refresh();

            Driver.Banner(view).ShouldBe("Showing 2 of 3 records");
        });
    }

    [Fact]
    public Task BackToFileOrderIsOfferedOnlyWhenSomethingIsInForce() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        Reset(view).IsVisible.ShouldBeFalse();

        tab.Query = new TableQuery([], [new ColumnSort(Betrag, Ordering.Descending)]);
        view.Refresh();

        Reset(view).IsVisible.ShouldBeTrue();
    });

    [Fact]
    public Task BackToFileOrderAsksForTheTableItself() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        tab.Query = new TableQuery(
            [new ColumnFilter(Betrag, FilterComparison.GreaterThan, ["15"])],
            [new ColumnSort(Betrag, Ordering.Descending)]);
        view.Refresh();

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        Driver.Click(Reset(view));

        // Everything at once, rather than one filter at a time.
        asked.ShouldNotBeNull().IsFileOrder.ShouldBeTrue();
    });
}
