using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GoBd.Reader.Ui.Controls;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The shortcuts that work anywhere in the reader's window: settings, the tabs, the overview.
/// </summary>
/// <remarks>
/// Pressed on the keyboard, the way a person presses them, rather than by calling the window: a
/// test that called the window would pass while the keys did nothing.
/// </remarks>
public sealed class WorkspaceKeysTests : HeadlessTest
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
    public Task TheSettingsShortcutOpensTheSettings() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            Driver.Press(window, ReaderKeys.Settings);
            window.OwnedWindows.OfType<SettingsWindow>().ShouldHaveSingleItem().Close();
        });
    });

    [Fact]
    public Task ATextFieldKeepsTheKeysItUsesItself() => Ui(() =>
    {
        var field = new TextBox();
        KeyEventArgs Pressed(Key key, KeyModifiers modifiers = KeyModifiers.None) =>
            new() { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };

        // Copy and select all as the platform's text fields take them.
        var hotkeys = Avalonia.Application.Current!.PlatformSettings.ShouldNotBeNull().HotkeyConfiguration;
        var copy = hotkeys.Copy[0];
        var selectAll = hotkeys.SelectAll[0];

        MainWindow.TextFieldOwns(field, Pressed(copy.Key, copy.KeyModifiers)).ShouldBeTrue("copy");
        MainWindow.TextFieldOwns(field, Pressed(selectAll.Key, selectAll.KeyModifiers)).ShouldBeTrue("select all");
        MainWindow.TextFieldOwns(field, Pressed(Key.Left)).ShouldBeTrue("moving the caret");
        MainWindow.TextFieldOwns(field, Pressed(Key.Enter)).ShouldBeTrue("Enter");
        MainWindow.TextFieldOwns(field, Pressed(Key.F6)).ShouldBeFalse("F6 types nothing");
        MainWindow.TextFieldOwns(field, Pressed(Key.W, Driver.Command)).ShouldBeFalse("closing a tab");
        MainWindow.TextFieldOwns(new Button(), Pressed(copy.Key, copy.KeyModifiers)).ShouldBeFalse("not a text field");
    });

    [Fact]
    public Task CloseTabClosesTheTableInFrontAndLeavesTheSummaryAndTheWindow() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"), ("u.csv", "B\r\n"));
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "T"));
        window.ShowTable(Driver.Table(window, "U"));

        Driver.Press(window, ReaderKeys.CloseTab);
        Driver.TabTitles(window).ShouldBe(["Summary", "T"]);
        Driver.FrontTitle(window).ShouldBe("T");
        Driver.FocusIsIn(window, Driver.View(window, "T"));

        Driver.Press(window, ReaderKeys.CloseTab);
        Driver.TabTitles(window).ShouldBe(["Summary"]);

        Driver.Press(window, ReaderKeys.CloseTab);
        Driver.TabTitles(window).ShouldBe(["Summary"]);
        window.IsVisible.ShouldBeTrue();
    });

    [Fact]
    public Task OneKeyPressAnsweredByTheMenuAndTheWindowClosesOneTab() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"), ("u.csv", "B\r\n"));
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "T"));
        window.ShowTable(Driver.Table(window, "U"));

        // As if the menu bar answered the shortcut and the window was handed it as well.
        var close = NativeMenu.GetMenu(window).ShouldNotBeNull().Items.OfType<NativeMenuItem>()
            .Single(item => Driver.Plain(item.Header) == "File").Menu.ShouldNotBeNull()
            .Items.OfType<NativeMenuItem>().Single(item => Driver.Plain(item.Header) == "Close Tab");
        close.Command.ShouldNotBeNull().Execute(null);
        Driver.Press(window, ReaderKeys.CloseTab);
        Driver.TabTitles(window).ShouldBe(["Summary", "T"]);

        // Pressed again, it is another press.
        Thread.Sleep(300);
        Driver.Press(window, ReaderKeys.CloseTab);
        Driver.TabTitles(window).ShouldBe(["Summary"]);
    });

    [Fact]
    public Task NextAndPreviousTabGoRound() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"), ("u.csv", "B\r\n"));
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "T"));
        window.ShowTable(Driver.Table(window, "U"));

        Driver.Press(window, ReaderKeys.TabAt(1));
        Driver.FrontTitle(window).ShouldBe("Summary");

        Driver.Press(window, ReaderKeys.PreviousTab, ReaderKeys.IsMac ? 1 : 0);
        Driver.FrontTitle(window).ShouldBe("U");

        Driver.Press(window, ReaderKeys.NextTab, ReaderKeys.IsMac ? 1 : 0);
        Driver.FrontTitle(window).ShouldBe("Summary");

        Driver.Press(window, ReaderKeys.NextTab);
        Driver.FrontTitle(window).ShouldBe("T");
        Driver.FocusIsIn(window, Driver.View(window, "T"));

        Driver.Press(window, ReaderKeys.TabAt(3));
        Driver.FrontTitle(window).ShouldBe("U");

        // There is no ninth tab, and asking for it changes nothing.
        Driver.Press(window, ReaderKeys.TabAt(9));
        Driver.FrontTitle(window).ShouldBe("U");
    });

    [Theory]
    [InlineData(ReportLanguage.English)]
    [InlineData(ReportLanguage.German)]
    public Task TheOverviewListsEveryShortcut(ReportLanguage language) => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            Driver.Press(window, ReaderKeys.Shortcuts);
            var overview = window.OwnedWindows.OfType<ShortcutsWindow>().ShouldHaveSingleItem();

            // Open, and told the language as every window beside the reader is.
            window.ApplyLanguage(language);

            overview.Title.ShouldBe(UiText.KeyboardShortcuts(language));
            foreach (var command in ReaderCommands.All.Where(command => command.Keys.Current.Count > 0))
            {
                if (command.Id.StartsWith("tab-", StringComparison.Ordinal))
                {
                    continue;
                }

                var keys = ReaderKeys.Describe(command.Keys.Current[0], language, ReaderKeys.IsMac);
                overview.Lines.ShouldContain(
                    line => line.Command == command.Label(language).TrimEnd('…') && line.Keys.Contains(keys, StringComparison.Ordinal),
                    command.Id);
            }

            overview.Lines.ShouldContain(line => line.Command == UiText.TabsByPlace(language));

            // Escape closes it, as it closes the other windows beside the reader.
            overview.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            window.OwnedWindows.OfType<ShortcutsWindow>().ShouldBeEmpty();
        }));
    });
}
