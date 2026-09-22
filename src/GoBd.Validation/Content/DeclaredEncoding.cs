using System.Text;
using GoBd.Validation.Model;

namespace GoBd.Validation.Content;

/// <summary>
/// Turns a table's declared codepage into an encoding that reads its bytes as it means them.
/// </summary>
/// <remarks>
/// Three of the six declared codepages — ANSI, OEM and Macintosh — are not in the default
/// encoding set of .NET, so the code page provider is registered before any of them is asked
/// for.
/// <para>
/// Undecodable bytes are replaced with <see cref="Sentinel"/> rather than thrown on. Throwing
/// would be simpler but would report the wrong record: a reader decodes in blocks, so the
/// exception surfaces where the buffer was filled, not where the bad bytes sit. A replacement
/// character occupies the position the bytes did, which lets the reader name the record and the
/// column exactly, and lets it carry on so the rest of the file is still checked. It is a
/// replacement, but it is not a silent one.
/// </para>
/// </remarks>
internal static class DeclaredEncoding
{
    /// <summary>ANSI, and the standard's default when a table declares no codepage.</summary>
    private const int Ansi = 1252;

    /// <summary>
    /// OEM. The standard says only "OEM", which on Windows means whatever the machine's OEM code
    /// page is — 437 in the United States, 850 across western Europe. A GoBD export is a German
    /// tax document, so 850 is the reading that keeps umlauts intact; 437 would render them as
    /// unrelated box-drawing characters. The two agree on everything below 128.
    /// </summary>
    private const int Oem = 850;

    private const int Macintosh = 10000;
    private const int Utf16 = 1200;
    private const int Utf7 = 65000;
    private const int Utf8 = 65001;

    /// <summary>
    /// Stands in the decoded text for bytes the declared codepage cannot represent.
    /// </summary>
    /// <remarks>
    /// U+FFFD is the character Unicode reserves for exactly this, so a file that legitimately
    /// contains one is a file whose producer already lost the original bytes. Treating that as a
    /// defect too is the right answer rather than a false positive.
    /// </remarks>
    internal const char Sentinel = '\uFFFD';

    private static readonly bool Registered = RegisterProvider();

    private static bool RegisterProvider()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return true;
    }

    /// <summary>
    /// Resolves the encoding a table declares, or reports that this build cannot supply it.
    /// </summary>
    internal static bool TryResolve(Codepage codepage, out Encoding encoding)
    {
        _ = Registered;

        var identifier = codepage switch
        {
            Codepage.Ansi or Codepage.Unspecified => Ansi,
            Codepage.Oem => Oem,
            Codepage.Macintosh => Macintosh,
            Codepage.Utf16 => Utf16,
            Codepage.Utf7 => Utf7,
            Codepage.Utf8 => Utf8,
            _ => 0,
        };

        if (identifier == 0)
        {
            encoding = Encoding.UTF8;
            return false;
        }

        try
        {
            encoding = Encoding.GetEncoding(
                identifier,
                EncoderFallback.ExceptionFallback,
                new DecoderReplacementFallback(Sentinel.ToString()));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            // UTF-7 arrives here unless the host has re-enabled it. Reporting the table as
            // unreadable is the truthful outcome; guessing another codepage would produce
            // findings about values that were never in the file.
            encoding = Encoding.UTF8;
            return false;
        }
    }

    /// <summary>The name to quote when a codepage cannot be supplied.</summary>
    internal static string NameOf(Codepage codepage) => codepage switch
    {
        Codepage.Ansi => "ANSI",
        Codepage.Oem => "OEM",
        Codepage.Macintosh => "Macintosh",
        Codepage.Utf16 => "UTF16",
        Codepage.Utf7 => "UTF7",
        Codepage.Utf8 => "UTF8",
        _ => "ANSI",
    };
}
