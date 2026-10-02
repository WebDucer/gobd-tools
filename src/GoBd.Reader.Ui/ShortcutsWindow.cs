using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>
/// Every shortcut the reader offers, for the platform it runs on and in the display language.
/// </summary>
/// <remarks>
/// Built from the command table, so it cannot list a shortcut the window does not answer, or
/// leave out one it does. Grouped by where a shortcut applies, because Enter means one thing in
/// the navigator and another in a table's records. See the improve-reader-accessibility change's
/// design.md D7.
/// </remarks>
internal sealed class ShortcutsWindow : Window, ILocalized
{
    private readonly StackPanel content = new() { Spacing = 6, Margin = new Thickness(24, 20) };

    /// <summary>What scrolls the list, which takes focus so the keys can scroll it.</summary>
    private readonly ScrollViewer scroller = new() { Focusable = true };

    /// <summary>Creates the window, in a display language.</summary>
    public ShortcutsWindow(ReportLanguage language)
    {
        scroller.Content = content;
        Icon = ReaderIcon.ForWindow();
        Width = 640;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new Zoomed(scroller);
        ReaderWindows.OpenUsable(this);

        KeyDown += (_, args) =>
        {
            if (ReaderKeys.CloseDialog.Matches(args))
            {
                args.Handled = true;
                Close();
            }
        };

        SetLanguage(language);
    }

    /// <summary>What the window lists, as one line per shortcut, for a test to read.</summary>
    internal IReadOnlyList<(string Command, string Keys)> Lines { get; private set; } = [];

    /// <inheritdoc />
    public void SetLanguage(ReportLanguage language)
    {
        Title = UiText.KeyboardShortcuts(language);
        AutomationProperties.SetName(scroller, UiText.KeyboardShortcuts(language));
        content.Children.Clear();
        content.Children.Add(new TextBlock
        {
            Text = UiText.KeyboardShortcuts(language),
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
        });

        var lines = new List<(string, string)>();
        foreach (var (scope, heading) in new (CommandScope, Func<ReportLanguage, string>)[]
        {
            (CommandScope.Global, UiText.ShortcutsEverywhere),
            (CommandScope.Records, UiText.ShortcutsInRecords),
            (CommandScope.Navigator, UiText.ShortcutsInNavigator),
        })
        {
            content.Children.Add(new TextBlock
            {
                Text = heading(language),
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 14, 0, 2),
            });

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            foreach (var (command, keys) in Listed(scope, language))
            {
                var row = grid.RowDefinitions.Count;
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Add(grid, row, 0, command, muted: false);
                Add(grid, row, 1, keys, muted: true);
                lines.Add((command, keys));
            }

            content.Children.Add(grid);
        }

        content.Children.Add(new TextBlock
        {
            Text = UiText.ShortcutsDialogs(language),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0),
            Classes = { ReaderTheme.MutedClass },
        });

        Lines = lines;
    }

    /// <summary>
    /// The commands of a scope that have a shortcut, each with its shortcuts as the platform writes
    /// them.
    /// </summary>
    /// <remarks>
    /// The nine tabs by place are one line rather than nine: they are one idea.
    /// </remarks>
    internal static IEnumerable<(string Command, string Keys)> Listed(CommandScope scope, ReportLanguage language)
    {
        var mac = ReaderKeys.IsMac;
        foreach (var definition in ReaderCommands.All.Where(command => command.Scope == scope && command.Keys.Current.Count > 0))
        {
            if (definition.Id == ReaderCommands.TabAt(1))
            {
                var first = ReaderKeys.Describe(definition.Keys.Current[0], language, mac);
                var last = ReaderKeys.Describe(ReaderCommands.Get(ReaderCommands.TabAt(9)).Keys.Current[0], language, mac);
                yield return (UiText.TabsByPlace(language), $"{first} … {last}");
                continue;
            }

            if (definition.Id.StartsWith("tab-", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (
                definition.Label(language).TrimEnd('…'),
                string.Join(" / ", definition.Keys.Current.Select(gesture => ReaderKeys.Describe(gesture, language, mac))));
        }
    }

    private static void Add(Grid grid, int row, int column, string text, bool muted)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, column == 0 ? 24 : 0, 3),
            HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
        };

        if (muted)
        {
            block.Classes.Add(ReaderTheme.MutedClass);
        }

        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }
}
