using System.Globalization;
using GoBd.Reader.Data;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>
/// A backwards walk in progress: the records referring to one record, and where in them the
/// reader is.
/// </summary>
/// <param name="Key">The key pointing at the record being walked back from.</param>
/// <param name="From">The referenced table the walk started in.</param>
/// <param name="Ordinal">The record in that table the walk is about.</param>
/// <param name="Index">Which of the referring records the reader is on.</param>
/// <param name="Matches">How many refer to it.</param>
public sealed record Walk(
    ResolvedForeignKey Key,
    TableNode From,
    long Ordinal,
    long Index,
    long Matches)
{
    /// <summary>True when there is an earlier referring record to step to.</summary>
    public bool HasPrevious => Index > 0;

    /// <summary>True when there is a later referring record to step to.</summary>
    public bool HasNext => Index + 1 < Matches;
}

/// <summary>
/// One table's place in the workspace: its view, where it is positioned, what was last said
/// about it, and the walk being stepped through in it, if any.
/// </summary>
/// <remarks>
/// Everything here belongs to one table and lives with it. That is the whole of the fix to the
/// stale walk bar and the stale status line: leaving a tab cannot leave a message behind,
/// because the message was never shared. See the change's design.md D4 and D5.
/// </remarks>
public sealed class TableTab(TableNode table)
{
    /// <summary>The table this tab shows.</summary>
    public TableNode Table { get; } = table;

    /// <summary>
    /// Which tab this is to the store, which is the connection its views are built on.
    /// </summary>
    /// <remarks>
    /// A number rather than the table, because a tab is what asks: two tabs of one table would
    /// each want their own, and a tab closed and opened again is a new one. Everything this tab
    /// asks of the store carries it, so that stopping this tab's work stops only this tab's work.
    /// </remarks>
    public int Lane { get; init; }

    /// <summary>The record in view, as an index into the table's rows, or -1 for none.</summary>
    public int Position { get; set; } = -1;

    /// <summary>What was last said about a navigation concerning this table.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The backwards walk being stepped through here, or none.</summary>
    public Walk? Walk { get; set; }

    /// <summary>
    /// What this tab has asked to see of its table: which records, and in what order.
    /// </summary>
    /// <remarks>
    /// Part of where a person is in a table, like the record they are positioned at, so it lives
    /// with the tab: leaving a table and coming back finds the filter still in force, and closing
    /// the tab is what puts the table back as the file delivers it.
    /// </remarks>
    public TableQuery Query { get; set; } = TableQuery.FileOrder;

    /// <summary>The figures chosen for this table's columns, in the order they were chosen.</summary>
    public IReadOnlyList<ColumnFigure> Figures { get; set; } = [];

    /// <summary>The store's name for this tab's view, or none while it shows the table itself.</summary>
    public string? View { get; set; }

    /// <summary>The records the grid is bound to, whether the table's or its view's.</summary>
    public TablePageList? Rows { get; set; }

    private string notice = string.Empty;

    /// <summary>How long a notice is worth showing.</summary>
    /// <remarks>
    /// Long enough to read a sentence, and no longer: what it reports has already happened, and
    /// the banner goes on saying what the table is showing after the notice has gone.
    /// </remarks>
    public static TimeSpan NoticeLife { get; } = TimeSpan.FromSeconds(6);

    /// <summary>What was last said about this tab's view, until it has been read.</summary>
    /// <remarks>
    /// Separate from <see cref="Status"/>, which reports a navigation and stays. A filter lifted
    /// to reach a record is worth saying once and then forgetting, and a person who missed it can
    /// see the banner say the table is unfiltered. Saying it stamps how long it lasts, so that
    /// whoever says it does not have to remember to take it back.
    /// </remarks>
    public string Notice
    {
        get => notice;
        set
        {
            notice = value;
            NoticeUntil = DateTimeOffset.UtcNow + NoticeLife;
        }
    }

    /// <summary>When this tab's notice stops being worth showing.</summary>
    public DateTimeOffset NoticeUntil { get; private set; }

    /// <summary>The notice, while it still stands.</summary>
    public string NoticeAt(DateTimeOffset now) => now < NoticeUntil ? notice : string.Empty;
}

/// <summary>
/// The tabs the reader has open: the summary, and one per table that has been reached.
/// </summary>
/// <remarks>
/// At most one tab per table, reused when the table is reached again, because a person following
/// references repeatedly must not accumulate duplicate views of the same table. The start page is
/// a tab of its own, shown first and never closed: an export's condition is what someone handed a
/// medium needs before they need any of its tables.
/// </remarks>
/// <param name="session">The open export, whose own table nodes identify a tab.</param>
public sealed class ReaderTabs(ReaderSession session)
{
    private readonly List<TableTab> tables = [];
    private int lanes;

    /// <summary>The table tabs, in the order they were opened.</summary>
    public IReadOnlyList<TableTab> Tables => tables;

    /// <summary>The tab in front, or null when that is the start page.</summary>
    public TableTab? Active { get; private set; }

    /// <summary>True when the start page is the tab in front.</summary>
    public bool StartPageActive => Active is null;

    /// <summary>The table in front, which is what the navigator indicates.</summary>
    public TableNode? ActiveTable => Active?.Table;

    /// <summary>The tab for a table, if it has one.</summary>
    /// <remarks>
    /// A table is identified by the session's own node for it. A caller may hold a node from
    /// another parse of the same <c>index.xml</c> — a navigation, a test fixture — and two nodes
    /// standing for one table would give that table two tabs, which is the very thing a tab per
    /// table exists to prevent.
    /// </remarks>
    public TableTab? For(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var owned = session.Own(table);
        return tables.FirstOrDefault(tab => ReferenceEquals(tab.Table, owned));
    }

    /// <summary>
    /// Brings a table to the front, creating its tab the first time it is reached.
    /// </summary>
    /// <remarks>
    /// The single way a table reaches the front, whether it was chosen in the navigator or
    /// arrived by following a reference.
    /// </remarks>
    public TableTab Open(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var tab = For(table);
        if (tab is null)
        {
            // Never reused, not even by a table whose tab was closed: the store may still be
            // closing the connection the old tab was querying on.
            tab = new TableTab(session.Own(table)) { Lane = lanes++ };
            tables.Add(tab);
        }

        Activate(tab);
        return tab;
    }

    /// <summary>Brings the start page to the front.</summary>
    public void ShowStartPage() => Activate(null);

    /// <summary>
    /// The table a navigator entry opens, or none when choosing it opens nothing.
    /// </summary>
    /// <remarks>
    /// The navigator lists a table's relationships beneath it, and those entries are information
    /// about the table rather than places to go: following a reference is an action on a record's
    /// value, not on a table, and a table is already reachable under its own medium. So a
    /// relationship entry selects and does nothing else. See the change's design.md D6.
    /// </remarks>
    public static TableNode? Opens(object? entry) => entry as TableNode;

    /// <summary>Closes a table's tab, leaving every other tab as it was.</summary>
    public void Close(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (For(table) is not { } tab)
        {
            return;
        }

        var index = tables.IndexOf(tab);
        tables.RemoveAt(index);

        // A view carries the records it selects, so a closed tab's view is a copy of a table
        // nobody is looking at. It goes with the tab, and the table opens again as the file
        // delivers it.
        session.Discard(tab.View, tab.Lane);
        tab.View = null;
        tab.Rows = null;

        if (ReferenceEquals(Active, tab))
        {
            // The neighbour it left, or the start page when it had none.
            Active = tables.Count == 0 ? null : tables[Math.Min(index, tables.Count - 1)];
        }
    }

    /// <summary>
    /// Applies what a navigation produced: brings the table it concerns to the front, positions
    /// it, and records what is said about it in that table's own tab.
    /// </summary>
    /// <param name="navigation">Where following the reference led.</param>
    /// <param name="walk">The walk this navigation is a step of, or none for a forward one.</param>
    public TableTab? Apply(Navigation navigation, Walk? walk = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        if (navigation.Table is not { } table)
        {
            return null;
        }

        var tab = Open(table);
        tab.Status = Describe(navigation);

        // A forward navigation clears any walk the tab was holding: it shows no stepping
        // controls at all, which is the bug this replaces — one bar served both jobs and forward
        // navigation showed it with its buttons disabled. See design.md D5.
        tab.Walk = walk;

        if (navigation.Kind == NavigationKind.Positioned && navigation.Position >= 0)
        {
            tab.Position = navigation.Position;
        }

        return tab;
    }

    /// <summary>
    /// Applies a navigation to a tab that may be filtered, lifting the filter when it hides the
    /// record the navigation names.
    /// </summary>
    /// <remarks>
    /// A navigation that landed anywhere but the record it names would be wrong, and a filter is
    /// a thing a person set rather than a thing the export says — so the filter gives way, the
    /// sort does not, and the tab says what happened. Only a filter that actually hides the record
    /// is lifted: a view that shows it is left exactly as it was. See the change's design.md D9.
    /// </remarks>
    /// <param name="navigation">Where following the reference led.</param>
    /// <param name="walk">The walk this navigation is a step of, or none for a forward one.</param>
    /// <param name="preparation">The tab's own preparation, for rebuilding its view.</param>
    public async Task<TableTab?> ApplyAsync(Navigation navigation, Walk? walk, ViewPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(preparation);

        if (Apply(navigation, walk) is not { } tab)
        {
            return null;
        }

        // A tab showing the table itself is already positioned: the session worked that index out
        // against the very records it is showing. A tab showing a view of them is not, because a
        // sort renumbers every position — so the view is asked, whether or not it also filters.
        if (navigation.Kind != NavigationKind.Positioned
            || tab.Rows is not { } rows
            || tab.View is null)
        {
            return tab;
        }

        var position = rows.IndexOfOrdinal(navigation.Ordinal);
        if (position < 0 && tab.Query.Filters.Count > 0)
        {
            var lifted = new TableQuery([], tab.Query.Sorts);
            var prepared = await preparation.PrepareAsync(tab.Table, lifted, tab.View).ConfigureAwait(false);
            if (prepared.Rows is { } rebuilt)
            {
                tab.Query = lifted;
                tab.View = rebuilt.View;
                tab.Rows = rebuilt.Records;
                tab.Notice = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Filter removed to show record {navigation.Ordinal}.");

                position = rebuilt.Records.IndexOfOrdinal(navigation.Ordinal);
            }
        }

        // What the view says, including that it holds the record nowhere: the index this tab was
        // given belongs to the table's own order, and scrolling to it in a sorted view would land
        // on an unrelated record.
        tab.Position = position;
        return tab;
    }

    /// <summary>
    /// Ends the walk in whichever tab is in front, as leaving that table does.
    /// </summary>
    /// <remarks>
    /// Stepping between referring records is an act inside one table; carrying it to another
    /// would step through records the reader can no longer see.
    /// </remarks>
    private void Activate(TableTab? tab)
    {
        if (Active is { } leaving && !ReferenceEquals(leaving, tab))
        {
            leaving.Walk = null;
        }

        Active = tab;
    }

    /// <summary>What a navigation is reported as, in the tab of the table it concerns.</summary>
    public static string Describe(Navigation navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        return navigation.Kind switch
        {
            NavigationKind.Positioned when navigation.Matches > 1 => string.Create(
                CultureInfo.InvariantCulture,
                $"Record {navigation.MatchIndex + 1} of {navigation.Matches} referring to '{navigation.Value}'."),

            NavigationKind.Positioned => string.Create(
                CultureInfo.InvariantCulture,
                $"Record {navigation.Ordinal} for '{navigation.Value}'."),

            NavigationKind.Unresolved =>
                $"'{navigation.Value}' matches no record of '{navigation.Table?.Identity}'. "
                + "The reference does not resolve.",

            NavigationKind.Unreadable =>
                $"'{navigation.Table?.Identity}' does not conform to its declaration, so it has no "
                + "data to show. There is nowhere to follow this reference to.",

            NavigationKind.NotReady =>
                $"'{navigation.Table?.Identity}' is still being read. It will show its data as soon "
                + "as it has been.",

            NavigationKind.NoReference => "This record refers to nothing here.",

            _ => string.Empty,
        };
    }
}
