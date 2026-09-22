using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using GoBd.Reader.Data;
using GoBd.Validation;
using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>A medium as the navigator shows it.</summary>
/// <param name="Name">The medium's declared name.</param>
/// <param name="Tables">The tables it carries, in declaration order.</param>
public sealed record NavigatorMedium(string Name, IReadOnlyList<NavigatorTable> Tables);

/// <summary>
/// A table as the navigator shows it: the table, and what it relates to.
/// </summary>
/// <remarks>
/// The relationships are information about the table, not further levels of the tree. The
/// reference graph is not a tree — a table may be referenced by several others, references cross
/// media, and a table may reference itself — so listing them as leaves one level deep is what
/// keeps the navigator a navigator. See the change's design.md D6.
/// </remarks>
/// <param name="Table">The declared table.</param>
/// <param name="Relationships">What it references, and what references it.</param>
public sealed record NavigatorTable(TableNode Table, TableRelationships Relationships)
{
    /// <summary>The table's identity, which is what the navigator labels it with.</summary>
    public string Identity => Table.Identity;
}

/// <summary>One declared relationship between two tables.</summary>
/// <param name="Other">The table at the other end.</param>
/// <param name="Columns">Columns of the referring table that form the key.</param>
/// <param name="ForeignKey">The declaration this relationship comes from.</param>
public sealed record TableRelationship(TableNode Other, IReadOnlyList<string> Columns, ForeignKeyNode ForeignKey)
{
    /// <summary>The table at the other end and the columns forming the key, as the navigator lists it.</summary>
    public string Label => Other.Identity + " (" + string.Join(", ", Columns) + ")";
}

/// <summary>A column that takes part in a declared foreign key, and where it leads.</summary>
/// <param name="Column">Its position among the declared columns.</param>
/// <param name="LeadsTo">The tables it can be followed to, in declaration order.</param>
public sealed record ColumnLink(int Column, IReadOnlyList<string> LeadsTo);

/// <summary>What a table relates to.</summary>
/// <param name="References">Tables this one points at.</param>
/// <param name="ReferencedBy">Tables that point at this one.</param>
public sealed record TableRelationships(
    IReadOnlyList<TableRelationship> References,
    IReadOnlyList<TableRelationship> ReferencedBy);

/// <summary>Whether a table is shown as data or as a report.</summary>
public enum TableViewKind
{
    /// <summary>The table has not been read yet, so there is nothing to show of it.</summary>
    NotReady,

    /// <summary>The table conforms, so its records are shown.</summary>
    Data,

    /// <summary>The table does not conform, so what is wrong with it is shown instead.</summary>
    Findings,

    /// <summary>The table could not be read at all, for a reason no check anticipated.</summary>
    Failed,
}

/// <summary>One table as the workspace presents it.</summary>
/// <param name="Table">The declared table.</param>
/// <param name="Kind">Data or report.</param>
/// <param name="Columns">Declared column names, in order.</param>
/// <param name="Rows">The records, when there are records to show.</param>
/// <param name="Findings">What is wrong with it, when something is.</param>
/// <param name="Truncated">True when analysis stopped at its bound, so the list is not complete.</param>
public sealed record TablePresentation(
    TableNode Table,
    TableViewKind Kind,
    IReadOnlyList<string> Columns,
    TablePageList? Rows,
    IReadOnlyList<Finding> Findings,
    bool Truncated);

/// <summary>Outcome of opening an export in the reader.</summary>
/// <param name="Session">The session, when the export could be opened.</param>
/// <param name="Findings">Why it could not be, when it could not.</param>
/// <param name="Refusal">Why the store would not open, when that was the reason.</param>
public sealed record ReaderOpenResult(
    ReaderSession? Session,
    IReadOnlyList<Finding> Findings,
    StoreRefusal? Refusal);

/// <summary>
/// One export, open for reading.
/// </summary>
/// <remarks>
/// The session owns everything with a lifetime: the export source, the store, and one view per
/// table. Views are created on demand and reused, because a person following references
/// repeatedly must not accumulate duplicate views of the same table.
/// </remarks>
public sealed class ReaderSession : IDisposable
{
    private readonly IExportSource source;
    private readonly ExportStore store;
    private readonly TableIndex index;

    // Written by the worker as each table finishes and read by the UI whenever a table is
    // chosen, so the collection itself has to tolerate the two. What it holds does not change
    // once published: a table's presentation is written once.
    private readonly ConcurrentDictionary<TableNode, TablePresentation> views = [];
    private readonly int bound;
    private readonly List<TableNode> order;

    // Findings in the order they were produced, which is the order a report lists them in:
    // the description first, then each table as it is read, then the keys. Appended to by the
    // worker only; every snapshot copies it into an immutable report.
    private readonly List<Finding> produced;
    private volatile ExportReading reading;

    /// <summary>
    /// How often byte progress is reported.
    /// </summary>
    /// <remarks>
    /// Every state change is reported as it happens; between them, progress is reported at most
    /// this often. Reporting per record would post millions of notifications to the dispatcher
    /// for a bar a hundred pixels wide, and the flood would cost more than the import.
    /// </remarks>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    private ReaderSession(
        IExportSource source,
        ExportStore store,
        DataSetNode dataSet,
        ValidationReport structure,
        int bound)
    {
        this.source = source;
        this.store = store;
        this.bound = bound;
        DataSet = dataSet;
        Structure = structure;
        index = new TableIndex(dataSet);
        produced = [.. structure.Findings];
        Navigator =
        [
            .. dataSet.Media.Select(medium => new NavigatorMedium(
                medium.Name.Value,
                [.. medium.Tables.Select(table => new NavigatorTable(table, RelationshipsOf(table)))])),
        ];

        // Document order, which is the order the pipeline reads in and the order the navigator
        // shows. Referenced-first would let the key checks start earlier, but they need every
        // table either way, and document order is what a person watching the progress reads down.
        order = [.. dataSet.Tables];
        reading = Snapshot(
            [.. order.Select(table => new TableReading(
                table,
                TableReadingState.Waiting,
                0,
                ExportStore.DeclaredBytes(source, table),
                0,
                [.. Structural(table)],
                false))],
            keysChecked: false,
            complete: false);
    }

    /// <summary>The parsed description.</summary>
    public DataSetNode DataSet { get; }

    /// <summary>What the structural checks found, which is true of the export whatever a table shows.</summary>
    public ValidationReport Structure { get; }

    /// <summary>
    /// The declared hierarchy: media, and the tables each carries.
    /// </summary>
    /// <remarks>
    /// Not the reference graph, which is not a tree: a table may be referenced by several
    /// others, references cross media, and a table may reference itself. Presenting that as a
    /// hierarchy would either duplicate tables or leave some unreachable. What <c>index.xml</c>
    /// declares is a tree, so that is what the navigator shows. See the change's design.md D1.
    /// </remarks>
    public IReadOnlyList<NavigatorMedium> Navigator { get; }

    /// <summary>Opens an export for reading.</summary>
    /// <param name="exportPath">A ZIP archive or an unpacked folder.</param>
    /// <param name="options">Where the store may put its files.</param>
    /// <param name="bound">Findings per table after which analysis stops.</param>
    public static ReaderOpenResult Open(
        string exportPath,
        StoreOptions? options = null,
        int bound = ContentOptions.DefaultMaximumFindingsPerTable)
    {
        ArgumentNullException.ThrowIfNull(exportPath);

        var opened = ExportSourceFactory.Open(exportPath);
        return opened.Source is { } source
            ? Open(source, exportPath, options, bound, opened.Findings)
            : new ReaderOpenResult(null, opened.Findings, null);
    }

    /// <summary>Opens an export already listed, for a caller that made the source itself.</summary>
    /// <remarks>
    /// The seam a test needs to stand a source of its own in the way — one that holds a table's
    /// file open, or that cancels part way through it. Nothing in the reader takes this route;
    /// the window opens a path.
    /// </remarks>
    internal static ReaderOpenResult Open(
        IExportSource source,
        string exportPath,
        StoreOptions? options,
        int bound,
        IReadOnlyList<Finding> listing)
    {
        DataSetNode? dataSet;
        var findings = new List<Finding>(listing);
        using (var indexXml = source.OpenRead(ExportSourceFactory.IndexFileName))
        {
            var parsed = IndexXmlParser.Parse(indexXml);
            findings.AddRange(parsed.Findings);
            dataSet = parsed.DataSet;
        }

        if (dataSet is null)
        {
            source.Dispose();
            return new ReaderOpenResult(null, findings, null);
        }

        var options_ = options ?? StoreOptions.Default;
        var result = ExportStore.Open(source, dataSet, options_);
        if (result.Store is not { } store)
        {
            source.Dispose();
            return new ReaderOpenResult(null, findings, result.Refusal);
        }

        var context = new CheckContext(dataSet, source, new ContentOptions(bound));
        findings.AddRange(CheckEngine.Run(CheckRegistry.Tier0, context));

        // Nothing here opens a data file. Opening stays as fast as it was — a listing, one small
        // document and the structural checks — and everything that reads data, the header check
        // included, belongs to the pipeline the caller runs on a worker. See design.md D1.
        var report = new ValidationReport(exportPath, findings, strict: false, contentsExamined: false);
        return new ReaderOpenResult(new ReaderSession(source, store, dataSet, report, bound), findings, null);
    }

    /// <summary>
    /// What reading the export has produced so far: the progress, and then the summary.
    /// </summary>
    /// <remarks>
    /// Replaced whole rather than mutated, so that whoever reads it sees one consistent state
    /// and not a half-applied one. Read from the UI thread while the worker writes it.
    /// </remarks>
    public ExportReading Reading => reading;

    /// <summary>
    /// Reads and checks every declared table, in document order, and then checks the keys.
    /// </summary>
    /// <remarks>
    /// Synchronous on purpose: it reads data, so it belongs on a worker, and which worker is the
    /// caller's decision rather than this type's. The window runs it on a thread pool thread and
    /// marshals <paramref name="progress"/> back through the dispatcher; a test runs it inline.
    /// <para>
    /// A table becomes <see cref="TableReadingState.Ready"/> the moment its own import and
    /// record checks are done, which is the gate of D8 — it does not wait for the key phase,
    /// because the gate does not depend on it. Key findings arrive at the end and are added to
    /// the tables they concern without changing whether any table shows its data.
    /// </para>
    /// </remarks>
    /// <param name="progress">Told each state change, and byte progress at most every 100 ms.</param>
    /// <param name="cancellationToken">Stops reading between records and between stages.</param>
    public void Read(IProgress<ExportReading>? progress = null, CancellationToken cancellationToken = default)
    {
        var budget = new FindingBudget(bound);
        var states = new Dictionary<TableNode, TableReading>(reading.Tables.Count);
        foreach (var table in reading.Tables)
        {
            states[table.Table] = table;
        }

        var clock = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;

        void Publish(bool keysChecked, bool complete)
        {
            reading = Snapshot([.. order.Select(table => states[table])], keysChecked, complete);
            lastReport = clock.Elapsed;
            progress?.Report(reading);
        }

        // The header check reads one record, so it costs nothing on a large export. It runs here
        // rather than in Open because it reads data, and nothing that reads data runs on the UI
        // thread; it runs per table rather than over the export so that its finding arrives with
        // the table it concerns and no file is opened before its turn.
        var context = new CheckContext(DataSet, source, new ContentOptions(bound));

        Publish(keysChecked: false, complete: false);

        foreach (var table in order)
        {
            cancellationToken.ThrowIfCancellationRequested();

            states[table] = states[table] with { State = TableReadingState.Reading, BytesRead = 0 };
            Publish(keysChecked: false, complete: false);

            void Read(long bytes)
            {
                states[table] = states[table] with { BytesRead = bytes };

                // Never per record, which on a table of millions would be millions of
                // notifications for a bar a hundred pixels wide. See design.md's risks.
                if (clock.Elapsed - lastReport >= ProgressInterval)
                {
                    Publish(keysChecked: false, complete: false);
                }
            }

            var layout = RecordLayout.For(table);
            var columns = layout.Columns.Select(column => column.Name).ToArray();

            try
            {
                if (HeaderRowCheck.ForTable(context, table) is { } header)
                {
                    Attribute(header, states);
                }

                var import = store.Import(table, budget, Read, cancellationToken);
                produced.AddRange(import.Findings);
                states[table] = states[table] with
                {
                    State = import.Conforms ? TableReadingState.Ready : TableReadingState.Defective,
                    BytesRead = import.Bytes,
                    Records = import.Records,
                    Findings = [.. states[table].Findings, .. import.Findings],
                    Truncated = import.Truncated,
                };

                views[table] = import.Conforms
                    ? new TablePresentation(
                        table,
                        TableViewKind.Data,
                        columns,
                        new TablePageList(store, table, layout, store.RecordCount(table), KeysOf(table)),
                        [],
                        false)
                    : new TablePresentation(
                        table,
                        TableViewKind.Findings,
                        columns,
                        null,
                        states[table].Findings,
                        import.Truncated);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One table's failure must not end the read and every other table with it.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // An interrupt that lands while the store is between statements does not surface
                // as a cancellation but as the store's own error, so the token is what says which
                // this was. Asked before anything is reported, because a cancelled read must not
                // leave a finding behind blaming the table it happened to be on.
                cancellationToken.ThrowIfCancellationRequested();

                // Contained to this table, whatever it was. The failures a check anticipated
                // already arrive as findings; what reaches here is the kind nobody anticipated,
                // and that is exactly the kind that must not take the export down: a crash
                // reported from a real export, an empty table beside a filled one, was one.
                // The message's first line is enough to act on, and the rest of a store error
                // can quote the record.
                var failure = Failure(table, nameof(ExportStore), exception);

                produced.Add(failure);
                states[table] = states[table] with
                {
                    State = TableReadingState.Failed,
                    Findings = [.. states[table].Findings, failure],
                };

                views[table] = new TablePresentation(
                    table,
                    TableViewKind.Failed,
                    columns,
                    null,
                    states[table].Findings,
                    false);
            }

            Publish(keysChecked: false, complete: false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Every table is in the store now, which is what the key checks need. They run across
        // the export rather than per table, so they can only run here — but a failure is still
        // contained to the table whose keys were being checked, as an import's is. Uncontained,
        // one store error lost every key finding and left the summary saying "reading" forever.
        var keyFindings = new List<Finding>();
        foreach (var table in DataSet.Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                StoreKeyChecks.ForReferencedTable(store, index, table, budget, keyFindings);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One table's failure must not cost every other table its key findings.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // As for an import: an interrupt can surface as the store's own error.
                cancellationToken.ThrowIfCancellationRequested();
                keyFindings.Add(Failure(table, nameof(StoreKeyChecks), exception));
            }
        }

        keyFindings.AddRange(budget.ClaimNotices());
        foreach (var finding in keyFindings)
        {
            Attribute(finding, states);
        }

        // A dangling reference is a finding and a mark, never a reason to withhold a table's
        // data, so no state changes here. What does change is a defective table's report: it is
        // rebuilt so that it and the summary say the same thing about that table.
        foreach (var table in order)
        {
            if (views.TryGetValue(table, out var view)
                && view.Kind is TableViewKind.Findings or TableViewKind.Failed)
            {
                views[table] = view with { Findings = states[table].Findings };
            }
        }

        Publish(keysChecked: true, complete: true);
    }

    /// <summary>Reports what nobody anticipated going wrong with one table, and where.</summary>
    /// <remarks>
    /// The message's first line is enough to act on, and the rest of a store error can quote the
    /// record.
    /// </remarks>
    private static Finding Failure(TableNode table, string stage, Exception exception) =>
        Finding.Create(
            FindingCodes.CheckFailed,
            table.Location,
            stage,
            exception.Message.Split('\n')[0].Trim())
            .About(FindingScope.Table(table.Identity));

    /// <summary>Adds a finding to the table it is about, and to the report in production order.</summary>
    private void Attribute(Finding finding, Dictionary<TableNode, TableReading> states)
    {
        produced.Add(finding);
        if (finding.Scope is not { Kind: FindingScopeKind.Table } scope)
        {
            return;
        }

        foreach (var table in order)
        {
            if (string.Equals(table.Identity, scope.Name, StringComparison.Ordinal))
            {
                states[table] = states[table] with
                {
                    Findings = [.. states[table].Findings, finding],
                };
            }
        }
    }

    private ExportReading Snapshot(
        IReadOnlyList<TableReading> tables,
        bool keysChecked,
        bool complete) =>
        new(
            tables,
            tables.Sum(table => table.BytesRead),
            tables.Sum(table => table.Bytes),
            new ValidationReport(Structure.ExportPath, produced, strict: false, contentsExamined: true),
            keysChecked,
            complete)
        {
            Notices = [.. Limits()],
        };

    /// <summary>
    /// What this reader cannot do with the columns of the tables it has read.
    /// </summary>
    /// <remarks>
    /// Taken from what the import measured, so a limit is stated as soon as the table it concerns
    /// has been read rather than when someone first tries to filter by it. None of these is a
    /// finding: the export is within the standard in every case, and the summary keeps them apart
    /// from what is wrong with it. See the change's design.md D3.
    /// </remarks>
    /// <summary>
    /// What each table cost the reader, worked out once for that table.
    /// </summary>
    /// <remarks>
    /// A snapshot is taken on every state change and every hundred milliseconds of progress, and
    /// what a table's columns can be queried as cannot change once it has been measured. Working
    /// it out afresh each time re-resolved every layout and rebuilt every sentence, ten times a
    /// second, for the length of the import.
    /// </remarks>
    private readonly ConcurrentDictionary<TableNode, IReadOnlyList<ReaderNotice>> limits = [];

    private IEnumerable<ReaderNotice> Limits()
    {
        foreach (var table in order)
        {
            if (limits.TryGetValue(table, out var known))
            {
                foreach (var notice in known)
                {
                    yield return notice;
                }

                continue;
            }

            // A table that has not been measured yet is not remembered as having no limits: it is
            // simply not answered for until its import has said what its columns are.
            if (store.Capabilities(table) is not { } capabilities)
            {
                continue;
            }

            foreach (var notice in limits.GetOrAdd(table, _ => Measured(table, capabilities)))
            {
                yield return notice;
            }
        }
    }

    /// <summary>What one measured table's columns cost the reader.</summary>
    private static IReadOnlyList<ReaderNotice> Measured(TableNode table, TableCapabilities capabilities)
    {
        var found = new List<ReaderNotice>();
        {
            var layout = RecordLayout.For(table);
            foreach (var column in capabilities.Columns)
            {
                if (column.Index >= layout.Columns.Count)
                {
                    continue;
                }

                var name = layout.Columns[column.Index].Name;
                if (column.Limitation == ColumnLimitation.NumberTooLarge)
                {
                    found.Add(new ReaderNotice(
                        table,
                        name,
                        $"'{name}' holds numbers with more digits than this reader computes with exactly, so it"
                        + " offers no filter, sort or figure for that column. Every value is still shown as the"
                        + " file writes it."));
                }
                else if (column.Kind == ColumnQueryKind.Time && column.ValuesNotTimes > 0)
                {
                    found.Add(new ReaderNotice(
                        table,
                        name,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"'{name}' declares a time, and {column.ValuesNotTimes} of its values cannot be read")
                        + " as one. Filtering, sorting and figures treat those as absent."));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The view of one table, as the pipeline left it.
    /// </summary>
    /// <remarks>
    /// A table that has not been read yet reports <see cref="TableViewKind.NotReady"/> rather
    /// than reading itself here: this is called on the UI thread, and importing a table of
    /// millions on it is the freeze this change exists to remove. The caller shows that the
    /// table is still being read and presents it when the pipeline reports it ready.
    /// <para>
    /// The gate is per table, not per export: one defective table withholds itself and nothing
    /// else. Showing a grid for a table the reader has declared inconsistent would present a
    /// guess as a fact, so such a table shows what is wrong with it instead.
    /// </para>
    /// </remarks>
    public TablePresentation View(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        table = Own(table);
        if (views.TryGetValue(table, out var existing))
        {
            return existing;
        }

        var layout = RecordLayout.For(table);
        return new TablePresentation(
            table,
            TableViewKind.NotReady,
            [.. layout.Columns.Select(column => column.Name)],
            null,
            [],
            false);
    }

    /// <summary>
    /// The session's own node for a table, whichever parse of the description it came from.
    /// </summary>
    /// <remarks>
    /// A caller may hold a table from another parse of the same <c>index.xml</c> — a validation
    /// run, a test fixture. Relationships are resolved by reference within one description,
    /// which is what makes two tables sharing a name distinguishable, so an incoming node is
    /// mapped to this session's own before anything is compared.
    /// </remarks>
    public TableNode Own(TableNode table) =>
        DataSet.Tables.FirstOrDefault(candidate => ReferenceEquals(candidate, table))
            ?? index.ResolveUnique(table.Identity)
            ?? table;

    /// <summary>Tables already read, in document order.</summary>
    public IReadOnlyList<TablePresentation> OpenViews =>
        [.. order.Where(views.ContainsKey).Select(table => views[table])];

    /// <summary>What a table references, and what references it.</summary>
    public TableRelationships RelationshipsOf(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        table = Own(table);
        var references = (table.Format?.ForeignKeys ?? [])
            .Select(key => (Key: key, Target: index.ResolveUnique(key.References.Value)))
            .Where(entry => entry.Target is not null)
            .Select(entry => new TableRelationship(
                entry.Target!,
                [.. entry.Key.Names.Select(name => name.Value)],
                entry.Key))
            .ToArray();

        var referencedBy = index.ReferencesTo(table)
            .Select(entry => new TableRelationship(
                entry.Table,
                [.. entry.ForeignKey.Names.Select(name => name.Value)],
                entry.ForeignKey))
            .ToArray();

        return new TableRelationships(references, referencedBy);
    }

    /// <summary>
    /// Every declared foreign key of a table, resolved into column positions on both sides.
    /// </summary>
    /// <remarks>
    /// Only keys that cover the referenced table's primary key whole. A key of the wrong arity
    /// is already reported as a defect, and joining on part of a key would call values dangling
    /// that the report says nothing about — the marks on the cells and the findings in the
    /// summary have to be the same claim.
    /// </remarks>
    public IReadOnlyList<ResolvedForeignKey> KeysOf(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return [.. Resolved(Own(table)).Where(Covers)];
    }

    private static bool Covers(ResolvedForeignKey key) =>
        key.Referenced.Format is { } format
        && key.ReferencedColumns.Count == format.PrimaryKeys.Count();

    /// <summary>A table's declared foreign keys, resolved into column positions, where they resolve.</summary>
    private IEnumerable<ResolvedForeignKey> Resolved(TableNode table) =>
        (table.Format?.ForeignKeys ?? [])
            .Select(key => index.ResolveUnique(key.References.Value) is { } target
                ? ResolvedForeignKey.Resolve(key, table, target)
                : null)
            .OfType<ResolvedForeignKey>();

    /// <summary>
    /// The columns of a table that take part in a declared foreign key, and where each leads.
    /// </summary>
    /// <remarks>
    /// The model the grid styles from. A composite key contributes all of its columns, and a
    /// column belonging to more than one key names every table it leads to, because what a
    /// person needs to know before acting on a value is where acting on it would take them.
    /// </remarks>
    public IReadOnlyList<ColumnLink> LinksOf(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var leads = new SortedDictionary<int, List<string>>();
        foreach (var key in KeysOf(table))
        {
            foreach (var column in key.ReferringColumns)
            {
                if (!leads.TryGetValue(column, out var targets))
                {
                    targets = [];
                    leads[column] = targets;
                }

                if (!targets.Contains(key.Referenced.Identity, StringComparer.Ordinal))
                {
                    targets.Add(key.Referenced.Identity);
                }
            }
        }

        return [.. leads.Select(entry => new ColumnLink(entry.Key, entry.Value))];
    }

    /// <summary>
    /// The foreign keys a column of a table takes part in.
    /// </summary>
    /// <remarks>
    /// A column may belong to more than one key. The unit of navigation is the key, not the
    /// cell: acting on any of a composite key's values follows the whole key.
    /// </remarks>
    public IReadOnlyList<ResolvedForeignKey> KeysAt(TableNode table, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(table);

        return [.. Resolved(Own(table)).Where(key => key.ReferringColumns.Contains(columnIndex))];
    }

    /// <summary>
    /// Follows a foreign key from the record in front of the reader to the record it names.
    /// </summary>
    /// <remarks>
    /// Positioning, never filtering. The referenced table is shown whole and moved to the record
    /// the key identifies; the records around it stay present, because record order is what
    /// makes a record citable.
    /// </remarks>
    /// <param name="from">The table the reader is looking at.</param>
    /// <param name="ordinal">The record in front of them.</param>
    /// <param name="key">The key to follow.</param>
    public Navigation Follow(TableNode from, long ordinal, ResolvedForeignKey key)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(key);

        if (Records(from) is not { } origin)
        {
            return Navigation.Nowhere(Why(from), from, string.Empty);
        }

        var values = store.ValuesAt(origin, ordinal, key.ReferringColumns);
        var quoted = string.Join('|', values);
        if (values.Count == 0 || values.All(value => value.Length == 0))
        {
            return Navigation.Nowhere(NavigationKind.NoReference, key.Referenced, quoted);
        }

        var target = View(key.Referenced);
        if (target.Kind != TableViewKind.Data || target.Rows is null)
        {
            // Offering a click that cannot complete is worse than saying why: the referenced
            // table either has no data to show because it does not conform to its declaration,
            // or has not been read yet, and those are not the same answer.
            return Navigation.Nowhere(Why(key.Referenced), key.Referenced, quoted);
        }

        var matches = store.OrdinalsMatching(key.Referenced, key.ReferencedColumns, values, 1);
        if (matches.Count == 0)
        {
            // An empty result presented as a resolved reference would read as "this customer has
            // no orders" when the truth is that the customer is not in the export.
            return Navigation.Nowhere(NavigationKind.Unresolved, key.Referenced, quoted);
        }

        return new Navigation(
            NavigationKind.Positioned,
            key.Referenced,
            matches[0],
            target.Rows.IndexOfOrdinal(matches[0]),
            key.ReferencedColumns,
            1,
            0,
            quoted);
    }

    /// <summary>
    /// Follows a reference backwards: which records refer to the one in front of the reader.
    /// </summary>
    /// <param name="from">The referenced table the reader is looking at.</param>
    /// <param name="ordinal">The record in front of them.</param>
    /// <param name="key">The key that points at it.</param>
    /// <param name="matchIndex">Which of the referring records to land on.</param>
    public Navigation FollowBack(TableNode from, long ordinal, ResolvedForeignKey key, long matchIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNegative(matchIndex);

        if (Records(from) is not { } origin)
        {
            return Navigation.Nowhere(Why(from), from, string.Empty);
        }

        var values = store.ValuesAt(origin, ordinal, key.ReferencedColumns);
        var quoted = string.Join('|', values);
        if (values.Count == 0 || values.All(value => value.Length == 0))
        {
            return Navigation.Nowhere(NavigationKind.NoReference, key.Referring, quoted);
        }

        var referring = View(key.Referring);
        if (referring.Kind != TableViewKind.Data || referring.Rows is null)
        {
            return Navigation.Nowhere(Why(key.Referring), key.Referring, quoted);
        }

        var total = store.CountMatching(key.Referring, key.ReferringColumns, values);
        if (total == 0)
        {
            return Navigation.Nowhere(NavigationKind.Unresolved, key.Referring, quoted);
        }

        var wanted = Math.Min(matchIndex, total - 1);
        var matches = store.OrdinalsMatching(
            key.Referring,
            key.ReferringColumns,
            values,
            checked((int)Math.Min(wanted + 1, int.MaxValue)));

        var landing = matches[^1];
        return new Navigation(
            NavigationKind.Positioned,
            key.Referring,
            landing,
            referring.Rows.IndexOfOrdinal(landing),
            key.ReferringColumns,
            total,
            wanted,
            quoted);
    }

    /// <summary>
    /// The table a navigation starts from, once its records are in the store.
    /// </summary>
    /// <remarks>
    /// A key is read back out of the store rather than taken from whatever the grid is holding,
    /// so the table it comes from has to have been imported. In use it always has been — the
    /// reader is looking at it — but a navigation that assumed so would fail where a caller
    /// arrived by another route.
    /// </remarks>
    private TableNode? Records(TableNode table)
    {
        var owned = Own(table);
        return View(owned).Kind == TableViewKind.Data ? owned : null;
    }

    /// <summary>
    /// Why a table has no data to navigate to: it has not been read yet, or it will not show
    /// its data at all.
    /// </summary>
    private NavigationKind Why(TableNode table) =>
        View(table).Kind == TableViewKind.NotReady
            ? NavigationKind.NotReady
            : NavigationKind.Unreadable;

    /// <summary>
    /// Builds what a tab has asked to see of a table: its records, and the view they come from.
    /// </summary>
    /// <remarks>
    /// Reads data, so it belongs on a worker, and which worker is the caller's decision rather
    /// than this type's — as <see cref="Read"/> is. A table in file order builds no view at all,
    /// so an export nobody filters costs nothing.
    /// </remarks>
    /// <param name="table">The table being looked at.</param>
    /// <param name="query">Which records, and in what order.</param>
    /// <param name="tab">Which view of that table this is, so a tab replaces only its own.</param>
    /// <param name="cancellationToken">Stops before the statement starts.</param>
    public TableRows Build(TableNode table, TableQuery query, int tab, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(query);

        table = Own(table);
        if (View(table) is not { Kind: TableViewKind.Data })
        {
            throw new InvalidOperationException("A table that is not shown as data has nothing to filter.");
        }

        var layout = RecordLayout.For(table);
        if (query.IsFileOrder)
        {
            var records = store.RecordCount(table);
            return new TableRows(
                new TablePageList(store, table, layout, records, KeysOf(table)),
                null,
                records);
        }

        var view = store.CreateView(table, query, tab, cancellationToken);
        var count = store.CountView(view);
        return new TableRows(
            new TablePageList(store, table, layout, count, KeysOf(table), view),
            view,
            count);
    }

    /// <summary>
    /// What each column of a table can be filtered, sorted and totalled as.
    /// </summary>
    /// <remarks>
    /// Asked of the session rather than of the store behind it, so that a control offering a
    /// person a filter and the summary stating what cannot be filtered are reading the same
    /// answer from the same place.
    /// </remarks>
    public TableCapabilities? Capabilities(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return store.Capabilities(Own(table));
    }

    /// <summary>Removes a view the reader is no longer showing.</summary>
    /// <param name="view">The view to remove, or null when the tab was showing the table itself.</param>
    /// <param name="tab">The tab that held it, which is the connection this runs on.</param>
    public void Discard(string? view, int tab)
    {
        if (view is not null)
        {
            store.DropView(view, tab);
        }
    }

    /// <summary>
    /// Computes the figures a person chose, over a view or over a table in file order.
    /// </summary>
    /// <remarks>
    /// Every figure the store computes is exact or says it is not. The average is the exception
    /// and is finished here: divided in decimal and rounded to the column's own decimal places,
    /// and marked as rounded so that what is shown is not mistaken for the figure itself.
    /// </remarks>
    public IReadOnlyList<FigureReading> Figures(
        TableNode table,
        string? view,
        IReadOnlyList<ColumnFigure> figures,
        int tab,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(figures);

        table = Own(table);
        var capabilities = store.Capabilities(table);
        var computed = store.Figures(table, view, figures, tab, cancellationToken);

        var readings = new List<FigureReading>(computed.Count);
        foreach (var result in computed)
        {
            if (result.Asked.Figure != Figure.Average)
            {
                readings.Add(new FigureReading(result.Asked, result.Value, result.Missing, result.Exact, Rounded: false));
                continue;
            }

            if (!result.Exact || result.Value is null || result.Counted == 0)
            {
                readings.Add(new FigureReading(result.Asked, null, result.Missing, result.Exact, Rounded: false));
                continue;
            }

            // Rounded away from zero, so that the half of a cent a person can see does not
            // disappear downwards, and to the places the column itself declares.
            var scale = capabilities?[result.Asked.Column].Scale ?? 2;
            var sum = Convert.ToDecimal(result.Value, CultureInfo.InvariantCulture);
            var average = decimal.Round(sum / result.Counted, scale, MidpointRounding.AwayFromZero);

            readings.Add(new FigureReading(
                result.Asked,
                average,
                result.Missing,
                Exact: true,
                Rounded: average * result.Counted != sum));
        }

        return readings;
    }

    /// <summary>The store, for navigation queries.</summary>
    internal ExportStore Store => store;

    /// <summary>Resolves the table a reference names, or null when it names none uniquely.</summary>
    internal TableNode? Resolve(string identity) => index.ResolveUnique(identity);

    /// <summary>What the structural checks said about one table.</summary>
    private IEnumerable<Finding> Structural(TableNode table) =>
        Structure.Findings.Where(finding =>
            finding.Scope is { Kind: FindingScopeKind.Table } scope
            && string.Equals(scope.Name, table.Identity, StringComparison.Ordinal));

    /// <summary>
    /// Stops whatever statement the store is running for this session.
    /// </summary>
    /// <remarks>
    /// The token that cancels a read stops extraction between records, but a statement already
    /// inside the store — a create over a multi-gigabyte extract, an anti-join across two large
    /// tables — would otherwise have to be waited out. Called from the thread that cancels,
    /// never from the worker. See the change's design.md D2.
    /// </remarks>
    public void Interrupt() => store.Interrupt();

    /// <summary>
    /// Stops whatever is being filtered, sorted or totalled, without touching the import.
    /// </summary>
    /// <remarks>
    /// A newer filter supersedes an older one, and the older one may be a statement already
    /// inside the store that a token cannot reach. This stops that statement alone: interrupting
    /// the import instead would abandon reading the export to answer a filter nobody is waiting
    /// for any more, and interrupting another tab would abandon work that tab is still waiting on.
    /// </remarks>
    /// <param name="tab">The tab whose work is superseded.</param>
    public void InterruptQuery(int tab) => store.InterruptQuery(tab);

    /// <summary>Closes the connection a tab was querying on, once its work has ended.</summary>
    /// <param name="tab">The tab that is done.</param>
    public void CloseQueries(int tab) => store.CloseQueries(tab);

    /// <summary>Closes the export and removes the store.</summary>
    public void Dispose()
    {
        store.Dispose();
        source.Dispose();
    }
}
