using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
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
    private readonly TreeView navigator = new() { Width = 300 };
    private readonly TabControl tabs = new();
    private readonly TabItem startPageTab;
    private readonly StartPageView startPage = new();

    private readonly PreferencesStore preferencesStore;
    private UserPreferences preferences;
    private readonly LocalizedTexts menuHeaders;
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
        tabs.SelectionChanged += (_, _) => OnTabChanged();

        ConfigureMenu();

        var layout = new Avalonia.Controls.Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };

        var navContainer = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = navigator,
        };
        navContainer[!Border.BorderBrushProperty] = new DynamicResourceExtension(ReaderTheme.Border);

        Avalonia.Controls.Grid.SetColumn(navContainer, 0);
        Avalonia.Controls.Grid.SetColumn(tabs, 1);
        layout.Children.Add(navContainer);
        layout.Children.Add(tabs);

        // Renders the window's menu inside the window on Windows and Linux. On macOS the menu
        // belongs to the system menu bar and this hides itself.
        var menuBar = new NativeMenuBar();
        var root = new DockPanel();
        DockPanel.SetDock(menuBar, Dock.Top);
        root.Children.Add(menuBar);
        root.Children.Add(layout);
        Content = root;

        navigator.SelectionChanged += (_, _) => OnNavigatorChanged();

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
    private void OpenExport(string? exportPath)
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
        startPage.Show(opened.Reading, workspace.ExportPath);
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
            var node = new TreeViewItem { Header = medium.Name, IsExpanded = true };
            foreach (var entry in medium.Tables)
            {
                var item = new TreeViewItem { Header = entry.Identity, Tag = entry.Table };
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
        navigatorHeadings.Follow(text => group.Header = text, heading);
        foreach (var relationship in related)
        {
            group.Items.Add(new TreeViewItem { Header = arrow + relationship.Label, Tag = relationship });
        }

        table.Items.Add(group);
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
            var view = new TableTabView(current, tab, Follow, OfferReferrers, Step, preferences.Language);
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
        var close = new Button
        {
            Content = "✕",
            FontSize = 12,
            Padding = new Thickness(4, 0),
            MinWidth = 0,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
        };

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

    /// <summary>Opens the table a navigator entry names; a relationship entry opens nothing.</summary>
    private void OnNavigatorChanged()
    {
        if (syncing)
        {
            return;
        }

        // A relationship entry is a leaf carrying a TableRelationship, and choosing one selects
        // it and does nothing else: neither the active tab nor any table's position changes.
        if (ReaderTabs.Opens((navigator.SelectedItem as TreeViewItem)?.Tag) is { } selected)
        {
            ShowTable(selected);
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

        Apply(current.Follow(from, row.Ordinal, key), null);
    }

    /// <summary>Offers the tables whose records refer to the record acted on.</summary>
    private void OfferReferrers(TableNode from, RecordRow row, Control anchor)
    {
        if (Session is not { } current)
        {
            return;
        }

        var referrers = current.RelationshipsOf(from).ReferencedBy;
        if (referrers.Count == 0)
        {
            return;
        }

        var menu = new MenuFlyout();
        foreach (var referrer in referrers)
        {
            if (ResolvedForeignKey.Resolve(referrer.ForeignKey, referrer.Other, from) is not { } key)
            {
                continue;
            }

            var item = new MenuItem { Header = UiText.ReferrersMenu(preferences.Language, referrer.Other.Identity) };
            item.Click += (_, _) =>
            {
                var navigation = current.FollowBack(from, row.Ordinal, key);
                Apply(navigation, new Walk(key, from, row.Ordinal, navigation.MatchIndex, navigation.Matches));
            };

            menu.Items.Add(item);
        }

        // Shown rather than attached: a menu set as the grid's context menu stays there, and the
        // grid reopens it — built for this record — on the next request it does not answer.
        menu.ShowAt(anchor, showAtPointer: true);
    }

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
    private void Apply(Navigation navigation, Walk? walk)
    {
        if (navigation.Table is not { } target)
        {
            return;
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
            view.Position();
        }
    }

    /// <summary>
    /// The File menu: two ways in, because an export is two things.
    /// </summary>
    /// <remarks>
    /// The standard permits a ZIP archive or an unpacked folder, and no platform picker selects
    /// either with one dialog. The menu is defined once and shown natively: in the system menu bar
    /// on macOS, in the window elsewhere. The native menu handles its own shortcuts on macOS; on
    /// the other platforms the in-window bar only displays them, so the window binds them itself.
    /// </remarks>
    private void ConfigureMenu()
    {
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        var archiveGesture = new KeyGesture(Key.O, command);
        var folderGesture = new KeyGesture(Key.O, command | KeyModifiers.Shift);
        var settingsGesture = new KeyGesture(Key.OemComma, command);

        var openArchive = menuHeaders.Follow(new NativeMenuItem { Gesture = archiveGesture }, UiText.OpenArchive);
        openArchive.Click += async (_, _) => await PickArchiveAsync();

        var openFolder = menuHeaders.Follow(new NativeMenuItem { Gesture = folderGesture }, UiText.OpenFolder);
        openFolder.Click += async (_, _) => await PickFolderAsync();

        var fileMenu = new NativeMenu();
        fileMenu.Items.Add(openArchive);
        fileMenu.Items.Add(openFolder);

        // Settings sit in the application menu on macOS, which App sets, and in File elsewhere.
        if (!OperatingSystem.IsMacOS())
        {
            var settings = menuHeaders.Follow(new NativeMenuItem { Gesture = settingsGesture }, UiText.SettingsMenu);
            settings.Click += (_, _) => ShowSettings();
            fileMenu.Items.Add(new NativeMenuItemSeparator());
            fileMenu.Items.Add(settings);
        }

        // What the reader is, and under what terms: it reaches a person as one file or one
        // application, so it says so itself. See the prepare-public-release change's design.md D5.
        var helpMenu = new NativeMenu();
        helpMenu.Items.Add(menuHeaders.Follow(new NativeMenuItem { Command = new ActionCommand(ShowLicence) }, UiText.Licence));
        helpMenu.Items.Add(menuHeaders.Follow(new NativeMenuItem { Command = new ActionCommand(ShowNotice) }, UiText.Notice));
        helpMenu.Items.Add(menuHeaders.Follow(new NativeMenuItem { Command = new ActionCommand(ShowNotices) }, UiText.ThirdPartyNotices));

        // What an application says about itself belongs in the application menu on macOS, which
        // App sets while the application initialises, and in Help everywhere else.
        if (!OperatingSystem.IsMacOS())
        {
            helpMenu.Items.Add(menuHeaders.Follow(new NativeMenuItem { Command = new ActionCommand(ShowAbout) }, UiText.About));
        }

        var menu = new NativeMenu();
        menu.Items.Add(menuHeaders.Follow(new NativeMenuItem { Menu = fileMenu }, UiText.FileMenu));
        menu.Items.Add(menuHeaders.Follow(new NativeMenuItem { Menu = helpMenu }, UiText.HelpMenu));
        NativeMenu.SetMenu(this, menu);

        if (!OperatingSystem.IsMacOS())
        {
            KeyDown += async (_, args) =>
            {
                if (archiveGesture.Matches(args))
                {
                    args.Handled = true;
                    await PickArchiveAsync();
                }
                else if (folderGesture.Matches(args))
                {
                    args.Handled = true;
                    await PickFolderAsync();
                }
                else if (settingsGesture.Matches(args))
                {
                    args.Handled = true;
                    ShowSettings();
                }
            };
        }
    }

    // Which window beside this one is which, whatever language its title is in.
    private const string SettingsWindowKey = "settings";
    private const string AboutWindowKey = "about";
    private const string LicenceWindowKey = "licence";
    private const string NoticeWindowKey = "notice";
    private const string NoticesWindowKey = "notices";

    /// <summary>Shows the settings window.</summary>
    internal void ShowSettings() => ShowBeside(
        SettingsWindowKey,
        () => new SettingsWindow(preferencesStore, preferences, OnPreferencesChanged));

    private void OnPreferencesChanged(UserPreferences updated)
    {
        var languageChanged = updated.Language != preferences.Language;
        preferences = updated;
        ApplyTheme();

        if (languageChanged)
        {
            ApplyLanguage(preferences.Language);
        }
    }

    /// <summary>Puts the chosen theme into effect, for every window the application shows.</summary>
    private void ApplyTheme()
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = preferences.Theme.ToThemeVariant();
        }
    }

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
