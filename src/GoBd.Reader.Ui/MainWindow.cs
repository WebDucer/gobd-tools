using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.Controls;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation;
using GoBd.Validation.Localisation;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui;

/// <summary>
/// The reader's window: the declared structure on the left, the summary and one tab per table on
/// the right.
/// </summary>
/// <remarks>
/// The window wires controls together and owns nothing that reads data. Opening an export runs
/// as a background pipeline in <see cref="ReaderWorkspace"/>, which reports its progress here
/// through the dispatcher; which tabs exist and what each holds is <see cref="ReaderTabs"/>'s.
/// <para>
/// Filtering and sorting run the same way: a tab says what a person asked for, and the window
/// owns the worker that prepares it, so the window stays answerable while the store works.
/// </para>
/// </remarks>
public sealed class MainWindow : Window
{
    private readonly TreeView navigator = new();

    /// <summary>The column the navigator stands in, which a person resizes and collapses.</summary>
    private readonly ColumnDefinition navigatorColumn = new();

    /// <summary>The column the tabs stand in, which takes whatever the navigator leaves.</summary>
    private readonly ColumnDefinition tabsColumn = new(GridLength.Star);

    /// <summary>The navigator with its edge, shown while it is expanded.</summary>
    private readonly Border navigatorArea;

    /// <summary>The boundary between the navigator and the tabs, dragged or moved with the arrow keys.</summary>
    private readonly GridSplitter navigatorSplitter = new()
    {
        ResizeDirection = GridResizeDirection.Columns,
        ResizeBehavior = GridResizeBehavior.PreviousAndNext,
        Width = 6,
        Focusable = true,
    };

    /// <summary>What stands in the navigator's place while it is collapsed.</summary>
    private readonly Border navigatorStrip;

    /// <summary>The control on the collapsed strip that brings the navigator back.</summary>
    private readonly Button showNavigator;
    private readonly TabControl tabs = new();
    private readonly TabItem startPageTab;
    private readonly StartPageView startPage = new();

    /// <summary>Where this window says what assistive technology should announce.</summary>
    private readonly Announcer announcer = new();

    private readonly PreferencesStore preferencesStore;
    private UserPreferences preferences;
    private readonly LocalizedTexts menuHeaders;

    /// <summary>What each command of the command table does in this window.</summary>
    private readonly Dictionary<string, ActionCommand> commands = new(StringComparer.Ordinal);

    /// <summary>The menu entry each command stands in, where it has one.</summary>
    private readonly Dictionary<string, NativeMenuItem> menuItems = new(StringComparer.Ordinal);
    private LocalizedTexts navigatorHeadings;

    private readonly ReaderWorkspace workspace;
    private ReaderTabs? model;
    private readonly Dictionary<TableNode, TabItem> items = [];
    private readonly Dictionary<TableNode, TreeViewItem> navigatorItems = [];

    /// <summary>
    /// One preparation per open tab, so a filter on one table cannot supersede another's.
    /// </summary>
    /// <remarks>
    /// Each also names the view it builds, which is why a tab replaces only its own and closing
    /// one takes its records with it.
    /// </remarks>
    private readonly Dictionary<TableNode, ViewPreparation> preparations = [];

    private bool syncing;

    /// <summary>
    /// The windows the Help menu opened that are still open, by key, so that choosing an entry
    /// again brings its window forward rather than opening a second.
    /// </summary>
    private readonly Dictionary<string, Window> beside = new(StringComparer.Ordinal);

    /// <summary>Creates the window, opening the given export when one was named.</summary>
    public MainWindow(string? exportPath)
        : this(exportPath, null)
    {
    }

    /// <summary>
    /// The same, with stores placed where the options say and preferences kept where the store
    /// says, for tests.
    /// </summary>
    internal MainWindow(string? exportPath, StoreOptions? options, PreferencesStore? preferencesStore = null)
    {
        // Read once, here: the window owns the preferences, and puts into effect the parts the
        // application holds — its theme, and on macOS its menu.
        this.preferencesStore = preferencesStore ?? new PreferencesStore();
        preferences = this.preferencesStore.Load();
        menuHeaders = new LocalizedTexts(preferences.Language);
        navigatorHeadings = new LocalizedTexts(preferences.Language);
        workspace = new ReaderWorkspace(options);
        if (Application.Current is App application)
        {
            application.Reader = this;
            application.UpdateApplicationMenuLanguage(preferences.Language);
        }

        ApplyTheme();
        ZoomLevel.Current.Set(preferences.Zoom);
        if (Application.Current?.PlatformSettings is { } platformSettings)
        {
            platformSettings.ColorValuesChanged += OnPlatformColoursChanged;
        }

        Title = "GoBD Reader";
        Icon = ReaderIcon.ForWindow();
        Width = 1280;
        Height = 800;

        // A theme sizes a tab header for a handful of top-level sections.
        // A tab here names a table, there is one per table a person has opened, and the strip has
        // to stay readable at a dozen of them — so the strip is sized like a document's tabs
        // rather than like a page's sections. Set as a style so every tab gets it, including the
        // ones created later.
        //
        // The height is set explicitly rather than left to the padding: a strip that shrinks to
        // fit its text reads as part of the table below it rather than as the thing that chooses
        // between tables, and it leaves nothing to aim at.
        tabs.Margin = new Thickness(0, 8, 0, 0);
        tabs.Styles.Add(new Style(selector => selector.OfType<TabItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.FontSizeProperty, 16d),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(14, 8)),
                new Setter(Layoutable.MinHeightProperty, 40d),
            },
        });

        startPageTab = new TabItem { Header = UiText.SummaryTab(preferences.Language), Content = startPage };
        startPage.SetLanguage(preferences.Language);
        tabs.Items.Add(startPageTab);
        tabs.SelectedItem = startPageTab;
        // Only the tab strip's own changes: the event bubbles, and a selection moving inside a
        // table's grid once arrived here too, where it was taken for a change of tab and its
        // interim selection overwrote the record a navigation had just landed on.
        tabs.SelectionChanged += (_, args) =>
        {
            if (!ReferenceEquals(args.Source, tabs))
            {
                return;
            }

            OnTabChanged();
            RefreshCommands();
        };

        BindCommands();
        BuildMenus();
        RefreshCommands();
        AddHandler(KeyDownEvent, OnCommandKey, RoutingStrategies.Tunnel);

        var layout = new Avalonia.Controls.Grid();
        layout.ColumnDefinitions.Add(navigatorColumn);
        layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        layout.ColumnDefinitions.Add(tabsColumn);

        navigatorArea = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = navigator,
        };
        navigatorArea[!Border.BorderBrushProperty] = new DynamicResourceExtension(ReaderTheme.SurfaceBorder);

        showNavigator = ReaderTheme.Glyph(new Button
        {
            Content = "»",
            FontSize = 16,
            Padding = new Thickness(6, 4),
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 0, 0),
        });
        showNavigator.Click += (_, _) => ToggleNavigator();

        navigatorStrip = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 0),
            Width = 32,
            Child = showNavigator,
        };
        navigatorStrip[!Border.BorderBrushProperty] = new DynamicResourceExtension(ReaderTheme.SurfaceBorder);

        // A drag is kept once it ends, and a move from the keyboard once the key is let go: the
        // file is not written for every step in between.
        navigatorSplitter.DragCompleted += (_, _) => KeepNavigatorWidth();
        navigatorSplitter.KeyUp += (_, _) => KeepNavigatorWidth();

        Avalonia.Controls.Grid.SetColumn(navigatorArea, 0);
        Avalonia.Controls.Grid.SetColumn(navigatorStrip, 0);
        Avalonia.Controls.Grid.SetColumn(navigatorSplitter, 1);
        Avalonia.Controls.Grid.SetColumn(tabs, 2);
        layout.Children.Add(navigatorArea);
        layout.Children.Add(navigatorStrip);
        layout.Children.Add(navigatorSplitter);
        layout.Children.Add(tabs);
        ShowNavigatorState();
        NameNavigator(preferences.Language);

        // The navigator may take at most half the window, at whatever zoom it is drawn.
        SizeChanged += (_, _) => BoundNavigator();
        ZoomLevel.Current.Changed += OnZoomChanged;

        // Renders the window's menu inside the window on Windows and Linux. On macOS the menu
        // belongs to the system menu bar and this hides itself.
        var menuBar = new NativeMenuBar();
        var root = new DockPanel();
        DockPanel.SetDock(menuBar, Dock.Top);
        root.Children.Add(menuBar);
        root.Children.Add(layout);
        Content = new Zoomed(new Panel { Children = { root, announcer } });
        startPage.Announce = announcer.Announce;

        // Moving through the navigator selects; only Enter or a click opens. See the
        // improve-reader-accessibility change's design.md D9.
        navigator.AddHandler(KeyDownEvent, OnNavigatorKey, RoutingStrategies.Tunnel);
        navigator.Tapped += OnNavigatorTapped;

        startPage.ShowNothingOpened();

        // The command line and the picker arrive at the same place, so a scripted run and a
        // person choosing a medium see the same reader.
        if (exportPath is not null)
        {
            OpenExport(exportPath);
        }
    }

    /// <summary>The open session, for tests and for navigation.</summary>
    public ReaderSession? Session => workspace.Session;

    /// <summary>Reading the open export, for a test that has to wait for it.</summary>
    internal Task Reading => workspace.Reading;

    /// <summary>
    /// Opens an export, whether it was named on the command line or chosen in the picker.
    /// </summary>
    /// <remarks>
    /// Something that cannot be opened leaves whatever was open in place and says why. A new
    /// export replaces the previous one, whose reading is stopped and whose store is released
    /// with it.
    /// </remarks>
    internal void OpenExport(string? exportPath)
    {
        // Created on the UI thread, so its callbacks come back on the UI thread: the worker
        // reports progress, the window is the only thing that touches controls.
        var progress = new Progress<ExportReading>(OnProgress);

        var result = workspace.Open(exportPath, progress);
        if (result.Outcome == OpenOutcome.Dismissed)
        {
            return;
        }

        if (result.Outcome == OpenOutcome.Refused)
        {
            // Said in whichever language is chosen, then and later: the page keeps how to say it
            // rather than what it said.
            Func<ReportLanguage, string> reason = result.Refusal is { } refusal
                ? language => Describe(refusal, language)
                : language => string.Join("\n", result.Findings.Select(finding => MessageCatalogue.Render(finding, language)));

            if (Session is null)
            {
                startPage.ShowRefusal(reason);
            }
            else
            {
                Select(startPageTab);
                startPage.ShowRefusal(language => UiText.KeptOpen(language) + " " + reason(language));
            }

            return;
        }

        Reset();
        var opened = workspace.Session!;
        model = new ReaderTabs(opened) { Language = preferences.Language };
        Title = "GoBD Reader — " + workspace.ExportPath;
        BuildNavigator(opened);
        startPage.BeginReading();
        startPage.Show(opened.Reading, workspace.ExportPath);
        RefreshCommands();
    }

    /// <summary>Forgets everything that belonged to the export that was open.</summary>
    private void Reset()
    {
        syncing = true;
        navigator.Items.Clear();
        navigatorItems.Clear();
        foreach (var item in items.Values)
        {
            tabs.Items.Remove(item);
        }

        // Stops anything still being filtered or sorted for the export being closed. The store
        // goes with the session; these hold only the work in flight.
        foreach (var preparation in preparations.Values)
        {
            preparation.Dispose();
        }

        preparations.Clear();
        items.Clear();
        model = null;
        tabs.SelectedItem = startPageTab;
        syncing = false;
    }

    /// <summary>
    /// The navigator: media, the tables each carries, and each table's relationships as
    /// information about it.
    /// </summary>
    /// <remarks>
    /// The relationship entries are leaves. They name the table at the other end and the columns
    /// forming the key, and choosing one does nothing beyond selecting it — following a reference
    /// is an action on a record's value, not on a table. See the change's design.md D6.
    /// </remarks>
    private void BuildNavigator(ReaderSession opened)
    {
        navigatorHeadings = new LocalizedTexts(preferences.Language);
        foreach (var medium in opened.Navigator)
        {
            var node = Named(new TreeViewItem { Header = medium.Name, IsExpanded = true });
            foreach (var entry in medium.Tables)
            {
                var item = Named(new TreeViewItem { Header = entry.Identity, Tag = entry.Table });
                Add(item, UiText.References, entry.Relationships.References, "→ ");
                Add(item, UiText.ReferencedBy, entry.Relationships.ReferencedBy, "← ");
                node.Items.Add(item);
                navigatorItems[entry.Table] = item;
            }

            navigator.Items.Add(node);
        }
    }

    private void Add(
        TreeViewItem table,
        Func<ReportLanguage, string> heading,
        IReadOnlyList<TableRelationship> related,
        string arrow)
    {
        if (related.Count == 0)
        {
            return;
        }

        var group = new TreeViewItem();
        navigatorHeadings.Follow(
            text =>
            {
                group.Header = text;
                AutomationProperties.SetName(group, text);
            },
            heading);
        foreach (var relationship in related)
        {
            group.Items.Add(Named(new TreeViewItem { Header = arrow + relationship.Label, Tag = relationship }));
        }

        table.Items.Add(group);
    }

    /// <summary>
    /// Names a navigator entry for assistive technology by its header: Avalonia does not take an
    /// entry's name from a header that is text.
    /// </summary>
    private static TreeViewItem Named(TreeViewItem entry)
    {
        AutomationProperties.SetName(entry, entry.Header as string);
        return entry;
    }

    /// <summary>Presents the progress the worker reported, and anything it has made ready.</summary>
    private void OnProgress(ExportReading reading)
    {
        if (Session is null)
        {
            return;
        }

        startPage.Show(reading, workspace.ExportPath);
        foreach (var item in items.Values)
        {
            ((TableTabView)item.Content!).Refresh();
        }
    }

    /// <summary>
    /// Presents one table, creating its tab or bringing the one it has to the front.
    /// </summary>
    /// <remarks>
    /// The single way a table reaches the front, whether it was chosen in the navigator or
    /// arrived by following a foreign key. A table occupies one place in the workspace.
    /// </remarks>
    public void ShowTable(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (Session is not { } current || model is null)
        {
            return;
        }

        Remember();
        var tab = model.Open(table);
        if (!items.TryGetValue(table, out var item))
        {
            var view = new TableTabView(current, tab, Follow, OfferRecordActions, Step, preferences.Language)
            {
                Announce = announcer.Announce,
            };
            var preparation = new ViewPreparation(current, tab.Lane);
            preparations[tab.Table] = preparation;

            // What a person asks of a table is prepared away from this thread, and only what
            // comes back changes what the tab shows. A superseded request changes nothing, and
            // one that fails leaves the tab with the records it already had.
            view.Asked = async query =>
            {
                view.Preparing(true);
                var prepared = await preparation.PrepareAsync(tab.Table, query, tab.View);
                view.Preparing(false);

                if (prepared.Superseded)
                {
                    return;
                }

                if (prepared.Failure is { } failure)
                {
                    tab.Notice = failure;
                    view.Refresh();
                    return;
                }

                if (prepared.Rows is { } rows)
                {
                    tab.Query = query;
                    tab.View = rows.View;
                    tab.Rows = rows.Records;
                    view.Refresh();
                    view.Position();
                    RefreshCommands();
                    await Recompute(tab, view);
                }
            };

            view.Totalled = async chosen =>
            {
                tab.Figures = chosen;
                view.Refresh();
                await Recompute(tab, view);
            };

            item = new TabItem { Header = Header(table), Content = view };
            items[table] = item;
            tabs.Items.Add(item);
        }

        ((TableTabView)item.Content!).Refresh();
        Select(item);
    }

    /// <summary>What this window has announced, for a test to read.</summary>
    internal Announcer Announcer => announcer;

    /// <summary>
    /// Computes the figures a tab has chosen, over the records it is showing.
    /// </summary>
    /// <remarks>
    /// On a worker, because a sum over four million records is a second the window would
    /// otherwise spend frozen. A figure nobody chose costs nothing at all.
    /// </remarks>
    private async Task Recompute(TableTab tab, TableTabView view)
    {
        if (Session is not { } current)
        {
            return;
        }

        var chosen = tab.Figures;
        if (chosen.Count == 0)
        {
            view.ShowFigures([]);
            return;
        }

        try
        {
            var readings = await Task.Run(() => current.Figures(tab.Table, tab.View, chosen, tab.Lane));
            view.ShowFigures(readings);
        }
#pragma warning disable CA1031 // A figure that could not be computed is said, not thrown at the person.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            tab.Notice = exception.Message.Split('\n')[0].Trim();
            view.ShowFigures([]);
            view.Refresh();
        }
    }

    /// <summary>A tab header carrying the table's name and a way to close it.</summary>
    private Control Header(TableNode table)
    {
        var close = ReaderTheme.Glyph(new Button
        {
            Content = "✕",
            FontSize = 12,
            Padding = new Thickness(4, 0),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        });

        close.Click += (_, _) => CloseTab(table);
        AutomationProperties.SetName(close, UiText.CloseTab(preferences.Language, table.Identity));

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = table.Identity,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeight.Medium,
                },
                close,
            },
        };
        AutomationProperties.SetName(header, UiText.TableTab(preferences.Language, table.Identity));
        return header;
    }

    /// <summary>Closes one table's tab, leaving every other tab as it was.</summary>
    private void CloseTab(TableNode table)
    {
        if (model is null || !items.Remove(table, out var item))
        {
            return;
        }

        // Stops whatever this tab was preparing before its view is dropped, so nothing is built
        // into a table that is about to go.
        var owned = model.For(table)?.Table ?? table;
        if (preparations.Remove(owned, out var preparation))
        {
            preparation.Dispose();
        }

        model.Close(table);
        syncing = true;
        tabs.Items.Remove(item);
        syncing = false;

        Select(model.ActiveTable is { } active && items.TryGetValue(active, out var next) ? next : startPageTab);
    }

    private void Select(TabItem item)
    {
        syncing = true;
        tabs.SelectedItem = item;
        syncing = false;
        OnTabChanged();
        RefreshCommands();
    }

    /// <summary>
    /// Keeps the navigator's selection on the table in front.
    /// </summary>
    /// <remarks>
    /// Set from the tab rather than from wherever the navigation happened to start, so that what
    /// is selected and what is shown can never disagree.
    /// </remarks>
    private void OnTabChanged()
    {
        if (syncing || model is null)
        {
            return;
        }

        Remember();

        var table = ((tabs.SelectedItem as TabItem)?.Content as TableTabView)?.Table;
        if (table is null)
        {
            model.ShowStartPage();
        }
        else
        {
            model.Open(table);
            if (items.TryGetValue(table, out var item))
            {
                var view = (TableTabView)item.Content!;
                view.Refresh();
                view.Position();
            }
        }

        syncing = true;
        navigator.SelectedItem = table is not null && navigatorItems.TryGetValue(table, out var node) ? node : null;
        syncing = false;
    }

    /// <summary>
    /// Opens the table whose entry has the navigator's selection, when Enter is pressed on it.
    /// </summary>
    /// <remarks>
    /// The arrow keys only move the selection. A person reading the navigator from the keyboard
    /// moves through every entry to find the one they want, and every move opened a tab while the
    /// selection opened tables. Opened from the keyboard, the table's records take focus, because
    /// reading them is what the table was opened for.
    /// </remarks>
    private void OnNavigatorKey(object? sender, KeyEventArgs args)
    {
        if (!ReaderKeys.OpenTable.Matches(args)
            || ReaderTabs.Opens((navigator.SelectedItem as TreeViewItem)?.Tag) is not { } table)
        {
            return;
        }

        args.Handled = true;
        ShowTable(table);
        FocusFront();
    }

    /// <summary>
    /// Opens the table whose entry was clicked; a relationship entry opens nothing, and neither does
    /// the arrow that expands an entry.
    /// </summary>
    /// <remarks>
    /// A relationship entry is a leaf carrying a TableRelationship, and choosing one selects it and
    /// does nothing else: neither the active tab nor any table's position changes.
    /// </remarks>
    private void OnNavigatorTapped(object? sender, TappedEventArgs args)
    {
        if (args.Source is not Visual source)
        {
            return;
        }

        foreach (var visual in source.GetSelfAndVisualAncestors())
        {
            if (visual is ToggleButton)
            {
                return;
            }

            if (visual is TreeViewItem entry)
            {
                if (ReaderTabs.Opens(entry.Tag) is { } table)
                {
                    ShowTable(table);
                }

                return;
            }
        }
    }

    /// <summary>Remembers where the tab in front is positioned, before it is left.</summary>
    private void Remember()
    {
        if (model?.ActiveTable is { } active && items.TryGetValue(active, out var item))
        {
            ((TableTabView)item.Content!).Remember();
        }
    }

    /// <summary>Follows the key the acted-on cell takes part in.</summary>
    private void Follow(TableNode from, RecordRow row, int column)
    {
        if (Session is not { } current)
        {
            return;
        }

        if (current.KeysAt(from, column).FirstOrDefault() is not { } key)
        {
            return;
        }

        Apply(current.Follow(from, row.Ordinal, key), null, new Place(from, row.Ordinal));
    }

    /// <summary>
    /// Offers what can be done with a record: the keys it can follow, the tables whose records
    /// refer to it, and copying it.
    /// </summary>
    /// <remarks>
    /// One menu for the keyboard and the mouse, so the two cannot drift apart. Each key is named by
    /// its columns and by the table it leads to, and one whose value refers to nothing says so, so
    /// that where a value leads can be learnt without following it. Choosing an entry navigates
    /// exactly as a click on the value or a request for the referrers always did. Asked for from
    /// the keyboard, the menu opens at the record and its first entry takes focus. See the
    /// improve-reader-accessibility change's design.md D8.
    /// </remarks>
    private void OfferRecordActions(TableNode from, RecordRow row, Control anchor, bool fromKeyboard)
    {
        if (Session is not { } current)
        {
            return;
        }

        var language = preferences.Language;
        var menu = new MenuFlyout();
        var chosen = false;

        void Add(string header, Action choose, KeyGesture? gesture = null)
        {
            if (menu.Items.Count > 0 && menu.Items[^1] is MenuItem && header.Length == 0)
            {
                menu.Items.Add(new Separator());
                return;
            }

            if (header.Length == 0)
            {
                return;
            }

            var item = new MenuItem { Header = header, InputGesture = gesture };
            ReaderTheme.Zoom(item);
            item.Click += (_, _) =>
            {
                chosen = true;
                choose();
            };
            menu.Items.Add(item);
        }

        foreach (var key in current.KeysOf(from))
        {
            var columns = string.Join(", ", key.ForeignKey.Names.Select(name => name.Value));
            var dangling = key.ReferringColumns.Any(row.RefersToNothing);
            Add(UiText.FollowKey(language, columns, key.Referenced.Identity, dangling), () => Apply(current.Follow(from, row.Ordinal, key), null, new Place(from, row.Ordinal)));
        }

        Add(string.Empty, () => { });
        foreach (var referrer in current.RelationshipsOf(from).ReferencedBy)
        {
            if (ResolvedForeignKey.Resolve(referrer.ForeignKey, referrer.Other, from) is not { } key)
            {
                continue;
            }

            Add(UiText.ReferrersMenu(language, referrer.Other.Identity), () =>
            {
                var navigation = current.FollowBack(from, row.Ordinal, key);
                Apply(navigation, new Walk(key, from, row.Ordinal, navigation.MatchIndex, navigation.Matches), new Place(from, row.Ordinal));
            });
        }

        Add(string.Empty, () => { });
        Add(UiText.CopyRecord(language), () => _ = ViewOf(from)?.CopyRecordAsync(row), ReaderKeys.CopyRecord.Current[0]);

        if (fromKeyboard)
        {
            // The first entry takes focus, so the arrow keys move through the menu at once; and
            // dismissed without a choice, the menu gives focus back to the record it was opened on.
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.Opened += (_, _) => Dispatcher.UIThread.Post(
                () => menu.Items.OfType<MenuItem>().FirstOrDefault()?.Focus(NavigationMethod.Directional),
                DispatcherPriority.Loaded);
            menu.Closed += (_, _) =>
            {
                if (!chosen)
                {
                    anchor.Focus(NavigationMethod.Directional);
                }
            };
        }

        // Shown rather than attached: a menu set as the grid's context menu stays there, and the
        // grid reopens it — built for this record — on the next request it does not answer.
        menu.ShowAt(anchor, showAtPointer: !fromKeyboard);
    }

    /// <summary>
    /// Presents a place Back or Forward led to: its table, positioned at its record.
    /// </summary>
    /// <remarks>
    /// Through the same path as any navigation, so a closed tab opens again and a filter hiding the
    /// record is lifted and said so. Not remembered itself: it moves through the history.
    /// </remarks>
    private void Retrace(Place? place)
    {
        if (place is null || model is null)
        {
            return;
        }

        if (place.Ordinal is { } ordinal && model.Reach(place.Table, ordinal) is { } navigation)
        {
            Apply(navigation, null);
        }
        else
        {
            ShowTable(place.Table);
            FocusFront();
        }

        RefreshCommands();
    }

    /// <summary>
    /// Goes to a record of the table in front by its number, remembered for Back like any
    /// navigation; false when the table holds no record of that number.
    /// </summary>
    private bool GoToRecord(long ordinal)
    {
        if (model is null || FrontView() is not { } view || model.Reach(view.Table, ordinal) is not { } navigation)
        {
            return false;
        }

        Apply(navigation, null, new Place(view.Table, view.CurrentOrdinal));
        return true;
    }

    /// <summary>The tab view of the table in front, if a table is in front.</summary>
    private TableTabView? FrontView() => (tabs.SelectedItem as TabItem)?.Content as TableTabView;

    /// <summary>
    /// Moves keyboard focus to the next or previous area: the navigator, the table's toolbar, and
    /// its records — or the summary when it is in front — going round.
    /// </summary>
    /// <remarks>
    /// Tab walks through every control; F6 jumps between the places a person works in, as it does
    /// between a window's panes elsewhere. A collapsed navigator is skipped, as is an empty one.
    /// </remarks>
    private void MoveArea(int direction)
    {
        var areas = new List<(Visual Area, Action Focus)>();
        if (!preferences.NavigatorCollapsed && navigator.ItemCount > 0)
        {
            areas.Add((navigatorArea, FocusNavigator));
        }

        if (FrontView() is { } view)
        {
            areas.Add((view.Toolbar, view.FocusToolbar));
            areas.Add((view.RecordsArea, view.FocusRecords));
        }
        else
        {
            areas.Add((startPage, () => startPage.Focus(NavigationMethod.Directional)));
        }

        var focused = FocusManager?.GetFocusedElement() as Visual;
        var current = areas.FindIndex(area => focused is not null && (area.Area == focused || area.Area.IsVisualAncestorOf(focused)));
        var next = current < 0
            ? (direction > 0 ? 0 : areas.Count - 1)
            : (((current + direction) % areas.Count) + areas.Count) % areas.Count;
        areas[next].Focus();
    }

    /// <summary>Moves focus to the navigator's selected entry, or its first.</summary>
    private void FocusNavigator() =>
        (navigator.SelectedItem as TreeViewItem ?? navigator.GetVisualDescendants().OfType<TreeViewItem>().FirstOrDefault())
            ?.Focus(NavigationMethod.Directional);

    /// <summary>The tab view of a table, if it has a tab.</summary>
    private TableTabView? ViewOf(TableNode table) =>
        model?.For(table) is { } tab && items.TryGetValue(tab.Table, out var item) ? item.Content as TableTabView : null;

    /// <summary>Moves one referring record along the walk the tab in front is stepping through.</summary>
    private void Step(int direction)
    {
        if (Session is not { } current || model?.Active?.Walk is not { } walk)
        {
            return;
        }

        var navigation = current.FollowBack(
            walk.From,
            walk.Ordinal,
            walk.Key,
            Math.Max(0, walk.Index + direction));

        Apply(navigation, walk with { Index = navigation.MatchIndex, Matches = navigation.Matches });
    }

    /// <summary>Presents what a navigation produced, in the tab of the table it concerns.</summary>
    /// <param name="navigation">Where it led.</param>
    /// <param name="walk">The walk it is a step of, or none.</param>
    /// <param name="origin">
    /// Where it started, to be remembered for Back; none for a step of a walk, or for going back
    /// and forward, which are not navigations of their own.
    /// </param>
    private void Apply(Navigation navigation, Walk? walk, Place? origin = null)
    {
        if (navigation.Table is not { } target)
        {
            return;
        }

        if (origin is not null)
        {
            model?.Remember(origin.Table, origin.Ordinal, navigation);
        }

        Remember();
        ShowTable(target);
        _ = ApplyAsync(navigation, walk, target);
    }

    /// <summary>
    /// Lands the navigation, lifting a filter that hides the record it names.
    /// </summary>
    /// <remarks>
    /// A navigation that landed anywhere but the record it names would be wrong, and rebuilding
    /// the view without the filter reads the store — so this waits, while the window does not.
    /// See the change's design.md D9.
    /// </remarks>
    private async Task ApplyAsync(Navigation navigation, Walk? walk, TableNode target)
    {
        if (model is null)
        {
            return;
        }

        var owned = model.For(target)?.Table ?? target;
        if (!preparations.TryGetValue(owned, out var preparation))
        {
            model.Apply(navigation, walk);
        }
        else
        {
            await model.ApplyAsync(navigation, walk, preparation);
        }

        if (items.TryGetValue(owned, out var item))
        {
            // Moved explicitly, because the navigation is what moved it: a refresh on its own
            // leaves the grid where the person left it.
            var view = (TableTabView)item.Content!;
            view.Refresh();
            view.Position(focus: true);
        }

        // Said where a screen reader is listening as well as where it happened: the outcome, and
        // that a filter gave way, if one did.
        var notice = model.For(owned)?.NoticeAt(DateTimeOffset.UtcNow) ?? string.Empty;
        announcer.Announce(string.Join(" ", new[] { ReaderTabs.Describe(navigation, preferences.Language), notice }.Where(text => text.Length > 0)));
        RefreshCommands();
    }

    /// <summary>
    /// What each command does in this window, and when it can.
    /// </summary>
    /// <remarks>
    /// A command the window cannot carry out yet stays in its menu, disabled, rather than being
    /// left out: a menu that changes its entries is harder to learn than one that greys them.
    /// </remarks>
    private void BindCommands()
    {
        Bind(ReaderCommands.OpenArchive, OpenArchiveFromCommand);
        Bind(ReaderCommands.OpenFolder, OpenFolderFromCommand);
        Bind(ReaderCommands.CloseTab, CloseFrontTab, () => model?.Active is not null);
        Bind(ReaderCommands.Settings, ShowSettings);
        Bind(ReaderCommands.NextTab, () => MoveTab(1), () => tabs.ItemCount > 1);
        Bind(ReaderCommands.PreviousTab, () => MoveTab(-1), () => tabs.ItemCount > 1);
        for (var place = 1; place <= 9; place++)
        {
            var at = place;
            Bind(ReaderCommands.TabAt(at), () => ShowTabAt(at), () => tabs.ItemCount >= at);
        }

        Bind(ReaderCommands.ZoomIn, () => Zoom(ZoomLevel.Current.Larger()), () => ZoomLevel.Current.Percent < ZoomLevel.Steps[^1]);
        Bind(ReaderCommands.ZoomOut, () => Zoom(ZoomLevel.Current.Smaller()), () => ZoomLevel.Current.Percent > ZoomLevel.Steps[0]);
        Bind(ReaderCommands.ActualSize, () => Zoom(UserPreferences.ActualSize), () => ZoomLevel.Current.Percent != UserPreferences.ActualSize);
        Bind(ReaderCommands.ToggleNavigator, ToggleNavigator);
        Bind(ReaderCommands.Back, () => Retrace(model?.History.Back()), () => model?.History.CanGoBack == true);
        Bind(ReaderCommands.Forward, () => Retrace(model?.History.Forward()), () => model?.History.CanGoForward == true);
        Bind(ReaderCommands.GoToRecord, () => FrontView()?.AskForRecord(GoToRecord), () => FrontView() is { HasData: true });
        Bind(ReaderCommands.NextArea, () => MoveArea(1));
        Bind(ReaderCommands.PreviousArea, () => MoveArea(-1));
        Bind(ReaderCommands.NextReferring, () => Step(1), () => model?.Active?.Walk is { HasNext: true });
        Bind(ReaderCommands.PreviousReferring, () => Step(-1), () => model?.Active?.Walk is { HasPrevious: true });
        Bind(ReaderCommands.AddFilter, () => FrontView()?.OpenFilterEditor(), () => FrontView() is { HasData: true });
        Bind(ReaderCommands.AddSort, () => FrontView()?.OpenSortEditor(), () => FrontView() is { HasData: true });
        Bind(ReaderCommands.AddFigure, () => FrontView()?.OpenFigureEditor(), () => FrontView() is { HasData: true });
        Bind(ReaderCommands.FileOrder, () => FrontView()?.ReturnToFileOrder(), () => FrontView() is { HasData: true, InFileOrder: false });
        Bind(ReaderCommands.Shortcuts, ShowShortcuts);
        Bind(ReaderCommands.Licence, ShowLicence);
        Bind(ReaderCommands.Notice, ShowNotice);
        Bind(ReaderCommands.Notices, ShowNotices);
        Bind(ReaderCommands.About, ShowAbout);

        // Everything else in the table stands disabled until this window answers it.
        foreach (var definition in ReaderCommands.All)
        {
            if (!commands.ContainsKey(definition.Id))
            {
                commands[definition.Id] = new ActionCommand(() => { }, () => false);
            }
        }
    }

    private void Bind(string id, Action execute, Func<bool>? canExecute = null) =>
        commands[id] = new ActionCommand(execute, canExecute);

    /// <summary>
    /// The menus, built from the command table: shown natively in the system menu bar on macOS, in
    /// the window elsewhere.
    /// </summary>
    /// <remarks>
    /// On Windows and Linux every title marks the letter that opens it with Alt, in the display
    /// language; macOS has no such letters. See the improve-reader-accessibility change's
    /// design.md D7 and D17.
    /// </remarks>
    private void BuildMenus()
    {
        var mac = ReaderKeys.IsMac;
        var bar = new NativeMenu();
        foreach (var (menu, label, access) in ReaderCommands.Menus)
        {
            var entries = new NativeMenu();
            int? group = null;
            foreach (var definition in ReaderCommands.All.Where(command => command.Menu == menu && !(mac && command.NotOnMac)))
            {
                if (group is { } previous && previous != definition.Group)
                {
                    entries.Items.Add(new NativeMenuItemSeparator());
                }

                group = definition.Group;
                var item = new NativeMenuItem
                {
                    Command = new ActionCommand(() => Run(definition.Id, fromMenu: true), () => commands[definition.Id].CanExecute(null)),
                    Gesture = definition.Keys.Current.FirstOrDefault(),
                    ToggleType = definition.Toggle ? MenuItemToggleType.CheckBox : MenuItemToggleType.None,
                };
                menuHeaders.Follow(
                    text => item.Header = text,
                    language => ReaderKeys.MenuTitle(definition.Label(language), definition.AccessKeyIn(language), mac));
                menuItems[definition.Id] = item;
                entries.Items.Add(item);
            }

            var title = new NativeMenuItem { Menu = entries };
            menuHeaders.Follow(
                text => title.Header = text,
                language => ReaderKeys.MenuTitle(label(language), language == ReportLanguage.German ? access.German : access.English, mac));
            bar.Items.Add(title);
        }

        NativeMenu.SetMenu(this, bar);
    }

    /// <summary>Says to every command and menu entry that what it can do may have changed.</summary>
    private void RefreshCommands()
    {
        foreach (var command in commands.Values)
        {
            command.Refresh();
        }

        foreach (var (id, item) in menuItems)
        {
            item.IsEnabled = commands[id].CanExecute(null);
        }

        if (menuItems.TryGetValue(ReaderCommands.ToggleNavigator, out var toggle))
        {
            toggle.IsChecked = !preferences.NavigatorCollapsed;
        }
    }

    /// <summary>
    /// Collapses the navigator to a narrow strip, or brings it back at the width it had.
    /// </summary>
    /// <remarks>
    /// Focus inside a navigator that collapses would be on nothing a person can see, so it moves to
    /// the tab in front. Nothing collapses the navigator but the person: not a zoom, not a narrow
    /// window. See the improve-reader-accessibility change's design.md D6.
    /// </remarks>
    private void ToggleNavigator()
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;
        var hadFocus = focused is not null && (navigatorArea == focused || navigatorArea.IsVisualAncestorOf(focused) || focused == navigatorSplitter);

        UpdatePreferences(current => current with { NavigatorCollapsed = !current.NavigatorCollapsed });
        ShowNavigatorState();
        RefreshCommands();

        if (hadFocus)
        {
            FocusFront();
        }
    }

    /// <summary>Lays the navigator out as the preferences say: expanded at its width, or collapsed.</summary>
    private void ShowNavigatorState()
    {
        var collapsed = preferences.NavigatorCollapsed;
        navigatorArea.IsVisible = !collapsed;
        navigatorSplitter.IsVisible = !collapsed;
        navigatorStrip.IsVisible = collapsed;

        if (collapsed)
        {
            navigatorColumn.MinWidth = 0;
            navigatorColumn.MaxWidth = double.PositiveInfinity;
            navigatorColumn.Width = GridLength.Auto;
        }
        else
        {
            navigatorColumn.MinWidth = UserPreferences.NarrowestNavigator;
            navigatorColumn.Width = new GridLength(preferences.NavigatorWidth);
            BoundNavigator();
        }
    }

    /// <summary>Keeps the navigator from taking more than half the window, at the zoom it is drawn at.</summary>
    private void BoundNavigator()
    {
        if (preferences.NavigatorCollapsed || Bounds.Width <= 0)
        {
            return;
        }

        var half = Bounds.Width / ZoomLevel.Current.Scale / 2;
        navigatorColumn.MaxWidth = Math.Max(UserPreferences.NarrowestNavigator, half);
    }

    private void OnZoomChanged(object? sender, EventArgs e) => BoundNavigator();

    /// <summary>
    /// Keeps the width the navigator was given, unscaled, so it means the same at every zoom.
    /// </summary>
    /// <remarks>
    /// The splitter sets both columns it divides to fixed widths; the tabs' column is given back
    /// its share of whatever the window leaves, so a wider window still widens the tables.
    /// </remarks>
    private void KeepNavigatorWidth()
    {
        tabsColumn.Width = GridLength.Star;
        var width = Math.Clamp(navigatorColumn.ActualWidth, UserPreferences.NarrowestNavigator, UserPreferences.WidestNavigator);
        navigatorColumn.Width = new GridLength(width);
        UpdatePreferences(current => current with { NavigatorWidth = width });
    }

    /// <summary>Names the navigator's controls for assistive technology, in the display language.</summary>
    private void NameNavigator(ReportLanguage language)
    {
        AutomationProperties.SetName(navigator, UiText.NavigatorName(language));
        AutomationProperties.SetName(navigatorSplitter, UiText.NavigatorWidth(language));
        AutomationProperties.SetName(showNavigator, UiText.ShowNavigator(language));
        ToolTip.SetTip(showNavigator, UiText.ShowNavigator(language));
    }

    /// <summary>
    /// Answers the reader's shortcuts, before the control with focus sees them.
    /// </summary>
    /// <remarks>
    /// Tunnelling, so that Ctrl+Tab and F6 reach the window before focus navigation takes them. A
    /// text field keeps the keys it uses itself.
    /// <para>
    /// Every shortcut that reaches the window is answered, including those a menu shows. On macOS
    /// the menu bar sees a shortcut first and keeps the ones it answers, but it matches by the
    /// character a key types on a US keyboard, so on another layout a shortcut it shows can pass it
    /// by: the "+" key of a German keyboard never matched "⌘=", and Zoom In did nothing at all
    /// while the window left that shortcut to the menu. <see cref="Run"/> keeps one key press from
    /// running a command twice should both answer it.
    /// </para>
    /// </remarks>
    private void OnCommandKey(object? sender, KeyEventArgs args)
    {
        if (args.Handled)
        {
            return;
        }

        foreach (var definition in ReaderCommands.All.Where(command => command.Scope == CommandScope.Global))
        {
            if (!definition.Keys.Matches(args))
            {
                continue;
            }

            if (TextFieldOwns(FocusManager?.GetFocusedElement(), args))
            {
                return;
            }

            if (commands[definition.Id].CanExecute(null))
            {
                args.Handled = true;
                Run(definition.Id, fromMenu: false);
            }

            return;
        }
    }

    /// <summary>The command run last, whether from a menu, and when.</summary>
    private (string Id, bool FromMenu, long At) lastRun = (string.Empty, false, 0);

    /// <summary>
    /// Runs a command, from a menu or from a key the window answered, but not once from each for
    /// the same key press.
    /// </summary>
    /// <remarks>
    /// The same command arriving from the other of the two within a quarter of a second is one key
    /// press reported twice, not two: closing a tab must not close two. Pressed twice from the
    /// same place, it runs twice.
    /// </remarks>
    private void Run(string id, bool fromMenu)
    {
        var now = Environment.TickCount64;
        if (lastRun.Id == id && lastRun.FromMenu != fromMenu && now - lastRun.At < 250)
        {
            return;
        }

        lastRun = (id, fromMenu, now);
        commands[id].Execute(null);
    }

    /// <summary>Whether the text field with focus uses this key itself, to type, move or edit.</summary>
    internal static bool TextFieldOwns(IInputElement? focused, KeyEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (focused is not TextBox)
        {
            return false;
        }

        var modifiers = args.KeyModifiers & ~KeyModifiers.Shift;
        if (modifiers == KeyModifiers.None)
        {
            return args.Key is not (>= Key.F1 and <= Key.F24);
        }

        if (Application.Current?.PlatformSettings?.HotkeyConfiguration is not { } hotkeys)
        {
            return false;
        }

        IEnumerable<KeyGesture> editing =
        [
            .. hotkeys.Copy, .. hotkeys.Cut, .. hotkeys.Paste, .. hotkeys.Undo, .. hotkeys.Redo, .. hotkeys.SelectAll,
            .. hotkeys.MoveCursorToTheStartOfLine, .. hotkeys.MoveCursorToTheEndOfLine,
            .. hotkeys.MoveCursorToTheStartOfDocument, .. hotkeys.MoveCursorToTheEndOfDocument,
            .. hotkeys.MoveCursorToTheStartOfLineWithSelection, .. hotkeys.MoveCursorToTheEndOfLineWithSelection,
            .. hotkeys.MoveCursorToTheStartOfDocumentWithSelection, .. hotkeys.MoveCursorToTheEndOfDocumentWithSelection,
        ];

        return editing.Any(gesture => gesture.Matches(args))
            || (args.Key is Key.Left or Key.Right or Key.Back or Key.Delete && modifiers == hotkeys.WholeWordTextActionModifiers);
    }

    /// <summary>
    /// Changes the preferences from this window rather than from Settings: kept for the next
    /// launch, and shown in Settings if it is open.
    /// </summary>
    private void UpdatePreferences(Func<UserPreferences, UserPreferences> change)
    {
        var next = change(preferences);
        if (next == preferences)
        {
            return;
        }

        preferences = next;
        preferencesStore.Save(preferences);
        (beside.GetValueOrDefault(SettingsWindowKey) as SettingsWindow)?.Show(preferences);
    }

    /// <summary>Draws every window of the reader at a zoom level, and keeps it.</summary>
    private void Zoom(int percent)
    {
        UpdatePreferences(current => current with { Zoom = percent });
        ZoomLevel.Current.Set(percent);
        RefreshCommands();
    }

    private async void OpenArchiveFromCommand() => await PickArchiveAsync();

    private async void OpenFolderFromCommand() => await PickFolderAsync();

    /// <summary>Closes the table tab in front; the summary is not closed, and neither is the window.</summary>
    private void CloseFrontTab()
    {
        if (model?.ActiveTable is not { } active)
        {
            return;
        }

        CloseTab(active);
        FocusFront();
    }

    /// <summary>Brings the next or previous tab to the front, going round from the last to the summary.</summary>
    private void MoveTab(int direction)
    {
        var count = tabs.ItemCount;
        if (count < 2)
        {
            return;
        }

        var next = (((tabs.SelectedIndex + direction) % count) + count) % count;
        Select((TabItem)tabs.Items[next]!);
        FocusFront();
    }

    /// <summary>Brings the tab in a place to the front, the summary being the first.</summary>
    private void ShowTabAt(int place)
    {
        if (place > tabs.ItemCount)
        {
            return;
        }

        Select((TabItem)tabs.Items[place - 1]!);
        FocusFront();
    }

    /// <summary>
    /// Moves keyboard focus into the tab in front: to its records, or to the summary.
    /// </summary>
    /// <remarks>
    /// Changing the tab from the keyboard leaves focus where the keys were pressed otherwise —
    /// on a tab that is no longer in front, or on nothing at all.
    /// </remarks>
    private void FocusFront()
    {
        if ((tabs.SelectedItem as TabItem)?.Content is TableTabView view)
        {
            view.FocusRecords();
        }
        else
        {
            startPage.Focus(NavigationMethod.Directional);
        }
    }

    // Which window beside this one is which, whatever language its title is in.
    private const string SettingsWindowKey = "settings";
    private const string AboutWindowKey = "about";
    private const string LicenceWindowKey = "licence";
    private const string NoticeWindowKey = "notice";
    private const string NoticesWindowKey = "notices";
    private const string ShortcutsWindowKey = "shortcuts";

    /// <summary>Shows every shortcut the reader offers.</summary>
    internal void ShowShortcuts() => ShowBeside(ShortcutsWindowKey, () => new ShortcutsWindow(preferences.Language));

    /// <summary>Shows the settings window.</summary>
    internal void ShowSettings() => ShowBeside(
        SettingsWindowKey,
        () => new SettingsWindow(preferencesStore, () => preferences, OnPreferencesChanged));

    private void OnPreferencesChanged(UserPreferences updated)
    {
        var languageChanged = updated.Language != preferences.Language;
        preferences = updated;
        ApplyTheme();
        ZoomLevel.Current.Set(preferences.Zoom);
        RefreshCommands();

        if (languageChanged)
        {
            ApplyLanguage(preferences.Language);
        }
    }

    /// <summary>Puts the chosen theme into effect, for every window the application shows.</summary>
    /// <remarks>
    /// Asked again whenever the platform's colours change, so that System follows the platform's
    /// high contrast setting as it is switched, without a restart.
    /// </remarks>
    private void ApplyTheme()
    {
        if (Application.Current is { } app)
        {
            var platform = app.PlatformSettings?.GetColorValues();
            app.RequestedThemeVariant = ReaderTheme.Resolve(
                preferences.Theme,
                platform?.ContrastPreference ?? ColorContrastPreference.NoPreference,
                platform?.ThemeVariant ?? PlatformThemeVariant.Light);
        }
    }

    /// <summary>Follows the platform's colours while the person has chosen to.</summary>
    private void OnPlatformColoursChanged(object? sender, PlatformColorValues colours) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (preferences.Theme == ThemePreference.System)
            {
                ApplyTheme();
            }
        });

    internal void ApplyLanguage(ReportLanguage lang)
    {
        startPageTab.Header = UiText.SummaryTab(lang);
        startPage.SetLanguage(lang);

        if (model is not null)
        {
            model.Language = lang;
        }

        foreach (var (table, item) in items)
        {
            item.Header = Header(table);
            ((TableTabView)item.Content!).SetLanguage(lang);
        }

        menuHeaders.Apply(lang);
        navigatorHeadings.Apply(lang);
        NameNavigator(lang);
        (Application.Current as App)?.UpdateApplicationMenuLanguage(lang);

        foreach (var window in beside.Values.OfType<ILocalized>())
        {
            window.SetLanguage(lang);
        }
    }

    /// <summary>Shows what the reader is, from the Help menu or from the application menu.</summary>
    internal void ShowAbout() => ShowBeside(
        AboutWindowKey,
        () => new AboutWindow(ShowLicence, ShowNotice, ShowNotices, preferences.Language));

    private void ShowLicence() => ShowText(LicenceWindowKey, UiText.Licence, () => LicenceTexts.Licence);

    private void ShowNotice() => ShowText(NoticeWindowKey, UiText.Notice, () => LicenceTexts.Notice);

    private void ShowNotices() => ShowText(NoticesWindowKey, UiText.ThirdPartyNotices, () => LicenceTexts.ThirdPartyNotices);

    /// <summary>
    /// Shows a window of its own beside this one, or brings forward the one already open under
    /// that key.
    /// </summary>
    private void ShowBeside(string key, Func<Window> create)
    {
        if (beside.TryGetValue(key, out var open))
        {
            open.Activate();
            return;
        }

        var window = create();
        window.Closed += (_, _) => beside.Remove(key);
        beside[key] = window;
        window.Show(this);
    }

    /// <summary>Shows a text in a window of its own beside this one.</summary>
    private void ShowText(string key, Func<ReportLanguage, string> title, Func<string> text) =>
        ShowBeside(key, () => new TextWindow(title, preferences.Language, text()));

    private async Task PickArchiveAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiText.PickArchiveTitle(preferences.Language),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(UiText.ZipArchive(preferences.Language)) { Patterns = ["*.zip"] }],
        });

        OpenExport(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }

    private async Task PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = UiText.PickFolderTitle(preferences.Language),
            AllowMultiple = false,
        });

        OpenExport(folders.Count > 0 ? folders[0].TryGetLocalPath() : null);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        // The store lives in the temporary location only while its export is open. Leaving it
        // for the next launch's cleanup would leave an auditor's data on disk in the meantime.
        // Disposing stops the worker first, because deleting a store it is writing into is not
        // a close but a crash — and whatever is still being filtered is stopped before that.
        foreach (var preparation in preparations.Values)
        {
            preparation.Dispose();
        }

        preparations.Clear();
        workspace.Dispose();
        if (Application.Current?.PlatformSettings is { } platformSettings)
        {
            platformSettings.ColorValuesChanged -= OnPlatformColoursChanged;
        }

        ZoomLevel.Current.Changed -= OnZoomChanged;

        if (Application.Current is App application && ReferenceEquals(application.Reader, this))
        {
            application.Reader = null;
        }

        base.OnClosed(e);
    }

    /// <summary>
    /// Says why the store would not open, in the terms a person can act on.
    /// </summary>
    /// <remarks>
    /// Naming the volume matters: on many Linux distributions the temporary location is memory
    /// rather than disk, and the answer is then to point the store somewhere else rather than to
    /// free space.
    /// <para>
    /// Naming the directory matters for the same reason when it is not private: the way out is
    /// to remove it or to point the reader elsewhere, and a person can do neither without knowing
    /// which directory it is. See the prepare-public-release change's design.md D6.
    /// </para>
    /// </remarks>
    internal static string Describe(StoreRefusal refusal, ReportLanguage language) => refusal switch
    {
        NotEnoughSpace space => DescribeSpace(space, language),
        LocationNotPrivate { Problem: StorePrivacyProblem.ReadableByOthers } location =>
            UiText.StoreReadableByOthers(language, location.Directory),
        LocationNotPrivate { Problem: StorePrivacyProblem.SymbolicLink } location =>
            UiText.StoreSymbolicLink(language, location.Directory),
        LocationNotPrivate location => UiText.StoreUnusable(language, location.Directory),
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "A refusal of no known kind."),
    };

    private static string DescribeSpace(NotEnoughSpace refusal, ReportLanguage language)
    {
        var space = UiText.StoreSpace(
            language,
            refusal.RequiredBytes / 1024 / 1024,
            refusal.VolumeRoot,
            refusal.AvailableBytes / 1024 / 1024);

        return refusal.MemoryBacked ? space + " " + UiText.MemoryBacked(language) : space;
    }
}
