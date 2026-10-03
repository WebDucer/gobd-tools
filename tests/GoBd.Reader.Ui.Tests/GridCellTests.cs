using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Media;
using Avalonia.VisualTree;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.Controls;
using GoBd.Reader.Ui.ViewModels;
using Semi.Avalonia;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// What the grid's cells say: where a record sits in the view, the number the file gives it, and
/// its values.
/// </summary>
/// <remarks>
/// The cells are bound without reflection, so that trimming cannot take away what they read. A
/// binding that resolved nothing would leave every cell empty while everything else kept passing,
/// so the realised cells themselves are read. See the change's design.md D4.
/// </remarks>
public sealed class GridCellTests : HeadlessTest
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

    [Fact]
    public Task EachRowShowsItsPositionItsRecordNumberAndItsValues() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        Cells(view, position: 1).ShouldBe("1|1|A|10,00");
        Cells(view, position: 3).ShouldBe("3|3|C|30,00");
    });

    [Fact]
    public Task InAViewThePositionAndTheRecordNumberPartCompany()
    {
        // Taken before the body, because the dispatcher the body runs on is not where xUnit put
        // the test's context.
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

            Cells(view, position: 1).ShouldBe("1|2|B|20,00");
        });
    }

    private const string Linked = """
                <Table>
                  <URL>kontakte.csv</URL>
                  <Name>Kontakte</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nr</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>rechnungen.csv</URL>
                  <Name>Rechnungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nr</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kontakte</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    [Fact]
    public Task AValueThatLeadsElsewhereIsColouredByItsClassRatherThanOnTheCell() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Linked, ("kontakte.csv", "K1\r\n"), ("rechnungen.csv", "R1;K1\r\nR2;K9\r\n"));
        var (view, _) = Driver.Show(harness, "Rechnungen");

        var link = Value(view, position: 1, "K1");
        link.Classes.ShouldContain(ReaderTheme.LinkClass);
        link.GetDiagnostic(TextBlock.ForegroundProperty).Priority.ShouldNotBe(BindingPriority.LocalValue);

        var dangling = Value(view, position: 2, "K9");
        dangling.Classes.ShouldContain(ReaderTheme.DanglingClass);
        dangling.GetDiagnostic(TextBlock.ForegroundProperty).Priority.ShouldNotBe(BindingPriority.LocalValue);
    });

    [Fact]
    public Task InHighContrastTheSelectedRowRecoloursItsLinks() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Linked, ("kontakte.csv", "K1\r\n"), ("rechnungen.csv", "R1;K1\r\nR2;K9\r\n"));
        RestoringApplication(() =>
        {
            Application.Current!.RequestedThemeVariant = SemiTheme.Aquatic;
            var (view, _) = Driver.Show(harness, "Rechnungen");

            // Hotlight on the window, as a link is drawn in high contrast...
            Colour(Value(view, position: 1, "K1")).ShouldBe(Color.Parse("#75E9FC"));
            Colour(Value(view, position: 2, "K9")).ShouldBe(Colors.White);

            // ...and HighlightText once its row is drawn in the selection colour, where Hotlight
            // would measure 1:1.
            view.GetVisualDescendants().OfType<TableView>().Single().SelectedIndex = 0;
            view.UpdateLayout();
            Colour(Value(view, position: 1, "K1")).ShouldBe(Color.Parse("#263B50"));
            Colour(Value(view, position: 2, "K9")).ShouldBe(Colors.White);
        });
    });

    /// <summary>The text block a realised row shows a value in.</summary>
    private static TextBlock Value(TableTabView view, long position, string value)
    {
        view.UpdateLayout();
        return view.GetVisualDescendants()
            .OfType<TableViewRow>()
            .First(candidate => candidate.DataContext is RecordRow record && record.Position == position)
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Text == value);
    }

    private static Color Colour(TextBlock block) =>
        block.Foreground.ShouldBeAssignableTo<ISolidColorBrush>().ShouldNotBeNull().Color;

    /// <summary>The texts of one realised row's cells, left to right, joined by a bar.</summary>
    private static string Cells(TableTabView view, long position)
    {
        view.UpdateLayout();

        var row = view.GetVisualDescendants()
            .OfType<TableViewRow>()
            .First(candidate => candidate.DataContext is RecordRow record && record.Position == position);

        var texts = row.GetVisualDescendants()
            .OfType<TableViewCell>()
            .OrderBy(cell => cell.Bounds.Left)
            .Select(cell => cell.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty);

        return string.Join("|", texts);
    }
}
