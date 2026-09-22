using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Resolves the identity a <c>References</c> names to the table or tables it matches.
/// </summary>
/// <remarks>
/// A table is identified by its <c>Name</c>, or by its <c>URL</c> when no name is declared, as
/// the standard specifies. Resolution spans the whole <c>DataSet</c> rather than a single
/// <c>Media</c>, because references are scoped to the data set.
/// </remarks>
public sealed class TableIndex
{
    private readonly Dictionary<string, List<TableNode>> byIdentity = new(StringComparer.Ordinal);
    private readonly DataSetNode dataSet;

    /// <summary>Indexes every table in the data set.</summary>
    public TableIndex(DataSetNode dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        this.dataSet = dataSet;
        foreach (var table in dataSet.Tables)
        {
            if (!byIdentity.TryGetValue(table.Identity, out var bucket))
            {
                bucket = [];
                byIdentity[table.Identity] = bucket;
            }

            bucket.Add(table);
        }
    }

    /// <summary>Tables matching the given identity; empty when nothing matches.</summary>
    public IReadOnlyList<TableNode> Resolve(string identity) =>
        byIdentity.TryGetValue(identity, out var bucket) ? bucket : [];

    /// <summary>The single table matching the identity, or null when absent or ambiguous.</summary>
    public TableNode? ResolveUnique(string identity) =>
        Resolve(identity) is [var single] ? single : null;

    /// <summary>Every foreign key that resolves to the given table, with the table declaring it.</summary>
    /// <remarks>
    /// Compared by reference rather than by identity, so that two tables sharing a name are never
    /// confused; a reference that matches more than one table resolves to none of them.
    /// </remarks>
    public IEnumerable<(TableNode Table, ForeignKeyNode ForeignKey)> ReferencesTo(TableNode table) =>
        dataSet.ForeignKeys.Where(entry =>
            ReferenceEquals(ResolveUnique(entry.ForeignKey.References.Value), table));
}
