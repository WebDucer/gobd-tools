namespace GoBd.Reader.Data;

/// <summary>How a filter compares a column's values.</summary>
/// <remarks>
/// Which of these a column is offered follows from what it can be queried as: a range comparison
/// means nothing on text, and a pattern means nothing on a number.
/// </remarks>
public enum FilterComparison
{
    /// <summary>The value is exactly the one given.</summary>
    Equals,

    /// <summary>The value is anything but the one given.</summary>
    NotEquals,

    /// <summary>The value is below the one given.</summary>
    LessThan,

    /// <summary>The value is the one given or below it.</summary>
    AtMost,

    /// <summary>The value is above the one given.</summary>
    GreaterThan,

    /// <summary>The value is the one given or above it.</summary>
    AtLeast,

    /// <summary>The value lies between two bounds, each inclusive, either of which may be absent.</summary>
    Between,

    /// <summary>The text holds the one given somewhere.</summary>
    Contains,

    /// <summary>The text begins with the one given.</summary>
    StartsWith,

    /// <summary>The text ends with the one given.</summary>
    EndsWith,

    /// <summary>The text matches a pattern in which <c>*</c> stands for any text and <c>?</c> for one character.</summary>
    Matches,

    /// <summary>The value is one of several given.</summary>
    OneOf,

    /// <summary>The file holds no value here.</summary>
    IsEmpty,

    /// <summary>The file holds some value here.</summary>
    IsNotEmpty,
}

/// <summary>Which way a sort runs.</summary>
public enum Ordering
{
    /// <summary>Smallest, earliest or first alphabetically, before the rest.</summary>
    Ascending,

    /// <summary>Largest, latest or last alphabetically, before the rest.</summary>
    Descending,
}

/// <summary>A figure a person asked for over one column.</summary>
/// <remarks>
/// Which of these a column is offered follows from its kind, and nothing here decides whether a
/// figure means anything: the sum of amounts is a figure and the sum of account numbers is not,
/// and only the person reading the table knows which a column holds.
/// </remarks>
public enum Figure
{
    /// <summary>How many records hold a value here.</summary>
    Count,

    /// <summary>How many values here differ from one another.</summary>
    DistinctCount,

    /// <summary>The values added together.</summary>
    Sum,

    /// <summary>The smallest value.</summary>
    Minimum,

    /// <summary>The largest value.</summary>
    Maximum,

    /// <summary>The sum divided by the count, which is the one figure that may be rounded.</summary>
    Average,
}

/// <summary>
/// One column's filter: how to compare, and what to compare against.
/// </summary>
/// <remarks>
/// <paramref name="Values"/> holds what the person entered, already read under the table's
/// declaration and written in the one form the store parses: a number as its digits with a
/// leading sign and a <c>.</c> for its point, a date as <c>YYYY-MM-DD</c>, a time as
/// <c>HH:MM:SS</c>, and text as itself. They reach the store as parameters, never as statement
/// text. A <see cref="FilterComparison.Between"/> carries two values, either of which may be
/// empty for an open end; <see cref="FilterComparison.IsEmpty"/> and
/// <see cref="FilterComparison.IsNotEmpty"/> carry none.
/// </remarks>
/// <param name="Column">Position of the column among the declared ones.</param>
/// <param name="Comparison">How to compare.</param>
/// <param name="Values">What to compare against.</param>
/// <param name="IgnoreCase">
/// True when a text comparison should treat letters differing only in case as the same. Never set
/// for a number, a date or a time, which have no case.
/// </param>
public sealed record ColumnFilter(
    int Column,
    FilterComparison Comparison,
    IReadOnlyList<string> Values,
    bool IgnoreCase = false)
{
    /// <summary>
    /// True when this filter names what its comparison needs to compare against.
    /// </summary>
    /// <remarks>
    /// A comparison that needs a value and has none is not a weaker filter, it is no filter: asked
    /// as one it would either be dropped — leaving the banner claiming a filter that is not in
    /// force — or compared against nothing, which the store cannot read as a number or a date. Both
    /// are refused where the filter is entered, so a person sees why nothing was applied.
    /// <para>
    /// A <see cref="FilterComparison.Between"/> is the exception: it names bounds, and an absent
    /// bound is an open end rather than a missing value. So are the two comparisons that ask
    /// whether a value is there at all.
    /// </para>
    /// </remarks>
    public bool NamesValue => Comparison switch
    {
        FilterComparison.IsEmpty or FilterComparison.IsNotEmpty or FilterComparison.Between => true,
        _ => Values.Count > 0 && Values.All(value => value.Length > 0),
    };
}

/// <summary>One column's part in the sort, and which way it runs.</summary>
/// <param name="Column">Position of the column among the declared ones.</param>
/// <param name="Direction">Which way it runs.</param>
public sealed record ColumnSort(int Column, Ordering Direction);

/// <summary>One figure asked for over one column.</summary>
/// <param name="Column">Position of the column among the declared ones.</param>
/// <param name="Figure">What to compute.</param>
public sealed record ColumnFigure(int Column, Figure Figure);

/// <summary>
/// What one figure came to, and what it had nothing to read.
/// </summary>
/// <remarks>
/// An average arrives as its <paramref name="Value"/> — the sum — and its
/// <paramref name="Counted"/>, so that the division and the rounding it needs happen where they
/// can be stated rather than inside a statement that would quietly approximate them.
/// </remarks>
/// <param name="Asked">The figure this answers.</param>
/// <param name="Value">What it came to, or null when no record held a value.</param>
/// <param name="Missing">Records the view holds that carry no value in that column.</param>
/// <param name="Counted">Values read, for a figure that needs dividing by them.</param>
/// <param name="Exact">
/// False when the figure could not be computed exactly — a sum of values too large to add
/// without losing digits. Such a figure carries no value: an approximation offered as a total
/// would be a guess presented as a fact, and the reader says it cannot compute it instead.
/// </param>
public sealed record FigureResult(
    ColumnFigure Asked,
    object? Value,
    long Missing,
    long Counted,
    bool Exact = true);

/// <summary>
/// What a person has asked to see of one table: which records, and in what order.
/// </summary>
/// <remarks>
/// A description, not a statement. It says nothing about how the store answers it, so the same
/// description serves the view the grid pages, the figures beneath it and what the banner says is
/// in force.
/// <para>
/// Sorting takes at most <see cref="MaximumSorts"/> columns, applied in the order they were added.
/// A fourth is refused rather than silently dropping one, because a person who cannot see which
/// sort was dropped cannot tell what they are looking at.
/// </para>
/// </remarks>
/// <param name="Filters">Filters, all of which a record must satisfy.</param>
/// <param name="Sorts">Sorted columns, most significant first.</param>
public sealed record TableQuery(IReadOnlyList<ColumnFilter> Filters, IReadOnlyList<ColumnSort> Sorts)
{
    /// <summary>Columns a sort may name at once.</summary>
    public const int MaximumSorts = 3;

    /// <summary>The table as the file delivers it: everything, in record order.</summary>
    public static TableQuery FileOrder { get; } = new([], []);

    /// <summary>True when this is the table itself rather than a view of it.</summary>
    public bool IsFileOrder => Filters.Count == 0 && Sorts.Count == 0;

    /// <summary>True when another sorted column would be one too many.</summary>
    public bool IsSortFull => Sorts.Count >= MaximumSorts;
}
