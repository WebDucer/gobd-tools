using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>
/// Window allowing the user to select their appearance theme and display language.
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly PreferencesStore store;
    private readonly Action<UserPreferences> onChanged;
    private UserPreferences current;

    private readonly TextBlock heading = new() { FontSize = 18, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock appearanceSection = new() { FontSize = 13, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock themeLabel = new() { VerticalAlignment = VerticalAlignment.Center, Width = 140 };
    private readonly ComboBox themePicker = new() { Width = 180 };

    private readonly TextBlock languageSection = new() { FontSize = 13, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock languageLabel = new() { VerticalAlignment = VerticalAlignment.Center, Width = 140 };
    private readonly ComboBox languagePicker = new() { Width = 180 };

    private readonly TextBlock pathText = new() { FontSize = 11, Opacity = 0.6, TextWrapping = TextWrapping.Wrap };
    private readonly Button close = new() { HorizontalAlignment = HorizontalAlignment.Right };

    public SettingsWindow(PreferencesStore store, UserPreferences preferences, Action<UserPreferences> onChanged)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(onChanged);

        this.store = store;
        current = preferences;
        this.onChanged = onChanged;
        pathText.Text = store.FilePath;

        Icon = ReaderIcon.ForWindow();
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        themePicker.Items.Add(new ComboBoxItem { Tag = ThemePreference.System });
        themePicker.Items.Add(new ComboBoxItem { Tag = ThemePreference.Light });
        themePicker.Items.Add(new ComboBoxItem { Tag = ThemePreference.Dark });
        themePicker.SelectedIndex = (int)current.Theme;

        languagePicker.Items.Add(new ComboBoxItem { Tag = ReportLanguage.English });
        languagePicker.Items.Add(new ComboBoxItem { Tag = ReportLanguage.German });
        languagePicker.SelectedIndex = current.Language == ReportLanguage.German ? 1 : 0;

        themePicker.SelectionChanged += (_, _) =>
        {
            if ((themePicker.SelectedItem as ComboBoxItem)?.Tag is ThemePreference selected && selected != current.Theme)
            {
                Commit(current with { Theme = selected });
            }
        };

        languagePicker.SelectionChanged += (_, _) =>
        {
            if ((languagePicker.SelectedItem as ComboBoxItem)?.Tag is ReportLanguage selected && selected != current.Language)
            {
                Commit(current with { Language = selected });
                UpdateTexts();
            }
        };

        close.Click += (_, _) => Close();

        // Escape closes it, as it closes About: the choices are kept the moment they are made.
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                Close();
            }
        };

        var separator = new Border
        {
            Height = 1,
            Opacity = 0.4,
            Margin = new Thickness(0, 4),
        };
        separator[!Border.BackgroundProperty] = new DynamicResourceExtension(ReaderTheme.Border);

        Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 12,
            Children =
            {
                heading,
                appearanceSection,
                Row(themeLabel, themePicker),
                separator,
                languageSection,
                Row(languageLabel, languagePicker),
                pathText,
                close,
            },
        };

        UpdateTexts();
    }

    /// <summary>Keeps a choice: saved for the next launch, and applied to this one.</summary>
    private void Commit(UserPreferences next)
    {
        current = next;
        store.Save(current);
        onChanged(current);
    }

    private static StackPanel Row(TextBlock label, ComboBox picker) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children = { label, picker },
    };

    private void UpdateTexts()
    {
        var lang = current.Language;
        Title = UiText.SettingsTitle(lang);
        heading.Text = UiText.SettingsTitle(lang);
        appearanceSection.Text = UiText.AppearanceSection(lang);
        themeLabel.Text = UiText.ThemeLabel(lang);
        ((ComboBoxItem)themePicker.Items[0]!).Content = UiText.ThemeSystem(lang);
        ((ComboBoxItem)themePicker.Items[1]!).Content = UiText.ThemeLight(lang);
        ((ComboBoxItem)themePicker.Items[2]!).Content = UiText.ThemeDark(lang);

        languageSection.Text = UiText.LanguageSection(lang);
        languageLabel.Text = UiText.DisplayLanguageLabel(lang);
        ((ComboBoxItem)languagePicker.Items[0]!).Content = UiText.LangEnglish(lang);
        ((ComboBoxItem)languagePicker.Items[1]!).Content = UiText.LangGerman(lang);

        close.Content = UiText.Close(lang);
        AutomationProperties.SetName(themePicker, UiText.ThemeSelection(lang));
        AutomationProperties.SetName(languagePicker, UiText.LanguageSelection(lang));
        AutomationProperties.SetName(close, UiText.CloseSettings(lang));
    }
}
