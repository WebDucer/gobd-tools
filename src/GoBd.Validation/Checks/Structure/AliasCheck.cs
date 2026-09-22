using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Validates the <c>Alias</c> elements that remap foreign key columns onto target key columns.
/// </summary>
/// <remarks>
/// Added in 1.2 and extended in 1.5 to allow several aliases per key, so that a composite key
/// can map columns whose names differ between the two tables.
/// </remarks>
public sealed class AliasCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.AliasFromUnknown,
        FindingCodes.AliasToNotPrimaryKey,
        FindingCodes.AliasTargetDuplicated,
        FindingCodes.AliasPartial,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var (table, foreignKey) in context.ForeignKeys)
        {
            foreach (var finding in Inspect(table, foreignKey, context.Tables))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<Finding> Inspect(TableNode table, ForeignKeyNode foreignKey, TableIndex index)
    {
        if (foreignKey.Aliases.Count == 0)
        {
            yield break;
        }

        var keyColumns = foreignKey.Names.Select(name => name.Value).ToHashSet(StringComparer.Ordinal);
        var target = index.ResolveUnique(foreignKey.References.Value);
        var targetKeys = target?.Format?.PrimaryKeys.Select(column => column.Name.Value).ToHashSet(StringComparer.Ordinal);

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        var aliasedSources = new HashSet<string>(StringComparer.Ordinal);

        foreach (var alias in foreignKey.Aliases)
        {
            if (!keyColumns.Contains(alias.From.Value))
            {
                yield return Finding.Create(
                    FindingCodes.AliasFromUnknown,
                    alias.Location,
                    table.Identity,
                    alias.From.Value,
                    foreignKey.References.Value).About(FindingScope.Table(table.Identity));
            }
            else
            {
                aliasedSources.Add(alias.From.Value);
            }

            // Only judged when the target resolved; a dangling reference is reported elsewhere.
            if (targetKeys is not null && !targetKeys.Contains(alias.To.Value))
            {
                yield return Finding.Create(
                    FindingCodes.AliasToNotPrimaryKey,
                    alias.Location,
                    table.Identity,
                    alias.To.Value,
                    foreignKey.References.Value).About(FindingScope.Table(table.Identity));
            }

            if (!seenTargets.Add(alias.To.Value))
            {
                yield return Finding.Create(
                    FindingCodes.AliasTargetDuplicated,
                    alias.Location,
                    table.Identity,
                    alias.To.Value,
                    foreignKey.References.Value).About(FindingScope.Table(table.Identity));
            }
        }

        // A composite key with only some columns aliased leaves the rest on positional matching.
        // The standard does not say that is intended, so it is reported rather than guessed at.
        if (foreignKey.Names.Count > 1 && aliasedSources.Count > 0 && aliasedSources.Count < foreignKey.Names.Count)
        {
            yield return Finding.Create(
                FindingCodes.AliasPartial,
                foreignKey.Location,
                table.Identity,
                foreignKey.References.Value,
                Invariant.Integer(aliasedSources.Count),
                Invariant.Integer(foreignKey.Names.Count)).About(FindingScope.Table(table.Identity));
        }
    }
}
