using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Moving between areas with F6, asking something of a table from the keyboard, stepping through
/// referring records, and closing the windows beside the reader.
/// </summary>
/// <remarks>
/// See the improve-reader-accessibility change's design.md D7, D17 and D18.
/// </remarks>
public sealed class TableKeysTests : HeadlessTest
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
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kontakte</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private static ExportHarness Export() => ExportHarness.Create(
        Tables,
        ("kontakte.csv", "K1\r\nK2\r\n"),
        ("rechnungen.csv", "R1;K1;10,00\r\nR2;K1;20,00\r\nR3;K2;30,00\r\n"));

    [Fact]
    public Task F6MovesBetweenTheNavigatorTheToolbarAndTheRecords() => Ui(() =>
    {
        using var harness = Export();
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() =>
        {
            var window = Driver.Reader(harness, preferences.Store);
            window.ShowTable(Driver.Table(window, "Rechnungen"));
            var view = Driver.View(window, "Rechnungen");
            view.FocusRecords();
            Driver.Settle(window);

            Driver.Press(window, ReaderKeys.NextArea);
            Driver.Focused(window).ShouldBeOfType<TreeViewItem>();

            Driver.Press(window, ReaderKeys.NextArea);
            Driver.FocusIsIn(window, view.Toolbar);

            Driver.Press(window, ReaderKeys.NextArea);
            Driver.FocusedRecord(window).ShouldNotBeNull();

            Driver.Press(window, ReaderKeys.PreviousArea);
            Driver.FocusIsIn(window, view.Toolbar);

            // A collapsed navigator is not one of the places.
            Driver.Press(window, ReaderKeys.ToggleNavigator);
            view.FocusRecords();
            Driver.Settle(window);
            Driver.Press(window, ReaderKeys.NextArea);
            Driver.FocusIsIn(window, view.Toolbar);
            Driver.Press(window, ReaderKeys.NextArea);
            Driver.FocusedRecord(window).ShouldNotBeNull();
            Driver.Press(window, ReaderKeys.ToggleNavigator);

            // With the summary in front, the summary is the place.
            Driver.Press(window, ReaderKeys.TabAt(1));
            Driver.Press(window, ReaderKeys.NextArea);
            Driver.Focused(window).ShouldBeOfType<TreeViewItem>();
            Driver.Press(window, ReaderKeys.NextArea);
            Driver.Focused(window).ShouldBeAssignableTo<Controls.StartPageView>();
        });
    });

    [Fact]
    public Task TheFilterEditorIsOpenedFilledAndAppliedFromTheKeyboard() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "Rechnungen"));
        var view = Driver.View(window, "Rechnungen");
        Driver.Settle(window);

        Driver.Press(window, ReaderKeys.AddFilter);
        var editor = Editor(window);
        var pickers = editor.OfType<ComboBox>().ToArray();
        Driver.Focused(window).ShouldBe(pickers[0]);

        pickers[0].SelectedIndex = 2;          // Betrag
        pickers[1].SelectedIndex = 5;          // greater than
        var value = editor.OfType<TextBox>().First();
        value.Text = "15";
        value.Focus();
        Driver.Settle(window);

        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Until(window, () => !view.InFileOrder);
        Driver.Banner(view).ShouldBe("Showing 2 of 3 records");

        // Escape closes it, and focus goes back to the button that opened it.
        Driver.Press(window, new KeyGesture(Key.Escape));
        window.GetVisualDescendants().OfType<FlyoutPresenter>().ShouldBeEmpty();
        Driver.Focused(window).ShouldBeOfType<Button>().Content.ShouldBe("+ Filter");

        // Back to file order, from the Table menu.
        Menu(window, "Table", "Back to File Order").Command.ShouldNotBeNull().Execute(null);
        Driver.Until(window, () => view.InFileOrder);
    });

    [Fact]
    public Task TheTableMenuOpensTheSortAndFigureEditors() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);

        // Nothing to ask of the summary.
        Menu(window, "Table", "Add Sort Column…").Command.ShouldNotBeNull().CanExecute(null).ShouldBeFalse();

        window.ShowTable(Driver.Table(window, "Rechnungen"));
        Driver.Settle(window);

        Menu(window, "Table", "Add Sort Column…").Command.ShouldNotBeNull().Execute(null);
        Driver.Settle(window);
        Driver.Texts(window.GetVisualDescendants().OfType<FlyoutPresenter>().Single()).ShouldContain("Sort by");
        Driver.Press(window, new KeyGesture(Key.Escape));

        Menu(window, "Table", "Add Figure…").Command.ShouldNotBeNull().Execute(null);
        Driver.Settle(window);
        Driver.Texts(window.GetVisualDescendants().OfType<FlyoutPresenter>().Single()).ShouldContain("Figure");
    });

    [Fact]
    public Task TheStepKeysMoveThroughReferringRecordsOnlyDuringAWalk() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "Kontakte"));
        Driver.Settle(window);
        Driver.View(window, "Kontakte").FocusRecords();
        Driver.Settle(window);

        // K1's referring records, from its actions.
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Focused(window).ShouldBeOfType<MenuItem>().Header.ShouldBe("Records in 'Rechnungen' referring to this");
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Settle(window);
        Driver.FrontTitle(window).ShouldBe("Rechnungen");
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);

        Driver.Press(window, ReaderKeys.NextReferring);
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(2);

        // There is no third, and asking for one changes nothing.
        Driver.Press(window, ReaderKeys.NextReferring);
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(2);

        Driver.Press(window, ReaderKeys.PreviousReferring);
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);

        // Leaving the table ends the walk; back in it, the keys do nothing.
        Driver.Press(window, ReaderKeys.TabAt(1));
        Driver.Press(window, ReaderKeys.TabAt(3));
        Driver.FrontTitle(window).ShouldBe("Rechnungen");
        Driver.Press(window, ReaderKeys.NextReferring);
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);
    });

    [Fact]
    public Task TheWindowsBesideTheReaderCloseFromTheKeyboard() => Ui(() =>
    {
        // Escape everywhere; on macOS Cmd+W as well, as for any window there.
        ReaderKeys.CloseDialog.Mac.Select(gesture => gesture.ToString()).ShouldBe(["Escape", "Cmd+W"], ignoreOrder: true);
        ReaderKeys.CloseDialog.Other.Select(gesture => gesture.ToString()).ShouldBe(["Escape"]);

        using var preferences = new TemporaryPreferences();
        WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            foreach (var gesture in ReaderKeys.CloseDialog.Current)
            {
                window.ShowSettings();
                window.ShowAbout();
                window.ShowShortcuts();
                Menu(window, "Help", "Licence").Command.ShouldNotBeNull().Execute(null);
                window.OwnedWindows.Count.ShouldBe(4);

                // Each in turn brought forward, as a person brings forward the window they close.
                foreach (var dialog in window.OwnedWindows.ToArray())
                {
                    dialog.Activate();
                    Driver.Settle(dialog);
                    Driver.Press(dialog, gesture);
                }

                window.OwnedWindows.ShouldBeEmpty($"after {gesture}");
            }
        });
    });

    [Fact]
    public Task AWindowBesideTheReaderTakesFocusWhenItOpens() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            foreach (var open in new Action[]
            {
                window.ShowSettings,
                window.ShowAbout,
                window.ShowShortcuts,
                () => Menu(window, "Help", "Licence").Command.ShouldNotBeNull().Execute(null),
            })
            {
                open();
                var dialog = window.OwnedWindows.ShouldHaveSingleItem();
                Driver.Settle(dialog);
                var focused = Driver.Focused(dialog).ShouldBeAssignableTo<Visual>().ShouldNotBeNull(dialog.GetType().Name);
                dialog.IsVisualAncestorOf(focused).ShouldBeTrue(dialog.GetType().Name);
                dialog.Close();
                Driver.Settle(window);
            }
        });
    });

    /// <summary>What the editor open over the window shows.</summary>
    private static IReadOnlyList<Control> Editor(MainWindow window) =>
        [.. window.GetVisualDescendants().OfType<FlyoutPresenter>().Single().GetLogicalDescendants().OfType<Control>()];

    private static NativeMenuItem Menu(MainWindow window, string menu, string entry) =>
        NativeMenu.GetMenu(window).ShouldNotBeNull().Items.OfType<NativeMenuItem>()
            .Single(item => Driver.Plain(item.Header) == menu).Menu.ShouldNotBeNull()
            .Items.OfType<NativeMenuItem>().Single(item => Driver.Plain(item.Header) == entry);
}
