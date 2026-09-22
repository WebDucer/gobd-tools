using Avalonia.Controls;
using GoBd.Reader.Data;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The sort control above the grid, driven as a person drives it.
/// </summary>
/// <remarks>
/// What the view models do with a sort is tested without a window. What is tested here is the
/// part only a window has: that choosing a column and a direction produces that sort, that the
/// order they were added in is the order they apply, and that a fourth is refused where a person
/// can read why rather than one they can no longer see being dropped.
/// </remarks>
public sealed class SortControlTests : HeadlessTest
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

    private const string Records =
        "A;10,00;01.01.2025;Miete\r\nB;20,00;02.01.2025;Porto\r\nC;30,00;03.01.2025;Zebra\r\n";

    private const int Betrag = 1;
    private const int Datum = 2;
    private const int Text = 3;

    [Fact]
    public Task ChoosingAColumnAndADirectionSortsByIt() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Sort");
        Driver.Choose(editor, 0, Betrag);
        Driver.Choose(editor, 1, 1);          // descending
        Driver.Apply(editor);

        var sort = asked.ShouldNotBeNull().Sorts.ShouldHaveSingleItem();
        sort.Column.ShouldBe(Betrag);
        sort.Direction.ShouldBe(Ordering.Descending);
    });

    [Fact]
    public Task ColumnsSortInTheOrderTheyWereAdded() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        tab.Query = new TableQuery([], [new ColumnSort(Datum, Ordering.Ascending)]);
        view.Refresh();

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Sort");
        Driver.Choose(editor, 0, Betrag);
        Driver.Apply(editor);

        asked.ShouldNotBeNull().Sorts.Select(sort => sort.Column).ShouldBe([Datum, Betrag]);
    });

    [Fact]
    public Task AFourthSortedColumnIsRefusedWhereItCanBeRead() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        tab.Query = new TableQuery(
            [],
            [
                new ColumnSort(0, Ordering.Ascending),
                new ColumnSort(Betrag, Ordering.Ascending),
                new ColumnSort(Datum, Ordering.Ascending),
            ]);
        view.Refresh();

        var asked = false;
        view.Asked = _ =>
        {
            asked = true;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Sort");
        Driver.Choose(editor, 0, Text);
        Driver.Apply(editor);

        asked.ShouldBeFalse();
        string.Join(" ", editor.OfType<TextBlock>().Select(block => block.Text ?? string.Empty))
            .ShouldContain("Remove one first");
    });

    [Fact]
    public Task TheBannerSaysWhetherTheTableIsWholeOrAView() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        Driver.Banner(view).ShouldContain("in file order");

        tab.Query = new TableQuery([new ColumnFilter(Betrag, FilterComparison.GreaterThan, ["15"])], []);
        view.Refresh();

        Driver.Banner(view).ShouldStartWith("Showing");
    });

    [Fact]
    public Task ASortInForceCanBeTakenOffAgain() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        tab.Query = new TableQuery([], [new ColumnSort(Betrag, Ordering.Descending)]);
        view.Refresh();

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        Driver.Remove(view, "descending");

        asked.ShouldNotBeNull().Sorts.ShouldBeEmpty();
    });
}
