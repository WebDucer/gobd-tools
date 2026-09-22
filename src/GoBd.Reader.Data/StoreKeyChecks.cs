using GoBd.Validation.Checks.Content;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Reader.Data;

/// <summary>
/// Checks declared keys over the store rather than by streaming.
/// </summary>
/// <remarks>
/// The same defects as the library's key checks, found a different way: a grouping and an
/// anti-join instead of a sorted array of hashes probed record by record. That is the point of
/// having two engines — the definition is shared, the finding of it is not — and it is why the
/// two are held to the same findings on the same fixtures. The definition includes which columns
/// form a key and how a finding quotes it, so those come from the library rather than being
/// written again here.
/// <para>
/// The store has no counterpart to the streaming engine's capacity ceiling. It spills to disk,
/// so a table too large for the CLI to check keys for is checked here without complaint. That is
/// why the reader is the right tool for an export that has outgrown the command line.
/// </para>
/// </remarks>
public static class StoreKeyChecks
{
    /// <summary>Checks every declared key in the export.</summary>
    /// <param name="store">The store the tables were imported into.</param>
    /// <param name="dataSet">The parsed description.</param>
    /// <param name="bound">Findings per table after which analysis of that table stops.</param>
    public static IReadOnlyList<Finding> Run(ExportStore store, DataSetNode dataSet, int bound) =>
        Run(store, dataSet, new FindingBudget(bound));

    /// <summary>Checks every declared key in the export.</summary>
    /// <param name="store">The store the tables were imported into.</param>
    /// <param name="dataSet">The parsed description.</param>
    /// <param name="budget">The room each table has left to report defects.</param>
    public static IReadOnlyList<Finding> Run(ExportStore store, DataSetNode dataSet, FindingBudget budget)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(budget);

        var tables = new TableIndex(dataSet);
        var findings = new List<Finding>();
        foreach (var parent in dataSet.Tables)
        {
            ForReferencedTable(store, tables, parent, budget, findings);
        }

        findings.AddRange(budget.ClaimNotices());
        return findings;
    }

    /// <summary>
    /// Checks one table's primary key, and every foreign key that references it.
    /// </summary>
    /// <remarks>
    /// The unit a failure is contained to. Findings are added as they are found rather than
    /// returned at the end, so that what was found before a failure is kept, and the budget the
    /// kept findings spent is the budget that was spent. Notices of truncation are the caller's to
    /// claim, once every table has been checked.
    /// </remarks>
    /// <param name="store">The store the tables were imported into.</param>
    /// <param name="tables">The export's tables, for resolving the references to this one.</param>
    /// <param name="parent">The referenced table.</param>
    /// <param name="budget">The room each table has left to report defects.</param>
    /// <param name="findings">Where findings are added, in the order they are found.</param>
    public static void ForReferencedTable(
        ExportStore store,
        TableIndex tables,
        TableNode parent,
        FindingBudget budget,
        ICollection<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(findings);

        var keyColumns = parent.Format?.PrimaryKeyPositions ?? [];
        if (keyColumns.Count == 0)
        {
            return;
        }

        void Report(TableNode table, Finding finding)
        {
            if (budget.TryReport(table))
            {
                findings.Add(finding);
            }
        }

        IReadOnlyList<(TableNode Table, ForeignKeyNode ForeignKey)> inbound = [.. tables.ReferencesTo(parent)];
        if (!store.IsImported(parent))
        {
            // Nothing was compared, so nothing may be reported as unresolved. The referenced
            // table carries its own finding for why it could not be read.
            foreach (var (child, foreignKey) in inbound)
            {
                Report(child, ContentFindings.ReferenceNotChecked(child, foreignKey, parent));
            }

            return;
        }

        var bound = budget.Bound;
        Incomplete(store, parent, keyColumns, bound, Report);
        Duplicates(store, parent, keyColumns, bound, Report);

        foreach (var (child, foreignKey) in inbound)
        {
            Unresolved(store, parent, child, foreignKey, keyColumns, bound, Report);
        }
    }

    private static void Incomplete(
        ExportStore store,
        TableNode parent,
        IReadOnlyList<int> keyColumns,
        int bound,
        Action<TableNode, Finding> report)
    {
        // A key that is not fully present is not a key: the record cannot be referred to, and it
        // cannot be told apart from any other record whose key is missing.
        var missing = string.Join(" OR ", keyColumns.Select(column => $"{Value(column)} = ''"));
        var sql = $"SELECT {TableExtraction.OrdinalColumn}, {Key(keyColumns)} FROM {store.NameOf(parent)}"
            + $" WHERE {missing}"
            + $" ORDER BY {TableExtraction.OrdinalColumn} LIMIT {DeclaredSql.Limit(bound)}";

        store.Query(sql, row => report(
            parent,
            ContentFindings.PrimaryKeyIncomplete(parent, row.GetInt64(0), row.GetString(1))));
    }

    private static void Duplicates(
        ExportStore store,
        TableNode parent,
        IReadOnlyList<int> keyColumns,
        int bound,
        Action<TableNode, Finding> report)
    {
        var present = string.Join(" AND ", keyColumns.Select(column => $"{Value(column)} <> ''"));
        var sql = "SELECT any_value(k), string_agg(o::VARCHAR, "
            + DeclaredSql.Literal(ContentFindings.RecordSeparator)
            + " ORDER BY o), min(o) AS first FROM ("
            + $"SELECT {TableExtraction.OrdinalColumn} AS o, {Key(keyColumns)} AS k, {Tuple(keyColumns)} AS t"
            + $" FROM {store.NameOf(parent)} WHERE {present})"
            + " GROUP BY t HAVING count(*) > 1"
            + $" ORDER BY first LIMIT {DeclaredSql.Limit(bound)}";

        store.Query(sql, row => report(
            parent,
            ContentFindings.PrimaryKeyDuplicated(parent, row.GetString(0), row.GetString(1))));
    }

    private static void Unresolved(
        ExportStore store,
        TableNode parent,
        TableNode child,
        ForeignKeyNode foreignKey,
        IReadOnlyList<int> keyColumns,
        int bound,
        Action<TableNode, Finding> report)
    {
        if (!store.IsImported(child)
            || ForeignKeyResolver.ChildPositions(
                foreignKey,
                parent,
                [.. store.LayoutOf(child).Columns.Select(column => column.Name)]) is not { } mapped)
        {
            // A key that does not map onto the referenced key at all is already reported by
            // GOBD3005 to GOBD3008; probing a mapping that does not exist would report every
            // record of the table.
            return;
        }

        var join = string.Join(
            " AND ",
            keyColumns.Select((column, position) =>
                $"{Value(column, "p.")} = {Value(mapped[position], "c.")} AND {Value(column, "p.")} <> ''"));
        var absent = string.Join(" AND ", mapped.Select(column => $"{Value(column, "c.")} = ''"));

        // An anti-join rather than a set built in memory: whether that costs a hash table, an
        // index scan or a spill to disk is the store's business, and it is the reason this engine
        // has no size at which it stops answering.
        var sql = $"SELECT c.{TableExtraction.OrdinalColumn}, {Key(mapped, "c.")}"
            + $" FROM {store.NameOf(child)} c"
            + $" WHERE NOT ({absent})"
            + $" AND NOT EXISTS (SELECT 1 FROM {store.NameOf(parent)} p WHERE {join})"
            + $" ORDER BY c.{TableExtraction.OrdinalColumn} LIMIT {DeclaredSql.Limit(bound)}";

        store.Query(sql, row => report(
            child,
            ContentFindings.ForeignKeyValueUnresolved(child, foreignKey, row.GetInt64(0), row.GetString(1), parent)));
    }

    private static string Value(int column, string prefix = "") =>
        $"coalesce({prefix}{ExportStore.Column(column)}, '')";

    /// <summary>The key as a finding quotes it: the parts joined, in declared order.</summary>
    private static string Key(IReadOnlyList<int> columns, string prefix = "") =>
        columns.Count == 1
            ? Value(columns[0], prefix)
            : "concat_ws(" + DeclaredSql.Literal(ContentFindings.KeySeparator) + ", "
                + string.Join(", ", columns.Select(column => Value(column, prefix))) + ")";

    /// <summary>
    /// The key as it is compared: a tuple, so that two different composite keys cannot be made
    /// equal by where their separators happen to fall.
    /// </summary>
    private static string Tuple(IReadOnlyList<int> columns) =>
        "ROW(" + string.Join(", ", columns.Select(column => Value(column))) + ")";
}
