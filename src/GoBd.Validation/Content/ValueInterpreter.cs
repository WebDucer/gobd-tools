using System.Globalization;
using GoBd.Validation.Checks;
using GoBd.Validation.Model;

namespace GoBd.Validation.Content;

/// <summary>
/// One value, read under its column's declaration.
/// </summary>
/// <param name="Stored">The value exactly as the file holds it.</param>
/// <param name="Presented">What the value means once a declared redefinition is applied.</param>
/// <param name="Number">The number it denotes, when the column declares <c>Numeric</c>.</param>
/// <param name="Date">The date it denotes, when the column declares <c>Date</c>.</param>
/// <param name="Defect">How the value fails its declaration, when it does.</param>
public sealed record InterpretedValue(
    string Stored,
    string Presented,
    decimal? Number,
    DateOnly? Date,
    ContentDefect? Defect);

/// <summary>
/// Interprets a value as the column that holds it declares it.
/// </summary>
/// <remarks>
/// Every rule applied here is declared in <c>index.xml</c>: the datatype, the date mask, the
/// table's decimal and grouping symbols, the two-digit-year window, the declared accuracy and
/// maximum length, and any <c>Map</c> redefining a value. The operating system's culture is
/// never consulted — a German export must read identically on an English machine.
/// <para>
/// An empty value is not a defect. GoBD exports use an empty field for an absent value, and
/// reporting every empty cell of an optional column would bury the defects that matter.
/// </para>
/// </remarks>
public static class ValueInterpreter
{
    /// <summary>
    /// The padding a numeric value may carry.
    /// </summary>
    /// <remarks>
    /// Stated explicitly rather than left to <see cref="string.Trim()"/>, which strips every
    /// character Unicode calls whitespace. The store applies the same rule expressed in its own
    /// language, and two libraries' notions of whitespace agreeing is not something to rely on:
    /// the engines have to agree because the rule is written down, not because two defaults
    /// happened to match.
    /// </remarks>
    internal static readonly char[] Padding = [' ', '\t', '\r', '\n', '\f', '\v'];

    /// <summary>Interprets one value under one column's declaration.</summary>
    public static InterpretedValue Interpret(string stored, ColumnLayout column, RecordLayout layout)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(layout);

        var presented = Redefine(stored, column);

        return column.Type switch
        {
            NumericType numeric => Numeric(stored, presented, numeric, column, layout),
            DateType date => Date(stored, presented, date, column, layout),
            AlphaNumericType alphanumeric => AlphaNumeric(stored, presented, alphanumeric, column),
            _ => new InterpretedValue(stored, presented, null, null, null),
        };
    }

    /// <summary>
    /// Applies a declared <c>Map</c>, which redefines one stored value as another.
    /// </summary>
    /// <remarks>
    /// The stored form stays on the result. A map says how a value is to be understood, not that
    /// the file holds something else, and a finding that quoted the mapped value would send the
    /// producer looking for text their file does not contain.
    /// </remarks>
    private static string Redefine(string stored, ColumnLayout column)
    {
        foreach (var map in column.Declaration.Maps)
        {
            if (string.Equals(map.From.Value, stored, StringComparison.Ordinal))
            {
                return map.To.Value;
            }
        }

        return stored;
    }

    /// <summary>
    /// What a stored value means once any declared <c>Map</c> is applied, and nothing more.
    /// </summary>
    /// <remarks>
    /// What the grid shows. It needs the redefinition alone, so it does not pay for the type
    /// checks <see cref="Interpret"/> makes on every value.
    /// </remarks>
    public static string Present(string stored, ColumnLayout column)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(column);
        return Redefine(stored, column);
    }

    /// <summary>What the declaration required, as a finding of the given kind quotes it.</summary>
    /// <remarks>
    /// Stated once for both engines. The store finds these defects by other means, but it must
    /// quote the same requirement for the two reports to be the same report.
    /// </remarks>
    public static string? Expected(ContentDefectKind kind, ColumnType type) => (kind, type) switch
    {
        (ContentDefectKind.TypeOrFormatMismatch, NumericType) => "Numeric",
        (ContentDefectKind.TypeOrFormatMismatch, DateType date) => date.Format?.Value ?? Masks.DefaultDateMask,
        (ContentDefectKind.AccuracyExceeded, NumericType numeric) => numeric.Accuracy?.Value,
        (ContentDefectKind.MaxLengthExceeded, AlphaNumericType alphanumeric) => alphanumeric.MaxLength?.Value,
        _ => null,
    };

    private static InterpretedValue AlphaNumeric(
        string stored,
        string presented,
        AlphaNumericType type,
        ColumnLayout column)
    {
        if (type.MaxLength is { } declared
            && Masks.TryNonNegativeInteger(declared.Value, out var maximum)
            && stored.Length > maximum)
        {
            return new InterpretedValue(
                stored,
                presented,
                null,
                null,
                new ContentDefect(
                    ContentDefectKind.MaxLengthExceeded,
                    column.Index,
                    stored,
                    declared.Value));
        }

        return new InterpretedValue(stored, presented, null, null, null);
    }

    private static InterpretedValue Numeric(
        string stored,
        string presented,
        NumericType type,
        ColumnLayout column,
        RecordLayout layout)
    {
        if (stored.Length == 0)
        {
            return new InterpretedValue(stored, presented, null, null, null);
        }

        if (!TryNumber(stored, layout, out var number, out var decimals))
        {
            return new InterpretedValue(
                stored,
                presented,
                null,
                null,
                new ContentDefect(
                    ContentDefectKind.TypeOrFormatMismatch,
                    column.Index,
                    stored,
                    Expected(ContentDefectKind.TypeOrFormatMismatch, type)));
        }

        // ImpliedAccuracy declares decimals the file does not write: the stored digits are whole,
        // and the declaration says where the point belongs. Such a value therefore cannot carry
        // too many decimal places, only the wrong number of digits, which is not a defect class.
        if (type.ImpliedAccuracy is { } implied && Masks.TryNonNegativeInteger(implied.Value, out var impliedPlaces))
        {
            var scale = (decimal)Math.Pow(10, impliedPlaces);
            return new InterpretedValue(stored, presented, number / scale, null, null);
        }

        if (type.Accuracy is { } accuracy
            && Masks.TryNonNegativeInteger(accuracy.Value, out var places)
            && decimals > places)
        {
            return new InterpretedValue(
                stored,
                presented,
                number,
                null,
                new ContentDefect(
                    ContentDefectKind.AccuracyExceeded,
                    column.Index,
                    stored,
                    accuracy.Value));
        }

        return new InterpretedValue(stored, presented, number, null, null);
    }

    private static InterpretedValue Date(
        string stored,
        string presented,
        DateType type,
        ColumnLayout column,
        RecordLayout layout)
    {
        if (stored.Length == 0)
        {
            return new InterpretedValue(stored, presented, null, null, null);
        }

        var mask = type.Format?.Value ?? Masks.DefaultDateMask;
        if (!Masks.TryDate(stored, mask, layout.Epoch, out var date))
        {
            return new InterpretedValue(
                stored,
                presented,
                null,
                null,
                new ContentDefect(ContentDefectKind.TypeOrFormatMismatch, column.Index, stored, mask));
        }

        return new InterpretedValue(stored, presented, null, date, null);
    }

    /// <summary>
    /// True when a sign may follow a number's digits under this table's declaration.
    /// </summary>
    /// <remarks>
    /// The standard lets a negative figure carry its sign behind it, as in <c>1782,90-</c>, and a
    /// trailing <c>+</c> is read the same way. A table that declares <c>+</c> or <c>-</c> as its
    /// decimal or grouping symbol cannot tell the sign in <c>15-</c> from the symbol, so for such
    /// a table no sign is read behind the digits and every value is read as it always was.
    /// Stated once for both engines, like <see cref="Padding"/>: it is the definition, not the
    /// finding of defects.
    /// </remarks>
    internal static bool TrailingSignReadable(RecordLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.DecimalSymbol.AsSpan().IndexOfAny('+', '-') < 0
            && layout.DigitGroupingSymbol.AsSpan().IndexOfAny('+', '-') < 0;
    }

    /// <summary>
    /// Reads a value as a number under the table's declared symbols.
    /// </summary>
    /// <remarks>
    /// Digit grouping is checked rather than merely stripped. A value such as <c>1.2.3</c> under
    /// a declared grouping symbol of <c>.</c> is not a number: stripping the separators would
    /// turn it into 123, which is precisely the "read as something else" outcome a content check
    /// exists to catch. Groups after the first must therefore be exactly three digits, and the
    /// first at most three.
    /// <para>
    /// Whether a value is a number is decided by its characters, and that grammar is the store's
    /// too: one optional sign, directly before the digits or — where
    /// <see cref="TrailingSignReadable"/> allows — directly after them; at least one digit; and,
    /// after a decimal symbol, at least one more. It is not decided by whether a
    /// <see cref="decimal"/> can hold the value. One of thirty digits is a number all the same,
    /// and deciding otherwise made the two engines disagree about it.
    /// </para>
    /// <para>
    /// A sign behind the digits is moved in front of them before anything else is read, so that
    /// decimal places are counted without it and every later step sees the one form it already
    /// knows. Only a value that does not already begin with a sign is rewritten: <c>-5-</c> and
    /// <c>5--</c> keep a sign where no sign may stand, and fail as they always did.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Reads a value as a number under a table's declared symbols, in the one written form that
    /// does not depend on them: an optional leading sign, digits, and a <c>.</c> before any
    /// decimals.
    /// </summary>
    /// <remarks>
    /// What the reader turns a person's typing into before it becomes a filter. Stated here, next
    /// to the grammar it has to agree with, rather than written a second time in the reader: a
    /// filter that read <c>1.234</c> differently from the way the same characters are read in the
    /// file would select the wrong records and look right doing it.
    /// <para>
    /// A string rather than a <see cref="decimal"/>, because a column may hold a number of more
    /// digits than one can carry, and a bound that quietly lost digits would compare against
    /// something the person did not type.
    /// </para>
    /// </remarks>
    /// <param name="value">The value as a person wrote it.</param>
    /// <param name="layout">The table whose declared symbols it is read under.</param>
    /// <param name="normalised">The same number, written invariantly.</param>
    public static bool TryNormalise(string value, RecordLayout layout, out string normalised)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(layout);

        var read = TryNumber(value, layout, out _, out _, out normalised);
        if (!read)
        {
            normalised = string.Empty;
        }

        return read;
    }

    private static bool TryNumber(string stored, RecordLayout layout, out decimal? number, out int decimals) =>
        TryNumber(stored, layout, out number, out decimals, out _);

    private static bool TryNumber(
        string stored,
        RecordLayout layout,
        out decimal? number,
        out int decimals,
        out string normalised)
    {
        number = null;
        decimals = 0;
        normalised = string.Empty;

        var working = stored.Trim(Padding);
        if (working.Length > 1
            && working[^1] is '+' or '-'
            && working[0] is not ('+' or '-')
            && TrailingSignReadable(layout))
        {
            working = working[^1] + working[..^1];
        }
        var integerPart = working;
        var fractionPart = string.Empty;

        var separator = layout.DecimalSymbol.Length > 0
            ? working.IndexOf(layout.DecimalSymbol, StringComparison.Ordinal)
            : -1;
        if (separator >= 0)
        {
            if (working.IndexOf(layout.DecimalSymbol, separator + layout.DecimalSymbol.Length, StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            integerPart = working[..separator];
            fractionPart = working[(separator + layout.DecimalSymbol.Length)..];
            decimals = fractionPart.Length;

            // A decimal symbol with nothing after it is not a number. .NET would read "1," as 1,
            // which would leave the two engines disagreeing about a value that is plainly wrong.
            if (decimals == 0)
            {
                return false;
            }
        }

        if (layout.DigitGroupingSymbol.Length > 0)
        {
            if (fractionPart.Contains(layout.DigitGroupingSymbol, StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryUngroup(ref integerPart, layout.DigitGroupingSymbol))
            {
                return false;
            }
        }

        // A digit on each side of a decimal symbol: ",5" is no more a number than "1," is.
        var digits = integerPart.Length > 0 && integerPart[0] is '+' or '-' ? integerPart[1..] : integerPart;
        if (!IsDigits(digits) || (fractionPart.Length > 0 && !IsDigits(fractionPart)))
        {
            return false;
        }

        // Invariant parsing of a string whose symbols have already been normalised: the declared
        // symbols govern, never the machine's culture. A value with more digits than a decimal
        // holds conforms, and simply has no value this type can carry.
        normalised = fractionPart.Length > 0 ? integerPart + "." + fractionPart : integerPart;
        number = decimal.TryParse(
            normalised,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
        return true;
    }

    private static bool IsDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private static bool TryUngroup(ref string integerPart, string grouping)
    {
        var groups = integerPart.Split(grouping);
        if (groups.Length == 1)
        {
            return true;
        }

        var first = groups[0];
        if (first.Length > 0 && (first[0] == '-' || first[0] == '+'))
        {
            first = first[1..];
        }

        if (first.Length is 0 or > 3)
        {
            return false;
        }

        for (var index = 1; index < groups.Length; index++)
        {
            if (groups[index].Length != 3)
            {
                return false;
            }
        }

        integerPart = string.Concat(groups);
        return true;
    }
}
