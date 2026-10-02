using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The navigator from the keyboard and the mouse: moving reads, Enter or a click opens.
/// </summary>
/// <remarks>
/// While choosing an entry opened its table, arrowing past six tables opened five tabs on the way.
/// See the improve-reader-accessibility change's design.md D9.
/// </remarks>
public sealed class NavigatorKeysTests : HeadlessTest
{
    private const string Tables = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>u.csv</URL>
                  <Name>U</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

    [Fact]
    public Task TheArrowKeysMoveWithoutOpeningAndEnterOpensAndFocusesTheRecords() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"), ("u.csv", "B\r\n"));
        var window = Driver.Reader(harness);
        Driver.Settle(window);
        Entry(window, "Disk 1").Focus(NavigationMethod.Tab).ShouldBeTrue();

        // Down until the last table is selected, through every entry before it: the first press
        // selects the entry that has focus, as a tree does.
        for (var press = 0; press < 5 && Selected(window) != "U"; press++)
        {
            Driver.Press(window, new KeyGesture(Key.Down));
            Driver.TabTitles(window).ShouldBe(["Summary"]);
        }

        Selected(window).ShouldBe("U");
        Driver.TabTitles(window).ShouldBe(["Summary"]);
        Driver.FrontTitle(window).ShouldBe("Summary");

        Driver.Press(window, new KeyGesture(Key.Up));
        Driver.Press(window, ReaderKeys.OpenTable);

        Driver.TabTitles(window).ShouldBe(["Summary", "T"]);
        Driver.FrontTitle(window).ShouldBe("T");
        Driver.FocusIsIn(window, Driver.View(window, "T"));
    });

    [Fact]
    public Task AClickOnATablesEntryOpensIt() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"), ("u.csv", "B\r\n"));
        var window = Driver.Reader(harness);
        Driver.Settle(window);

        // Beside the name, on the entry's own background: the headless platform does not hit-test
        // text.
        var entry = Entry(window, "U");
        var header = entry.GetVisualDescendants().OfType<Border>().First(border => border.Background is not null && border.Bounds.Height > 0);
        var at = header.TranslatePoint(new Point(header.Bounds.Width - 4, header.Bounds.Height / 2), window).ShouldNotBeNull();
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Driver.Settle(window);

        Driver.FrontTitle(window).ShouldBe("U");
    });

    private static TreeViewItem Entry(MainWindow window, string header) =>
        window.GetVisualDescendants().OfType<TreeViewItem>().First(item => (string?)item.Header == header);

    private static string? Selected(MainWindow window) =>
        (window.GetVisualDescendants().OfType<TreeView>().Single().SelectedItem as TreeViewItem)?.Header as string;
}
