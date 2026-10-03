using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GoBd.Reader.Ui.Controls;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Model;

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
        var view = new TableTabView(session, tab, (_, _, _) => { }, (_, _, _, _) => { }, _ => { });

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
            .OfType<Panel>()
            .First(candidate => candidate.Children.OfType<TextBlock>().Any(label => label.Text == row));

        var adder = panel.Children.OfType<Button>().Last();
        Click(adder);

        var flyout = adder.Flyout.ShouldBeOfType<Flyout>();
        var content = flyout.Content.ShouldBeOfType<Zoomed>().Child.ShouldBeOfType<StackPanel>();
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

    /// <summary>
    /// Opens an export in the reader's own window and waits until it has been read.
    /// </summary>
    /// <remarks>
    /// The window is the harness's to close, so it goes before the store it reads.
    /// </remarks>
    public static MainWindow Reader(ExportHarness harness, PreferencesStore? preferences = null)
    {
        ArgumentNullException.ThrowIfNull(harness);

        var window = new MainWindow(harness.ExportPath, harness.Options, preferences);
        harness.Track(window);
        window.Show();
        Wait(window.Reading);
        return window;
    }

    /// <summary>Waits for a task, keeping the dispatcher running as the window's own loop would.</summary>
    public static void Wait(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var clock = Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(60))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Lets everything the dispatcher has been handed run, and lays the window out.</summary>
    public static void Settle(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);

        for (var pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Keeps the window running until something has come about, such as a view a worker prepared.
    /// </summary>
    public static void Until(TopLevel window, Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(condition);

        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            Settle(window);
            Thread.Sleep(10);
        }

        condition().ShouldBeTrue();
    }

    /// <summary>
    /// Puts a filter in force through the filter editor, as a person does, and waits until the
    /// tab shows it.
    /// </summary>
    public static void Filter(MainWindow window, TableTabView view, int column, int comparison, string value)
    {
        ArgumentNullException.ThrowIfNull(window);

        var editor = Editor(view, "Filters");
        Choose(editor, 0, column);
        Choose(editor, 1, comparison);
        Type(editor, 0, value);
        Apply(editor);
        Until(window, () => view.GetLogicalDescendants().OfType<Border>().Any(border => border.CornerRadius.TopLeft == 12));

        // Closed, as a person closes it before going on.
        Press(window, new KeyGesture(Key.Escape));
    }

    /// <summary>The command key: Cmd on macOS, Ctrl elsewhere, as the reader's shortcuts use it.</summary>
    public static KeyModifiers Command => ReaderKeys.IsMac ? KeyModifiers.Meta : KeyModifiers.Control;

    /// <summary>Presses and releases a shortcut on the keyboard, and lets what it started run.</summary>
    public static void Press(TopLevel window, KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(gesture);

        var modifiers = RawInputModifiers.None;
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= RawInputModifiers.Control;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= RawInputModifiers.Shift;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= RawInputModifiers.Alt;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            modifiers |= RawInputModifiers.Meta;
        }

        window.KeyPress(gesture.Key, modifiers, PhysicalKey.None, null);

        // A key that closed its window is let go of on no window at all.
        if (window is Window { IsVisible: false })
        {
            Dispatcher.UIThread.RunJobs();
            return;
        }

        window.KeyRelease(gesture.Key, modifiers, PhysicalKey.None, null);
        Settle(window);
    }

    /// <summary>Presses the shortcut a command has on the platform the tests run on.</summary>
    public static void Press(TopLevel window, PlatformKeys keys, int which = 0)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Press(window, keys.Current[which]);
    }

    /// <summary>A menu title as it reads, without the mark of its access key.</summary>
    public static string? Plain(object? header) => (header as string)?.Replace("_", string.Empty, StringComparison.Ordinal);

    /// <summary>The control that has keyboard focus in a window.</summary>
    public static IInputElement? Focused(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.FocusManager?.GetFocusedElement();
    }

    /// <summary>The titles of the reader's tabs, in order: the summary, then the tables by name.</summary>
    public static string[] TabTitles(MainWindow window) =>
        [.. Tabs(window).Items.OfType<TabItem>().Select(Title)];

    /// <summary>The title of the tab in front.</summary>
    public static string FrontTitle(MainWindow window) =>
        Title(Tabs(window).SelectedItem.ShouldBeOfType<TabItem>());

    /// <summary>Asserts that keyboard focus is on a control or inside it.</summary>
    public static void FocusIsIn(TopLevel window, Visual area)
    {
        var focused = Focused(window).ShouldBeAssignableTo<Visual>().ShouldNotBeNull();
        (focused == area || area.IsVisualAncestorOf(focused)).ShouldBeTrue($"focus is on {focused}");
    }

    /// <summary>The record a row with keyboard focus shows, if a row has it.</summary>
    public static RecordRow? FocusedRecord(TopLevel window) =>
        (Focused(window) as Visual)?.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()?.DataContext as RecordRow;

    private static TabControl Tabs(MainWindow window) => window.GetVisualDescendants().OfType<TabControl>().Single();

    private static string Title(TabItem item) =>
        item.Content is TableTabView view ? view.Table.Identity : (string)item.Header!;

    /// <summary>The tab view of one table in the reader's window.</summary>
    public static TableTabView View(MainWindow window, string identity)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.GetLogicalDescendants().OfType<TableTabView>().First(view => view.Table.Identity == identity);
    }

    /// <summary>The table of the export with the given identity.</summary>
    public static TableNode Table(MainWindow window, string identity)
    {
        ArgumentNullException.ThrowIfNull(window);
        return ExportHarness.Table(window.Session.ShouldNotBeNull(), identity);
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
