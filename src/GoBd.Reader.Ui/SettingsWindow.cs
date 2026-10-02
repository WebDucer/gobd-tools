using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>
/// Window allowing the user to select their appearance theme, zoom level and display language.
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly PreferencesStore store;
    private readonly Func<UserPreferences> preferences;
    private readonly Action<UserPreferences> onChanged;

    /// <summary>Set while the pickers are being made to show the preferences, which is not a choice.</summary>
    private bool showing;

    private readonly TextBlock heading = new() { FontSize = 18, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock appearanceSection = new() { FontSize = 13, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock themeLabel = new() { VerticalAlignment = VerticalAlignment.Center, Width = 140 };
    private readonly ComboBox themePicker = new() { MinWidth = 220 };
    private readonly TextBlock zoomLabel = new() { VerticalAlignment = VerticalAlignment.Center, Width = 140 };
    private readonly ComboBox zoomPicker = new() { MinWidth = 220 };

    private readonly TextBlock languageSection = new() { FontSize = 13, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock languageLabel = new() { VerticalAlignment = VerticalAlignment.Center, Width = 140 };
    private readonly ComboBox languagePicker = new() { MinWidth = 220 };

    private readonly TextBlock pathText = new() { FontSize = 11, Classes = { ReaderTheme.MutedClass }, TextWrapping = TextWrapping.Wrap };
    private readonly Button close = new() { HorizontalAlignment = HorizontalAlignment.Right };

    /// <summary>Creates the window.</summary>
    /// <param name="store">Where choices are kept.</param>
    /// <param name="preferences">The preferences as they stand, read again for every choice.</param>
    /// <param name="onChanged">Told of every choice, to put it into effect.</param>
    /// <remarks>
    /// The preferences are read again for every choice rather than kept here, because the zoom level
    /// and the navigator change outside this window too, and a choice made here must not put back
    /// what they were when it opened.
    /// </remarks>
    public SettingsWindow(PreferencesStore store, Func<UserPreferences> preferences, Action<UserPreferences> onChanged)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(onChanged);

        this.store = store;
        this.preferences = preferences;
        this.onChanged = onChanged;
        pathText.Text = store.FilePath;

        Icon = ReaderIcon.ForWindow();
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        foreach (var theme in Enum.GetValues<ThemePreference>())
        {
            themePicker.Items.Add(new ComboBoxItem { Tag = theme });
        }

        foreach (var step in ZoomLevel.Steps)
        {
            zoomPicker.Items.Add(new ComboBoxItem { Tag = step });
        }

        languagePicker.Items.Add(new ComboBoxItem { Tag = ReportLanguage.English });
        languagePicker.Items.Add(new ComboBoxItem { Tag = ReportLanguage.German });

        Show(preferences());

        themePicker.SelectionChanged += (_, _) =>
        {
            if ((themePicker.SelectedItem as ComboBoxItem)?.Tag is ThemePreference selected)
            {
                Commit(current => current with { Theme = selected });
            }
        };

        zoomPicker.SelectionChanged += (_, _) =>
        {
            if ((zoomPicker.SelectedItem as ComboBoxItem)?.Tag is int selected)
            {
                Commit(current => current with { Zoom = selected });
            }
        };

        languagePicker.SelectionChanged += (_, _) =>
        {
            if ((languagePicker.SelectedItem as ComboBoxItem)?.Tag is ReportLanguage selected)
            {
                Commit(current => current with { Language = selected });
                UpdateTexts();
            }
        };

        close.Click += (_, _) => Close();

        // Escape closes it, as it closes About: the choices are kept the moment they are made.
        // On macOS, Cmd+W closes it too, as it closes any window there.
        KeyDown += (_, args) =>
        {
            if (ReaderKeys.CloseDialog.Matches(args))
            {
                args.Handled = true;
                Close();
            }
        };

        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4),
        };
        separator[!Border.BackgroundProperty] = new DynamicResourceExtension(ReaderTheme.SurfaceBorder);

        Content = ReaderWindows.Scrolling(new Zoomed(new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 12,
            Width = 412,
            Children =
            {
                heading,
                appearanceSection,
                Row(themeLabel, themePicker),
                Row(zoomLabel, zoomPicker),
                separator,
                languageSection,
                Row(languageLabel, languagePicker),
                pathText,
                close,
            },
        }));
        ReaderWindows.OpenUsable(this);
        ReaderWindows.KeepFocusOnPickers(this);

        UpdateTexts();
    }

    /// <summary>Makes the pickers show the preferences as they stand, without choosing anything.</summary>
    internal void Show(UserPreferences shown)
    {
        ArgumentNullException.ThrowIfNull(shown);

        showing = true;
        themePicker.SelectedItem = themePicker.Items.OfType<ComboBoxItem>().First(item => Equals(item.Tag, shown.Theme));
        zoomPicker.SelectedItem = zoomPicker.Items.OfType<ComboBoxItem>()
            .OrderBy(item => Math.Abs((int)item.Tag! - shown.Zoom))
            .First();
        languagePicker.SelectedIndex = shown.Language == ReportLanguage.German ? 1 : 0;
        showing = false;
    }

    /// <summary>Keeps a choice: saved for the next launch, and applied to this one.</summary>
    private void Commit(Func<UserPreferences, UserPreferences> choose)
    {
        if (showing)
        {
            return;
        }

        var current = preferences();
        var next = choose(current);
        if (next == current)
        {
            return;
        }

        store.Save(next);
        onChanged(next);
    }

    private static StackPanel Row(TextBlock label, ComboBox picker) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children = { label, picker },
    };

    private void UpdateTexts()
    {
        var lang = preferences().Language;
        Title = UiText.SettingsTitle(lang);
        heading.Text = UiText.SettingsTitle(lang);
        appearanceSection.Text = UiText.AppearanceSection(lang);
        themeLabel.Text = UiText.ThemeLabel(lang);
        foreach (var item in themePicker.Items.OfType<ComboBoxItem>())
        {
            item.Content = (ThemePreference)item.Tag! switch
            {
                ThemePreference.Light => UiText.ThemeLight(lang),
                ThemePreference.Dark => UiText.ThemeDark(lang),
                ThemePreference.HighContrastDark => UiText.ThemeHighContrastDark(lang),
                ThemePreference.HighContrastLight => UiText.ThemeHighContrastLight(lang),
                _ => UiText.ThemeSystem(lang),
            };
        }

        zoomLabel.Text = UiText.ZoomLabel(lang);
        foreach (var item in zoomPicker.Items.OfType<ComboBoxItem>())
        {
            item.Content = UiText.Percent(lang, (int)item.Tag!);
        }

        languageSection.Text = UiText.LanguageSection(lang);
        languageLabel.Text = UiText.DisplayLanguageLabel(lang);
        ((ComboBoxItem)languagePicker.Items[0]!).Content = UiText.LangEnglish(lang);
        ((ComboBoxItem)languagePicker.Items[1]!).Content = UiText.LangGerman(lang);

        close.Content = UiText.Close(lang);
        AutomationProperties.SetName(themePicker, UiText.ThemeSelection(lang));
        AutomationProperties.SetName(zoomPicker, UiText.ZoomSelection(lang));
        AutomationProperties.SetName(languagePicker, UiText.LanguageSelection(lang));
        AutomationProperties.SetName(close, UiText.CloseSettings(lang));
    }
}
