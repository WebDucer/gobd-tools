using System.Collections.Concurrent;
using System.Globalization;
using DuckDB.NET.Data;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;
using GoBd.Validation.Sources;

namespace GoBd.Reader.Data;

/// <summary>Why a store would not open.</summary>
public abstract record StoreRefusal;

/// <summary>The import would not fit on the volume the store lives on.</summary>
/// <param name="RequiredBytes">Room the import was estimated to need.</param>
/// <param name="AvailableBytes">Room the backing volume had.</param>
/// <param name="VolumeRoot">The volume that was measured.</param>
/// <param name="MemoryBacked">True when that volume is memory rather than disk.</param>
public sealed record NotEnoughSpace(long RequiredBytes, long AvailableBytes, string VolumeRoot, bool MemoryBacked)
    : StoreRefusal;

/// <summary>The directory the stores live in would not keep the export's data private.</summary>
/// <param name="Directory">The directory that was refused.</param>
/// <param name="Problem">Why it was refused.</param>
public sealed record LocationNotPrivate(string Directory, StorePrivacyProblem Problem) : StoreRefusal;

/// <summary>Outcome of opening a store.</summary>
/// <param name="Store">The store, when it opened.</param>
/// <param name="Refusal">Why it did not, when it did not.</param>
public sealed record StoreOpenResult(ExportStore? Store, StoreRefusal? Refusal);

/// <summary>How far a table got.</summary>
public enum TableImportStatus
{
    /// <summary>The table's records are in the store.</summary>
    Imported,

    /// <summary>The declared file is not in the export.</summary>
    FileAbsent,

    /// <summary>The declaration does not say how to read the file.</summary>
    LayoutUnusable,
}

/// <summary>What importing one table produced.</summary>
/// <param name="Status">How far it got.</param>
/// <param name="Findings">What was wrong with its records, in record order.</param>
/// <param name="Records">Data records imported.</param>
/// <param name="Truncated">True when analysis stopped at the bound.</param>
/// <param name="Bytes">Bytes read from the table's file, which is what progress is measured in.</param>
public sealed record TableImport(
    TableImportStatus Status,
    IReadOnlyList<Finding> Findings,
    long Records,
    bool Truncated,
    long Bytes = 0)
{
    /// <summary>True when the table may be presented as data rather than as a report.</summary>
    public bool Conforms => Status == TableImportStatus.Imported && Findings.Count == 0;
}

/// <summary>
/// One export, imported into an embedded analytical store.
/// </summary>
/// <remarks>
/// The store exists so that a table of several million records can be paged through at constant
/// memory and its keys checked without a ceiling. It is deliberately short-lived: written to the
/// platform temporary location, never beside the export, and deleted when it is closed. The
/// export is a data carrier and an auditor's medium must come back unchanged.
/// </remarks>
public sealed class ExportStore : IDisposable
{
    private readonly IExportSource source;
    private readonly DataSetNode dataSet;
    private readonly DuckDBConnection writing;
    private readonly DuckDBConnection reading;

    /// <summary>
    /// The connection each tab filters, sorts and totals on, made when that tab first asks.
    /// </summary>
    /// <remarks>
    /// A connection apart from the other two, because those are busy with work a person is waiting
    /// on: the import writes on one, and the grid pages on the other from the UI thread, where a
    /// statement that takes a second would be a second of a frozen window. See the change's
    /// design.md D5.
    /// <para>
    /// And one per tab rather than one for all of them, for the two reasons a shared one cannot
    /// serve. A connection is not safe to use from two threads at once, and two tabs building
    /// views are two threads. And stopping a superseded view means interrupting the connection it
    /// runs on, which on a shared one would also stop whatever every other tab was computing —
    /// work nobody asked to abandon.
    /// </para>
    /// </remarks>
    private readonly Dictionary<int, QueryLane> lanes = [];
    private readonly Lock lanesGate = new();
    private readonly string connectionString;
    private readonly StoreLock claim;

    /// <summary>How many views this store has made, so that no two of them share a name.</summary>
    private int generated;
    private readonly Dictionary<TableNode, string> names = [];
    private readonly ConcurrentDictionary<TableNode, RecordLayout> layouts = [];
    private readonly ConcurrentDictionary<TableNode, TableExtent> extents = [];
    private readonly ConcurrentDictionary<TableNode, TableCapabilities> capabilities = [];

    private static int opened;

    private ExportStore(
        IExportSource source,
        DataSetNode dataSet,
        string directory,
        DuckDBConnection writing,
        DuckDBConnection reading,
        StoreLock claim)
    {
        this.source = source;
        this.dataSet = dataSet;
        this.writing = writing;
        this.reading = reading;
        this.claim = claim;
        connectionString = ConnectionString(directory);
        Directory = directory;

        // Positional names, so that a declared name cannot reach SQL and two columns declaring
        // the same name — which GOBD3026 reports and the standard does not forbid outright —
        // cannot collide here.
        var index = 0;
        foreach (var table in dataSet.Tables)
        {
            names[table] = "t_" + index.ToString(CultureInfo.InvariantCulture);
            index++;
        }
    }

    /// <summary>Where this store's files live.</summary>
    public string Directory { get; }

    /// <summary>
    /// Removes stores left behind by runs that are no longer alive.
    /// </summary>
    /// <remarks>
    /// Only stores nobody holds. A second instance of the reader may be part way through an
    /// import right now, and age cannot tell that apart from an abandonment — so cleanup proves
    /// a store is unowned rather than assuming it from how old it looks.
    /// </remarks>
    public static void RemoveAbandonedStores(StoreOptions? options = null)
    {
        // A directory that is not private is not the reader's to clean: it may be someone
        // else's, and opening an export there is refused anyway.
        var root = (options ?? StoreOptions.Default).Root;
        if (!System.IO.Directory.Exists(root) || StorePrivacy.Check(root) is not null)
        {
            return;
        }

        foreach (var directory in System.IO.Directory.EnumerateDirectories(root))
        {
            if (StoreLock.IsUnowned(directory))
            {
                Delete(directory);
            }
        }
    }

    /// <summary>Opens a store for one export, or refuses because there is not room for it.</summary>
    public static StoreOpenResult Open(IExportSource source, DataSetNode dataSet, StoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dataSet);

        var settings = options ?? StoreOptions.Default;

        // The export's data goes nowhere another account can read it: before anything is swept,
        // measured or written, the directory is made private or refused. See the
        // prepare-public-release change's design.md D6.
        if (StorePrivacy.Ensure(settings.Root) is { } problem)
        {
            return new StoreOpenResult(null, new LocationNotPrivate(settings.Root, problem));
        }

        RemoveAbandonedStores(settings);

        // The space check happens before the first byte is written. Discovering the shortfall
        // half way through a multi-gigabyte import wastes exactly the time it was meant to save.
        var required = (long)(DeclaredBytes(source, dataSet) * settings.SpaceHeadroom);
        var volume = Volumes.For(settings.Root);
        if (volume.AvailableBytes < required)
        {
            return new StoreOpenResult(
                null,
                new NotEnoughSpace(required, volume.AvailableBytes, volume.VolumeRoot, volume.IsMemoryBacked));
        }

        // One directory per open store, not merely per export. Two instances of the reader —
        // or one instance opening the same medium twice — must not share a database file, and
        // since a store is deleted when it closes there is nothing to be gained by making the
        // location reusable. The key still says which export a directory belongs to.
        var directory = StoreLocation.For(source, settings)
            + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
            + "." + Interlocked.Increment(ref opened).ToString(CultureInfo.InvariantCulture);
        var claim = StoreLock.TryTake(directory)
            ?? throw new IOException($"The store directory '{directory}' is held by another process.");

        // A connection per thread that uses the store: the import writes on this first one, the
        // grid pages finished tables on the second from the UI thread, and each tab filters,
        // sorts and totals on one of its own, opened when it first asks. DuckDB serves several
        // connections to one database within a process, and a connection is not safe to share
        // across threads, so none of these is shared. Duplicate() is not the way to get the
        // others — it refuses for anything but an in-memory database — so each is opened from the
        // same connection string, which reaches the same in-process database. A table one of them
        // creates becomes visible to the others without any being reopened. Measured in
        // StoreConcurrencyTests; see the change's design.md D3 and D5.
        var connectionString = ConnectionString(directory);
        var writing = new DuckDBConnection(connectionString);
        writing.Open();
        var reading = new DuckDBConnection(connectionString);
        reading.Open();
        return new StoreOpenResult(
            new ExportStore(source, dataSet, directory, writing, reading, claim),
            null);
    }

    /// <summary>
    /// Imports one table and reports what was wrong with its records.
    /// </summary>
    /// <param name="table">The declared table.</param>
    /// <param name="bound">Findings after which analysis of this table stops.</param>
    public TableImport Import(TableNode table, int bound = ContentOptions.DefaultMaximumFindingsPerTable) =>
        Import(table, new FindingBudget(bound));

    /// <summary>
    /// Imports one table and reports what was wrong with its records.
    /// </summary>
    /// <param name="table">The declared table.</param>
    /// <param name="budget">The room this table has left to report defects.</param>
    /// <param name="read">Told the bytes read from this table's file, as they are read.</param>
    /// <param name="cancellationToken">
    /// Stops the import between records and between its stages. A statement already inside the
    /// store is stopped by <see cref="Interrupt"/> instead, from the thread that cancels.
    /// </param>
    public TableImport Import(
        TableNode table,
        FindingBudget budget,
        Action<long>? read = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(budget);
        var bound = budget.Bound;

        var data = TableData.For(source, table);
        if (data.Status == TableDataStatus.LayoutUnusable)
        {
            // The same finding the streaming engine reports for the same table. The file is
            // there and nothing else says why it was not read.
            return new TableImport(
                TableImportStatus.LayoutUnusable,
                [ContentFindings.TableNotReadable(table, data.Layout.Fault)],
                0,
                false);
        }

        if (!data.IsReady)
        {
            // An absent file is GOBD1003's to report, not a content code's.
            return new TableImport(TableImportStatus.FileAbsent, [], 0, false);
        }

        var layout = data.Layout;
        var name = names[table];
        var extracted = TableExtraction.Extract(
            source,
            data.EntryName,
            layout,
            Path.Combine(Directory, name + ".csv"),
            bound,
            read,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        Create(name, layout, extracted.Path);
        Discard(extracted.Path);

        cancellationToken.ThrowIfCancellationRequested();
        var defects = new List<ExtractionDefect>(extracted.Defects);
        defects.AddRange(CellDefects(name, layout, bound));

        // The same order the streaming engine reports in: what is wrong with a record first,
        // then its columns from left to right, records in file order.
        var ordered = defects
            .OrderBy(defect => defect.Record)
            .ThenBy(defect => defect.Defect.ColumnIndex ?? -1)
            .ToList();

        var findings = new List<Finding>();
        foreach (var defect in ordered)
        {
            if (!budget.TryReport(table))
            {
                break;
            }

            findings.Add(ContentFindings.For(table, layout, defect.Record, defect.Defect));
        }

        var truncated = budget.WasTruncated(table);
        findings.AddRange(budget.ClaimNotices());

        cancellationToken.ThrowIfCancellationRequested();

        // Published only now, at the end, because this is what says the table's records are in
        // the store: the key checks ask IsImported before joining against a table, and a table
        // whose extraction threw half way has a layout but no rows to join. Recording the layout
        // on the way in made IsImported true for a table that does not exist in SQL, and the
        // key checks then failed with a catalog error rather than skipping it.
        layouts[table] = layout;
        extents[table] = Extent(name);

        // Only a table that will be shown is measured and given a query view. A table whose data
        // is withheld cannot be filtered, sorted or totalled, and measuring it would read values
        // the reader has already declared unreadable.
        if (findings.Count == 0)
        {
            var measured = Measure(name, layout);
            capabilities[table] = measured;
            CreateQueryView(name, layout, measured);
        }
        return new TableImport(
            TableImportStatus.Imported,
            findings,
            extracted.Records,
            truncated,
            extracted.Bytes);
    }

    /// <summary>Data records the store holds for a table.</summary>
    public long RecordCount(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return extents.TryGetValue(table, out var extent)
            ? extent.Count
            : Convert.ToInt64(
                Scalar(reading, $"SELECT count(*) FROM {names[table]}"),
                CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The span of record numbers a table occupies, and whether they run without a gap.
    /// </summary>
    /// <remarks>
    /// Measured once, at import, because it decides how a page is addressed. Data records are
    /// consecutive by construction — the extract holds every record the declaration includes, in
    /// file order — but a claim that governs correctness is worth checking rather than assuming,
    /// and the slower addressing is there for the case where it does not hold.
    /// </remarks>
    private TableExtent Extent(string name)
    {
        var first = 0L;
        var last = 0L;
        var count = 0L;
        Query(
            writing,
            $"SELECT coalesce(min({TableExtraction.OrdinalColumn}), 0),"
            + $" coalesce(max({TableExtraction.OrdinalColumn}), 0), count(*) FROM {name}",
            [],
            row =>
            {
                first = row.GetInt64(0);
                last = row.GetInt64(1);
                count = row.GetInt64(2);
            });

        return new TableExtent(first, count, count > 0 && last - first + 1 == count);
    }

    /// <summary>
    /// Reads one window of a table, in file order.
    /// </summary>
    /// <remarks>
    /// Addressed by position rather than by key, and materialising only the window asked for.
    /// This is what the grid's paging list is built on: it reports a count of several million and
    /// asks for the twenty rows it is about to draw.
    /// </remarks>
    /// <param name="table">The declared table.</param>
    /// <param name="offset">Rows to skip, counting from the first data record.</param>
    /// <param name="count">Rows wanted.</param>
    public IReadOnlyList<StoredRecord> Page(TableNode table, long offset, int count)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var layout = layouts[table];
        var columns = string.Join(", ", Enumerable.Range(0, layout.Columns.Count).Select(Column));

        // Addressed by record number rather than by OFFSET. OFFSET makes the store count past
        // every row it skips, so a page near the end of a table of millions costs a scan of the
        // whole table — which is felt as soon as anyone drags the scrollbar rather than wheeling.
        var where = extents.TryGetValue(table, out var extent) && extent.Contiguous
            ? $" WHERE {TableExtraction.OrdinalColumn} >= {(extent.First + offset).ToString(CultureInfo.InvariantCulture)}"
                + $" AND {TableExtraction.OrdinalColumn} < {(extent.First + offset + count).ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        var skip = where.Length > 0
            ? string.Empty
            : $" OFFSET {offset.ToString(CultureInfo.InvariantCulture)}";

        var sql = $"SELECT {TableExtraction.OrdinalColumn}, {columns} FROM {names[table]}"
            + where
            + $" ORDER BY {TableExtraction.OrdinalColumn}"
            + $" LIMIT {count.ToString(CultureInfo.InvariantCulture)}"
            + skip;

        var page = new List<StoredRecord>(count);
        Query(reading, sql, [], row => page.Add(Materialise(row, layout.Columns.Count)));
        return page;
    }

    /// <summary>
    /// One record as the store holds it: the record number, then a value per declared column.
    /// </summary>
    /// <remarks>
    /// Stated once, because a page of a table and a page of a view of it must agree about what a
    /// value is. An absent value is the empty string, which is what the rest of the store reads a
    /// missing one as.
    /// </remarks>
    private static StoredRecord Materialise(System.Data.IDataReader row, int columns)
    {
        var values = new string[columns];
        for (var index = 0; index < columns; index++)
        {
            values[index] = row.IsDBNull(index + 1) ? string.Empty : row.GetString(index + 1);
        }

        return new StoredRecord(row.GetInt64(0), values);
    }

    /// <summary>
    /// The values of one record's columns, by record number.
    /// </summary>
    /// <remarks>
    /// Following a reference starts from the record in front of the reader, so the key it
    /// carries has to be read back out of the store rather than assumed from what the grid
    /// happens to be holding.
    /// </remarks>
    public IReadOnlyList<string> ValuesAt(TableNode table, long ordinal, IReadOnlyList<int> columns)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columns);

        var selected = string.Join(", ", columns.Select(column => $"coalesce({Column(column)}, '')"));
        var sql = $"SELECT {selected} FROM {names[table]}"
            + $" WHERE {TableExtraction.OrdinalColumn} = {ordinal.ToString(CultureInfo.InvariantCulture)}";

        var values = Array.Empty<string>();
        Query(reading, sql, [], row =>
        {
            values = new string[columns.Count];
            for (var index = 0; index < columns.Count; index++)
            {
                values[index] = row.IsDBNull(index) ? string.Empty : row.GetString(index);
            }
        });

        return values;
    }

    /// <summary>Record numbers whose columns carry exactly these values, in file order.</summary>
    /// <param name="table">The table to search.</param>
    /// <param name="columns">Columns forming the key.</param>
    /// <param name="values">Values the key must carry, in the same order.</param>
    /// <param name="limit">Most record numbers to return.</param>
    public IReadOnlyList<long> OrdinalsMatching(
        TableNode table,
        IReadOnlyList<int> columns,
        IReadOnlyList<string> values,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(table);

        var found = new List<long>(Math.Min(limit, 1024));
        Query(
            reading,
            Matching(table, columns, values)
                + $" ORDER BY {TableExtraction.OrdinalColumn} LIMIT {limit.ToString(CultureInfo.InvariantCulture)}",
            values,
            row => found.Add(row.GetInt64(0)));

        return found;
    }

    /// <summary>How many records carry exactly these values.</summary>
    public long CountMatching(TableNode table, IReadOnlyList<int> columns, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(table);

        var total = 0L;
        Query(
            reading,
            "SELECT count(*) FROM (" + Matching(table, columns, values) + ")",
            values,
            row => total = row.GetInt64(0));

        return total;
    }

    private string Matching(TableNode table, IReadOnlyList<int> columns, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(values);
        if (columns.Count != values.Count)
        {
            throw new ArgumentException("A key needs as many values as it has columns.", nameof(values));
        }

        // The values come out of the data file, so they are passed as parameters rather than
        // written into the statement. A key value is not something to trust with SQL.
        var where = string.Join(
            " AND ",
            columns.Select(column => $"coalesce({Column(column)}, '') = ?"));

        return $"SELECT {TableExtraction.OrdinalColumn} FROM {names[table]} WHERE {where}";
    }

    private static void Query(
        DuckDBConnection on,
        string sql,
        IReadOnlyList<string> parameters,
        Action<System.Data.IDataReader> row)
    {
        using var command = on.CreateCommand();
        command.CommandText = sql;
        foreach (var value in parameters)
        {
            command.Parameters.Add(new DuckDBParameter(value));
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            row(reader);
        }
    }

    /// <summary>
    /// Record numbers in a span whose foreign key matches no record of the referenced table.
    /// </summary>
    /// <remarks>
    /// Asked per page rather than taken from the findings. Key findings stop at the per-table
    /// bound, so marking from them would silently stop at the fiftieth dangling value and leave
    /// the fifty-first looking sound. One anti-join restricted to the page's record numbers has
    /// no cap and costs only what is on screen. See the change's design.md D7.
    /// </remarks>
    /// <param name="child">The table carrying the key.</param>
    /// <param name="childColumns">Its columns forming the key.</param>
    /// <param name="parent">The table the key points at.</param>
    /// <param name="parentColumns">The referenced columns, in the same order.</param>
    /// <param name="first">Lowest record number to consider.</param>
    /// <param name="last">Highest record number to consider.</param>
    public IReadOnlyList<long> UnresolvedBetween(
        TableNode child,
        IReadOnlyList<int> childColumns,
        TableNode parent,
        IReadOnlyList<int> parentColumns,
        long first,
        long last)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(childColumns);
        ArgumentNullException.ThrowIfNull(parentColumns);
        if (childColumns.Count != parentColumns.Count)
        {
            throw new ArgumentException("A key joins as many columns on one side as on the other.", nameof(parentColumns));
        }

        if (childColumns.Count == 0 || !IsImported(child) || !IsImported(parent))
        {
            // Nothing was compared, so nothing may be marked as unresolved. A table still being
            // read has no rows to join against, and a guess would be a claim about the export.
            return [];
        }

        var (join, absent) = Comparison(childColumns, parentColumns);
        var restriction = $"c.{TableExtraction.OrdinalColumn} >= {first.ToString(CultureInfo.InvariantCulture)}"
            + $" AND c.{TableExtraction.OrdinalColumn} <= {last.ToString(CultureInfo.InvariantCulture)}";

        return Unresolved(child, parent, restriction, join, absent);
    }

    /// <summary>
    /// Which of these records' foreign keys match no record of the referenced table.
    /// </summary>
    /// <remarks>
    /// The same question as <see cref="UnresolvedBetween"/>, asked of a page whose record numbers
    /// do not run consecutively. A sorted or filtered view draws its page from anywhere in the
    /// table, so a range would cover records the page does not show and answer for values nobody
    /// is looking at. The numbers come from the page the reader just drew, never from anything a
    /// person typed.
    /// </remarks>
    /// <param name="child">The table carrying the key.</param>
    /// <param name="childColumns">Its columns forming the key.</param>
    /// <param name="parent">The table the key points at.</param>
    /// <param name="parentColumns">The referenced columns, in the same order.</param>
    /// <param name="ordinals">Record numbers to consider.</param>
    public IReadOnlyList<long> UnresolvedAmong(
        TableNode child,
        IReadOnlyList<int> childColumns,
        TableNode parent,
        IReadOnlyList<int> parentColumns,
        IReadOnlyList<long> ordinals)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(childColumns);
        ArgumentNullException.ThrowIfNull(parentColumns);
        ArgumentNullException.ThrowIfNull(ordinals);
        if (childColumns.Count != parentColumns.Count)
        {
            throw new ArgumentException("A key joins as many columns on one side as on the other.", nameof(parentColumns));
        }

        if (childColumns.Count == 0 || ordinals.Count == 0 || !IsImported(child) || !IsImported(parent))
        {
            return [];
        }

        var (join, absent) = Comparison(childColumns, parentColumns);
        var listed = string.Join(", ", ordinals.Select(ordinal => ordinal.ToString(CultureInfo.InvariantCulture)));

        return Unresolved(child, parent, $"c.{TableExtraction.OrdinalColumn} IN ({listed})", join, absent);
    }

    /// <summary>
    /// The comparison a key is matched by: the same one the key check makes.
    /// </summary>
    /// <remarks>
    /// An all-empty key refers to nothing on purpose, and a parent key that is itself incomplete
    /// is not a key to match against — so a marked cell and a reported finding stay the same
    /// claim.
    /// </remarks>
    private static (string Join, string Absent) Comparison(
        IReadOnlyList<int> childColumns,
        IReadOnlyList<int> parentColumns)
    {
        var join = string.Join(
            " AND ",
            childColumns.Select((column, position) =>
                $"{Value(column, "c.")} = {Value(parentColumns[position], "p.")}"
                + $" AND {Value(parentColumns[position], "p.")} <> ''"));

        return (join, string.Join(" AND ", childColumns.Select(column => $"{Value(column, "c.")} = ''")));
    }

    /// <summary>The records a restriction selects whose key matches nothing in the referenced table.</summary>
    private IReadOnlyList<long> Unresolved(
        TableNode child,
        TableNode parent,
        string restriction,
        string join,
        string absent)
    {
        var sql = $"SELECT c.{TableExtraction.OrdinalColumn} FROM {names[child]} c"
            + $" WHERE {restriction}"
            + $" AND NOT ({absent})"
            + $" AND NOT EXISTS (SELECT 1 FROM {names[parent]} p WHERE {join})";

        var found = new List<long>();
        Query(reading, sql, [], row => found.Add(row.GetInt64(0)));
        return found;
    }

    private static string Value(int column, string prefix) =>
        $"coalesce({prefix}{Column(column)}, '')";

    /// <summary>
    /// The position of the record with the given number, or -1 when the table has no such record.
    /// </summary>
    /// <remarks>
    /// Navigation positions the grid at a record, and positioning a virtualised list needs that
    /// record's index rather than its number. The two differ whenever the declaration excludes
    /// records from the start of the file.
    /// </remarks>
    public long PositionOfOrdinal(TableNode table, long ordinal)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (extents.TryGetValue(table, out var extent) && extent.Contiguous)
        {
            var offset = ordinal - extent.First;
            return offset >= 0 && offset < extent.Count ? offset : -1;
        }

        var name = names[table];
        var value = ordinal.ToString(CultureInfo.InvariantCulture);
        var position = -1L;
        Query(
            reading,
            $"SELECT count(*) FILTER (WHERE {TableExtraction.OrdinalColumn} < {value}),"
            + $" count(*) FILTER (WHERE {TableExtraction.OrdinalColumn} = {value}) FROM {name}",
            [],
            row => position = row.GetInt64(1) > 0 ? row.GetInt64(0) : -1);

        return position;
    }

    /// <summary>
    /// Stops whatever statement the importing connection is running.
    /// </summary>
    /// <remarks>
    /// Called from another thread, which is the whole point: the token that cancels an open
    /// stops extraction between records, but a statement already inside the store — a create
    /// over a multi-gigabyte extract, an anti-join across two large tables — would otherwise
    /// have to be waited out. <c>duckdb_interrupt</c> raises
    /// <see cref="OperationCanceledException"/> in the thread that is executing it, within
    /// milliseconds. See the change's design.md D2.
    /// </remarks>
    public void Interrupt() => writing.NativeConnection.Interrupt();

    /// <summary>The SQL name a table was imported under.</summary>
    internal string NameOf(TableNode table) => names[table];

    /// <summary>The layout a table was imported under.</summary>
    internal RecordLayout LayoutOf(TableNode table) => layouts[table];

    /// <summary>True when this table's records reached the store.</summary>
    public bool IsImported(TableNode table) => layouts.ContainsKey(table);

    /// <summary>Runs a query on the importing connection and hands each row to the reader.</summary>
    /// <remarks>
    /// The key checks run on the worker, after the last table is in the store, so they belong on
    /// the connection the import wrote with rather than the one the grid pages on.
    /// </remarks>
    internal void Query(string sql, Action<System.Data.IDataReader> row) => Query(writing, sql, [], row);

    /// <summary>The SQL name of a declared column, by position.</summary>
    internal static string Column(int index) =>
        "c" + index.ToString(CultureInfo.InvariantCulture);

    private void Create(string name, RecordLayout layout, string path)
    {
        var columns = new List<string>(layout.Columns.Count + 1)
        {
            $"{DeclaredSql.Identifier(TableExtraction.OrdinalColumn)}: 'BIGINT'",
        };
        columns.AddRange(layout.Columns.Select((_, index) => $"{DeclaredSql.Identifier(Column(index))}: 'VARCHAR'"));

        // Everything is stored as text. The declaration decides what a value means, and a value
        // that does not match its declaration still has to be shown to the person looking at it —
        // which a typed column would have thrown away on the way in.
        //
        // And nothing is detected. The reader sniffs a file's dialect even when handed the
        // columns, and on a file this tool wrote, to a shape it already knows, there is nothing to
        // infer and everything to get wrong: given an empty extract — a table with no records —
        // the sniffer decided it had one column where the table declared several, refused the
        // file, and took the reader down with it. That was reported from a real export, as a
        // crash on selecting a table with no records beside one with records.
        Execute("CREATE OR REPLACE TABLE " + name + " AS SELECT * FROM read_csv("
            + DeclaredSql.Literal(path)
            + ", columns = {" + string.Join(", ", columns) + "}"
            + ", delim = ',', quote = '\"', escape = '\"', new_line = '\\n'"
            + ", header = false, all_varchar = true, auto_detect = false)");
    }

    /// <summary>
    /// What each column of a table can be filtered, sorted and totalled as, or null when the
    /// table has none.
    /// </summary>
    /// <remarks>
    /// A table that does not conform to its declaration is never measured: its data is withheld,
    /// so there is nothing to query, and measuring it would read values the reader has already
    /// declared unreadable.
    /// </remarks>
    public TableCapabilities? Capabilities(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return capabilities.TryGetValue(table, out var measured) ? measured : null;
    }

    /// <summary>
    /// Measures a table's columns once, at import, deciding what each can be queried as.
    /// </summary>
    /// <remarks>
    /// One statement for the whole table, whatever it declares: a column measured per query would
    /// answer differently for the same column depending on which values a filter had already
    /// excluded. See the change's design.md D3.
    /// </remarks>
    private TableCapabilities Measure(string name, RecordLayout layout)
    {
        var plan = QueryView.Measure(name, layout);
        if (plan.Sql is null)
        {
            return QueryView.Capabilities(layout, plan, null);
        }

        TableCapabilities? measured = null;
        Query(writing, plan.Sql, [], row => measured = QueryView.Capabilities(layout, plan, row));

        // A table of no records answers nothing, and its columns are then what their declarations
        // alone make them.
        return measured ?? QueryView.Capabilities(layout, plan, null);
    }

    /// <summary>Creates the view that filters, sorts and figures read a table through.</summary>
    private void CreateQueryView(string name, RecordLayout layout, TableCapabilities measured) =>
        Execute(QueryView.Create(name, layout, measured));

    /// <summary>
    /// Builds the view a filter and a sort select, replacing whatever that tab held before.
    /// </summary>
    /// <remarks>
    /// Runs on the tab's own connection, which is neither the one the import writes on nor the one
    /// the grid pages on, so a person can keep reading the table they have while the next view of
    /// it is being built. A view carries the records it selects rather than pointing back at the
    /// table, which is what keeps a page of it as cheap as a page in file order. See the change's
    /// design.md D4 and D5.
    /// <para>
    /// Each build makes a table of its own rather than replacing the one the tab had. The grid may
    /// be paging that one on another connection at this very moment, and a page read out of a
    /// table being replaced underneath it is a page of whatever the replacement happens to hold.
    /// The tab drops the view it replaced once it is showing the new one.
    /// </para>
    /// </remarks>
    /// <param name="table">The table being viewed.</param>
    /// <param name="query">Which records, and in what order.</param>
    /// <param name="tab">Which tab is asking, which is the connection it is answered on.</param>
    /// <param name="cancellationToken">Stops before the statement starts; once it has,
    /// <see cref="InterruptQuery"/> stops it.</param>
    public string CreateView(TableNode table, TableQuery query, int tab, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(tab);

        var measured = Capabilities(table)
            ?? throw new InvalidOperationException("A table that is not shown as data has nothing to query.");

        var view = ViewName(table, tab);
        var statement = ViewStatements.Create(view, QueryView.NameOf(names[table]), layouts[table], measured, query);
        var lane = Lane(tab);

        cancellationToken.ThrowIfCancellationRequested();
        lock (lane.Gate)
        {
            Query(lane.Connection, statement.Sql, statement.Parameters, _ => { });
        }

        return view;
    }

    /// <summary>Reads one page of a view, in the order that view holds its records.</summary>
    /// <remarks>
    /// Read on the paging connection, like a page in file order, and addressed by position rather
    /// than joined back to the table.
    /// </remarks>
    public IReadOnlyList<StoredRecord> PageView(TableNode table, string view, long offset, int count)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var layout = layouts[table];
        var page = new List<StoredRecord>(count);
        Query(
            reading,
            ViewStatements.Page(Named(view), layout, offset, count),
            [],
            row => page.Add(Materialise(row, layout.Columns.Count)));

        return page;
    }

    /// <summary>Where a record sits in a view, or -1 when the view does not hold it.</summary>
    /// <remarks>
    /// What a navigation asks before it lands: a record the view hides has no position, and the
    /// reader then lifts the filter rather than landing anywhere else.
    /// </remarks>
    public long PositionInView(string view, long ordinal)
    {
        var position = -1L;
        Query(reading, ViewStatements.Position(Named(view), ordinal), [], row => position = row.GetInt64(0));
        return position;
    }

    /// <summary>How many records a view holds.</summary>
    public long CountView(string view)
    {
        var count = 0L;
        Query(reading, ViewStatements.Count(Named(view)), [], row => count = row.GetInt64(0));
        return count;
    }

    /// <summary>
    /// Computes the figures asked for, over a view or over a whole table in file order.
    /// </summary>
    /// <remarks>
    /// One statement for every figure on screen, so that what they say is one reading of the
    /// records rather than several. An average comes back as its sum and its count, because the
    /// rounding it needs is the caller's to state.
    /// </remarks>
    /// <param name="table">The table the figures are over.</param>
    /// <param name="view">Its view, or null for the table in file order.</param>
    /// <param name="figures">What to compute.</param>
    /// <param name="tab">Which tab is asking, which is the connection it is answered on.</param>
    /// <param name="cancellationToken">Stops before the statement starts.</param>
    public IReadOnlyList<FigureResult> Figures(
        TableNode table,
        string? view,
        IReadOnlyList<ColumnFigure> figures,
        int tab,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(figures);
        ArgumentOutOfRangeException.ThrowIfNegative(tab);

        var measured = Capabilities(table)
            ?? throw new InvalidOperationException("A table that is not shown as data has nothing to compute.");
        if (figures.Count == 0)
        {
            return [];
        }

        var source = view is null ? QueryView.NameOf(names[table]) : Named(view);
        var results = new List<FigureResult>(figures.Count);
        var lane = Lane(tab);

        cancellationToken.ThrowIfCancellationRequested();
        lock (lane.Gate)
        {
            try
            {
                Query(
                    lane.Connection,
                    ViewStatements.Figures(source, measured, figures),
                    [],
                    row => Read(figures, row, results));
                return results;
            }
            catch (DuckDBException)
            {
                // A sum can outgrow what the store adds without losing digits, and the statement
                // that overflows takes every figure on it down. Each is therefore asked again on
                // its own, so that one figure nobody can compute does not cost the others: the one
                // that cannot be computed says so, and the rest are exact. Retrying in floating
                // point instead would answer every time and be wrong.
                results.Clear();
                foreach (var figure in figures)
                {
                    try
                    {
                        Query(
                            lane.Connection,
                            ViewStatements.Figures(source, measured, [figure]),
                            [],
                            row => Read([figure], row, results));
                    }
                    catch (DuckDBException)
                    {
                        results.Add(new FigureResult(figure, null, 0, 0, Exact: false));
                    }
                }

                return results;
            }
        }
    }

    /// <summary>Reads what one statement answered for each figure it was asked.</summary>
    private static void Read(
        IReadOnlyList<ColumnFigure> figures,
        System.Data.IDataReader row,
        List<FigureResult> results)
    {
        var at = 0;
        foreach (var figure in figures)
        {
            var value = row.IsDBNull(at) ? null : row.GetValue(at);
            var missing = row.GetInt64(at + 1);
            at += 2;

            var counted = 0L;
            if (figure.Figure == Figure.Average)
            {
                counted = row.GetInt64(at);
                at++;
            }

            results.Add(new FigureResult(figure, value, missing, counted));
        }
    }

    /// <summary>Removes a view's table, with everything it was carrying.</summary>
    /// <param name="view">The view to remove.</param>
    /// <param name="tab">The tab that held it, which is the connection this runs on.</param>
    public void DropView(string view, int tab)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tab);

        var lane = Lane(tab);
        lock (lane.Gate)
        {
            Query(lane.Connection, ViewStatements.Drop(Named(view)), [], _ => { });
        }
    }

    /// <summary>
    /// Stops what one tab is filtering, sorting or totalling, so its newer request replaces its
    /// older one.
    /// </summary>
    /// <remarks>
    /// That tab's connection and no other: every other tab is computing something a person is
    /// still waiting for, and a statement inside the store cannot tell an interrupt meant for it
    /// from one meant for a neighbour. Called from the thread that supersedes the view, never from
    /// the worker building it, as <see cref="Interrupt"/> is for the import.
    /// <para>
    /// A tab that has never asked anything has no connection to interrupt, and nothing to stop.
    /// </para>
    /// </remarks>
    /// <param name="tab">The tab whose work is superseded.</param>
    public void InterruptQuery(int tab)
    {
        QueryLane? lane;
        lock (lanesGate)
        {
            lanes.TryGetValue(tab, out lane);
        }

        lane?.Connection.NativeConnection.Interrupt();
    }

    /// <summary>
    /// Closes the connection a tab was querying on, once whatever it was running has ended.
    /// </summary>
    /// <remarks>
    /// Called when a tab is closed. Waiting for the gate is waiting for a statement that has just
    /// been interrupted, which is the same wait as closing the tab already implies — and closing a
    /// connection out from under a running statement would not be a close but a crash.
    /// </remarks>
    /// <param name="tab">The tab that is done.</param>
    public void CloseQueries(int tab)
    {
        QueryLane? lane;
        lock (lanesGate)
        {
            if (!lanes.Remove(tab, out lane))
            {
                return;
            }
        }

        lock (lane.Gate)
        {
            lane.Dispose();
        }
    }

    /// <summary>The connection a tab queries on, opened the first time that tab asks.</summary>
    private QueryLane Lane(int tab)
    {
        lock (lanesGate)
        {
            if (lanes.TryGetValue(tab, out var existing))
            {
                return existing;
            }

            var connection = new DuckDBConnection(connectionString);
            connection.Open();
            var lane = new QueryLane(connection);
            lanes[tab] = lane;
            return lane;
        }
    }

    /// <summary>Where a store's database lives, as the connections reach it.</summary>
    private static string ConnectionString(string directory) =>
        $"Data Source={Path.Combine(directory, "store.duckdb")}";

    /// <summary>
    /// The name a view carries: which table it is of, which tab asked, and which build it is.
    /// </summary>
    /// <remarks>
    /// Never a name this store has used before. A tab's next view is a table of its own, so that
    /// building it cannot disturb the one the grid is paging; the tab drops the old one once it
    /// has swapped.
    /// </remarks>
    private string ViewName(TableNode table, int tab) =>
        names[table] + "_v" + tab.ToString(CultureInfo.InvariantCulture)
        + "_" + Interlocked.Increment(ref generated).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// One tab's connection for filtering, sorting and totalling, and the gate that keeps it to
    /// one statement at a time.
    /// </summary>
    /// <remarks>
    /// A tab asks one thing at a time in the ordinary course — its preparation sees to that — but
    /// a figure recomputed while a view is still building would be a second thread on this
    /// connection, which DuckDB does not allow. The gate is not taken to interrupt: that is the
    /// one thing a thread does while another holds it.
    /// </remarks>
    private sealed class QueryLane(DuckDBConnection connection) : IDisposable
    {
        /// <summary>The connection itself.</summary>
        internal DuckDBConnection Connection { get; } = connection;

        /// <summary>Held for the length of one statement.</summary>
        internal Lock Gate { get; } = new();

        /// <summary>Closes the connection.</summary>
        public void Dispose() => Connection.Dispose();
    }

    /// <summary>
    /// A view name this store made, and nothing else.
    /// </summary>
    /// <remarks>
    /// A name reaches these methods from the caller and is written into a statement, where an
    /// identifier cannot be a parameter. Only the shape this store generates is accepted, so
    /// nothing else can be smuggled in through one.
    /// </remarks>
    private static string Named(string view)
    {
        ArgumentException.ThrowIfNullOrEmpty(view);
        foreach (var character in view)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                throw new ArgumentException("That is not a view this store made.", nameof(view));
            }
        }

        return view;
    }

    private IEnumerable<ExtractionDefect> CellDefects(string name, RecordLayout layout, int bound)
    {
        var kinds = new[]
        {
            ContentDefectKind.TypeOrFormatMismatch,
            ContentDefectKind.AccuracyExceeded,
            ContentDefectKind.MaxLengthExceeded,
        };

        var defects = new List<ExtractionDefect>();
        for (var index = 0; index < layout.Columns.Count; index++)
        {
            var column = layout.Columns[index];
            var quoted = DeclaredSql.Identifier(Column(index));
            foreach (var kind in kinds)
            {
                if (DeclaredSql.Conforms(quoted, column, layout, kind) is not { } conforms)
                {
                    continue;
                }

                // An empty value is an absent value, never a defect: a GoBD export writes an
                // empty field where there is nothing to write.
                var sql = $"SELECT {TableExtraction.OrdinalColumn}, {quoted} FROM {name}"
                    + $" WHERE {quoted} <> '' AND NOT ({conforms})"
                    + $" ORDER BY {TableExtraction.OrdinalColumn}"
                    // One more than the bound, so that "there are further defects" can be told
                    // from "there are exactly this many".
                    + $" LIMIT {DeclaredSql.Limit(bound)}";

                using var command = writing.CreateCommand();
                command.CommandText = sql;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    defects.Add(new ExtractionDefect(
                        reader.GetInt64(0),
                        new ContentDefect(kind, index, reader.GetString(1), ValueInterpreter.Expected(kind, column.Type))));
                }
            }
        }

        return defects;
    }

    /// <summary>Bytes the export's listing reports for a table's file, or zero when it has none.</summary>
    /// <remarks>
    /// What the space check estimates from and what progress is measured against, so it is stated
    /// once for both.
    /// </remarks>
    public static long DeclaredBytes(IExportSource source, TableNode table)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(table);
        var resolution = ExportPath.Resolve(table.Url.Value);
        return resolution.IsResolved && source.Find(resolution.EntryName) is { } entry ? entry.Length : 0;
    }

    private static long DeclaredBytes(IExportSource source, DataSetNode dataSet) =>
        dataSet.Tables.Sum(table => DeclaredBytes(source, table));

    /// <summary>
    /// Removes an extract once the store holds its records.
    /// </summary>
    /// <remarks>
    /// Nothing reads it again, and keeping it held a second, uncompressed copy of every table on the
    /// temporary volume until the store closed. A copy that cannot be removed now is removed with
    /// the store's directory when it closes, so failing here would cost the import for nothing.
    /// </remarks>
    private static void Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void Execute(string sql)
    {
        using var command = writing.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(DuckDBConnection on, string sql)
    {
        using var command = on.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    /// <summary>Closes the store and removes everything it wrote.</summary>
    public void Dispose()
    {
        lock (lanesGate)
        {
            foreach (var lane in lanes.Values)
            {
                lane.Dispose();
            }

            lanes.Clear();
        }

        reading.Dispose();
        writing.Dispose();
        claim.Dispose();
        Delete(Directory);
    }

    private static void Delete(string directory)
    {
        try
        {
            if (System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A store that cannot be removed now is removed by the next run's startup cleanup.
            // Failing to close over it would be the worse outcome.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>The record numbers a table occupies.</summary>
/// <param name="First">Lowest record number the table holds.</param>
/// <param name="Count">Records held.</param>
/// <param name="Contiguous">True when the numbers run from <paramref name="First"/> without a gap.</param>
internal readonly record struct TableExtent(long First, long Count, bool Contiguous);

/// <summary>One record as the store holds it.</summary>
/// <param name="Ordinal">Record number, as the file counts records.</param>
/// <param name="Values">Column values in declared order, exactly as the file stores them.</param>
public sealed record StoredRecord(long Ordinal, IReadOnlyList<string> Values);
