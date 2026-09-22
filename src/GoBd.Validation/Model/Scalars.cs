using GoBd.Validation.Findings;

namespace GoBd.Validation.Model;

/// <summary>
/// A text-bearing element, kept as the document declared it.
/// </summary>
/// <remarks>
/// Scalars are deliberately <b>not</b> parsed into numbers or dates while the model is built.
/// The standard's own semantics are declared inside the document, and a value that fails to
/// parse is a finding to report rather than a parse error to throw — so the model preserves the
/// declared text verbatim and the metadata checks interpret it later.
/// </remarks>
/// <param name="Value">The declared text.</param>
/// <param name="Location">Where the element begins in <c>index.xml</c>.</param>
public sealed record TextNode(string Value, SourceLocation Location);

/// <summary>The datatype declared for a column.</summary>
public abstract record ColumnType
{
    private protected ColumnType()
    {
    }
}

/// <summary>An <c>AlphaNumeric</c> column, optionally declaring a maximum length.</summary>
/// <param name="MaxLength">Declared <c>MaxLength</c>, verbatim.</param>
public sealed record AlphaNumericType(TextNode? MaxLength) : ColumnType;

/// <summary>A <c>Numeric</c> column.</summary>
/// <param name="Accuracy">Declared decimal places, verbatim.</param>
/// <param name="ImpliedAccuracy">Declared implied decimal places, verbatim.</param>
public sealed record NumericType(TextNode? Accuracy, TextNode? ImpliedAccuracy) : ColumnType;

/// <summary>A <c>Date</c> column.</summary>
/// <param name="Format">Declared mask, verbatim.</param>
public sealed record DateType(TextNode? Format) : ColumnType;

/// <summary>Character set declared for a table.</summary>
public enum Codepage
{
    /// <summary>No codepage declared; the standard's default applies.</summary>
    Unspecified,

    /// <summary>ANSI.</summary>
    Ansi,

    /// <summary>Macintosh.</summary>
    Macintosh,

    /// <summary>OEM.</summary>
    Oem,

    /// <summary>UTF-16.</summary>
    Utf16,

    /// <summary>UTF-7.</summary>
    Utf7,

    /// <summary>UTF-8.</summary>
    Utf8,
}
