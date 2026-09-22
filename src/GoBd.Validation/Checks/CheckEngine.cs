using System.Collections.Immutable;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Checks;

/// <summary>Runs a set of checks and gathers their findings.</summary>
public static class CheckEngine
{
    /// <summary>
    /// Runs every check and concatenates the results.
    /// </summary>
    /// <remarks>
    /// A check that throws is a defect in this tool, not in the export. It is reported as a tool
    /// failure and the remaining checks still run, because one broken check must not cost the
    /// producer the other forty findings in the report.
    /// </remarks>
    public static ImmutableArray<Finding> Run(IEnumerable<ICheck> checks, CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(context);

        var findings = new List<Finding>();
        foreach (var check in checks)
        {
            try
            {
                findings.AddRange(check.Run(context));
            }
#pragma warning disable CA1031 // One check's defect must not suppress every other check's findings.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                findings.Add(Finding.Create(
                    FindingCodes.CheckFailed,
                    null,
                    check.GetType().Name,
                    exception.Message));
            }
        }

        return [.. findings];
    }
}
