using GoBd.Reader.Ui.Controls;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// What the reader says while it is working, and what it says it cannot do.
/// </summary>
/// <remarks>
/// Both are things a person reads rather than acts on, and both are easy to get wrong in a way no
/// view-model test would catch: a notice that is never shown, or a limit of the reader listed
/// among the export's defects.
/// </remarks>
public sealed class SummaryAndNoticeTests : HeadlessTest
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

    private const string Records = "A;10,00\r\nB;20,00\r\n";

    /// <summary>A table holding a number longer than the reader computes with exactly.</summary>
    private const string Wide = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Riesig</Name><Numeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

    [Fact]
    public Task ATabSaysWhileItIsPreparingAView() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        Driver.Texts(view).ShouldNotContain("Preparing…");

        view.Preparing(true);
        Driver.Texts(view).ShouldContain("Preparing…");

        view.Preparing(false);

        // Gone again. What a person must never see is an empty grid while the store works: that
        // reads as a table with no records in it.
        Driver.Texts(view).ShouldNotContain("Preparing…");
    });

    [Fact]
    public Task ALiftedFilterIsSaidInTheTabItHappenedIn() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, tab) = Driver.Show(harness);

        tab.Notice = "Filter removed to show record 2.";
        view.Refresh();

        Driver.Texts(view).ShouldContain("Filter removed to show record 2.");
    });

    [Fact]
    public Task TheSummaryListsTheReadersLimitsApartFromTheExportsDefects() => Ui(() =>
    {
        using var harness = ExportHarness.Create(
            Wide,
            ("t.csv", "A;123456789012345678901234567890123456789\r\nB;12\r\n"));

        var session = harness.Read();
        var summary = new StartPageView();
        summary.Show(session.Reading, harness.ExportPath);

        var said = Driver.Texts(summary);

        // The export is within the standard, so it is conformant and has no findings…
        said.ShouldContain(text => text.StartsWith("Conformant", StringComparison.Ordinal));

        // …and what the reader cannot do with it is said under its own heading.
        said.ShouldContain("What this reader cannot do with it");
        said.ShouldContain(text => text.Contains("no filter, sort or figure", StringComparison.Ordinal));
    });

    [Fact]
    public Task AnExportTheReaderCanWorkWithEntirelyShowsNoSuchSection() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var session = harness.Read();

        var summary = new StartPageView();
        summary.Show(session.Reading, harness.ExportPath);

        Driver.Texts(summary).ShouldNotContain("What this reader cannot do with it");
    });
}
