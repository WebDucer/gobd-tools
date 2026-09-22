using System.Collections;
using GoBd.Reader.Data;
using GoBd.Validation.Content;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>One record as the grid shows it.</summary>
/// <param name="Position">Where this record sits in what is being shown, counting from one.</param>
/// <param name="Ordinal">Record number, as the file counts records, so a record can be cited.</param>
/// <param name="Values">Column values in declared order, as the declaration presents them.</param>
/// <param name="Unresolved">
/// Columns of this record whose foreign key matches no record of the table it points at.
/// </param>
/// <remarks>
/// A record carries both numbers because they answer different questions. The position says where
/// to look on the screen and changes with every filter; the record number is the file's own and is
/// what a finding cites and a person quotes, whatever order the records are shown in.
/// </remarks>
public sealed record RecordRow(
    long Position,
    long Ordinal,
    IReadOnlyList<string> Values,
    IReadOnlyList<int> Unresolved)
{
    /// <summary>True when this record's value in that column refers to nothing.</summary>
    public bool RefersToNothing(int column) => Unresolved.Contains(column);
}

/// <summary>
/// The grid's view of a table: a list that reports millions and holds a few thousand.
/// </summary>
/// <remarks>
/// Row virtualisation is not data virtualisation. The control realises only the rows it draws,
/// but it still asks the bound collection for its count and indexes into it — back that with
/// four million materialised objects and the recycling saves nothing. So this serves a window
/// from the store and materialises nothing outside it, evicting the page used longest ago.
/// See the change's design.md D6.
/// <para>
/// The same list serves a table in file order and a filtered or sorted view of it. Which one it
/// is showing changes where a page comes from and how a record's position is found; it changes
/// nothing about what a row carries, because a view holds the records themselves and every one
/// of them keeps the number the file gave it.
/// </para>
/// </remarks>
public sealed class TablePageList : IList, IReadOnlyList<RecordRow>
{
    private readonly ExportStore store;
    private readonly TableNode table;
    private readonly RecordLayout layout;
    private readonly IReadOnlyList<ResolvedForeignKey> keys;
    private readonly string? view;
    private readonly bool redefines;
    private readonly int pageSize;
    private readonly int maxPages;
    private readonly Dictionary<long, RecordRow[]> pages = [];
    private readonly HashSet<long> incomplete = [];
    private readonly LinkedList<long> recency = new();
    private readonly Dictionary<long, LinkedListNode<long>> recencyNodes = [];

    /// <summary>Creates a view over one imported table.</summary>
    /// <param name="store">Where the table's records are.</param>
    /// <param name="table">The declared table.</param>
    /// <param name="layout">The layout it was imported under.</param>
    /// <param name="count">Records it holds, or that its view holds.</param>
    /// <param name="keys">Its declared foreign keys, for marking the values that resolve to nothing.</param>
    /// <param name="view">The store's view to page, or null for the table in file order.</param>
    /// <param name="pageSize">Records fetched at a time.</param>
    /// <param name="maxPages">Pages held at once.</param>
    public TablePageList(
        ExportStore store,
        TableNode table,
        RecordLayout layout,
        long count,
        IReadOnlyList<ResolvedForeignKey>? keys = null,
        string? view = null,
        int pageSize = 512,
        int maxPages = 8)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);

        this.store = store;
        this.table = table;
        this.layout = layout;
        this.keys = keys ?? [];
        this.view = view;
        this.pageSize = pageSize;
        this.maxPages = maxPages;
        redefines = layout.Columns.Any(column => column.Declaration.Maps.Count > 0);
        Count = count > int.MaxValue ? int.MaxValue : (int)count;
    }

    /// <summary>Records the table holds, or that its view selects.</summary>
    public int Count { get; }

    /// <summary>Records materialised at once, at the worst moment.</summary>
    public int PeakResident { get; private set; }

    /// <summary>Pages fetched from the store.</summary>
    public long PageLoads { get; private set; }

    /// <summary>The record at the given position, fetching its page if it is not resident.</summary>
    public RecordRow this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

            var pageIndex = (long)index / pageSize;
            if (pages.TryGetValue(pageIndex, out var page)
                && !(incomplete.Contains(pageIndex) && MarksAvailable()))
            {
                Touch(pageIndex);
            }
            else
            {
                page = Load(pageIndex);
            }

            return page[index - (int)(pageIndex * pageSize)];
        }
    }

    /// <summary>The position of the record with the given number, or -1.</summary>
    /// <remarks>
    /// Navigation positions the grid at a record, and positioning a virtualised list needs that
    /// record's index rather than its key. The store materialised the record number at import,
    /// so this is a lookup rather than a scan. A view answers -1 for a record it hides, which is
    /// what tells a navigation to lift the filter rather than land somewhere else.
    /// </remarks>
    public int IndexOfOrdinal(long ordinal)
    {
        var position = view is null
            ? store.PositionOfOrdinal(table, ordinal)
            : store.PositionInView(view, ordinal);

        return position < 0 || position >= Count ? -1 : (int)position;
    }

    private RecordRow[] Load(long pageIndex)
    {
        // A page loaded again to complete its marks replaces itself rather than counting against
        // the cache as a second page.
        if (recencyNodes.Remove(pageIndex, out var existing))
        {
            recency.Remove(existing);
            pages.Remove(pageIndex);
        }

        while (pages.Count >= maxPages)
        {
            var evict = recency.First!.Value;
            recency.RemoveFirst();
            recencyNodes.Remove(evict);
            pages.Remove(evict);
            incomplete.Remove(evict);
        }

        var offset = pageIndex * pageSize;
        var records = view is null
            ? store.Page(table, offset, pageSize)
            : store.PageView(table, view, offset, pageSize);
        var marks = Marks(records, out var complete);

        var page = new RecordRow[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            page[index] = Present(
                records[index],
                offset + index + 1,
                marks.TryGetValue(records[index].Ordinal, out var columns) ? columns : []);
        }

        PageLoads++;

        // A page whose marks could not all be asked for is kept, but marked incomplete. The tables
        // a key points at are read in document order, so a table opened early may be paged before
        // the table it references exists. Such a page is loaded again the first time it is touched
        // once every table its keys point at is in the store — not on every access, which paid a
        // page query and an anti-join per key for each row the grid drew while an import ran.
        if (complete)
        {
            incomplete.Remove(pageIndex);
        }
        else
        {
            incomplete.Add(pageIndex);
        }

        pages[pageIndex] = page;
        recencyNodes[pageIndex] = recency.AddLast(pageIndex);
        PeakResident = Math.Max(PeakResident, pages.Count * pageSize);
        return page;
    }

    /// <summary>True when every table this table's keys point at is in the store.</summary>
    private bool MarksAvailable() => keys.All(key => store.IsImported(key.Referenced));

    /// <summary>
    /// Which of this page's foreign key values match no record of the table they point at.
    /// </summary>
    /// <remarks>
    /// One anti-join per key, restricted to the records this page actually shows. A page in file
    /// order occupies a run of record numbers, and asking by range lets the store skip whole row
    /// groups; a sorted or filtered page draws from anywhere in the table, and is asked for by
    /// number instead. Not taken from the findings either: those stop at the per-table bound, and
    /// a table is not bounded. See the change's design.md D6 and D7.
    /// </remarks>
    private Dictionary<long, List<int>> Marks(IReadOnlyList<StoredRecord> records, out bool complete)
    {
        var marks = new Dictionary<long, List<int>>();
        complete = true;
        if (records.Count == 0 || keys.Count == 0)
        {
            return marks;
        }

        var ordinals = records.Select(record => record.Ordinal).ToArray();
        foreach (var key in keys)
        {
            if (!store.IsImported(key.Referenced))
            {
                complete = false;
                continue;
            }

            var unresolved = view is null
                ? store.UnresolvedBetween(
                    table,
                    key.ReferringColumns,
                    key.Referenced,
                    key.ReferencedColumns,
                    ordinals[0],
                    ordinals[^1])
                : store.UnresolvedAmong(
                    table,
                    key.ReferringColumns,
                    key.Referenced,
                    key.ReferencedColumns,
                    ordinals);

            foreach (var ordinal in unresolved)
            {
                if (!marks.TryGetValue(ordinal, out var columns))
                {
                    columns = [];
                    marks[ordinal] = columns;
                }

                foreach (var column in key.ReferringColumns)
                {
                    if (!columns.Contains(column))
                    {
                        columns.Add(column);
                    }
                }
            }
        }

        return marks;
    }

    /// <summary>
    /// Applies the declaration to a record's values.
    /// </summary>
    /// <remarks>
    /// A declared <c>Map</c> redefines a stored value, so the grid shows what the declaration
    /// says the value means. That is the only thing applied: every other value is shown exactly
    /// as the file stores it, because the values are already written in the declared format and
    /// reformatting them would put this tool's rendering between the reader and the evidence. A
    /// table that declares no map is therefore shown from the values as fetched.
    /// </remarks>
    private RecordRow Present(StoredRecord record, long position, IReadOnlyList<int> unresolved)
    {
        if (!redefines && record.Values.Count == layout.Columns.Count)
        {
            return new RecordRow(position, record.Ordinal, record.Values, unresolved);
        }

        var values = new string[layout.Columns.Count];
        for (var index = 0; index < values.Length; index++)
        {
            var stored = index < record.Values.Count ? record.Values[index] : string.Empty;
            values[index] = ValueInterpreter.Present(stored, layout.Columns[index]);
        }

        return new RecordRow(position, record.Ordinal, values, unresolved);
    }

    private void Touch(long pageIndex)
    {
        var node = recencyNodes[pageIndex];
        recency.Remove(node);
        recency.AddLast(node);
    }

    /// <summary>Enumerates by index, so nothing outside the window is held.</summary>
    public IEnumerator<RecordRow> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    bool IList.IsFixedSize => true;

    bool IList.IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Clear() => throw new NotSupportedException();

    bool IList.Contains(object? value) => IndexOf(value) >= 0;

    int IList.IndexOf(object? value) => IndexOf(value);

    private int IndexOf(object? value) =>
        value is RecordRow row ? IndexOfOrdinal(row.Ordinal) : -1;

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();

    void IList.RemoveAt(int index) => throw new NotSupportedException();

    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();
}
