using System.Text.Json;
using GoBd.Validation.Cli.Reporting;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Tests.Reporting;

public sealed class TextReportWriterTests
{
    private static string Render(ValidationReport report, ReportLanguage language = ReportLanguage.English)
    {
        var output = new StringWriter();
        new TextReportWriter().Write(output, report, language);
        return output.ToString();
    }

    private static ValidationReport Mixed(bool strict = false) => new("/exports/beispiel.zip",
    [
        Finding.Create(FindingCodes.DtdLegacySystemIdentifier, new SourceLocation(2, 1), "gdpdu-01-08-2002.dtd"),
        Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(31, 9), "Bestellungen", "Kunden"),
        Finding.Create(FindingCodes.MediaWithoutTables, new SourceLocation(5, 3), "Disk 2"),
        Finding.Create(FindingCodes.FixedRangeGap, new SourceLocation(44, 7), "Sales", "11", "20"),
        Finding.Create(FindingCodes.ColumnNameDuplicated, new SourceLocation(22, 5), "Bestellungen", "Id"),
    ], strict);

    [Fact]
    public void ReportLeadsWithTheVerdict()
    {
        var lines = Render(Mixed()).Split(Environment.NewLine);

        lines[0].ShouldBe("GoBD export validation: NOT CONFORMANT");
        lines[1].ShouldBe("Export: /exports/beispiel.zip");
        lines[2].ShouldBe("2 error(s), 1 warning(s), 2 note(s)");
    }

    [Fact]
    public void CleanReportSaysConformant() =>
        Render(new ValidationReport("/exports/clean.zip", []))
            .ShouldContain("GoBD export validation: CONFORMANT");

    [Fact]
    public void FindingsAreGroupedByWhatTheyConcern()
    {
        var text = Render(Mixed());

        text.ShouldContain("Bestellungen");
        text.ShouldContain("Disk 2");
        text.ShouldContain("Sales");
        // A finding about the export itself is not filed under a table.
        text.ShouldContain("(export as a whole)");
    }

    [Fact]
    public void FindingsWithinAGroupAreOrderedBySeverity()
    {
        var report = new ValidationReport("/e.zip",
        [
            Finding.Create(FindingCodes.DescriptionTooLong, new SourceLocation(9, 1), "Kunden", "300"),
            Finding.Create(FindingCodes.ColumnNameDuplicated, new SourceLocation(8, 1), "Kunden", "Id"),
            Finding.Create(FindingCodes.ReservedCharacterInName, new SourceLocation(7, 1), "Kunden", "A&B", "&"),
        ]);

        var body = Render(report);
        var error = body.IndexOf("GOBD3026", StringComparison.Ordinal);
        var warning = body.IndexOf("GOBD3027", StringComparison.Ordinal);
        var info = body.IndexOf("GOBD3028", StringComparison.Ordinal);

        error.ShouldBeLessThan(warning);
        warning.ShouldBeLessThan(info);
    }

    [Fact]
    public void EveryFindingCarriesItsCodeSeverityAndPosition()
    {
        var text = Render(Mixed());

        text.ShouldContain("error    GOBD3002  31:9");
        text.ShouldContain("warning  GOBD3013  5:3");
    }

    [Fact]
    public void FindingWithoutAPositionRendersADashNotAPlaceholderPosition() =>
        Render(new ValidationReport("/e.zip", [Finding.Create(FindingCodes.DtdMissing, null, "gdpdu-01-03-2019.dtd")]))
            .ShouldContain("GOBD2003  -");

    [Fact]
    public void StrictModeIsStatedWhenActive()
    {
        Render(Mixed(strict: true)).ShouldContain("Strict mode: warnings count against the verdict.");
        Render(Mixed(strict: false)).ShouldNotContain("Strict mode");
    }

    // ---- Task 10.2 ------------------------------------------------------------------------

    [Fact]
    public void CleanRunStatesThatDataFilesWereNotExamined()
    {
        // The single most likely way v1 misleads its user is a clean result being read as
        // "my export is valid" when no data file was ever opened.
        var text = Render(new ValidationReport("/exports/clean.zip", []));

        text.ShouldContain("index.xml, the DTD and the export's file listing were checked");
        text.ShouldContain("contents of the data files were NOT examined");
    }

    [Fact]
    public void ScopeStatementAppearsOnEveryRunNotOnlyCleanOnes() =>
        Render(Mixed()).ShouldContain("NOT examined");

    [Fact]
    public void GermanReportIsRenderedInGerman()
    {
        var text = Render(Mixed(), ReportLanguage.German);

        text.ShouldContain("GoBD-Exportprüfung: NICHT KONFORM");
        text.ShouldContain("Die Inhalte der Datendateien wurden NICHT untersucht.");
        text.ShouldContain("Fehler");
    }

    [Fact]
    public void RenderingIsDeterministic() => Render(Mixed()).ShouldBe(Render(Mixed()));
}
