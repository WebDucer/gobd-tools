using System.Globalization;

namespace GoBd.Validation.Checks;

/// <summary>
/// Formatting helpers for values that appear in findings.
/// </summary>
/// <remarks>
/// Every conversion here is explicitly invariant. Ambient culture never reaches a report, so
/// the same findings render identically whoever runs the validator. See design.md D5 and D8.
/// </remarks>
internal static class Invariant
{
    /// <summary>Renders an integer for inclusion in a finding.</summary>
    internal static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
}
