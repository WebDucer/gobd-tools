using System.Globalization;
using System.Text;
using Avalonia.Input;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>Where a command's shortcut applies.</summary>
internal enum CommandScope
{
    /// <summary>Anywhere in the reader's window.</summary>
    Global,

    /// <summary>While a table's records have keyboard focus.</summary>
    Records,

    /// <summary>While the navigator has keyboard focus.</summary>
    Navigator,
}

/// <summary>The menus of the reader's window, in the order they stand in the menu bar.</summary>
internal enum ReaderMenu
{
    /// <summary>Opening an export, closing a tab, settings.</summary>
    File,

    /// <summary>Zoom, the navigator, the tabs.</summary>
    View,

    /// <summary>Back and forward, records by number, referring records.</summary>
    Go,

    /// <summary>What is asked of the table in front.</summary>
    Table,

    /// <summary>Shortcuts, licence and notices, and what the reader is.</summary>
    Help,
}

/// <summary>A command's shortcuts on macOS and on Windows and Linux.</summary>
/// <param name="Mac">The shortcuts on macOS, the one a menu shows first.</param>
/// <param name="Other">The shortcuts on Windows and Linux, the one a menu shows first.</param>
internal sealed record PlatformKeys(IReadOnlyList<KeyGesture> Mac, IReadOnlyList<KeyGesture> Other)
{
    /// <summary>A command without a shortcut.</summary>
    public static PlatformKeys None { get; } = new([], []);

    /// <summary>The shortcuts on the platform the reader runs on.</summary>
    public IReadOnlyList<KeyGesture> Current => ReaderKeys.IsMac ? Mac : Other;
}

/// <summary>
/// One command of the reader: what it is called, where its shortcuts apply, and where its menu
/// shows it.
/// </summary>
/// <param name="Id">What the window knows the command by.</param>
/// <param name="Label">What it is called, in a display language.</param>
/// <param name="AccessKey">The letter a menu marks for Alt on Windows and Linux, in English and German.</param>
/// <param name="Scope">Where its shortcuts apply.</param>
/// <param name="Keys">Its shortcuts.</param>
/// <param name="Menu">The menu it stands in, or none for a key that only moves around.</param>
/// <param name="Group">Its group within that menu; a separator stands between groups.</param>
/// <param name="NotOnMac">Whether macOS shows it in the application menu instead.</param>
/// <param name="Toggle">Whether the menu shows it with a check mark.</param>
internal sealed record CommandDefinition(
    string Id,
    Func<ReportLanguage, string> Label,
    (char English, char German) AccessKey,
    CommandScope Scope,
    PlatformKeys Keys,
    ReaderMenu? Menu = null,
    int Group = 0,
    bool NotOnMac = false,
    bool Toggle = false)
{
    /// <summary>The letter a menu marks for Alt, in a display language.</summary>
    public char AccessKeyIn(ReportLanguage language) =>
        language == ReportLanguage.German ? AccessKey.German : AccessKey.English;
}

/// <summary>
/// The reader's shortcuts, written once for each platform's conventions.
/// </summary>
/// <remarks>
/// Cmd on macOS where Windows and Linux use Ctrl, and each platform's own key where the two differ:
/// "find next" is F3 on Windows and Cmd+G on macOS, "back" Alt+Left and Cmd+[. See the
/// improve-reader-accessibility change's design.md D18.
/// </remarks>
internal static class ReaderKeys
{
    /// <summary>Whether the reader runs on macOS, whose conventions differ.</summary>
    public static bool IsMac => OperatingSystem.IsMacOS();

    private static KeyGesture Cmd(Key key, KeyModifiers more = KeyModifiers.None) => new(key, KeyModifiers.Meta | more);

    private static KeyGesture Ctrl(Key key, KeyModifiers more = KeyModifiers.None) => new(key, KeyModifiers.Control | more);

    private static KeyGesture Plain(Key key, KeyModifiers modifiers = KeyModifiers.None) => new(key, modifiers);

    /// <summary>The same key with Cmd on macOS and with Ctrl elsewhere.</summary>
    private static PlatformKeys Command(Key key, KeyModifiers more = KeyModifiers.None) =>
        new([Cmd(key, more)], [Ctrl(key, more)]);

    private static PlatformKeys Same(params KeyGesture[] gestures) => new(gestures, gestures);

    public static PlatformKeys OpenArchive { get; } = Command(Key.O);

    public static PlatformKeys OpenFolder { get; } = Command(Key.O, KeyModifiers.Shift);

    public static PlatformKeys Settings { get; } = Command(Key.OemComma);

    public static PlatformKeys CloseTab { get; } = new([Cmd(Key.W)], [Ctrl(Key.W), Ctrl(Key.F4)]);

    // Avalonia takes the plus of the number pad and the key that types "+" or "=" for one key, so
    // one shortcut answers all three. On macOS it is written as the number pad's, because the menu
    // bar shows a shortcut by the character it types, and Key.OemPlus types "=" on a US keyboard:
    // the menu showed "⌘=", which the "+" key of a German keyboard never matched.
    public static PlatformKeys ZoomIn { get; } = new([Cmd(Key.Add)], [Ctrl(Key.OemPlus)]);

    public static PlatformKeys ZoomOut { get; } = new([Cmd(Key.OemMinus)], [Ctrl(Key.OemMinus)]);

    public static PlatformKeys ActualSize { get; } = new([Cmd(Key.D0), Cmd(Key.NumPad0)], [Ctrl(Key.D0), Ctrl(Key.NumPad0)]);

    public static PlatformKeys ToggleNavigator { get; } = new([new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Control)], [Ctrl(Key.B)]);

    // A menu shows the first shortcut. macOS menus cannot show Ctrl+Tab, so they show Cmd+Shift+].
    public static PlatformKeys NextTab { get; } = new([Cmd(Key.OemCloseBrackets, KeyModifiers.Shift), Ctrl(Key.Tab)], [Ctrl(Key.Tab)]);

    public static PlatformKeys PreviousTab { get; } = new(
        [Cmd(Key.OemOpenBrackets, KeyModifiers.Shift), Ctrl(Key.Tab, KeyModifiers.Shift)],
        [Ctrl(Key.Tab, KeyModifiers.Shift)]);

    public static PlatformKeys Back { get; } = new([Cmd(Key.OemOpenBrackets)], [Plain(Key.Left, KeyModifiers.Alt)]);

    public static PlatformKeys Forward { get; } = new([Cmd(Key.OemCloseBrackets)], [Plain(Key.Right, KeyModifiers.Alt)]);

    public static PlatformKeys GoToRecord { get; } = new([Cmd(Key.L)], [Ctrl(Key.G)]);

    public static PlatformKeys NextReferring { get; } = new([Cmd(Key.G)], [Plain(Key.F3)]);

    public static PlatformKeys PreviousReferring { get; } = new([Cmd(Key.G, KeyModifiers.Shift)], [Plain(Key.F3, KeyModifiers.Shift)]);

    public static PlatformKeys AddFilter { get; } = Command(Key.F);

    public static PlatformKeys Shortcuts { get; } = Same(Plain(Key.F1));

    public static PlatformKeys NextArea { get; } = Same(Plain(Key.F6));

    public static PlatformKeys PreviousArea { get; } = Same(Plain(Key.F6, KeyModifiers.Shift));

    /// <summary>The tab in that place, the summary being the first.</summary>
    public static PlatformKeys TabAt(int place) => Command(Key.D1 + (place - 1));

    public static PlatformKeys RecordActions { get; } = Same(Plain(Key.Enter), Plain(Key.F10, KeyModifiers.Shift), Plain(Key.Apps));

    public static PlatformKeys CopyRecord { get; } = Command(Key.C);

    public static PlatformKeys ScrollLeft { get; } = Same(Plain(Key.Left));

    public static PlatformKeys ScrollRight { get; } = Same(Plain(Key.Right));

    public static PlatformKeys OpenTable { get; } = Same(Plain(Key.Enter));

    /// <summary>Closes a dialog, besides Escape: Cmd+W on macOS, as for any window there.</summary>
    public static PlatformKeys CloseDialog { get; } = new([Plain(Key.Escape), Cmd(Key.W)], [Plain(Key.Escape)]);

    /// <summary>Whether a key press is one of these shortcuts on the platform the reader runs on.</summary>
    public static bool Matches(this PlatformKeys keys, KeyEventArgs args) =>
        keys.Current.Any(gesture => gesture.Matches(args));

    /// <summary>A shortcut as the platform writes it, in a display language.</summary>
    /// <remarks>
    /// macOS writes modifiers as symbols, run together. Windows and Linux spell them out, and a
    /// German keyboard says Strg and Umschalt where an English one says Ctrl and Shift.
    /// </remarks>
    public static string Describe(KeyGesture gesture, ReportLanguage language, bool mac)
    {
        ArgumentNullException.ThrowIfNull(gesture);

        var german = language == ReportLanguage.German;
        var modifiers = gesture.KeyModifiers;
        if (mac)
        {
            var text = new StringBuilder();
            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                text.Append('⌃');
            }

            if (modifiers.HasFlag(KeyModifiers.Alt))
            {
                text.Append('⌥');
            }

            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                text.Append('⇧');
            }

            if (modifiers.HasFlag(KeyModifiers.Meta))
            {
                text.Append('⌘');
            }

            return text.Append(KeyName(gesture.Key, german, mac)).ToString();
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add(german ? "Strg" : "Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add(german ? "Umschalt" : "Shift");
        }

        parts.Add(KeyName(gesture.Key, german, mac));
        return string.Join('+', parts);
    }

    private static string KeyName(Key key, bool german, bool mac) => key switch
    {
        Key.OemComma => ",",
        Key.OemPlus or Key.Add => "+",
        Key.OemMinus or Key.Subtract => "-",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture),
        Key.NumPad0 => german ? "0 (Ziffernblock)" : "0 (keypad)",
        Key.Left => mac ? "←" : german ? "Pfeil links" : "Left",
        Key.Right => mac ? "→" : german ? "Pfeil rechts" : "Right",
        Key.Enter => mac ? "↩" : german ? "Eingabe" : "Enter",
        Key.Escape => mac ? "⎋" : german ? "Esc" : "Esc",
        Key.Tab => mac ? "⇥" : "Tab",
        Key.Apps => german ? "Menütaste" : "Menu key",
        _ => key.ToString(),
    };

    /// <summary>A menu title with its access key marked, as Avalonia marks one, where the platform shows it.</summary>
    /// <remarks>
    /// macOS menus have no access keys, so there the title is left as it is.
    /// </remarks>
    public static string MenuTitle(string label, char accessKey, bool mac)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (mac)
        {
            return label;
        }

        var at = label.IndexOf(accessKey.ToString(), StringComparison.OrdinalIgnoreCase);
        return at < 0 ? label : label.Insert(at, "_");
    }
}

/// <summary>
/// Every command of the reader, defined once.
/// </summary>
/// <remarks>
/// The menus, the keys the window answers, the shortcut overview and the tests all come from this
/// list, so that a shortcut cannot be shown in one place and answered in another, or bound twice.
/// The handlers are the window's; this says only what each command is. See the
/// improve-reader-accessibility change's design.md D7.
/// </remarks>
internal static class ReaderCommands
{
    public const string OpenArchive = "open-archive";
    public const string OpenFolder = "open-folder";
    public const string CloseTab = "close-tab";
    public const string Settings = "settings";
    public const string ZoomIn = "zoom-in";
    public const string ZoomOut = "zoom-out";
    public const string ActualSize = "actual-size";
    public const string ToggleNavigator = "toggle-navigator";
    public const string NextTab = "next-tab";
    public const string PreviousTab = "previous-tab";
    public const string Back = "back";
    public const string Forward = "forward";
    public const string GoToRecord = "go-to-record";
    public const string NextReferring = "next-referring";
    public const string PreviousReferring = "previous-referring";
    public const string AddFilter = "add-filter";
    public const string AddSort = "add-sort";
    public const string AddFigure = "add-figure";
    public const string FileOrder = "file-order";
    public const string Shortcuts = "shortcuts";
    public const string Licence = "licence";
    public const string Notice = "notice";
    public const string Notices = "notices";
    public const string About = "about";
    public const string NextArea = "next-area";
    public const string PreviousArea = "previous-area";
    public const string RecordActions = "record-actions";
    public const string CopyRecord = "copy-record";
    public const string ScrollLeft = "scroll-left";
    public const string ScrollRight = "scroll-right";
    public const string OpenTable = "open-table";

    /// <summary>The id of the command that brings the tab in a place to the front.</summary>
    public static string TabAt(int place) => "tab-" + place.ToString(CultureInfo.InvariantCulture);

    /// <summary>The menus' own titles and access keys.</summary>
    public static IReadOnlyList<(ReaderMenu Menu, Func<ReportLanguage, string> Label, (char English, char German) AccessKey)> Menus { get; } =
    [
        (ReaderMenu.File, UiText.FileMenu, ('F', 'D')),
        (ReaderMenu.View, UiText.ViewMenu, ('V', 'A')),
        (ReaderMenu.Go, UiText.GoMenu, ('G', 'G')),
        (ReaderMenu.Table, UiText.TableMenu, ('T', 'T')),
        (ReaderMenu.Help, UiText.HelpMenu, ('H', 'H')),
    ];

    /// <summary>Every command, in the order its menu lists it.</summary>
    public static IReadOnlyList<CommandDefinition> All { get; } =
    [
        new(OpenArchive, UiText.OpenArchive, ('A', 'A'), CommandScope.Global, ReaderKeys.OpenArchive, ReaderMenu.File, 0),
        new(OpenFolder, UiText.OpenFolder, ('F', 'O'), CommandScope.Global, ReaderKeys.OpenFolder, ReaderMenu.File, 0),
        new(CloseTab, UiText.CloseTabMenu, ('C', 'S'), CommandScope.Global, ReaderKeys.CloseTab, ReaderMenu.File, 1),
        new(Settings, UiText.SettingsMenu, ('S', 'E'), CommandScope.Global, ReaderKeys.Settings, ReaderMenu.File, 2, NotOnMac: true),

        new(ZoomIn, UiText.ZoomIn, ('I', 'V'), CommandScope.Global, ReaderKeys.ZoomIn, ReaderMenu.View, 0),
        new(ZoomOut, UiText.ZoomOut, ('U', 'K'), CommandScope.Global, ReaderKeys.ZoomOut, ReaderMenu.View, 0),
        new(ActualSize, UiText.ActualSize, ('A', 'O'), CommandScope.Global, ReaderKeys.ActualSize, ReaderMenu.View, 0),
        new(ToggleNavigator, UiText.ShowNavigator, ('N', 'Z'), CommandScope.Global, ReaderKeys.ToggleNavigator, ReaderMenu.View, 1, Toggle: true),
        new(NextTab, UiText.NextTab, ('X', 'N'), CommandScope.Global, ReaderKeys.NextTab, ReaderMenu.View, 2),
        new(PreviousTab, UiText.PreviousTab, ('P', 'R'), CommandScope.Global, ReaderKeys.PreviousTab, ReaderMenu.View, 2),

        new(Back, UiText.Back, ('B', 'Z'), CommandScope.Global, ReaderKeys.Back, ReaderMenu.Go, 0),
        new(Forward, UiText.Forward, ('F', 'V'), CommandScope.Global, ReaderKeys.Forward, ReaderMenu.Go, 0),
        new(GoToRecord, UiText.GoToRecordMenu, ('R', 'D'), CommandScope.Global, ReaderKeys.GoToRecord, ReaderMenu.Go, 1),
        new(NextReferring, UiText.NextReferringMenu, ('N', 'N'), CommandScope.Global, ReaderKeys.NextReferring, ReaderMenu.Go, 2),
        new(PreviousReferring, UiText.PreviousReferringMenu, ('P', 'H'), CommandScope.Global, ReaderKeys.PreviousReferring, ReaderMenu.Go, 2),

        new(AddFilter, UiText.AddFilterMenu, ('F', 'F'), CommandScope.Global, ReaderKeys.AddFilter, ReaderMenu.Table, 0),
        new(AddSort, UiText.AddSortMenu, ('S', 'S'), CommandScope.Global, PlatformKeys.None, ReaderMenu.Table, 0),
        new(AddFigure, UiText.AddFigureMenu, ('G', 'K'), CommandScope.Global, PlatformKeys.None, ReaderMenu.Table, 0),
        new(FileOrder, UiText.FileOrderMenu, ('B', 'D'), CommandScope.Global, PlatformKeys.None, ReaderMenu.Table, 1),

        new(Shortcuts, UiText.KeyboardShortcuts, ('K', 'T'), CommandScope.Global, ReaderKeys.Shortcuts, ReaderMenu.Help, 0),
        new(Licence, UiText.Licence, ('L', 'L'), CommandScope.Global, PlatformKeys.None, ReaderMenu.Help, 1),
        new(Notice, UiText.Notice, ('N', 'H'), CommandScope.Global, PlatformKeys.None, ReaderMenu.Help, 1),
        new(Notices, UiText.ThirdPartyNotices, ('T', 'D'), CommandScope.Global, PlatformKeys.None, ReaderMenu.Help, 1),
        new(About, UiText.About, ('A', 'B'), CommandScope.Global, PlatformKeys.None, ReaderMenu.Help, 2, NotOnMac: true),

        // Keys that move around rather than do something: they have no menu entry.
        new(NextArea, UiText.NextArea, default, CommandScope.Global, ReaderKeys.NextArea),
        new(PreviousArea, UiText.PreviousArea, default, CommandScope.Global, ReaderKeys.PreviousArea),
        .. Enumerable.Range(1, 9).Select(place =>
            new CommandDefinition(TabAt(place), language => UiText.TabAt(language, place), default, CommandScope.Global, ReaderKeys.TabAt(place))),

        new(RecordActions, UiText.RecordActions, default, CommandScope.Records, ReaderKeys.RecordActions),
        new(CopyRecord, UiText.CopyRecord, default, CommandScope.Records, ReaderKeys.CopyRecord),
        new(ScrollLeft, UiText.ScrollLeft, default, CommandScope.Records, ReaderKeys.ScrollLeft),
        new(ScrollRight, UiText.ScrollRight, default, CommandScope.Records, ReaderKeys.ScrollRight),

        new(OpenTable, UiText.OpenTable, default, CommandScope.Navigator, ReaderKeys.OpenTable),
    ];

    /// <summary>The definition of a command.</summary>
    public static CommandDefinition Get(string id) => All.Single(command => command.Id == id);
}
