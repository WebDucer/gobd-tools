using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Applies the 1.6 convention that overloads <c>Map</c> to declare an alphanumeric column as
/// carrying time values.
/// </summary>
/// <remarks>
/// A <c>Map</c> whose <c>From</c> and <c>To</c> are equal and are a valid time mask is a type
/// declaration, not a value substitution. One whose masks differ looks like the same intent but
/// silently behaves as a substitution, which is worth saying out loud.
/// </remarks>
public sealed class MapCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.TimeMapMasksDiffer, FindingCodes.MapOnNonAlphanumeric];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var table in context.DataSet.Tables)
        {
            foreach (var column in table.Format?.Columns ?? [])
            {
                foreach (var map in column.Maps)
                {
                    if (column.Type is not AlphaNumericType)
                    {
                        // The standard permits Map only on alphanumeric columns.
                        yield return Finding.Create(
                            FindingCodes.MapOnNonAlphanumeric,
                            map.Location,
                            table.Identity,
                            column.Name.Value,
                            ForeignKeyResolver.DatatypeName(column.Type)).About(FindingScope.Table(table.Identity));
                        continue;
                    }

                    var from = map.From.Value;
                    var to = map.To.Value;
                    if (Masks.IsTimeMask(from) && Masks.IsTimeMask(to)
                        && !string.Equals(from, to, StringComparison.Ordinal))
                    {
                        yield return Finding.Create(
                            FindingCodes.TimeMapMasksDiffer,
                            map.Location,
                            table.Identity,
                            column.Name.Value,
                            from,
                            to).About(FindingScope.Table(table.Identity));
                    }
                }
            }
        }
    }
}

/// <summary>
/// A medium carrying no table is legal since 1.4, but only when it says so.
/// </summary>
public sealed class MediaTablesCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.MediaWithoutTables, FindingCodes.MediaWithoutTablesAccepted];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var medium in context.DataSet.Media)
        {
            if (medium.Tables.Count > 0)
            {
                continue;
            }

            yield return medium.AcceptNoTables is null
                ? Finding.Create(FindingCodes.MediaWithoutTables, medium.Location, medium.Name.Value).About(FindingScope.Medium(medium.Name.Value))
                : Finding.Create(FindingCodes.MediaWithoutTablesAccepted, medium.Location, medium.Name.Value).About(FindingScope.Medium(medium.Name.Value));
        }
    }
}
