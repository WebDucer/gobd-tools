using System.Runtime.InteropServices;

namespace GoBd.Validation.Localisation;

/// <summary>Supplies the ambient signals language resolution consults.</summary>
public interface ILanguageEnvironment
{
    /// <summary>Reads an environment variable, or null when unset.</summary>
    string? GetEnvironmentVariable(string name);

    /// <summary>The operating system's UI language tag, or null when it cannot be determined.</summary>
    string? GetOperatingSystemLanguage();
}

/// <summary>Reads the real process environment.</summary>
public sealed class SystemLanguageEnvironment : ILanguageEnvironment
{
    /// <summary>Primary language identifier for German in the Windows NLS scheme.</summary>
    private const int LangGerman = 0x07;

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    /// <inheritdoc />
    /// <remarks>
    /// On Unix the POSIX locale variables are read in their conventional precedence. On Windows
    /// the NLS call is used, which needs no ICU and therefore works in globalization-invariant
    /// mode; a failure degrades to null so that resolution falls back to English rather than
    /// throwing. See design.md D8.
    /// <para>
    /// On macOS an application started from the Finder or the Dock inherits none of the POSIX
    /// variables, so when none is set the language the person put first in the system's settings
    /// is asked of CoreFoundation, which needs no ICU either. A terminal's variables still win,
    /// as they do everywhere else.
    /// </para>
    /// </remarks>
    public string? GetOperatingSystemLanguage()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var languageId = GetUserDefaultUILanguage();
                // The low 10 bits carry the primary language.
                return (languageId & 0x3FF) == LangGerman ? "de" : "en";
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        foreach (var name in (string[])["LC_ALL", "LC_MESSAGES", "LANG"])
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return OperatingSystem.IsMacOS() ? PreferredMacLanguage() : null;
    }

    /// <summary>The first of the languages the person prefers on macOS, such as <c>de-DE</c>.</summary>
    private static string? PreferredMacLanguage()
    {
        try
        {
            var languages = CFLocaleCopyPreferredLanguages();
            if (languages == 0)
            {
                return null;
            }

            try
            {
                if (CFArrayGetCount(languages) == 0)
                {
                    return null;
                }

                // Read a character at a time: each call returns a blittable UniChar, so nothing
                // needs marshalling. A language identifier is a handful of characters.
                var tag = CFArrayGetValueAtIndex(languages, 0);
                var length = (int)Math.Min(CFStringGetLength(tag), 64);
                var characters = new char[length];
                for (var index = 0; index < length; index++)
                {
                    characters[index] = (char)CFStringGetCharacterAtIndex(tag, index);
                }

                return new string(characters);
            }
            finally
            {
                CFRelease(languages);
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    // DllImport rather than LibraryImport: the signature takes no arguments and returns a
    // blittable ushort, so there is nothing to marshal and no reason to enable unsafe code
    // across the library for it. It stays fully NativeAOT-compatible.
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern ushort GetUserDefaultUILanguage();

    // DllImport for the same reason: every argument and result is a pointer-sized handle or a
    // UniChar, so there is nothing to marshal.
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreFoundation, ExactSpelling = true)]
    private static extern nint CFLocaleCopyPreferredLanguages();

    [DllImport(CoreFoundation, ExactSpelling = true)]
    private static extern nint CFArrayGetCount(nint array);

    [DllImport(CoreFoundation, ExactSpelling = true)]
    private static extern nint CFArrayGetValueAtIndex(nint array, nint index);

    [DllImport(CoreFoundation, ExactSpelling = true)]
    private static extern nint CFStringGetLength(nint text);

    [DllImport(CoreFoundation, ExactSpelling = true)]
    private static extern ushort CFStringGetCharacterAtIndex(nint text, nint index);

    [DllImport(CoreFoundation, ExactSpelling = true)]
    private static extern void CFRelease(nint value);
}
