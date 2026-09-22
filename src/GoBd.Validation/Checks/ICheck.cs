using GoBd.Validation.Checks.Content;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Checks;

/// <summary>
/// Everything a check may look at.
/// </summary>
/// <remarks>
/// Deliberately a class rather than a record. The lazily-built <see cref="Tables"/> and
/// <see cref="ForeignKeys"/> are caches of <see cref="DataSet"/>; a record's generated copy
/// constructor copies every instance field including private ones, so
/// <c>context with { DataSet = other }</c> would hand back a context still holding the previous
/// document's index and resolve references against the wrong data set. A record would also
/// materialise both caches on any <c>ToString()</c> through the generated <c>PrintMembers</c>.
/// </remarks>
/// <param name="dataSet">The parsed description.</param>
/// <param name="source">The export, for questions the listing can answer.</param>
/// <param name="content">How much a content check reports per table before it stops.</param>
public sealed class CheckContext(DataSetNode dataSet, IExportSource source, ContentOptions? content = null)
{
    private TableIndex? tables;
    private IReadOnlyList<(TableNode Table, ForeignKeyNode ForeignKey)>? foreignKeys;

    /// <summary>The parsed description.</summary>
    public DataSetNode DataSet { get; } = dataSet;

    /// <summary>
    /// The export. The structural checks ask only whether an entry exists; the content checks
    /// open the data files themselves, and only when the caller asked for them.
    /// </summary>
    public IExportSource Source { get; } = source;

    /// <summary>How much a content check reports about one table before it stops.</summary>
    public ContentOptions Content { get; } = content ?? ContentOptions.Default;

    /// <summary>
    /// The remaining room each table has to report defects, shared by every content check.
    /// </summary>
    /// <remarks>
    /// Shared rather than per check, because the bound belongs to the table: two passes each
    /// keeping their own count would let one table produce twice what the caller asked for.
    /// </remarks>
    public FindingBudget Budget { get; } = new((content ?? ContentOptions.Default).MaximumFindingsPerTable);

    /// <summary>
    /// Table identities, resolved once and shared. Five checks used to build their own.
    /// </summary>
    public TableIndex Tables => tables ??= new TableIndex(DataSet);

    /// <summary>Every foreign key in the data set, paired with the table that declares it.</summary>
    public IReadOnlyList<(TableNode Table, ForeignKeyNode ForeignKey)> ForeignKeys =>
        foreignKeys ??= [.. DataSet.ForeignKeys];
}

/// <summary>
/// One self-contained check over the parsed description.
/// </summary>
/// <remarks>
/// Checks are small and independent so that each is testable against a small fixture, so that
/// strict mode and future suppression can operate uniformly by code, and so that adding a Tier 1
/// check in v2 is a matter of registering one more unit. See design.md D4.
/// </remarks>
public interface ICheck
{
    /// <summary>Codes this check can emit. Every one must exist in the catalogue.</summary>
    IReadOnlyList<string> Codes { get; }

    /// <summary>Runs the check, yielding a finding for each problem found.</summary>
    IEnumerable<Finding> Run(CheckContext context);
}
