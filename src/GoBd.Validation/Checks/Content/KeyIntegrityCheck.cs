using System.Globalization;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Checks.Content;

/// <summary>
/// Checks that declared primary keys are unique and that every foreign key value resolves.
/// </summary>
/// <remarks>
/// The work is ordered by referenced table rather than by referring table. A table's own
/// uniqueness check builds exactly the structure every inbound foreign key needs to probe, so it
/// is built once, used by all of them, and released before the next table's is built. Peak
/// memory is therefore the largest single table's keys rather than the sum of every table's.
/// See the change's design.md D5.
/// </remarks>
public sealed class KeyIntegrityCheck : ICheck
{
    private readonly Func<IReadOnlyList<string>, long> hash;

    /// <summary>Creates the check as it runs in a real validation.</summary>
    public KeyIntegrityCheck()
        : this(KeyHash.Of)
    {
    }

    /// <summary>
    /// Creates the check with the key hash supplied, so that a collision can be arranged.
    /// </summary>
    /// <remarks>
    /// A seam for tests only. Two 64-bit hashes collide roughly once in nine quintillion keys,
    /// so the confirmation step that prevents a collision from becoming a false accusation could
    /// otherwise never be exercised — and a guard that has only been seen to pass proves
    /// nothing.
    /// </remarks>
    internal KeyIntegrityCheck(Func<IReadOnlyList<string>, long> hash)
    {
        this.hash = hash;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.PrimaryKeyDuplicated,
        FindingCodes.PrimaryKeyIncomplete,
        FindingCodes.ForeignKeyValueUnresolved,
        FindingCodes.ReferenceNotChecked,
        FindingCodes.TableAnalysisStopped,
        FindingCodes.KeyCheckCapacityExceeded,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var budget = context.Budget;
        foreach (var parent in context.DataSet.Tables)
        {
            foreach (var finding in ForReferencedTable(context, parent, budget, hash))
            {
                yield return finding;
            }
        }

        foreach (var finding in budget.ClaimNotices())
        {
            yield return finding;
        }
    }

    private static IEnumerable<Finding> ForReferencedTable(
        CheckContext context,
        TableNode parent,
        FindingBudget budget,
        Func<IReadOnlyList<string>, long> hash)
    {
        IReadOnlyList<(TableNode Table, ForeignKeyNode ForeignKey)> inbound = [.. context.Tables.ReferencesTo(parent)];
        var keyColumns = parent.Format?.PrimaryKeyPositions ?? [];
        if (keyColumns.Count == 0)
        {
            // GOBD3004 already reports a referenced table with no primary key, and a table with
            // no key and no inbound references has nothing to check.
            yield break;
        }

        var data = TableData.For(context, parent);
        if (!data.IsReady)
        {
            foreach (var (child, foreignKey) in inbound)
            {
                yield return ContentFindings.ReferenceNotChecked(child, foreignKey, parent);
            }

            yield break;
        }

        using var index = new KeyIndex(context.Content.MaximumKeyRecords);
        var incomplete = new List<(long Number, string Key)>();

        foreach (var record in KeyTuples(context.Source, data, keyColumns))
        {
            if (record.Parts.Any(part => part.Length == 0))
            {
                // A key that is not fully present is not a key. It is not added to the index, so
                // a reference to it correctly fails to resolve.
                incomplete.Add((record.Number, ContentFindings.Key(record.Parts)));
                continue;
            }

            if (!index.Add(hash(record.Parts)))
            {
                break;
            }
        }

        if (index.CapacityExceeded)
        {
            // Reporting the table as clean on the strength of a check that did not run is the
            // one outcome worse than admitting the limit.
            yield return Finding.Create(
                FindingCodes.KeyCheckCapacityExceeded,
                parent.Location,
                parent.Identity,
                context.Content.MaximumKeyRecords.ToString(CultureInfo.InvariantCulture))
                .About(FindingScope.Table(parent.Identity));
            yield break;
        }

        index.Seal();

        foreach (var (number, key) in incomplete)
        {
            if (!budget.TryReport(parent))
            {
                break;
            }

            yield return ContentFindings.PrimaryKeyIncomplete(parent, number, key);
        }

        foreach (var finding in Duplicates(context, parent, data, keyColumns, index, budget, hash))
        {
            yield return finding;
        }

        foreach (var (child, foreignKey) in inbound)
        {
            foreach (var finding in Resolve(context, parent, child, foreignKey, index, budget, hash))
            {
                yield return finding;
            }
        }
    }

    /// <summary>
    /// Reports primary keys that occur more than once, confirming each against the actual values.
    /// </summary>
    /// <remarks>
    /// The index holds hashes, so a repeated hash is a candidate rather than a duplicate. The
    /// table is read a second time to gather the keys behind those candidates and compare them
    /// as text — which costs a pass only when candidates exist, and makes a false accusation
    /// impossible.
    /// </remarks>
    private static IEnumerable<Finding> Duplicates(
        CheckContext context,
        TableNode parent,
        TableData data,
        IReadOnlyList<int> keyColumns,
        KeyIndex index,
        FindingBudget budget,
        Func<IReadOnlyList<string>, long> hash)
    {
        var candidates = new HashSet<long>(index.Repeated());
        if (candidates.Count == 0)
        {
            yield break;
        }

        var byKey = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        foreach (var record in KeyTuples(context.Source, data, keyColumns))
        {
            if (record.Parts.Any(part => part.Length == 0) || !candidates.Contains(hash(record.Parts)))
            {
                continue;
            }

            var key = ContentFindings.Key(record.Parts);
            if (!byKey.TryGetValue(key, out var numbers))
            {
                numbers = [];
                byKey[key] = numbers;
            }

            numbers.Add(record.Number);
        }

        foreach (var (key, numbers) in byKey.Where(entry => entry.Value.Count > 1))
        {
            if (!budget.TryReport(parent))
            {
                yield break;
            }

            yield return ContentFindings.PrimaryKeyDuplicated(parent, key, ContentFindings.Records(numbers));
        }
    }

    private static IEnumerable<Finding> Resolve(
        CheckContext context,
        TableNode parent,
        TableNode child,
        ForeignKeyNode foreignKey,
        KeyIndex index,
        FindingBudget budget,
        Func<IReadOnlyList<string>, long> hash)
    {
        var childData = TableData.For(context, child);
        if (!childData.IsReady)
        {
            yield break;
        }

        var childColumns = childData.Layout.Columns.Select(column => column.Name).ToArray();
        if (ForeignKeyResolver.ChildPositions(foreignKey, parent, childColumns) is not { } mapped)
        {
            // The key does not map onto the referenced key at all. GOBD3005 to GOBD3008 already
            // say so, and probing a mapping that does not exist would report every record.
            yield break;
        }

        foreach (var record in KeyTuples(context.Source, childData, mapped))
        {
            // Every part empty is an absent reference, which the standard permits; a partly
            // present one is probed and will fail, which is the truthful answer.
            if (record.Parts.All(part => part.Length == 0) || index.Contains(hash(record.Parts)))
            {
                continue;
            }

            if (!budget.TryReport(child))
            {
                yield break;
            }

            yield return ContentFindings.ForeignKeyValueUnresolved(
                child,
                foreignKey,
                record.Number,
                ContentFindings.Key(record.Parts),
                parent);
        }
    }

    private static IEnumerable<(long Number, string[] Parts)> KeyTuples(
        IExportSource source,
        TableData data,
        IReadOnlyList<int> columns)
    {
        using var stream = source.OpenRead(data.EntryName);
        foreach (var record in RecordReader.Read(stream, data.Layout))
        {
            if (!record.IsData)
            {
                continue;
            }

            var parts = new string[columns.Count];
            for (var index = 0; index < columns.Count; index++)
            {
                var column = columns[index];
                parts[index] = column < record.Values.Count ? record.Values[column] : string.Empty;
            }

            yield return (record.Number, parts);
        }
    }
}
