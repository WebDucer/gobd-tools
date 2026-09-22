using System.Text.Json;
using GoBd.Validation.Cli.Reporting;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Tests.Reporting;

/// <summary>
/// A message is written for a person, and the JSON report is where a person most often reads it.
/// These assert on the raw text, not on parsed values: every other JSON test goes through
/// JsonDocument.Parse, which un-escapes transparently and so cannot see this at all.
/// </summary>
public sealed class JsonReadabilityTests
{
    private static string Render(Finding finding, ReportLanguage language = ReportLanguage.English)
    {
        var output = new StringWriter();
        new JsonReportWriter().Write(output, new ValidationReport("/e.zip", [finding]), language);
        return output.ToString();
    }

    [Fact]
    public void QuotedNamesKeepTheirApostrophes()
    {
        var text = Render(Finding.Create(FindingCodes.ReferenceDangling, null, "Bestellungen", "Kunden"));

        text.ShouldContain("'Bestellungen'");
        text.ShouldNotContain("\\u0027");
    }

    [Fact]
    public void GermanTextKeepsItsUmlauts()
    {
        var text = Render(
            Finding.Create(FindingCodes.ForeignKeyColumnUndeclared, null, "T", "C", "R"),
            ReportLanguage.German);

        // "Fremdschlüsselspalte" was rendering as "Fremdschl\u00FCsselspalte" for the audience
        // this tool is primarily for.
        text.ShouldContain("Fremdschlüsselspalte");
        text.ShouldNotContain("\\u00");
    }

    [Fact]
    public void TheCharactersAFindingIsAboutAreVisible()
    {
        // GOBD3027 reports which forbidden characters were found. Escaping them defeats it.
        var text = Render(Finding.Create(FindingCodes.ReservedCharacterInName, null, "Kunden", "A&B<C>", "&<>"));

        text.ShouldContain("&");
        text.ShouldContain("<");
        text.ShouldContain(">");
        text.ShouldNotContain("\\u0026");
        text.ShouldNotContain("\\u003C");
    }

    [Fact]
    public void WhatTheGrammarRequiresIsStillEscaped()
    {
        // A quotation mark and a backslash must be escaped, or the document is not JSON.
        var text = Render(Finding.Create(FindingCodes.XmlNotWellFormed, null, "he said \"no\" at C:\\temp"));

        text.ShouldContain("\\\"");
        text.ShouldContain("\\\\");
        Should.NotThrow(() => JsonDocument.Parse(text));
    }

    [Fact]
    public void TheDocumentIsStillExactlyOneValidJsonDocument()
    {
        var text = Render(Finding.Create(FindingCodes.ReservedCharacterInName, null, "K", "A&B", "&"));

        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(text));
        reader.Read().ShouldBeTrue();
        reader.TrySkip().ShouldBeTrue();
        reader.Read().ShouldBeFalse("the stream must contain no second document");
    }

    [Fact]
    public void AParserSeesExactlyWhatItSawBefore()
    {
        // The change is invisible to a consumer: same values, same field names, same version.
        var finding = Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(3, 5), "Bestellungen", "Kunden").About(FindingScope.Table("Bestellungen"));
        var root = JsonDocument.Parse(Render(finding)).RootElement;

        root.GetProperty("schemaVersion").GetInt32().ShouldBe(JsonReportWriter.SchemaVersion);
        root.GetProperty("verdict").GetString().ShouldBe("nonConformant");
        var parsed = root.GetProperty("findings").EnumerateArray().Single();
        parsed.GetProperty("code").GetString().ShouldBe(FindingCodes.ReferenceDangling);
        parsed.GetProperty("severity").GetString().ShouldBe("error");
        parsed.GetProperty("scope").GetString().ShouldBe("Bestellungen");
        parsed.GetProperty("line").GetInt32().ShouldBe(3);
        parsed.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("'Kunden'");
    }

    [Fact]
    public void FieldNamesAreStillCamelCase()
    {
        // Supplying options to the context constructor replaces the attribute's settings, so the
        // naming policy has to be restated. Losing it would rename every field in the contract.
        var names = JsonDocument.Parse(Render(Finding.Create(FindingCodes.DtdMissing, null, "x")))
            .RootElement.EnumerateObject().Select(property => property.Name);

        names.ShouldBe([
            "schemaVersion", "export", "verdict", "strict", "language", "contentsExamined",
            "counts", "findings",
        ]);
    }
}
