using GoBd.Validation.Collections;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Model;

/// <summary>The record layout declared for a table.</summary>
public abstract record TableFormat
{
    private protected TableFormat(EquatableArray<ColumnNode> columns, EquatableArray<ForeignKeyNode> foreignKeys, SourceLocation location)
    {
        Columns = columns;
        ForeignKeys = foreignKeys;
        Location = location;
    }

    /// <summary>Columns and primary keys, in the order the file delivers them.</summary>
    public EquatableArray<ColumnNode> Columns { get; }

    /// <summary>Declared foreign keys.</summary>
    public EquatableArray<ForeignKeyNode> ForeignKeys { get; }

    /// <summary>Where the element begins.</summary>
    public SourceLocation Location { get; }

    /// <summary>Columns declared as primary keys, in declaration order.</summary>
    public IEnumerable<ColumnNode> PrimaryKeys => Columns.Where(column => column.IsPrimaryKey);

    /// <summary>Positions of the primary key columns among <see cref="Columns"/>, in declaration order.</summary>
    /// <remarks>
    /// What both engines assemble a key from. A composite key is one key, so the order of its parts
    /// is the order the table declares them, on every side of every comparison.
    /// </remarks>
    public IReadOnlyList<int> PrimaryKeyPositions =>
        [.. Enumerable.Range(0, Columns.Count).Where(index => Columns[index].IsPrimaryKey)];
}

/// <summary><c>VariableLength</c> — delimited records.</summary>
public sealed record VariableLengthFormat(
    TextNode? ColumnDelimiter,
    TextNode? RecordDelimiter,
    TextNode? TextEncapsulator,
    EquatableArray<ColumnNode> Columns,
    EquatableArray<ForeignKeyNode> ForeignKeys,
    SourceLocation Location)
    : TableFormat(Columns, ForeignKeys, Location);

/// <summary><c>FixedLength</c> — positional records.</summary>
public sealed record FixedLengthFormat(
    TextNode? Length,
    TextNode? RecordDelimiter,
    EquatableArray<ColumnNode> Columns,
    EquatableArray<ForeignKeyNode> ForeignKeys,
    SourceLocation Location)
    : TableFormat(Columns, ForeignKeys, Location);

/// <summary><c>Table</c> — one file and the structure of its records.</summary>
/// <param name="Url">Declared relative location of the data file.</param>
/// <param name="Name">Business name; when absent, the URL serves as the table's identity.</param>
/// <param name="Description">Optional commentary.</param>
/// <param name="Validity">Declared period the data covers.</param>
/// <param name="Codepage">Declared character set.</param>
/// <param name="DecimalSymbol">Declared decimal separator.</param>
/// <param name="DigitGroupingSymbol">Declared thousands separator.</param>
/// <param name="SkipNumBytes">Bytes to skip before reading begins.</param>
/// <param name="Range">Record range, used among other things to skip a header row.</param>
/// <param name="Epoch">Two-digit-year window.</param>
/// <param name="Format">The declared record layout.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record TableNode(
    TextNode Url,
    TextNode? Name,
    TextNode? Description,
    ValidityNode? Validity,
    Codepage Codepage,
    TextNode? DecimalSymbol,
    TextNode? DigitGroupingSymbol,
    TextNode? SkipNumBytes,
    RangeNode? Range,
    TextNode? Epoch,
    TableFormat? Format,
    SourceLocation Location)
{
    /// <summary>
    /// How a <c>References</c> names this table: its <c>Name</c> when declared, otherwise its
    /// <c>URL</c>, as the standard specifies.
    /// </summary>
    public string Identity => Name?.Value ?? Url.Value;
}
