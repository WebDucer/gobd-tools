using System.Globalization;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Content;

/// <summary>
/// How many findings one table may still produce before analysis of it stops.
/// </summary>
/// <remarks>
/// One budget per run, shared by every content check, because the bound belongs to the table
/// rather than to whichever pass happens to be reading it. A record-conformance pass and a key
/// pass that each kept their own count would let one table produce twice what was asked for.
/// <para>
/// The bound is counted against the table a finding is <em>about</em>, which for a dangling
/// reference is the referring table rather than the one it points at.
/// </para>
/// </remarks>
/// <param name="bound">Findings per table after which analysis of that table stops.</param>
public sealed class FindingBudget(int bound)
{
    private readonly Dictionary<string, int> reported = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TableNode> truncated = new(StringComparer.Ordinal);
    private readonly HashSet<string> announced = new(StringComparer.Ordinal);

    /// <summary>Findings per table after which analysis stops.</summary>
    public int Bound => bound;

    /// <summary>
    /// Claims room for one more finding about this table, or reports that the bound is reached.
    /// </summary>
    public bool TryReport(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        reported.TryGetValue(table.Identity, out var count);
        if (count >= bound)
        {
            truncated[table.Identity] = table;
            return false;
        }

        reported[table.Identity] = count + 1;
        return true;
    }

    /// <summary>True when analysis of this table stopped short of its defects.</summary>
    public bool WasTruncated(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return truncated.ContainsKey(table.Identity);
    }

    /// <summary>
    /// The findings that say a table's list was cut short, each table announced once.
    /// </summary>
    /// <remarks>
    /// Claimed rather than emitted by a particular pass: a table's findings arrive from more
    /// than one, and which of them exhausts the budget is not something a report should depend
    /// on.
    /// </remarks>
    public IEnumerable<Finding> ClaimNotices()
    {
        foreach (var (identity, table) in truncated)
        {
            if (announced.Add(identity))
            {
                yield return Finding.Create(
                    FindingCodes.TableAnalysisStopped,
                    table.Location,
                    identity,
                    bound.ToString(CultureInfo.InvariantCulture))
                    .About(FindingScope.Table(identity));
            }
        }
    }
}
