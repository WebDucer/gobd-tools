using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>One foreign key column and the target key column it joins to.</summary>
/// <param name="Source">The column named by the foreign key.</param>
/// <param name="TargetName">Name of the primary key column it maps onto.</param>
public sealed record KeyMapping(TextNode Source, string TargetName);

/// <summary>Works out which target key column each foreign key column joins to.</summary>
public static class ForeignKeyResolver
{
    /// <summary>
    /// Maps each <c>ForeignKey/Name</c> onto a primary key column of the referenced table.
    /// </summary>
    /// <remarks>
    /// Without an alias the mapping is positional: the n-th foreign key column joins to the n-th
    /// primary key column. Each <c>Alias</c> overrides that for one column. A key that aliases
    /// some columns and not others is therefore order-dependent, which is why it is reported.
    /// </remarks>
    public static IReadOnlyList<KeyMapping> Map(ForeignKeyNode foreignKey, TableNode target)
    {
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(target);

        var primaryKeys = target.Format?.PrimaryKeys.ToArray() ?? [];
        var mappings = new List<KeyMapping>(foreignKey.Names.Count);

        for (var index = 0; index < foreignKey.Names.Count; index++)
        {
            var name = foreignKey.Names[index];
            var alias = foreignKey.Aliases.FirstOrDefault(candidate =>
                string.Equals(candidate.From.Value, name.Value, StringComparison.Ordinal));

            if (alias is not null)
            {
                mappings.Add(new KeyMapping(name, alias.To.Value));
                continue;
            }

            if (index < primaryKeys.Length)
            {
                mappings.Add(new KeyMapping(name, primaryKeys[index].Name.Value));
            }
        }

        return mappings;
    }

    /// <summary>
    /// Where each column of a foreign key sits in the referring table, in the order the referenced
    /// table declares its primary key.
    /// </summary>
    /// <remarks>
    /// A composite key is one key, so a tuple has to be assembled in the same order on both sides.
    /// Returns null when the key does not map onto the whole referenced primary key: GOBD3005 to
    /// GOBD3008 already report that, and probing a mapping that does not exist would report every
    /// record of the table.
    /// </remarks>
    /// <param name="foreignKey">The declared foreign key.</param>
    /// <param name="target">The table it references.</param>
    /// <param name="childColumnNames">The referring table's column names, in declared order.</param>
    public static IReadOnlyList<int>? ChildPositions(
        ForeignKeyNode foreignKey,
        TableNode target,
        IReadOnlyList<string> childColumnNames)
    {
        ArgumentNullException.ThrowIfNull(childColumnNames);
        if (target?.Format is not { } format)
        {
            return null;
        }

        var byTarget = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mapping in Map(foreignKey, target))
        {
            byTarget[mapping.TargetName] = mapping.Source.Value;
        }

        var positions = new List<int>();
        foreach (var keyColumn in format.PrimaryKeyPositions)
        {
            if (!byTarget.TryGetValue(format.Columns[keyColumn].Name.Value, out var childName))
            {
                return null;
            }

            var position = -1;
            for (var index = 0; index < childColumnNames.Count; index++)
            {
                if (string.Equals(childColumnNames[index], childName, StringComparison.Ordinal))
                {
                    position = index;
                    break;
                }
            }

            if (position < 0)
            {
                return null;
            }

            positions.Add(position);
        }

        return positions;
    }

    /// <summary>The declared datatype's name, for comparing a key column against its target.</summary>
    public static string DatatypeName(ColumnType type) => type switch
    {
        NumericType => "Numeric",
        DateType => "Date",
        AlphaNumericType => "AlphaNumeric",
        _ => "Unknown",
    };
}
