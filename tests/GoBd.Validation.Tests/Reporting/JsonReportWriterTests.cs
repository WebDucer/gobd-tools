using System.Text.Json;
using GoBd.Validation.Cli.Reporting;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Tests.Reporting;

public sealed class JsonReportWriterTests
{
    private static string Render(ValidationReport report, ReportLanguage language = ReportLanguage.English)
    {
        var output = new StringWriter();
        new JsonReportWriter().Write(output, report, language);
        return output.ToString();
    }

    private static JsonDocument Parse(ValidationReport report, ReportLanguage language = ReportLanguage.English) =>
        JsonDocument.Parse(Render(report, language));

    private static ValidationReport Mixed(bool strict = false) => new("/exports/beispiel.zip",
    [
        Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(31, 9), "Bestellungen", "Kunden").About(FindingScope.Table("Bestellungen")),
        Finding.Create(FindingCodes.MediaWithoutTables, new SourceLocation(5, 3), "Disk 2"),
        Finding.Create(FindingCodes.DtdLegacySystemIdentifier, null, "gdpdu-01-08-2002.dtd"),
    ], strict);

    [Fact]
    public void ReportCarriesSchemaVersionVerdictAndCounts()
    {
        var root = Parse(Mixed()).RootElement;

        root.GetProperty("schemaVersion").GetInt32().ShouldBe(JsonReportWriter.SchemaVersion);
        root.GetProperty("export").GetString().ShouldBe("/exports/beispiel.zip");
        root.GetProperty("verdict").GetString().ShouldBe("nonConformant");
        root.GetProperty("strict").GetBoolean().ShouldBeFalse();
        root.GetProperty("counts").GetProperty("error").GetInt32().ShouldBe(1);
        root.GetProperty("counts").GetProperty("warning").GetInt32().ShouldBe(1);
        root.GetProperty("counts").GetProperty("info").GetInt32().ShouldBe(1);
    }

    [Fact]
    public void EveryFindingCarriesCodeSeverityScopeAndPosition()
    {
        var findings = Parse(Mixed()).RootElement.GetProperty("findings");
        var dangling = findings.EnumerateArray().First(finding =>
            finding.GetProperty("code").GetString() == FindingCodes.ReferenceDangling);

        dangling.GetProperty("severity").GetString().ShouldBe("error");
        dangling.GetProperty("scope").GetString().ShouldBe("Bestellungen");
        dangling.GetProperty("line").GetInt32().ShouldBe(31);
        dangling.GetProperty("column").GetInt32().ShouldBe(9);
        dangling.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("Kunden");
    }

    [Fact]
    public void FindingWithoutAPositionCarriesNullNotZero()
    {
        var findings = Parse(Mixed()).RootElement.GetProperty("findings");
        var legacy = findings.EnumerateArray().First(finding =>
            finding.GetProperty("code").GetString() == FindingCodes.DtdLegacySystemIdentifier);

        // A placeholder 0:0 would be a lie about where the finding arose.
        legacy.GetProperty("line").ValueKind.ShouldBe(JsonValueKind.Null);
        legacy.GetProperty("column").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void CleanRunCarriesAnEmptyArrayRatherThanOmittingTheField()
    {
        var root = Parse(new ValidationReport("/exports/clean.zip", [])).RootElement;

        root.GetProperty("verdict").GetString().ShouldBe("conformant");
        root.GetProperty("findings").ValueKind.ShouldBe(JsonValueKind.Array);
        root.GetProperty("findings").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void StrictModeIsRecorded() =>
        Parse(Mixed(strict: true)).RootElement.GetProperty("strict").GetBoolean().ShouldBeTrue();

    // ---- Language neutrality ---------------------------------------------------------------

    [Fact]
    public void OnlyTheMessageFieldDiffersBetweenLanguages()
    {
        var english = Parse(Mixed()).RootElement;
        var german = Parse(Mixed(), ReportLanguage.German).RootElement;

        // A pipeline parsing this must behave identically whoever ran the validator.
        german.GetProperty("schemaVersion").GetInt32().ShouldBe(english.GetProperty("schemaVersion").GetInt32());
        german.GetProperty("verdict").GetString().ShouldBe(english.GetProperty("verdict").GetString());
        german.GetProperty("language").GetString().ShouldBe("de");
        english.GetProperty("language").GetString().ShouldBe("en");

        var englishFindings = english.GetProperty("findings").EnumerateArray().ToArray();
        var germanFindings = german.GetProperty("findings").EnumerateArray().ToArray();
        for (var index = 0; index < englishFindings.Length; index++)
        {
            germanFindings[index].GetProperty("code").GetString()
                .ShouldBe(englishFindings[index].GetProperty("code").GetString());
            germanFindings[index].GetProperty("severity").GetString()
                .ShouldBe(englishFindings[index].GetProperty("severity").GetString());
            germanFindings[index].GetProperty("scope").GetString()
                .ShouldBe(englishFindings[index].GetProperty("scope").GetString());
            germanFindings[index].GetProperty("message").GetString()
                .ShouldNotBe(englishFindings[index].GetProperty("message").GetString());
        }
    }

    [Fact]
    public void FieldNamesAreIdenticalBetweenLanguages()
    {
        static IEnumerable<string> Names(JsonElement element) =>
            element.EnumerateObject().Select(property => property.Name);

        Names(Parse(Mixed(), ReportLanguage.German).RootElement)
            .ShouldBe(Names(Parse(Mixed()).RootElement));
    }

    // ---- Task 10.4 --------------------------------------------------------------------------

    [Fact]
    public void OutputIsExactlyOneJsonDocumentAndNothingElse()
    {
        var text = Render(Mixed());

        // Nothing may be interleaved, or the stream cannot be parsed directly.
        Should.NotThrow(() => JsonDocument.Parse(text));
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(text));
        reader.Read().ShouldBeTrue();
        reader.TrySkip().ShouldBeTrue();
        reader.Read().ShouldBeFalse("the stream must contain no second document");
    }

    [Fact]
    public void OutputStartsWithTheOpeningBrace() => Render(Mixed()).TrimStart().ShouldStartWith("{");

    [Fact]
    public void RenderingIsDeterministic() => Render(Mixed()).ShouldBe(Render(Mixed()));
}
