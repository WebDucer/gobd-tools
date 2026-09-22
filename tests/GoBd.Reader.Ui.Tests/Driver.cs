using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using GoBd.Reader.Ui.Controls;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Drives a table's tab as a person drives it: opening editors, choosing, typing and applying.
/// </summary>
/// <remarks>
/// Controls are found through the logical tree by what they say, not by a field a test was handed.
/// A test that reached into the view's private controls would keep passing after the strip stopped
/// showing them; one that has to find "Sort" and the button beside it fails when a person could no
/// longer find them either.
/// </remarks>
internal static class Driver
{
    /// <summary>The tab view for one table of an export, in a window that draws nothing.</summary>
    public static (TableTabView View, TableTab Tab) Show(ExportHarness harness, string identity = "T")
    {
        var (view, tab, _) = Open(harness, identity);
        return (view, tab);
    }

    /// <summary>
    /// The same, with the open export, for a test that needs to build a view the way the window
    /// does.
    /// </summary>
    /// <remarks>
    /// Reading an export twice would open two stores for one harness, so whoever needs the
    /// session takes the one this opened rather than opening another.
    /// </remarks>
    public static (TableTabView View, TableTab Tab, ReaderSession Session) Open(
        ExportHarness harness,
        string identity = "T")
    {
        ArgumentNullException.ThrowIfNull(harness);

        var session = harness.Read();
        var table = ExportHarness.Table(session, identity);
        var tab = new ReaderTabs(session).Open(table);
        var view = new TableTabView(session, tab, (_, _, _) => { }, (_, _, _) => { }, _ => { });

        var window = new Window { Content = view, Width = 1000, Height = 700 };
        harness.Track(window);
        window.Show();

        return (view, tab, session);
    }

    /// <summary>
    /// Opens one of the control strip's editors and returns what it is showing.
    /// </summary>
    /// <remarks>
    /// Each row of the strip is a label, what is in force, and the button that adds another. The
    /// editor itself lives in that button's flyout, which is where a person meets it.
    /// </remarks>
    public static IReadOnlyList<Control> Editor(TableTabView view, string row)
    {
        ArgumentNullException.ThrowIfNull(view);

        var panel = view.GetLogicalDescendants()
            .OfType<StackPanel>()
            .First(candidate => candidate.Children.OfType<TextBlock>().Any(label => label.Text == row));

        var adder = panel.Children.OfType<Button>().Last();
        Click(adder);

        var flyout = adder.Flyout.ShouldBeOfType<Flyout>();
        var content = flyout.Content.ShouldBeOfType<StackPanel>();
        return [.. content.GetLogicalDescendants().OfType<Control>()];
    }

    /// <summary>Chooses an item in one of an editor's pickers.</summary>
    public static void Choose(IReadOnlyList<Control> editor, int picker, int item)
    {
        ArgumentNullException.ThrowIfNull(editor);
        editor.OfType<ComboBox>().ElementAt(picker).SelectedIndex = item;
    }

    /// <summary>The items one of an editor's pickers is offering.</summary>
    public static IReadOnlyList<ComboBoxItem> Offered(IReadOnlyList<Control> editor, int picker)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return [.. editor.OfType<ComboBox>().ElementAt(picker).Items.OfType<ComboBoxItem>()];
    }

    /// <summary>Types into one of an editor's boxes.</summary>
    public static void Type(IReadOnlyList<Control> editor, int box, string text)
    {
        ArgumentNullException.ThrowIfNull(editor);
        editor.OfType<TextBox>().ElementAt(box).Text = text;
    }

    /// <summary>Ticks one of an editor's options.</summary>
    public static void Tick(IReadOnlyList<Control> editor, string label)
    {
        ArgumentNullException.ThrowIfNull(editor);
        editor.OfType<CheckBox>().First(box => (string?)box.Content == label).IsChecked = true;
    }

    /// <summary>Applies what an editor is showing.</summary>
    public static void Apply(IReadOnlyList<Control> editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        Click(editor.OfType<Button>().First(button => (string?)button.Content == "Apply"));
    }

    /// <summary>Presses a button, as a person presses it.</summary>
    public static void Click(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>What the banner says, which is not the subtitle above it.</summary>
    public static string Banner(TableTabView view) =>
        Texts(view).First(text => text.Contains("in file order", StringComparison.Ordinal)
            || text.StartsWith("Showing", StringComparison.Ordinal)
            || text.Equals("No record matches", StringComparison.Ordinal));

    /// <summary>
    /// Everything a control is saying, in the order it says it.
    /// </summary>
    /// <remarks>
    /// What is shown, not what is built. A heading kept in the tree and hidden says nothing to the
    /// person in front of it, and a test that counted it would pass while they saw an empty
    /// section — or a stale "Preparing…" that had only been made invisible.
    /// </remarks>
    public static IReadOnlyList<string> Texts(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return
        [
            .. root.GetLogicalDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsVisible)
                .Select(block => block.Text ?? string.Empty),
        ];
    }

    /// <summary>Takes off the one thing in force whose chip says this.</summary>
    public static void Remove(TableTabView view, string saying)
    {
        ArgumentNullException.ThrowIfNull(view);

        var chip = view.GetLogicalDescendants()
            .OfType<Border>()
            .First(border => border.GetLogicalDescendants()
                .OfType<TextBlock>()
                .Any(block => (block.Text ?? string.Empty).Contains(saying, StringComparison.Ordinal)));

        Click(chip.GetLogicalDescendants().OfType<Button>().First());
    }
}
