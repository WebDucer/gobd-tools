namespace GoBd.Reader.Data;

/// <summary>What a column's declaration lets it be queried as.</summary>
public enum ColumnQueryKind
{
    /// <summary>Text, compared and ordered as the grid shows it.</summary>
    Text,

    /// <summary>A number, under the table's declared symbols.</summary>
    Number,

    /// <summary>A date, under the column's declared mask and the table's year window.</summary>
    Date,

    /// <summary>A time, under the mask the column's <c>Map</c> declares.</summary>
    Time,
}

/// <summary>Why the reader cannot filter, sort or compute with a column.</summary>
public enum ColumnLimitation
{
    /// <summary>It can.</summary>
    None,

    /// <summary>
    /// The column holds a number with more digits than the reader can hold exactly.
    /// </summary>
    /// <remarks>
    /// The whole column is turned off rather than the offending values dropped: a sum quietly
    /// short by one value presents a guess as a fact. The values are still shown — the export is
    /// not defective, and the standard sets no limit on how long a number may be.
    /// </remarks>
    NumberTooLarge,
}

/// <summary>
/// What one column can be queried as, measured from its values when the table was read.
/// </summary>
/// <param name="Index">Its position among the declared columns.</param>
/// <param name="Kind">What its declaration makes it.</param>
/// <param name="Scale">Decimal places a number is read at; zero for every other kind.</param>
/// <param name="ValuesNotTimes">
/// Values of a Time column that cannot be read under its mask, which time filters, sorting and
/// figures treat as absent.
/// </param>
/// <param name="Limitation">Why it cannot be queried, when it cannot.</param>
public sealed record ColumnCapability(
    int Index,
    ColumnQueryKind Kind,
    int Scale,
    long ValuesNotTimes,
    ColumnLimitation Limitation)
{
    /// <summary>True when this column may be filtered, sorted and computed with.</summary>
    public bool IsQueryable => Limitation == ColumnLimitation.None;

    /// <summary>True when it carries a value the store holds as something other than text.</summary>
    public bool IsTyped => IsQueryable && Kind != ColumnQueryKind.Text;
}

/// <summary>
/// What a table's columns can be queried as.
/// </summary>
/// <remarks>
/// Measured once, when the table is imported, because it decides both what the store can hold a
/// value as and what the reader may offer for that column. The alternative — deciding per query —
/// would answer differently for the same column depending on which values a filter had already
/// excluded.
/// </remarks>
/// <param name="Columns">One entry per declared column, in declared order.</param>
public sealed record TableCapabilities(IReadOnlyList<ColumnCapability> Columns)
{
    /// <summary>What the column at that position can be queried as.</summary>
    public ColumnCapability this[int index] => Columns[index];

    /// <summary>True when any column had to be turned off.</summary>
    public bool HasLimitations => Columns.Any(column => !column.IsQueryable);
}
