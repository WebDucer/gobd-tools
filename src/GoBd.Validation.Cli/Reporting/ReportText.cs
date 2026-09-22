using System.Globalization;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Cli.Reporting;

/// <summary>
/// The report's own wording, as opposed to the findings' wording.
/// </summary>
/// <remarks>
/// Compiled in alongside the message catalogue, for the same reason: satellite assemblies would
/// defeat the single-binary goal. See design.md D8.
/// </remarks>
internal static class ReportText
{
    private static string Pick(ReportLanguage language, string english, string german) =>
        language == ReportLanguage.German ? german : english;

    internal static string Title(ReportLanguage language) =>
        Pick(language, "GoBD export validation", "GoBD-Exportprüfung");

    internal static string Conformant(ReportLanguage language) =>
        Pick(language, "CONFORMANT", "KONFORM");

    internal static string NonConformant(ReportLanguage language) =>
        Pick(language, "NOT CONFORMANT", "NICHT KONFORM");

    // Same word in both languages; no Pick needed.
    internal static string Export(ReportLanguage language) => "Export";

    internal static string General(ReportLanguage language) =>
        Pick(language, "(export as a whole)", "(Export insgesamt)");

    internal static string Severity(ReportLanguage language, Severity severity) => severity switch
    {
        Findings.Severity.Error => Pick(language, "error", "Fehler"),
        Findings.Severity.Warning => Pick(language, "warning", "Warnung"),
        _ => Pick(language, "info", "Hinweis"),
    };

    /// <remarks>
    /// The counts are formatted invariantly by hand. Interpolating them directly would use the
    /// ambient culture, and CA1305 does not fire on interpolated string handlers — so this is
    /// precisely the leak the analyzers cannot catch for us. See design.md D5 and D8.
    /// </remarks>
    internal static string Counts(ReportLanguage language, int errors, int warnings, int info)
    {
        var e = errors.ToString(CultureInfo.InvariantCulture);
        var w = warnings.ToString(CultureInfo.InvariantCulture);
        var i = info.ToString(CultureInfo.InvariantCulture);
        return Pick(language,
            $"{e} error(s), {w} warning(s), {i} note(s)",
            $"{e} Fehler, {w} Warnung(en), {i} Hinweis(e)");
    }

    internal static string WrittenTo(ReportLanguage language, string destination) =>
        Pick(language,
            $"Report written to {destination}",
            $"Bericht geschrieben nach {destination}");

    internal static string StrictNote(ReportLanguage language) =>
        Pick(language,
            "Strict mode: warnings count against the verdict.",
            "Strikter Modus: Warnungen zählen gegen das Ergebnis.");

    /// <summary>
    /// States what this run did and did not examine.
    /// </summary>
    /// <remarks>
    /// The single most likely way v1 misleads its user is a clean result being read as "my
    /// export is valid" when no data file was ever opened. That is why this is required product
    /// behaviour rather than a note in the documentation.
    /// </remarks>
    internal static string Scope(ReportLanguage language, bool contentsExamined) =>
        contentsExamined
            ? Pick(language,
                "Scope: index.xml, the DTD, the export's file listing and the contents of the "
                + "data files were checked.",
                "Prüfumfang: index.xml, die DTD, das Dateiverzeichnis des Exports und die Inhalte "
                + "der Datendateien wurden geprüft.")
            : Pick(language,
                "Scope: index.xml, the DTD and the export's file listing were checked. "
                + "The contents of the data files were NOT examined.",
                "Prüfumfang: index.xml, die DTD und das Dateiverzeichnis des Exports wurden geprüft. "
                + "Die Inhalte der Datendateien wurden NICHT untersucht.");
}
