using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Zooming the whole interface, from the keyboard, the menu and Settings, and keeping the level.
/// </summary>
/// <remarks>
/// Read from what each window and editor actually draws at, not from the zoom level alone: a level
/// that changed while a window kept its size would pass a test that only asked the level. See the
/// improve-reader-accessibility change's design.md D5.
/// </remarks>
public sealed class ZoomTests : HeadlessTest
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
    public Task TheKeysStepTheZoomWithinItsRangeAndKeepIt() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            Drawn(window).ShouldBe(1);

            // The key that types "+" — or "=" on a US keyboard — and the number pad's plus alike.
            Driver.Press(window, new Avalonia.Input.KeyGesture(Avalonia.Input.Key.OemPlus, Driver.Command));
            Drawn(window).ShouldBe(1.1);

            Driver.Press(window, new Avalonia.Input.KeyGesture(Avalonia.Input.Key.Add, Driver.Command));
            Drawn(window).ShouldBe(1.25);

            for (var press = 0; press < 6; press++)
            {
                Driver.Press(window, ReaderKeys.ZoomIn);
            }

            Drawn(window).ShouldBe(2);
            preferences.Store.Load().Zoom.ShouldBe(200);

            Driver.Press(window, ReaderKeys.ZoomOut);
            Drawn(window).ShouldBe(1.75);

            Driver.Press(window, ReaderKeys.ActualSize);
            Drawn(window).ShouldBe(1);
            Driver.Press(window, ReaderKeys.ZoomOut);
            Drawn(window).ShouldBe(1);
            preferences.Store.Load().Zoom.ShouldBe(100);
        }));
    });

    [Fact]
    public Task SettingsChoosesTheZoomAndShowsItWhenItChangesElsewhere() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            window.ShowSettings();
            var settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
            var zoom = settings.GetLogicalDescendants().OfType<ComboBox>().ElementAt(1);

            zoom.SelectedItem = zoom.Items.OfType<ComboBoxItem>().Single(item => (int)item.Tag! == 150);
            Drawn(window).ShouldBe(1.5);
            Drawn(settings).ShouldBe(1.5);
            preferences.Store.Load().Zoom.ShouldBe(150);

            // Zoomed from the View menu while Settings is open: Settings shows it, and a choice
            // made there afterwards does not put the old level back.
            NativeMenu.GetMenu(window).ShouldNotBeNull().Items.OfType<NativeMenuItem>()
                .Single(item => Driver.Plain(item.Header) == "View").Menu.ShouldNotBeNull()
                .Items.OfType<NativeMenuItem>().Single(item => Driver.Plain(item.Header) == "Zoom In")
                .Command.ShouldNotBeNull().Execute(null);
            Driver.Settle(window);
            ((int)((ComboBoxItem)zoom.SelectedItem!).Tag!).ShouldBe(175);

            var theme = settings.GetLogicalDescendants().OfType<ComboBox>().First();
            theme.SelectedIndex = (int)ThemePreference.Light;
            preferences.Store.Load().ShouldBe(new UserPreferences(ThemePreference.Light, preferences.Store.Load().Language, 175));
            Drawn(window).ShouldBe(1.75);
            settings.Close();
        }));
    });

    [Fact]
    public Task AWindowOpensAtTheZoomLevelKept() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        preferences.Store.Save(UserPreferences.Default with { Zoom = 175 });

        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
            Drawn(window).ShouldBe(1.75)));
    });

    [Fact]
    public Task TheDialogsAndTheEditorsAreDrawnAtTheZoomLevel() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A;10,00\r\n"));
        using var preferences = new TemporaryPreferences();
        preferences.Store.Save(UserPreferences.Default with { Zoom = 150 });

        RestoringApplication(() =>
        {
            var window = Driver.Reader(harness, preferences.Store);
            window.ShowTable(Driver.Table(window, "T"));

            window.ShowAbout();
            window.ShowShortcuts();
            window.ShowSettings();

            // The licence, as the Help menu opens it.
            var help = NativeMenu.GetMenu(window).ShouldNotBeNull().Items.OfType<NativeMenuItem>()
                .Single(item => Driver.Plain(item.Header) == "Help").Menu.ShouldNotBeNull();
            help.Items.OfType<NativeMenuItem>().Single(item => Driver.Plain(item.Header) == "Licence").Command.ShouldNotBeNull().Execute(null);

            window.OwnedWindows.Count.ShouldBe(4);
            foreach (var shown in window.OwnedWindows.ToArray())
            {
                Drawn(shown).ShouldBe(1.5, shown.GetType().Name);

                // What does not fit the screen at this size scrolls, rather than being cut off.
                shown.GetVisualDescendants().OfType<ScrollViewer>().ShouldNotBeEmpty(shown.GetType().Name);
                shown.Close();
            }

            var editor = Driver.Editor(Driver.View(window, "T"), "Filters");
            editor[0].FindLogicalAncestorOfType<Zoomed>().ShouldNotBeNull().Scale.ShouldBe(1.5);
        });
    });

    [Fact]
    public Task TheRecordActionsAreDrawnAtTheZoomLevel() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A;10,00\r\n"));
        using var preferences = new TemporaryPreferences();
        preferences.Store.Save(UserPreferences.Default with { Zoom = 150 });

        RestoringApplication(() =>
        {
            var window = Driver.Reader(harness, preferences.Store);
            window.ShowTable(Driver.Table(window, "T"));
            Driver.Settle(window);
            Driver.View(window, "T").FocusRecords();
            Driver.Settle(window);

            Driver.Press(window, new Avalonia.Input.KeyGesture(Avalonia.Input.Key.Enter));
            var entries = window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().Single().GetVisualDescendants().OfType<MenuItem>().ToArray();
            entries.ShouldNotBeEmpty();
            entries.ShouldAllBe(entry => entry.FontSize == 21);
        });
    });

    /// <summary>The factor a window draws its content at.</summary>
    private static double Drawn(Window window)
    {
        Driver.Settle(window);
        return window.GetVisualDescendants().OfType<Zoomed>().First().Scale;
    }
}
