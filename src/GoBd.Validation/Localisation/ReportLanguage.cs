using System.Buffers;

namespace GoBd.Validation.Localisation;

/// <summary>Languages the reports are rendered in.</summary>
/// <remarks>
/// Two, deliberately. GoBD is a German regime, so German earns its place; everything else gets
/// English. This is a two-language switch, not a culture system. See design.md D8.
/// </remarks>
public enum ReportLanguage
{
    /// <summary>English — the default.</summary>
    English,

    /// <summary>German.</summary>
    German,
}

/// <summary>Interprets language tags without consulting the globalization stack.</summary>
public static class ReportLanguages
{
    private static readonly SearchValues<char> SubtagSeparators = SearchValues.Create("_-.@");

    /// <summary>
    /// Maps a language tag onto a supported language, inspecting only the leading subtag.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>CultureInfo</c>: the process runs in globalization-invariant mode, so
    /// constructing a culture for "de-DE" would throw. Only "German or not" needs answering.
    /// </remarks>
    public static ReportLanguage? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var primary = PrimarySubtag(tag);
        return primary switch
        {
            var value when value.Equals("de", StringComparison.OrdinalIgnoreCase) => ReportLanguage.German,
            var value when value.Equals("en", StringComparison.OrdinalIgnoreCase) => ReportLanguage.English,
            _ => null,
        };
    }

    /// <summary>
    /// The leading subtag of a language tag or POSIX locale, e.g. <c>de</c> from
    /// <c>de_DE.UTF-8</c> or from <c>de-DE</c>.
    /// </summary>
    public static ReadOnlySpan<char> PrimarySubtag(string tag)
    {
        // POSIX locales use '_' and '.', BCP-47 tags use '-', and '@' introduces a modifier.
        ArgumentNullException.ThrowIfNull(tag);
        var span = tag.AsSpan().Trim();
        var end = span.IndexOfAny(SubtagSeparators);
        return end < 0 ? span : span[..end];
    }
}
