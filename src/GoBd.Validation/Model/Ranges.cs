using GoBd.Validation.Findings;

namespace GoBd.Validation.Model;

/// <summary><c>Range (From, (To | Length)?)</c> — a record range on a table.</summary>
/// <param name="From">Declared start, verbatim.</param>
/// <param name="To">Declared end, verbatim, when given.</param>
/// <param name="Length">Declared length, verbatim, when given instead of an end.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record RangeNode(TextNode From, TextNode? To, TextNode? Length, SourceLocation Location);

/// <summary><c>FixedRange (From, (To | Length))</c> — a column's span in a fixed-length record.</summary>
/// <param name="From">Declared 1-based start position, verbatim.</param>
/// <param name="To">Declared inclusive end position, verbatim, when given.</param>
/// <param name="Length">Declared length, verbatim, when given instead of an end.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record FixedRangeNode(TextNode From, TextNode? To, TextNode? Length, SourceLocation Location);

/// <summary><c>Validity (Range, Format?)</c> — the period a table's data covers.</summary>
/// <param name="Range">The declared period.</param>
/// <param name="Format">Mask for interpreting the period's bounds.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record ValidityNode(RangeNode Range, TextNode? Format, SourceLocation Location);
