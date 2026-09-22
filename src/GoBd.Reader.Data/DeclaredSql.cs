using System.Globalization;
using System.Text;
using GoBd.Validation.Checks;
using GoBd.Validation.Content;
using GoBd.Validation.Model;

namespace GoBd.Reader.Data;

/// <summary>
/// Expresses a column's declaration as the test the store applies to it.
/// </summary>
/// <remarks>
/// The same rules as the streaming reader's, written in the store's language rather than shared
/// with it. That is the point: two engines find the defects by different means and must report
/// the same ones, and a shared implementation would make the agreement true by construction and
/// therefore worthless as a check. What is shared is the definition — the defect classes and how
/// they are reported — not the finding of them. See the change's design.md D4.
/// </remarks>
internal static class DeclaredSql
{
    /// <summary>
    /// The padding a numeric value may carry, as the streaming engine defines it.
    /// </summary>
    /// <remarks>
    /// The store's own <c>trim</c> strips spaces alone and .NET's strips every Unicode
    /// whitespace character, so neither default would do: the set is named here so that both
    /// engines apply the same one.
    /// </remarks>
    private static readonly string PaddingCharacters =
        new(GoBd.Validation.Content.ValueInterpreter.Padding);

    /// <summary>A SQL predicate that is true when the value satisfies its declaration.</summary>
    /// <remarks>
    /// Returns null when the declaration imposes nothing this defect class can test.
    /// </remarks>
    internal static string? Conforms(
        string quoted,
        ColumnLayout column,
        RecordLayout layout,
        ContentDefectKind kind) =>
        (kind, column.Type) switch
        {
            (ContentDefectKind.TypeOrFormatMismatch, NumericType) =>
                $"regexp_full_match({Trimmed(quoted)}, {Literal(NumberPattern(layout))})",

            (ContentDefectKind.TypeOrFormatMismatch, DateType date) =>
                DateConforms(quoted, date),

            (ContentDefectKind.AccuracyExceeded, NumericType numeric) =>
                AccuracyConforms(quoted, layout, numeric),

            (ContentDefectKind.MaxLengthExceeded, AlphaNumericType alphanumeric) =>
                MaxLengthConforms(quoted, alphanumeric),

            _ => null,
        };

    /// <summary>
    /// The number a value denotes, as SQL that can be compared, ordered and totalled.
    /// </summary>
    /// <remarks>
    /// Only ever queried, never displayed: the grid shows what the file stores, and a number
    /// rendered back from a <c>DECIMAL</c> is not that. Guarded by the emptiness test its caller
    /// wraps it in, and otherwise safe because a table only reaches the grid when every one of its
    /// values matches its declaration — so the cast cannot meet a value it cannot read.
    /// <para>
    /// The sign is taken from either end, as <see cref="ValueInterpreter.TrailingSignReadable"/>
    /// allows. Under <c>ImpliedAccuracy</c> the decimal point is written into the digits rather
    /// than divided in, so nothing rounds.
    /// </para>
    /// </remarks>
    /// <param name="quoted">The column, already quoted as an identifier.</param>
    /// <param name="layout">The table's layout, for its declared symbols.</param>
    /// <param name="numeric">The column's declared type.</param>
    /// <param name="scale">Decimal places the value is read at.</param>
    internal static string Number(string quoted, RecordLayout layout, NumericType numeric, int scale)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(numeric);

        var trimmed = Trimmed(quoted);
        var negative = ValueInterpreter.TrailingSignReadable(layout)
            ? $"({trimmed} LIKE '-%' OR {trimmed} LIKE '%-')"
            : $"{trimmed} LIKE '-%'";

        var value = $"CAST({NumberDigits(quoted, layout, numeric)}"
            + $" AS DECIMAL(38, {scale.ToString(CultureInfo.InvariantCulture)}))";

        return $"CASE WHEN {negative} THEN -{value} ELSE {value} END";
    }

    /// <summary>
    /// A number's digits without its sign, with <c>.</c> where its decimal point belongs.
    /// </summary>
    /// <remarks>
    /// Both what <see cref="Number"/> casts and what the import measures a column's width from,
    /// stated once so that the width a column is judged by is the width the cast will meet.
    /// </remarks>
    internal static string NumberDigits(string quoted, RecordLayout layout, NumericType numeric)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(numeric);

        var digits = $"trim({Trimmed(quoted)}, '+-')";
        if (layout.DigitGroupingSymbol.Length > 0)
        {
            digits = $"replace({digits}, {Literal(layout.DigitGroupingSymbol)}, '')";
        }

        if (layout.DecimalSymbol.Length > 0)
        {
            digits = $"replace({digits}, {Literal(layout.DecimalSymbol)}, '.')";
        }

        if (numeric.ImpliedAccuracy is { } implied
            && Masks.TryNonNegativeInteger(implied.Value, out var places)
            && places > 0)
        {
            // The declaration says the file writes decimals it does not print, so the point moves
            // that many places to the left — past any decimals the value does print, which is how
            // the streaming engine reads such a value too. Moved rather than divided by a power
            // of ten, because a division would round a value the file states exactly.
            var written = $"CASE WHEN position('.' IN {digits}) = 0 THEN 0"
                + $" ELSE length({digits}) - position('.' IN {digits}) END";
            var bare = $"replace({digits}, '.', '')";

            // Counted as an INTEGER because that is what left, right and lpad take; a length is a
            // BIGINT, and handed one of those they match no overload at all.
            var shift = $"CAST({written} + {places.ToString(CultureInfo.InvariantCulture)} AS INTEGER)";
            var kept = $"CAST(length({bare}) AS INTEGER) - {shift}";

            digits = $"CASE WHEN length({bare}) <= {shift}"
                + $" THEN '0.' || lpad({bare}, {shift}, '0')"
                + $" ELSE left({bare}, {kept}) || '.' || right({bare}, {shift}) END";
        }

        return digits;
    }

    /// <summary>
    /// The date a value denotes under its column's mask, as SQL that can be compared and ordered.
    /// </summary>
    /// <remarks>
    /// Built from the mask's own positions rather than handed to <c>strptime</c>, because a
    /// two-digit year takes its century from the table's declared <c>Epoch</c> and the store's
    /// own pivot is not that. Returns null when the mask is unusable, which
    /// <c>GOBD3022</c> already reports.
    /// </remarks>
    internal static string? Date(string quoted, DateType date, RecordLayout layout)
    {
        ArgumentNullException.ThrowIfNull(date);
        ArgumentNullException.ThrowIfNull(layout);

        var mask = date.Format?.Value ?? Masks.DefaultDateMask;
        if (Positions(mask) is not { } positions)
        {
            return null;
        }

        var epoch = layout.Epoch.ToString(CultureInfo.InvariantCulture);
        var year = positions.YearDigits == 4
            ? Digits(quoted, positions.Year, 4)
            : $"(CASE WHEN {Digits(quoted, positions.Year, 2)} < {epoch} THEN 2000 ELSE 1900 END"
                + $" + {Digits(quoted, positions.Year, 2)})";

        return $"make_date({year}, {Digits(quoted, positions.Month, 2)}, {Digits(quoted, positions.Day, 2)})";
    }

    /// <summary>A fixed-width run of digits within the value, as an integer.</summary>
    private static string Digits(string quoted, int from, int length) =>
        $"CAST(substr({quoted}, {from.ToString(CultureInfo.InvariantCulture)},"
        + $" {length.ToString(CultureInfo.InvariantCulture)}) AS INTEGER)";

    /// <summary>Where each field of a date mask begins, one-based, or null when it is unusable.</summary>
    /// <remarks>
    /// Every placeholder the standard defines is fixed-width and every other character is one
    /// literal, so a field's position is the same in every value the mask describes.
    /// </remarks>
    private static (int Year, int YearDigits, int Month, int Day)? Positions(string mask)
    {
        var year = 0;
        var yearDigits = 0;
        var month = 0;
        var day = 0;
        var at = 1;

        for (var index = 0; index < mask.Length;)
        {
            if (Match(mask, index, "YYYY") || Match(mask, index, "YY"))
            {
                var width = Match(mask, index, "YYYY") ? 4 : 2;
                if (year > 0)
                {
                    return null;
                }

                (year, yearDigits) = (at, width);
                (at, index) = (at + width, index + width);
            }
            else if (Match(mask, index, "MM"))
            {
                if (month > 0)
                {
                    return null;
                }

                month = at;
                (at, index) = (at + 2, index + 2);
            }
            else if (Match(mask, index, "DD"))
            {
                if (day > 0)
                {
                    return null;
                }

                day = at;
                (at, index) = (at + 2, index + 2);
            }
            else
            {
                (at, index) = (at + 1, index + 1);
            }
        }

        return year > 0 && month > 0 && day > 0 ? (year, yearDigits, month, day) : null;
    }

    /// <summary>
    /// The time a value denotes under a column's declared time mask, or null when it declares none.
    /// </summary>
    /// <remarks>
    /// A Time column is an <c>AlphaNumeric</c> column whose <c>Map</c> names the same time mask on
    /// both sides, which is how the standard lets a column say it holds a time. Nothing checks
    /// such a column's values, so unlike a number or a date this reading has to tolerate a value
    /// that is not a time: <c>try_strptime</c> answers null for one, and the column reports how
    /// many there are.
    /// </remarks>
    internal static string? Time(string quoted, ColumnLayout column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (column.Type is not AlphaNumericType || TimeMaskOf(column) is not { } mask)
        {
            return null;
        }

        var format = new StringBuilder();
        var meridiem = mask.Contains("TT", StringComparison.Ordinal);
        for (var index = 0; index < mask.Length;)
        {
            if (Match(mask, index, "HH"))
            {
                format.Append(meridiem ? "%I" : "%H");
                index += 2;
            }
            else if (Match(mask, index, "MM"))
            {
                format.Append("%M");
                index += 2;
            }
            else if (Match(mask, index, "SS"))
            {
                format.Append("%S");
                index += 2;
            }
            else if (Match(mask, index, "TT"))
            {
                format.Append("%p");
                index += 2;
            }
            else
            {
                format.Append(mask[index]);
                index++;
            }
        }

        return $"CAST(try_strptime({quoted}, {Literal(format.ToString())}) AS TIME)";
    }

    /// <summary>The time mask a column declares, or null when it declares none.</summary>
    internal static string? TimeMaskOf(ColumnLayout column)
    {
        ArgumentNullException.ThrowIfNull(column);

        foreach (var map in column.Declaration.Maps)
        {
            // The same map that declares a Time column: both sides the same mask. A map whose
            // sides differ is a value redefinition, which GOBD3027 reports when both are masks.
            if (string.Equals(map.From.Value, map.To.Value, StringComparison.Ordinal)
                && Masks.IsTimeMask(map.From.Value))
            {
                return map.From.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// The value as the grid shows it: the declared redefinition applied, and nothing else.
    /// </summary>
    /// <remarks>
    /// What a text filter compares and a text sort orders by, because a person filters what they
    /// can see. The standard allows <c>Map</c> on alphanumeric columns only, so this is the only
    /// declared reading a text column has. Declared values are written into the statement as
    /// literals; no value from the data reaches it.
    /// </remarks>
    internal static string Presented(string quoted, ColumnLayout column)
    {
        ArgumentNullException.ThrowIfNull(column);

        var redefinitions = column.Declaration.Maps
            .Where(map => !string.Equals(map.From.Value, map.To.Value, StringComparison.Ordinal))
            .ToList();
        if (redefinitions.Count == 0)
        {
            return quoted;
        }

        var cases = string.Concat(redefinitions.Select(map =>
            $" WHEN {Literal(map.From.Value)} THEN {Literal(map.To.Value)}"));

        return $"CASE {quoted}{cases} ELSE {quoted} END";
    }

    /// <summary>
    /// The grammar of a number under the table's declared symbols.
    /// </summary>
    /// <remarks>
    /// Digit grouping is tested rather than stripped. Under a declared grouping symbol of
    /// <c>.</c> the value <c>1.2.3</c> is not a number, and removing the separators before
    /// parsing would read it as 123 — the "silently read as something else" outcome the check
    /// exists to prevent.
    /// <para>
    /// One sign may stand directly before the digits or, where the declaration lets a trailing
    /// sign be told from its symbols, directly after them. The alternation states that there is
    /// one sign at one end; it is the same rule the streaming engine reaches by moving the sign.
    /// </para>
    /// </remarks>
    private static string NumberPattern(RecordLayout layout)
    {
        var decimalSymbol = Escape(layout.DecimalSymbol);
        var grouping = layout.DigitGroupingSymbol.Length > 0 ? Escape(layout.DigitGroupingSymbol) : null;

        var integer = grouping is null
            ? "[0-9]+"
            : $"(?:[0-9]+|[0-9]{{1,3}}(?:{grouping}[0-9]{{3}})+)";
        var fraction = layout.DecimalSymbol.Length > 0 ? $"(?:{decimalSymbol}[0-9]+)?" : string.Empty;
        var body = integer + fraction;

        return ValueInterpreter.TrailingSignReadable(layout)
            ? $"^(?:[+-]?{body}|{body}[+-])$"
            : $"^[+-]?{body}$";
    }

    private static string? DateConforms(string quoted, DateType date)
    {
        var mask = date.Format?.Value ?? Masks.DefaultDateMask;
        if (Translate(mask) is not { } translated)
        {
            return null;
        }

        // Two tests, because neither alone is enough: the pattern settles the shape, including
        // that a two-digit field is written with its leading zero, and the calendar settles
        // whether the shape names a day that exists.
        return $"(regexp_full_match({quoted}, {Literal(translated.Pattern)})"
            + $" AND try_strptime({quoted}, {Literal(translated.Format)}) IS NOT NULL)";
    }

    private static string? AccuracyConforms(string quoted, RecordLayout layout, NumericType numeric)
    {
        if (numeric.ImpliedAccuracy is not null
            || numeric.Accuracy is not { } accuracy
            || !Masks.TryNonNegativeInteger(accuracy.Value, out var places)
            || layout.DecimalSymbol.Length == 0)
        {
            return null;
        }

        // Only a value that is a number can carry too many decimals; one that is not is reported
        // as a type mismatch instead, and reporting both for one value would be noise. The digits
        // are counted up to a sign that may follow them: a sign is not a digit, and without room
        // for it "1,234-" would carry three decimals past this check unreported.
        var pattern = $"^.*{Escape(layout.DecimalSymbol)}[0-9]{{{places + 1},}}[+-]?$";
        return $"(NOT regexp_full_match({Trimmed(quoted)}, {Literal(NumberPattern(layout))})"
            + $" OR NOT regexp_full_match({Trimmed(quoted)}, {Literal(pattern)}))";
    }

    private static string? MaxLengthConforms(string quoted, AlphaNumericType alphanumeric)
    {
        if (alphanumeric.MaxLength is not { } declared
            || !Masks.TryNonNegativeInteger(declared.Value, out var maximum))
        {
            return null;
        }

        return $"length({quoted}) <= {maximum.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Turns a declared date mask into a pattern and a calendar format.</summary>
    private static (string Pattern, string Format)? Translate(string mask)
    {
        var pattern = new StringBuilder("^");
        var format = new StringBuilder();
        var years = 0;
        var months = 0;
        var days = 0;

        for (var index = 0; index < mask.Length;)
        {
            if (Match(mask, index, "YYYY"))
            {
                pattern.Append("[0-9]{4}");
                format.Append("%Y");
                years++;
                index += 4;
            }
            else if (Match(mask, index, "YY"))
            {
                pattern.Append("[0-9]{2}");
                format.Append("%y");
                years++;
                index += 2;
            }
            else if (Match(mask, index, "MM"))
            {
                pattern.Append("[0-9]{2}");
                format.Append("%m");
                months++;
                index += 2;
            }
            else if (Match(mask, index, "DD"))
            {
                pattern.Append("[0-9]{2}");
                format.Append("%d");
                days++;
                index += 2;
            }
            else
            {
                pattern.Append(Escape(mask[index].ToString()));
                format.Append(mask[index]);
                index++;
            }
        }

        pattern.Append('$');

        // A mask that does not name exactly one year, month and day is unusable, and GOBD3022
        // already reports it. Testing values against it would report every record for one
        // mistake in the declaration.
        return years == 1 && months == 1 && days == 1
            ? (pattern.ToString(), format.ToString())
            : null;
    }

    private static bool Match(string mask, int index, string token) =>
        index + token.Length <= mask.Length && mask.AsSpan(index, token.Length).SequenceEqual(token);

    /// <summary>The value with the padding a number may carry removed.</summary>
    private static string Trimmed(string quoted) => $"trim({quoted}, {Literal(PaddingCharacters)})";

    /// <summary>
    /// The row limit for a query that looks for defects under a bound: one more than the bound.
    /// </summary>
    /// <remarks>
    /// One more, so that "there are further defects" can be told from "there are exactly this
    /// many" — the budget records truncation by being asked for more than it allows.
    /// </remarks>
    internal static string Limit(int bound) => (bound + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Quotes an identifier so that a declared name cannot be read as SQL.</summary>
    internal static string Identifier(string name) =>
        "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>Quotes a string so that a declared value cannot be read as SQL.</summary>
    internal static string Literal(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>Escapes a declared symbol for use inside a pattern.</summary>
    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length * 2);
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
