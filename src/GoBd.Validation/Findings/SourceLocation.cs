using System.Globalization;

namespace GoBd.Validation.Findings;

/// <summary>
/// A position within <c>index.xml</c>. Captured while the model is built, because line and
/// column cannot be recovered from the tree afterwards. See design.md D3.
/// </summary>
public readonly record struct SourceLocation(int Line, int Column)
{
    /// <summary>Renders as <c>line:column</c> using invariant formatting.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Line}:{Column}");
}
