using GoBd.Validation.Findings;

namespace GoBd.Validation.Localisation;

/// <summary>The language chosen for a run, and anything worth reporting about the choice.</summary>
/// <param name="Language">The language reports will be rendered in.</param>
/// <param name="Finding">Raised when a requested language was not available.</param>
public sealed record LanguageResolution(ReportLanguage Language, Finding? Finding);

/// <summary>Chooses the report language for a run.</summary>
public static class LanguageResolver
{
    /// <summary>Environment variable a caller can set instead of passing an option.</summary>
    public const string EnvironmentVariable = "GOBD_LANG";

    /// <summary>
    /// Resolves the report language, first match winning: an explicit request, then
    /// <c>GOBD_LANG</c>, then the operating system, then English.
    /// </summary>
    /// <remarks>
    /// An unsupported request degrades to English with an informational finding rather than
    /// failing the run — the caller wanted a report, and refusing to produce one over a language
    /// tag would be a poor trade.
    /// </remarks>
    public static LanguageResolution Resolve(string? requested, ILanguageEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (!string.IsNullOrWhiteSpace(requested))
        {
            return ReportLanguages.Parse(requested) is { } explicitLanguage
                ? new LanguageResolution(explicitLanguage, null)
                : new LanguageResolution(
                    ReportLanguage.English,
                    Finding.Create(FindingCodes.LanguageUnsupported, null, requested));
        }

        if (ReportLanguages.Parse(environment.GetEnvironmentVariable(EnvironmentVariable)) is { } fromEnvironment)
        {
            return new LanguageResolution(fromEnvironment, null);
        }

        if (ReportLanguages.Parse(environment.GetOperatingSystemLanguage()) is { } fromSystem)
        {
            return new LanguageResolution(fromSystem, null);
        }

        return new LanguageResolution(ReportLanguage.English, null);
    }

    /// <summary>Resolves against the real process environment.</summary>
    public static LanguageResolution Resolve(string? requested) =>
        Resolve(requested, new SystemLanguageEnvironment());
}
