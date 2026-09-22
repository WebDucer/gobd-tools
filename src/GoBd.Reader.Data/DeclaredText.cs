using System.Globalization;
using GoBd.Validation.Content;
using GoBd.Validation.Model;

namespace GoBd.Reader.Data;

/// <summary>
/// Writes a value the way the export writes its values.
/// </summary>
/// <remarks>
/// The counterpart to reading one. A total shown as <c>1234.56</c> beside a column of
/// <c>1.234,56</c> reads as a different number, and a date shown as <c>2025-01-31</c> beside
/// <c>31.01.2025</c> reads as a different date — so a figure the reader computes is written under
/// the same declaration the values around it are written under.
/// <para>
/// Here rather than in the control that shows it: the declaration is this layer's business, and a
/// mask read one way in SQL and another way in a panel is two answers to one question. What a
/// column declares is asked of the same places everything else asks —
/// <see cref="ValueInterpreter.Expected"/> for a date mask and <see cref="DeclaredSql.TimeMaskOf"/>
/// for a time one.
/// </para>
/// </remarks>
public static class DeclaredText
{
    /// <summary>The placeholders a date mask is made of, longest first.</summary>
    private static readonly (string Mask, string Written)[] DatePlaceholders =
        [("YYYY", "yyyy"), ("YY", "yy"), ("MM", "MM"), ("DD", "dd")];

    /// <summary>The placeholders a time mask is made of when it names no half of the day.</summary>
    private static readonly (string Mask, string Written)[] TimePlaceholders =
        [("HH", "HH"), ("MM", "mm"), ("SS", "ss")];

    /// <summary>
    /// The same, for a mask that names one.
    /// </summary>
    /// <remarks>
    /// With <c>TT</c> the hours run from 1 to 12, so they are written as such: a mask of
    /// <c>HH:MM TT</c> writing half past two in the afternoon as <c>14:30 PM</c> would be a time
    /// no one uses, and one the reader itself would refuse as a filter.
    /// </remarks>
    private static readonly (string Mask, string Written)[] MeridiemPlaceholders =
        [("HH", "hh"), ("MM", "mm"), ("SS", "ss"), ("TT", "tt")];

    /// <summary>Writes one value as its column and table declare it.</summary>
    /// <param name="value">The value, as the store answered it.</param>
    /// <param name="column">The column it belongs to, for its declared mask.</param>
    /// <param name="layout">Its table, for the declared decimal and grouping symbols.</param>
    public static string Write(object value, ColumnLayout? column, RecordLayout layout)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(layout);

        return value switch
        {
            DateOnly date => date.ToString(DateFormat(column), CultureInfo.InvariantCulture),
            DateTime moment => DateOnly.FromDateTime(moment).ToString(DateFormat(column), CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString(TimeFormat(column), CultureInfo.InvariantCulture),
            TimeSpan elapsed => TimeOnly.FromTimeSpan(elapsed).ToString(TimeFormat(column), CultureInfo.InvariantCulture),
            decimal number => Number(number.ToString(CultureInfo.InvariantCulture), layout),
            long whole => Number(whole.ToString(CultureInfo.InvariantCulture), layout),
            int whole => Number(whole.ToString(CultureInfo.InvariantCulture), layout),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    /// <summary>A number under the table's declared decimal and digit grouping symbols.</summary>
    private static string Number(string invariant, RecordLayout layout)
    {
        var negative = invariant.StartsWith('-');
        var digits = negative ? invariant[1..] : invariant;
        var point = digits.IndexOf('.', StringComparison.Ordinal);
        var whole = point < 0 ? digits : digits[..point];
        var fraction = point < 0 ? string.Empty : digits[(point + 1)..];

        if (layout.DigitGroupingSymbol.Length > 0)
        {
            var grouped = string.Empty;
            for (var index = 0; index < whole.Length; index++)
            {
                if (index > 0 && (whole.Length - index) % 3 == 0)
                {
                    grouped += layout.DigitGroupingSymbol;
                }

                grouped += whole[index];
            }

            whole = grouped;
        }

        var written = fraction.Length > 0 ? whole + layout.DecimalSymbol + fraction : whole;
        return negative ? "-" + written : written;
    }

    /// <summary>The column's declared date mask, as a way of writing one.</summary>
    private static string DateFormat(ColumnLayout? column)
    {
        var mask = column?.Type is DateType type
            ? ValueInterpreter.Expected(ContentDefectKind.TypeOrFormatMismatch, type)
            : null;

        return Translate(mask ?? "DD.MM.YYYY", DatePlaceholders);
    }

    /// <summary>The column's declared time mask, as a way of writing one.</summary>
    private static string TimeFormat(ColumnLayout? column)
    {
        var mask = (column is null ? null : DeclaredSql.TimeMaskOf(column)) ?? "HHMMSS";

        return Translate(
            mask,
            mask.Contains("TT", StringComparison.Ordinal) ? MeridiemPlaceholders : TimePlaceholders);
    }

    /// <summary>
    /// A declared mask as a way of writing a value.
    /// </summary>
    /// <remarks>
    /// Only the placeholders the standard defines are translated; every other character is quoted,
    /// including letters. A separator the standard permits may be a character .NET reads as a
    /// specifier of its own — an <c>h</c> is an hour and a <c>K</c> a time zone — and left
    /// unquoted it would write something the export never wrote.
    /// </remarks>
    private static string Translate(string mask, (string Mask, string Written)[] placeholders)
    {
        var written = string.Empty;
        for (var index = 0; index < mask.Length;)
        {
            var placeholder = placeholders.FirstOrDefault(candidate =>
                index + candidate.Mask.Length <= mask.Length
                && mask.AsSpan(index, candidate.Mask.Length).SequenceEqual(candidate.Mask));

            if (placeholder.Mask is null)
            {
                written += "\\" + mask[index];
                index++;
                continue;
            }

            written += placeholder.Written;
            index += placeholder.Mask.Length;
        }

        return written;
    }
}
