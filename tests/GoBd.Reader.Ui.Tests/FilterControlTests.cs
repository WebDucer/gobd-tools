using Avalonia.Controls;
using GoBd.Reader.Data;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The filter control above the grid, driven as a person drives it.
/// </summary>
/// <remarks>
/// What a filter selects is tested without a window. What is tested here is the part only a
/// window has: that a column is offered the comparisons its declared type supports, that what a
/// person types is read under that table's own symbols, and that what cannot be read is refused
/// where they typed it rather than quietly ignored.
/// </remarks>
public sealed class FilterControlTests : HeadlessTest
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
    public Task ANumberIsReadUnderTheTablesOwnSymbols() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Filters");
        Driver.Choose(editor, 0, Betrag);
        Driver.Choose(editor, 1, 1);          // equals
        Driver.Type(editor, 0, "20,00");
        Driver.Apply(editor);

        var filter = asked.ShouldNotBeNull().Filters.ShouldHaveSingleItem();
        filter.Column.ShouldBe(Betrag);
        filter.Comparison.ShouldBe(FilterComparison.Equals);
        filter.Values.ShouldBe(["20.00"]);
    });

    [Fact]
    public Task WhatCannotBeReadIsRefusedWhereItWasTyped() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        var asked = false;
        view.Asked = _ =>
        {
            asked = true;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Filters");
        Driver.Choose(editor, 0, Betrag);
        Driver.Choose(editor, 1, 1);
        Driver.Type(editor, 0, "ungefähr hundert");
        Driver.Apply(editor);

        asked.ShouldBeFalse();

        var said = string.Join(" ", editor.OfType<TextBlock>().Select(block => block.Text ?? string.Empty));
        said.ShouldContain("is not a number");
        said.ShouldContain("','");
    });

    [Fact]
    public Task ADateIsReadUnderItsOwnMask() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Filters");
        Driver.Choose(editor, 0, Datum);
        Driver.Choose(editor, 1, 0);          // between
        Driver.Type(editor, 0, "02.01.2025");
        Driver.Type(editor, 1, "31.12.2025");
        Driver.Apply(editor);

        var filter = asked.ShouldNotBeNull().Filters.ShouldHaveSingleItem();
        filter.Comparison.ShouldBe(FilterComparison.Between);
        filter.Values.ShouldBe(["2025-01-02", "2025-12-31"]);
    });

    [Fact]
    public Task AColumnIsOfferedWhatItsDeclaredTypeSupports() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        var editor = Driver.Editor(view, "Filters");

        Driver.Choose(editor, 0, Text);
        var forText = Driver.Offered(editor, 1).Select(item => (string?)item.Content ?? string.Empty).ToArray();
        forText.ShouldContain("contains");
        forText.ShouldContain("matches (* and ?)");
        forText.ShouldNotContain("between");

        Driver.Choose(editor, 0, Betrag);
        var forNumber = Driver.Offered(editor, 1).Select(item => (string?)item.Content ?? string.Empty).ToArray();
        forNumber.ShouldContain("between");
        forNumber.ShouldNotContain("contains");
    });

    [Fact]
    public Task CaseIsIgnoredOnlyWhenAskedAndOnlyWhereItCouldBe() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        var editor = Driver.Editor(view, "Filters");

        // A number has no case, so the option is not there to tick.
        Driver.Choose(editor, 0, Betrag);
        editor.OfType<CheckBox>().Single().IsVisible.ShouldBeFalse();

        Driver.Choose(editor, 0, Text);
        editor.OfType<CheckBox>().Single().IsVisible.ShouldBeTrue();
        Driver.Choose(editor, 1, 0);          // contains
        Driver.Type(editor, 0, "miete");
        Driver.Tick(editor, "ignore case");
        Driver.Apply(editor);

        asked.ShouldNotBeNull().Filters.ShouldHaveSingleItem().IgnoreCase.ShouldBeTrue();
    });

    [Fact]
    public Task AFilterInForceCanBeTakenOffAgain() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        tab.Query = new TableQuery([new ColumnFilter(Betrag, FilterComparison.Equals, ["20.00"])], []);
        view.Refresh();

        TableQuery? asked = null;
        view.Asked = query =>
        {
            asked = query;
            return Task.CompletedTask;
        };

        Driver.Remove(view, "Betrag");

        asked.ShouldNotBeNull().Filters.ShouldBeEmpty();
    });

    [Fact]
    public Task AColumnTheReaderCannotFilterIsOfferedButNotChoosable() => Ui(() =>
    {
        // 39 digits is a number the standard permits and this reader cannot hold exactly. The
        // column still shows its values; it just cannot be filtered by.
        const string Wide = """
                    <Table>
                      <URL>t.csv</URL>
                      <Name>T</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                        <VariableColumn><Name>Riesig</Name><Numeric/></VariableColumn>
                      </VariableLength>
                    </Table>
            """;

        using var harness = ExportHarness.Create(
            Wide,
            ("t.csv", "A;123456789012345678901234567890123456789\r\nB;12\r\n"));
        var (view, _) = Driver.Show(harness);

        var editor = Driver.Editor(view, "Filters");
        var huge = Driver.Offered(editor, 0).First(item => (string?)item.Content == "Riesig");

        huge.IsEnabled.ShouldBeFalse();
        ((string?)ToolTip.GetTip(huge)).ShouldNotBeNull().ShouldContain("still shown");
    });
}
