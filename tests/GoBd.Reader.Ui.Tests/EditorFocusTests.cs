using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// An editor filled in from the keyboard alone: every picker chosen from its list, and Apply
/// reached, without focus leaving the editor.
/// </summary>
/// <remarks>
/// A picker gives focus to its list when the list opens, and Avalonia gives it back only to a
/// picker that can be typed in. So once a column had been chosen, focus was on a list that had
/// closed, and on macOS, where an editor is a window of its own, it left the editor altogether.
/// </remarks>
public sealed class EditorFocusTests : HeadlessTest
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

    [Fact]
    public Task TheSortEditorIsFilledInAndAppliedFromTheKeyboardAlone() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A;10,00\r\nB;20,00\r\n"));
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "T"));
        var view = Driver.View(window, "T");
        Driver.Settle(window);

        view.OpenSortEditor();
        Driver.Settle(window);
        var editor = window.GetVisualDescendants().OfType<FlyoutPresenter>().Single();
        var pickers = editor.GetVisualDescendants().OfType<ComboBox>().ToArray();
        Driver.Focused(window).ShouldBe(pickers[0]);

        // The column, from its list: open it, move to Betrag, take it.
        Choose(window, pickers[0], downs: 2);
        pickers[0].SelectedIndex.ShouldBe(1);
        Driver.Focused(window).ShouldBe(pickers[0]);

        // On to the direction, chosen from its list too.
        Driver.Press(window, new KeyGesture(Key.Tab));
        Driver.Focused(window).ShouldBe(pickers[1]);
        Choose(window, pickers[1], downs: 1);
        pickers[1].SelectedIndex.ShouldBe(1);
        Driver.Focused(window).ShouldBe(pickers[1]);

        // On to Apply, and pressed.
        Driver.Press(window, new KeyGesture(Key.Tab));
        Driver.Focused(window).ShouldBeOfType<Button>().Content.ShouldBe("Apply");
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Until(window, () => !view.InFileOrder);
    });

    [Fact]
    public Task TheFilterEditorIsFilledInAndAppliedFromTheKeyboardAlone() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A;10,00\r\nB;20,00\r\n"));
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "T"));
        var view = Driver.View(window, "T");
        Driver.Settle(window);

        Driver.Press(window, ReaderKeys.AddFilter);
        var editor = window.GetVisualDescendants().OfType<FlyoutPresenter>().Single();
        var pickers = editor.GetVisualDescendants().OfType<ComboBox>().ToArray();

        // Betrag, then "greater than" — the sixth comparison a number is offered.
        Choose(window, pickers[0], downs: 2);
        Driver.Press(window, new KeyGesture(Key.Tab));
        Driver.Focused(window).ShouldBe(pickers[1]);
        Choose(window, pickers[1], downs: 5);
        pickers[1].SelectedIndex.ShouldBe(5);

        // The value, typed, and Enter applies.
        Driver.Press(window, new KeyGesture(Key.Tab));
        Driver.Focused(window).ShouldBeOfType<TextBox>();
        window.KeyTextInput("15");
        Driver.Press(window, new KeyGesture(Key.Enter));

        Driver.Until(window, () => !view.InFileOrder);
        Driver.Banner(view).ShouldBe("Showing 1 of 2 records");
    });

    [Fact]
    public Task SettingsAreChosenFromTheKeyboardAlone() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            window.ShowSettings();
            var settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
            Driver.Settle(settings);
            var pickers = settings.GetVisualDescendants().OfType<ComboBox>().ToArray();
            Driver.Focused(settings).ShouldBe(pickers[0]);

            // The second zoom step, from the zoom picker's list.
            Driver.Press(settings, new KeyGesture(Key.Tab));
            Driver.Focused(settings).ShouldBe(pickers[1]);
            Choose(settings, pickers[1], downs: 1);
            Driver.Focused(settings).ShouldBe(pickers[1]);
            preferences.Store.Load().Zoom.ShouldBe(110);

            Driver.Press(settings, new KeyGesture(Key.Tab));
            Driver.Focused(settings).ShouldBe(pickers[2]);
            settings.Close();
        }));
    });

    [Fact]
    public Task TabStaysInsideTheEditor() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A;10,00\r\n"));
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "T"));
        var view = Driver.View(window, "T");
        Driver.Settle(window);

        view.OpenFigureEditor();
        Driver.Settle(window);
        var editor = window.GetVisualDescendants().OfType<FlyoutPresenter>().Single();

        for (var press = 0; press < 8; press++)
        {
            Driver.Press(window, new KeyGesture(Key.Tab));
            Driver.FocusIsIn(window, editor);
        }
    });

    /// <summary>Opens a picker's list from the keyboard, moves down it, and takes the item.</summary>
    private static void Choose(Avalonia.Controls.Window window, ComboBox picker, int downs)
    {
        Driver.Press(window, new KeyGesture(Key.Enter));
        picker.IsDropDownOpen.ShouldBeTrue();
        for (var down = 0; down < downs; down++)
        {
            Driver.Press(window, new KeyGesture(Key.Down));
        }

        Driver.Press(window, new KeyGesture(Key.Enter));
        picker.IsDropDownOpen.ShouldBeFalse();
    }
}
