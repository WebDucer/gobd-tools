using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using GoBd.Reader.Ui.Controls;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

public sealed class SettingsWindowTests : HeadlessTest
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

    [Fact]
    public Task SettingsWindowOpensAndSwitchesThemeAndLanguage() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            // Verify settings window opens
            window.ShowSettings();
            var settings = window.OwnedWindows.OfType<SettingsWindow>().Single();

            var pickers = settings.GetLogicalDescendants().OfType<ComboBox>().ToArray();
            pickers.Length.ShouldBe(2);

            var themePicker = pickers[0];
            var langPicker = pickers[1];

            // Change Theme to Dark
            themePicker.SelectedIndex = (int)ThemePreference.Dark;
            Avalonia.Application.Current!.RequestedThemeVariant.ShouldBe(ThemeVariant.Dark);

            // Change Language to German
            langPicker.SelectedIndex = 1; // Deutsch

            // Check that StartPage updated to German
            var startTab = window.GetLogicalDescendants().OfType<TabItem>().First();
            startTab.Header.ShouldBe("Übersicht");

            // Verify persisted in preferences store
            var reloaded = preferences.Store.Load();
            reloaded.Theme.ShouldBe(ThemePreference.Dark);
            reloaded.Language.ShouldBe(ReportLanguage.German);

            // Close settings window
            var closeButton = settings.GetLogicalDescendants().OfType<Button>().Last();
            Driver.Click(closeButton);
            settings.IsVisible.ShouldBeFalse();
        }));
    });

    [Fact]
    public Task EscapeClosesTheSettings() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            window.ShowSettings();
            var settings = window.OwnedWindows.OfType<SettingsWindow>().Single();

            settings.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

            settings.IsVisible.ShouldBeFalse();
            window.OwnedWindows.OfType<SettingsWindow>().ShouldBeEmpty();
        });
    });

    [Fact]
    public Task SwitchingLanguageUpdatesOpenTableTabViews() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _, _) = Driver.Open(harness);

        view.SetLanguage(ReportLanguage.English);
        Driver.Texts(view).ShouldContain("2 records, in file order");
        Driver.Texts(view).ShouldContain("Sort");

        view.SetLanguage(ReportLanguage.German);
        Driver.Texts(view).ShouldContain("2 Datensätze, in Dateireihenfolge");
        Driver.Texts(view).ShouldContain("Sortierung");
        Driver.Texts(view).ShouldNotContain("2 records, in file order");
    });

    [Fact]
    public Task SettingsWindowRemainsSingletonWhenLanguageChanges() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            window.ShowSettings();
            var settings = window.OwnedWindows.OfType<SettingsWindow>().Single();

            var langPicker = settings.GetLogicalDescendants().OfType<ComboBox>().ToArray()[1];
            langPicker.SelectedIndex = 1; // German

            // Calling ShowSettings again must not spawn a duplicate window
            window.ShowSettings();
            window.OwnedWindows.OfType<SettingsWindow>().Count().ShouldBe(1);

            var closeButton = settings.GetLogicalDescendants().OfType<Button>().Last();
            Driver.Click(closeButton);
        }));
    });

    [Fact]
    public Task StartPagePromptTranslatesWhenNoExportIsOpen() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            window.ApplyLanguage(ReportLanguage.English);
            Driver.Texts(window).ShouldContain(UiText.OpenPrompt(ReportLanguage.English));

            window.ApplyLanguage(ReportLanguage.German);
            Driver.Texts(window).ShouldContain(UiText.OpenPrompt(ReportLanguage.German));
            Driver.Texts(window).ShouldNotContain(UiText.OpenPrompt(ReportLanguage.English));
        }));
    });

    [Fact]
    public Task TableTabViewRowLabelsDynamicallySizeToLongestLabel() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _, _) = Driver.Open(harness);

        // All labels share the exact same width based on the longest label
        var english = LabelWidths(view, ReportLanguage.English);
        english.Distinct().Count().ShouldBe(1);
        english[0].ShouldBeGreaterThan(0d);

        var german = LabelWidths(view, ReportLanguage.German);
        german.Distinct().Count().ShouldBe(1);

        // German labels ("Kennzahlen" / "Sortierung") are longer than English, so the column dynamically expands
        german[0].ShouldBeGreaterThan(english[0]);
    });

    /// <summary>The widths the control strip's three row labels are laid out at, in a language.</summary>
    private static double[] LabelWidths(TableTabView view, ReportLanguage language)
    {
        view.SetLanguage(language);

        // A shared column settles in two passes: the grids agree on its width once one pass is
        // done, and take it up in the next.
        view.UpdateLayout();
        view.UpdateLayout();

        string[] labels = [UiText.FiltersLabel(language), UiText.SortLabel(language), UiText.FiguresLabel(language)];
        var blocks = view.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Where(block => labels.Contains(block.Text))
            .ToArray();

        blocks.Length.ShouldBe(3);
        return [.. blocks.Select(block => block.Bounds.Width)];
    }
}
