using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Reports tables and columns that cannot be told apart.
/// </summary>
public sealed class IdentityAmbiguityCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.TableNameDuplicated,
        FindingCodes.TableUrlDuplicated,
        FindingCodes.ColumnNameDuplicated,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tables = context.DataSet.Tables.ToArray();

        var referenced = context.ForeignKeys
            .Select(pair => pair.ForeignKey.References.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var group in tables
            .Where(table => table.Name is not null)
            .GroupBy(table => table.Name!.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            // Ambiguity only becomes an error once something actually resolves through it.
            var severity = referenced.Contains(group.Key) ? Severity.Error : Severity.Warning;
            yield return Finding.Create(
                FindingCodes.TableNameDuplicated, severity,
                group.First().Location,
                group.Key,
                string.Join(", ", group.Select(table => table.Url.Value)));
        }

        foreach (var group in tables
            .GroupBy(table => table.Url.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            yield return Finding.Create(
                FindingCodes.TableUrlDuplicated, group.First().Url.Location,
                group.Key, Invariant.Integer(group.Count()));
        }

        foreach (var table in tables)
        {
            foreach (var group in (table.Format?.Columns ?? [])
                .GroupBy(column => column.Name.Value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1))
            {
                // Foreign keys and aliases resolve columns by name, so this is never benign.
                yield return Finding.Create(
                    FindingCodes.ColumnNameDuplicated, group.First().Location,
                    table.Identity, group.Key).About(FindingScope.Table(table.Identity));
            }
        }
    }
}

/// <summary>
/// Reports names and descriptive text that breach the limits the standard documents.
/// </summary>
public sealed class NamingLimitsCheck : ICheck
{
    private const int MaximumDescriptionLength = 255;
    private static readonly char[] Reserved = ['"', '&', '<', '>'];

    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.ReservedCharacterInName, FindingCodes.DescriptionTooLong];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var table in context.DataSet.Tables)
        {
            foreach (var finding in InspectName(table.Identity, table.Name?.Value, table.Name?.Location ?? table.Location))
            {
                yield return finding;
            }

            foreach (var finding in InspectDescription(table.Identity, table.Description))
            {
                yield return finding;
            }

            foreach (var column in table.Format?.Columns ?? [])
            {
                foreach (var finding in InspectName(table.Identity, column.Name.Value, column.Name.Location))
                {
                    yield return finding;
                }

                foreach (var finding in InspectDescription(table.Identity, column.Description))
                {
                    yield return finding;
                }
            }
        }
    }

    private static IEnumerable<Finding> InspectName(string table, string? name, SourceLocation location)
    {
        // Values arrive entity-decoded, so this sees the characters an importer would see.
        if (name is null || name.IndexOfAny(Reserved) < 0)
        {
            yield break;
        }

        yield return Finding.Create(
            FindingCodes.ReservedCharacterInName, location,
            table, name, new string([.. name.Where(character => Reserved.Contains(character))]));
    }

    private static IEnumerable<Finding> InspectDescription(string table, TextNode? description)
    {
        if (description is null || description.Value.Length <= MaximumDescriptionLength)
        {
            yield break;
        }

        yield return Finding.Create(
            FindingCodes.DescriptionTooLong, description.Location,
            table, Invariant.Integer(description.Value.Length)).About(FindingScope.Table(table));
    }
}
