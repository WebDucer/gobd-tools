using GoBd.Validation.Findings;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Checks each declared <c>Table/URL</c> against the export's listing.
/// </summary>
/// <remarks>
/// Presence is decided from the directory or archive listing alone; no data file is opened, so
/// this costs nothing on a multi-gigabyte export.
/// </remarks>
public sealed class TableFilePresenceCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.TableFileMissing,
        FindingCodes.UrlNotRelative,
        FindingCodes.UrlEscapesRoot,
        FindingCodes.EntryCaseMismatch,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var table in context.DataSet.Tables)
        {
            var url = table.Url;
            var resolution = ExportPath.Resolve(url.Value);

            switch (resolution.Status)
            {
                case UrlResolutionStatus.NotRelative:
                    yield return Finding.Create(
                        FindingCodes.UrlNotRelative, url.Location, table.Identity, url.Value).About(FindingScope.Table(table.Identity));
                    continue;

                case UrlResolutionStatus.EscapesRoot:
                    yield return Finding.Create(
                        FindingCodes.UrlEscapesRoot, url.Location, table.Identity, url.Value).About(FindingScope.Table(table.Identity));
                    continue;

                default:
                    break;
            }

            if (!resolution.IsResolved || context.Source.Find(resolution.EntryName) is not null)
            {
                continue;
            }

            // Naming the near-miss turns "file not found" into something the producer can act on.
            var caseOnly = context.Source.FindCaseInsensitive(resolution.EntryName);
            yield return caseOnly.Count > 0
                ? Finding.Create(
                    FindingCodes.EntryCaseMismatch, url.Location,
                    table.Identity, url.Value, caseOnly[0].Name).About(FindingScope.Table(table.Identity))
                : Finding.Create(
                    FindingCodes.TableFileMissing, url.Location, table.Identity, url.Value).About(FindingScope.Table(table.Identity));
        }
    }
}
