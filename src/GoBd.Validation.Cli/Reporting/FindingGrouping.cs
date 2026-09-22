using GoBd.Validation.Findings;

namespace GoBd.Validation.Cli.Reporting;

/// <summary>Orders and groups findings for presentation.</summary>
internal static class FindingGrouping
{
    /// <summary>
    /// The table, medium or extension a finding concerns, or null when it concerns the export as
    /// a whole.
    /// </summary>
    internal static string? ScopeOf(Finding finding) => finding.Scope?.Name;

    /// <summary>
    /// Groups findings by what they concern, most severe group first, and orders within a group
    /// by severity then code then position — so the same findings always render identically.
    /// </summary>
    internal static IEnumerable<IGrouping<string?, Finding>> Arrange(IEnumerable<Finding> findings) =>
        findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ThenBy(finding => finding.Location?.Line ?? 0)
            .ThenBy(finding => finding.Location?.Column ?? 0)
            .GroupBy(ScopeOf)
            // Ungrouped findings lead: they are about the export itself.
            .OrderBy(group => group.Key is null ? 0 : 1)
            .ThenByDescending(group => group.Max(finding => finding.Severity))
            .ThenBy(group => group.Key, StringComparer.Ordinal);
}
