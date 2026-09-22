namespace GoBd.Validation.Findings;

/// <summary>The overall outcome of one validation run.</summary>
public enum Verdict
{
    /// <summary>Nothing counted against the export.</summary>
    Conformant,

    /// <summary>At least one finding counted against the export.</summary>
    NonConformant,
}
