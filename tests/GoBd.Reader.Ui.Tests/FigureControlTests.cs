using Avalonia.Controls;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The figures control above the grid and the strip beneath it, driven as a person drives them.
/// </summary>
/// <remarks>
/// Whether a figure means anything is the person's to judge — the sum of amounts is a figure and
/// the sum of account numbers is not — so what is tested here is that they are offered what their
/// column's type allows, and that what comes back is stated as exactly as it was computed.
/// </remarks>
public sealed class FigureControlTests : HeadlessTest
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
    private const int Text = 3;

    [Fact]
    public Task ChoosingAFigureAsksForIt() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        IReadOnlyList<ColumnFigure>? totalled = null;
        view.Totalled = chosen =>
        {
            totalled = chosen;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Figures");
        Driver.Choose(editor, 0, Betrag);
        Driver.Choose(editor, 1, 0);          // sum
        Driver.Apply(editor);

        var figure = totalled.ShouldNotBeNull().ShouldHaveSingleItem();
        figure.Column.ShouldBe(Betrag);
        figure.Figure.ShouldBe(Figure.Sum);
    });

    [Fact]
    public Task AColumnIsOfferedOnlyTheFiguresItsTypeAllows() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        var editor = Driver.Editor(view, "Figures");

        Driver.Choose(editor, 0, Text);
        var forText = Driver.Offered(editor, 1).Select(item => (string?)item.Content ?? string.Empty).ToArray();
        forText.ShouldContain("count");
        forText.ShouldContain("distinct");
        forText.ShouldNotContain("sum");

        Driver.Choose(editor, 0, Betrag);
        var forNumber = Driver.Offered(editor, 1).Select(item => (string?)item.Content ?? string.Empty).ToArray();
        forNumber.ShouldContain("sum");
        forNumber.ShouldContain("average");
    });

    [Fact]
    public Task TheStripSaysWhatEachFigureCameTo() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        view.ShowFigures(
        [
            new FigureReading(new ColumnFigure(Betrag, Figure.Sum), 1234567.89m, 0, Exact: true, Rounded: false),
            new FigureReading(new ColumnFigure(Betrag, Figure.Average), 20.00m, 1, Exact: true, Rounded: true),
            new FigureReading(new ColumnFigure(Betrag, Figure.Maximum), null, 0, Exact: false, Rounded: false),
        ]);

        var said = string.Join(" | ", Driver.Texts(view));

        // Written the way the export writes its values, not the way the machine would.
        said.ShouldContain("Betrag sum 1.234.567,89");
        said.ShouldContain("Betrag average 20,00 (rounded), 1 without a value");
        said.ShouldContain("cannot be computed exactly");
    });

    [Fact]
    public Task ADateFigureIsWrittenInTheColumnsOwnMask() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        view.ShowFigures(
        [
            new FigureReading(new ColumnFigure(2, Figure.Minimum), new DateOnly(2025, 1, 31), 0, true, false),
        ]);

        string.Join(" | ", Driver.Texts(view)).ShouldContain("Datum min 31.01.2025");
    });

    [Fact]
    public Task AFigureInForceCanBeTakenOffAgain() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        tab.Figures = [new ColumnFigure(Betrag, Figure.Sum)];
        view.Refresh();

        IReadOnlyList<ColumnFigure>? totalled = null;
        view.Totalled = chosen =>
        {
            totalled = chosen;
            return Task.CompletedTask;
        };

        Driver.Remove(view, "Betrag sum");

        totalled.ShouldNotBeNull().ShouldBeEmpty();
    });
}
