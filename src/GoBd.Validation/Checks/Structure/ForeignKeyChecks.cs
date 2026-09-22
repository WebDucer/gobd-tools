using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// A foreign key never introduces a column: every <c>ForeignKey/Name</c> must already be
/// declared in the same table.
/// </summary>
public sealed class ForeignKeyColumnDeclarationCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.ForeignKeyColumnUndeclared];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var table in context.DataSet.Tables)
        {
            if (table.Format is not { } format)
            {
                continue;
            }

            var declared = format.Columns
                .Select(column => column.Name.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var foreignKey in format.ForeignKeys)
            {
                foreach (var name in foreignKey.Names)
                {
                    if (!declared.Contains(name.Value))
                    {
                        yield return Finding.Create(
                            FindingCodes.ForeignKeyColumnUndeclared,
                            name.Location,
                            table.Identity,
                            name.Value,
                            foreignKey.References.Value).About(FindingScope.Table(table.Identity));
                    }
                }
            }
        }
    }
}

/// <summary>
/// Every <c>References</c> must name exactly one table in the data set.
/// </summary>
public sealed class ReferenceResolutionCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.ReferenceDangling, FindingCodes.ReferenceAmbiguous];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var (table, foreignKey) in context.ForeignKeys)
        {
            var matches = context.Tables.Resolve(foreignKey.References.Value);
            if (matches.Count == 0)
            {
                yield return Finding.Create(
                    FindingCodes.ReferenceDangling,
                    foreignKey.References.Location,
                    table.Identity,
                    foreignKey.References.Value).About(FindingScope.Table(table.Identity));
            }
            else if (matches.Count > 1)
            {
                yield return Finding.Create(
                    FindingCodes.ReferenceAmbiguous,
                    foreignKey.References.Location,
                    table.Identity,
                    foreignKey.References.Value,
                    string.Join(", ", matches.Select(match => match.Url.Value)));
            }
        }
    }
}

/// <summary>
/// A foreign key joins to the referenced table's primary key, so that table must declare one.
/// </summary>
public sealed class ReferencedPrimaryKeyCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.ReferencedTableHasNoPrimaryKey];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var (table, foreignKey) in context.ForeignKeys)
        {
            // A dangling or ambiguous reference is already reported; do not pile on.
            if (context.Tables.ResolveUnique(foreignKey.References.Value) is not { } target)
            {
                continue;
            }

            if (target.Format?.PrimaryKeys.Any() != true)
            {
                yield return Finding.Create(
                    FindingCodes.ReferencedTableHasNoPrimaryKey,
                    foreignKey.References.Location,
                    table.Identity,
                    target.Identity).About(FindingScope.Table(table.Identity));
            }
        }
    }
}

/// <summary>
/// The number of foreign key columns must equal the referenced primary key's column count.
/// </summary>
public sealed class ForeignKeyArityCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.ForeignKeyArityMismatch];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var (table, foreignKey) in context.ForeignKeys)
        {
            if (context.Tables.ResolveUnique(foreignKey.References.Value) is not { } target)
            {
                continue;
            }

            var primaryKeyCount = target.Format?.PrimaryKeys.Count() ?? 0;
            if (primaryKeyCount == 0 || primaryKeyCount == foreignKey.Names.Count)
            {
                continue;
            }

            yield return Finding.Create(
                FindingCodes.ForeignKeyArityMismatch,
                foreignKey.Location,
                table.Identity,
                target.Identity,
                Invariant.Integer(foreignKey.Names.Count),
                Invariant.Integer(primaryKeyCount)).About(FindingScope.Table(table.Identity));
        }
    }
}

/// <summary>
/// Joining columns of differing datatypes produces undefined results, so each foreign key
/// column's declared type must match the key column it maps onto.
/// </summary>
public sealed class ForeignKeyDatatypeCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.ForeignKeyDatatypeMismatch];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var (table, foreignKey) in context.ForeignKeys)
        {
            if (context.Tables.ResolveUnique(foreignKey.References.Value) is not { } target
                || target.Format is null
                || table.Format is null)
            {
                continue;
            }

            foreach (var mapping in ForeignKeyResolver.Map(foreignKey, target))
            {
                var source = table.Format.Columns.FirstOrDefault(column =>
                    string.Equals(column.Name.Value, mapping.Source.Value, StringComparison.Ordinal));
                var destination = target.Format.Columns.FirstOrDefault(column =>
                    string.Equals(column.Name.Value, mapping.TargetName, StringComparison.Ordinal));

                // Undeclared columns are reported by their own checks.
                if (source is null || destination is null)
                {
                    continue;
                }

                var sourceType = ForeignKeyResolver.DatatypeName(source.Type);
                var targetType = ForeignKeyResolver.DatatypeName(destination.Type);
                if (!string.Equals(sourceType, targetType, StringComparison.Ordinal))
                {
                    yield return Finding.Create(
                        FindingCodes.ForeignKeyDatatypeMismatch,
                        mapping.Source.Location,
                        table.Identity,
                        mapping.Source.Value,
                        sourceType,
                        target.Identity,
                        mapping.TargetName,
                        targetType).About(FindingScope.Table(table.Identity));
                }
            }
        }
    }
}
