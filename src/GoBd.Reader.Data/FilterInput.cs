using System.Globalization;
using GoBd.Validation.Checks;
using GoBd.Validation.Content;
using GoBd.Validation.Model;

namespace GoBd.Reader.Data;

/// <summary>
/// What a person typed, read as the column's kind — or refused, saying what was expected.
/// </summary>
/// <param name="Read">True when it could be read.</param>
/// <param name="Value">
/// The value as a filter carries it: a number written invariantly, a date as
/// <c>YYYY-MM-DD</c>, a time as <c>HH:MM:SS</c>, text as typed. Empty when nothing was typed,
/// which is how a range is left open at one end.
/// </param>
/// <param name="Expected">The form that was expected, for saying so where it was typed.</param>
public sealed record FilterReading(bool Read, string Value, string Expected);

/// <summary>
/// Reads what a person types into a filter under the declaration the grid shows values by.
/// </summary>
/// <remarks>
/// A filter bound is read exactly as the file's own values are read: the table's decimal and
/// digit grouping symbols, the column's date mask and two-digit-year window, its time mask. The
/// machine's locale is never consulted — a German export must filter identically on an English
/// machine, which is the same promise the validator makes about reading it.
/// <para>
/// Input that cannot be read is refused rather than approximated, and the refusal names the form
/// expected: a filter that silently ignored what it could not read would show records nobody
/// asked for.
/// </para>
/// </remarks>
public static class FilterInput
{
    /// <summary>Reads one filter bound for one column.</summary>
    /// <param name="capability">What the column can be queried as.</param>
    /// <param name="column">The declared column.</param>
    /// <param name="layout">The table's layout, for its declared symbols and year window.</param>
    /// <param name="typed">What the person wrote.</param>
    public static FilterReading For(
        ColumnCapability capability,
        ColumnLayout column,
        RecordLayout layout,
        string typed)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(typed);

        var expected = Expected(capability, column, layout);

        // Nothing typed is not a value that failed to read: it is a bound left open, and a filter
        // that wants one says so by leaving it empty.
        if (typed.Length == 0)
        {
            return new FilterReading(true, string.Empty, expected);
        }

        return capability.Kind switch
        {
            ColumnQueryKind.Number => ValueInterpreter.TryNormalise(typed, layout, out var number)
                ? new FilterReading(true, number, expected)
                : new FilterReading(false, string.Empty, expected),

            ColumnQueryKind.Date => Masks.TryDate(typed, Mask(column), layout.Epoch, out var date)
                ? new FilterReading(true, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), expected)
                : new FilterReading(false, string.Empty, expected),

            ColumnQueryKind.Time => DeclaredSql.TimeMaskOf(column) is { } mask
                && Masks.TryTime(typed, mask, out var time)
                ? new FilterReading(true, time.ToString("HH:mm:ss", CultureInfo.InvariantCulture), expected)
                : new FilterReading(false, string.Empty, expected),

            _ => new FilterReading(true, typed, expected),
        };
    }

    /// <summary>The form a column's filter expects, in the terms its own declaration uses.</summary>
    public static string Expected(ColumnCapability capability, ColumnLayout column, RecordLayout layout)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(layout);

        return capability.Kind switch
        {
            ColumnQueryKind.Number => layout.DigitGroupingSymbol.Length > 0
                ? $"a number, decimals after '{layout.DecimalSymbol}', thousands grouped by '{layout.DigitGroupingSymbol}'"
                : $"a number, decimals after '{layout.DecimalSymbol}'",
            ColumnQueryKind.Date => Mask(column),
            ColumnQueryKind.Time => DeclaredSql.TimeMaskOf(column) ?? "a time",
            _ => "text",
        };
    }

    private static string Mask(ColumnLayout column) =>
        column.Type is DateType date ? date.Format?.Value ?? Masks.DefaultDateMask : Masks.DefaultDateMask;
}
