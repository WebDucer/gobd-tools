using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GoBd.Reader.Ui.Controls;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Localisation;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The tables whose records refer to a record, offered where a person asks for them on it.
/// </summary>
/// <remarks>
/// Asked for with the mouse, the way a person asks: a test that called the window directly would
/// pass while the grid answered a right click with a menu built for some other record.
/// </remarks>
public sealed class ReferrerMenuTests : HeadlessTest
{
    private const string Tables = """
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
    public Task EachRequestOffersTheReferrersOfTheRecordItWasMadeOn() => Ui(() =>
    {
        using var harness = ExportHarness.Create(
            Tables,
            ("kontakte.csv", "K1\r\nK2\r\n"),
            ("rechnungen.csv", "R1;K1\r\n"));

        WithWindow(new MainWindow(harness.ExportPath, harness.Options), window =>
        {
            Wait(window.Reading);
            var kontakte = ExportHarness.Table(window.Session.ShouldNotBeNull(), "Kontakte");

            // No invoice names K2, so following it back finds nothing...
            ChooseReferrers(window, kontakte, ordinal: 2);
            Status(window).ShouldBe("'K2' matches no record of 'Rechnungen'. The reference does not resolve.");

            // ...and asking again, on K1, follows K1 rather than the record asked about before.
            ChooseReferrers(window, kontakte, ordinal: 1);
            Status(window).ShouldBe("Record 1 for 'K1'.");
        });
    });

    [Fact]
    public Task TheNavigatorAndAStatusAlreadyStandingFollowTheLanguage() => Ui(() =>
    {
        using var harness = ExportHarness.Create(
            Tables,
            ("kontakte.csv", "K1\r\nK2\r\n"),
            ("rechnungen.csv", "R1;K1\r\n"));

        RestoringApplication(() => WithWindow(new MainWindow(harness.ExportPath, harness.Options), window =>
        {
            Wait(window.Reading);
            var kontakte = ExportHarness.Table(window.Session.ShouldNotBeNull(), "Kontakte");
            ChooseReferrers(window, kontakte, ordinal: 2);

            window.ApplyLanguage(ReportLanguage.German);

            var headings = window.GetLogicalDescendants().OfType<TreeViewItem>().Select(item => item.Header as string).ToArray();
            headings.ShouldContain("Referenziert von");
            headings.ShouldNotContain("Referenced by");
            Status(window).ShouldBe("'K2' entspricht keinem Datensatz in 'Rechnungen'. Der Verweis lässt sich nicht auflösen.");

            // A cell that leads elsewhere says where in the new language too, though it was
            // realised in the old one.
            window.UpdateLayout();
            var rechnungen = ExportHarness.Table(window.Session.ShouldNotBeNull(), "Rechnungen");
            var link = View(window, rechnungen).GetVisualDescendants().OfType<TextBlock>().First(block => ToolTip.GetTip(block) is string);
            ToolTip.GetTip(link).ShouldBe("Verweist auf 'Kontakte'.");
        }));
    });

    /// <summary>
    /// Right-clicks a record beside its value, as a person does, and chooses the one table offered.
    /// </summary>
    private static void ChooseReferrers(MainWindow window, TableNode table, long ordinal)
    {
        window.ShowTable(table);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var grid = View(window, table).GetVisualDescendants().OfType<TableView>().First();
        var cell = grid.GetVisualDescendants()
            .OfType<TableViewCell>()
            .Where(candidate => candidate.DataContext is RecordRow row && row.Ordinal == ordinal)
            .OrderBy(candidate => candidate.Bounds.Left)
            .Last();

        var beside = cell.TranslatePoint(new Point(cell.Bounds.Width - 4, cell.Bounds.Height / 2), window).ShouldNotBeNull();
        window.MouseDown(beside, MouseButton.Right);
        window.MouseUp(beside, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();

        // Offered in a menu of its own over the window, among the record's actions, and chosen with
        // the mouse too, so the menu closes the way it does for a person.
        var offered = window.GetVisualDescendants()
            .OfType<MenuFlyoutPresenter>()
            .Single()
            .GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(item => (item.Header as string)?.StartsWith("Records in", StringComparison.Ordinal) == true);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var on = offered.TranslatePoint(new Point(offered.Bounds.Width / 2, offered.Bounds.Height / 2), window).ShouldNotBeNull();
        window.MouseDown(on, MouseButton.Left);
        window.MouseUp(on, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>What the tab of the referring table last said about a navigation.</summary>
    private static string Status(MainWindow window)
    {
        var rechnungen = ExportHarness.Table(window.Session.ShouldNotBeNull(), "Rechnungen");
        return Driver.Texts(View(window, rechnungen)).Single(text => text.Contains("'K", StringComparison.Ordinal));
    }

    private static TableTabView View(MainWindow window, TableNode table) =>
        window.GetLogicalDescendants().OfType<TableTabView>().First(view => view.Table == table);

    /// <summary>Waits for the reading, keeping the dispatcher running as the window's own loop would.</summary>
    private static void Wait(Task reading)
    {
        var clock = Stopwatch.StartNew();
        while (!reading.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(60))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        reading.IsCompletedSuccessfully.ShouldBeTrue();
    }
}
