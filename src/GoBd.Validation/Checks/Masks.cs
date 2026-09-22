using System.Globalization;
using System.Text.RegularExpressions;

namespace GoBd.Validation.Checks;

/// <summary>Interprets the format masks and scalar values the standard declares.</summary>
internal static partial class Masks
{
    /// <summary>
    /// Time masks recognised by standard 1.6: <c>HH</c> and <c>MM</c> are required, <c>SS</c>
    /// and <c>TT</c> optional, and a separator may appear between parts.
    /// </summary>
    [GeneratedRegex(@"^HH([^A-Za-z0-9]?)MM(([^A-Za-z0-9]?)SS)?(([^A-Za-z0-9]?)TT)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimeMask();

    /// <summary>True when the value is a valid time mask.</summary>
    internal static bool IsTimeMask(string value) =>
        !string.IsNullOrEmpty(value) && TimeMask().IsMatch(value);

    /// <summary>
    /// True when the mask carries exactly one year, month and day placeholder.
    /// </summary>
    /// <remarks>
    /// The placeholders are consumed longest-first so that <c>YYYY</c> is not mistaken for two
    /// <c>YY</c>. Anything left over must be separator characters.
    /// </remarks>
    internal static bool IsUsableDateMask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var working = value;
        var years = Consume(ref working, "YYYY") + Consume(ref working, "YY");
        var months = Consume(ref working, "MM");
        var days = Consume(ref working, "DD");

        if (years != 1 || months != 1 || days != 1)
        {
            return false;
        }

        // Whatever remains must be punctuation, not stray placeholder letters.
        return working.All(character => !char.IsAsciiLetterOrDigit(character));
    }

    private static int Consume(ref string working, string token)
    {
        var count = 0;
        int index;
        while ((index = working.IndexOf(token, StringComparison.Ordinal)) >= 0)
        {
            working = working.Remove(index, token.Length);
            count++;
        }

        return count;
    }

    /// <summary>Parses a declared scalar as a non-negative integer, invariantly.</summary>
    internal static bool TryNonNegativeInteger(string value, out int parsed) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);

    /// <summary>The mask a <c>Date</c> column carries when it declares none, per the DTD.</summary>
    internal const string DefaultDateMask = "DD.MM.YYYY";

    /// <summary>
    /// Reads a value as the date its column's mask describes.
    /// </summary>
    /// <remarks>
    /// The mask is walked rather than translated into a .NET format string: the standard's
    /// placeholders are only <c>YYYY</c>, <c>YY</c>, <c>MM</c> and <c>DD</c>, everything else is
    /// a literal separator, and a .NET custom format would drag in culture-sensitive behaviour
    /// this must not have. A value matches only if it matches exactly — no leniency about
    /// missing leading zeros, because a mask that says <c>DD</c> and a file that writes <c>1</c>
    /// disagree, and that disagreement is the finding.
    /// </remarks>
    /// <param name="value">The value as the file holds it.</param>
    /// <param name="mask">The declared mask.</param>
    /// <param name="epoch">Two-digit years below this belong to the twenty-first century.</param>
    /// <param name="parsed">The date the value denotes.</param>
    internal static bool TryDate(string value, string mask, int epoch, out DateOnly parsed)
    {
        parsed = default;
        if (string.IsNullOrEmpty(mask))
        {
            return false;
        }

        int? year = null;
        int? month = null;
        int? day = null;
        var position = 0;

        for (var index = 0; index < mask.Length;)
        {
            if (Match(mask, index, "YYYY"))
            {
                if (!Digits(value, ref position, 4, out var read))
                {
                    return false;
                }

                year = read;
                index += 4;
            }
            else if (Match(mask, index, "YY"))
            {
                if (!Digits(value, ref position, 2, out var read))
                {
                    return false;
                }

                year = read < epoch ? 2000 + read : 1900 + read;
                index += 2;
            }
            else if (Match(mask, index, "MM"))
            {
                if (!Digits(value, ref position, 2, out var read))
                {
                    return false;
                }

                month = read;
                index += 2;
            }
            else if (Match(mask, index, "DD"))
            {
                if (!Digits(value, ref position, 2, out var read))
                {
                    return false;
                }

                day = read;
                index += 2;
            }
            else
            {
                if (position >= value.Length || value[position] != mask[index])
                {
                    return false;
                }

                position++;
                index++;
            }
        }

        if (position != value.Length || year is not { } y || month is not { } m || day is not { } d)
        {
            return false;
        }

        if (m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m))
        {
            return false;
        }

        parsed = new DateOnly(y, m, d);
        return true;
    }

    /// <summary>
    /// Reads a value as the time its column's mask describes.
    /// </summary>
    /// <remarks>
    /// A column says it holds a time by mapping a time mask to itself, and nothing checks its
    /// values against that mask — so unlike a date, a value here may simply not be a time, and
    /// that is an answer rather than a defect. The mask is walked as a date mask is: <c>HH</c> and
    /// <c>MM</c> are required, <c>SS</c> and <c>TT</c> optional, and everything else is a literal
    /// that must match exactly. With <c>TT</c> the hours run from 1 to 12, and without it from 0
    /// to 23.
    /// </remarks>
    /// <param name="value">The value as the file holds it, or as a person wrote it.</param>
    /// <param name="mask">The declared time mask.</param>
    /// <param name="parsed">The time the value denotes.</param>
    internal static bool TryTime(string value, string mask, out TimeOnly parsed)
    {
        parsed = default;
        if (string.IsNullOrEmpty(mask) || !IsTimeMask(mask))
        {
            return false;
        }

        int? hours = null;
        int? minutes = null;
        var seconds = 0;
        var afternoon = false;
        var meridiem = false;
        var position = 0;

        for (var index = 0; index < mask.Length;)
        {
            if (Match(mask, index, "HH"))
            {
                if (!Digits(value, ref position, 2, out var read))
                {
                    return false;
                }

                hours = read;
                index += 2;
            }
            else if (Match(mask, index, "MM"))
            {
                if (!Digits(value, ref position, 2, out var read))
                {
                    return false;
                }

                minutes = read;
                index += 2;
            }
            else if (Match(mask, index, "SS"))
            {
                if (!Digits(value, ref position, 2, out var read))
                {
                    return false;
                }

                seconds = read;
                index += 2;
            }
            else if (Match(mask, index, "TT"))
            {
                if (position + 2 > value.Length)
                {
                    return false;
                }

                var half = value.AsSpan(position, 2);
                if (half.Equals("AM", StringComparison.OrdinalIgnoreCase))
                {
                    afternoon = false;
                }
                else if (half.Equals("PM", StringComparison.OrdinalIgnoreCase))
                {
                    afternoon = true;
                }
                else
                {
                    return false;
                }

                meridiem = true;
                position += 2;
                index += 2;
            }
            else
            {
                if (position >= value.Length || value[position] != mask[index])
                {
                    return false;
                }

                position++;
                index++;
            }
        }

        if (position != value.Length || hours is not { } h || minutes is not { } m)
        {
            return false;
        }

        if (meridiem)
        {
            if (h is < 1 or > 12)
            {
                return false;
            }

            // Noon is 12 PM and midnight is 12 AM, so the twelve is the hour that moves.
            h = afternoon ? (h == 12 ? 12 : h + 12) : (h == 12 ? 0 : h);
        }

        if (h > 23 || m > 59 || seconds > 59)
        {
            return false;
        }

        parsed = new TimeOnly(h, m, seconds);
        return true;
    }

    private static bool Match(string mask, int index, string token) =>
        index + token.Length <= mask.Length && mask.AsSpan(index, token.Length).SequenceEqual(token);

    private static bool Digits(string value, ref int position, int count, out int parsed)
    {
        parsed = 0;
        if (position + count > value.Length)
        {
            return false;
        }

        for (var index = 0; index < count; index++)
        {
            var character = value[position + index];
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }

            parsed = (parsed * 10) + (character - '0');
        }

        position += count;
        return true;
    }
}
