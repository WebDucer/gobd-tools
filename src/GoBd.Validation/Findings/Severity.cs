namespace GoBd.Validation.Findings;

/// <summary>
/// What a finding says about conformance — never about how confident the check is.
/// A check that is unsure emits nothing. See design.md D4.
/// </summary>
public enum Severity
{
    /// <summary>A neutral observation implying no defect.</summary>
    Info = 0,

    /// <summary>Permitted by the standard, but suspect.</summary>
    Warning = 1,

    /// <summary>A violation of the standard.</summary>
    Error = 2,
}
