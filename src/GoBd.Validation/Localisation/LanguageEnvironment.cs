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

        return null;
    }

    // DllImport rather than LibraryImport: the signature takes no arguments and returns a
    // blittable ushort, so there is nothing to marshal and no reason to enable unsafe code
    // across the library for it. It stays fully NativeAOT-compatible.
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern ushort GetUserDefaultUILanguage();
}
