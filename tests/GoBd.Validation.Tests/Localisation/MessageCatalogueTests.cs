using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Tests.Localisation;

public sealed class MessageCatalogueTests
{
    [Fact]
    public void EveryCodeInTheCatalogueHasBothLanguages()
    {
        // A code with no rendering would throw at report time, so this is the guard that keeps
        // the finding catalogue and the message catalogue in step.
        var missing = FindingCodes.All
            .Select(info => info.Code)
            .Where(code => !MessageCatalogue.Contains(code))
            .ToArray();

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void TheCatalogueCarriesNoCodesThatDoNotExist() =>
        MessageCatalogue.Codes.ShouldAllBe(code => FindingCodes.IsKnown(code));

    [Fact]
    public void EveryCodeRendersInBothLanguagesWithoutThrowing()
    {
        // Arity mismatches between a check's arguments and its template surface as format
        // exceptions, so every template is exercised with a generous argument list.
        var arguments = Enumerable.Range(0, 8).Select(index => $"arg{index}").ToArray();

        foreach (var info in FindingCodes.All)
        {
            var finding = Finding.Create(info.Code, null, arguments);

            foreach (var language in (ReportLanguage[])[ReportLanguage.English, ReportLanguage.German])
            {
                var rendered = MessageCatalogue.Render(finding, language);
                rendered.ShouldNotBeNullOrWhiteSpace();
                rendered.ShouldNotContain("{0}", Case.Sensitive);
            }
        }
    }

    [Fact]
    public void EnglishAndGermanRenderingsDiffer()
    {
        var finding = Finding.Create(FindingCodes.ReferenceDangling, null, "Bestellungen", "Kunden");

        MessageCatalogue.Render(finding, ReportLanguage.English)
            .ShouldNotBe(MessageCatalogue.Render(finding, ReportLanguage.German));
    }

    [Fact]
    public void ArgumentsAreSubstitutedIntoBothLanguages()
    {
        var finding = Finding.Create(FindingCodes.ReferenceDangling, null, "Bestellungen", "Kunden");

        MessageCatalogue.Render(finding, ReportLanguage.English).ShouldContain("Bestellungen");
        MessageCatalogue.Render(finding, ReportLanguage.German).ShouldContain("Kunden");
    }

    [Fact]
    public void ValuesQuotedFromIndexXmlAreReproducedVerbatim()
    {
        // A declared decimal symbol, an accuracy and a date mask must appear exactly as the
        // document wrote them -- never reformatted according to any culture.
        var symbol = Finding.Create(FindingCodes.SymbolCollision, null, "Kunden", ",");
        var mask = Finding.Create(FindingCodes.DateMaskUnusable, null, "Kunden", "Bestelldatum", "DD.MM");

        foreach (var language in (ReportLanguage[])[ReportLanguage.English, ReportLanguage.German])
        {
            MessageCatalogue.Render(symbol, language).ShouldContain("','");
            MessageCatalogue.Render(mask, language).ShouldContain("DD.MM");
        }
    }

    [Fact]
    public void RenderingIsDeterministicForTheSameFinding()
    {
        var finding = Finding.Create(FindingCodes.FixedRangeGap, null, "Sales", "11", "20");

        MessageCatalogue.Render(finding, ReportLanguage.German)
            .ShouldBe(MessageCatalogue.Render(finding, ReportLanguage.German));
    }

    [Fact]
    public void RenderingDoesNotConsultTheAmbientEnvironment()
    {
        var finding = Finding.Create(FindingCodes.FixedRangeGap, null, "Sales", "11", "20");
        var before = MessageCatalogue.Render(finding, ReportLanguage.English);

        var original = Environment.GetEnvironmentVariable("LANG");
        try
        {
            Environment.SetEnvironmentVariable("LANG", "de_DE.UTF-8");
            MessageCatalogue.Render(finding, ReportLanguage.English).ShouldBe(before);
            Environment.SetEnvironmentVariable("LANG", "en_US.UTF-8");
            MessageCatalogue.Render(finding, ReportLanguage.English).ShouldBe(before);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LANG", original);
        }
    }
}

/// <summary>
/// Prose chosen by a check travels as a token so the catalogue can render it in the report's
/// language. Every other argument is a value copied from index.xml and must stay verbatim.
/// </summary>
public sealed class LocalisableArgumentTests
{
    private static Finding WithArtefacts(params string[] names) => Finding.Create(
        FindingCodes.DtdNormalisationDifference, null,
        "gdpdu-01-03-2019.dtd", MessageCatalogue.Token(names), "expected", "found");

    [Fact]
    public void ATokenRendersInTheReportsLanguage()
    {
        MessageCatalogue.Render(WithArtefacts("lineEndings"), ReportLanguage.English)
            .ShouldContain("line endings");
        MessageCatalogue.Render(WithArtefacts("lineEndings"), ReportLanguage.German)
            .ShouldContain("Zeilenenden");
    }

    [Fact]
    public void AGermanReportContainsNoEnglishPhrase() =>
        MessageCatalogue.Render(WithArtefacts("byteOrderMark", "trailingWhitespace"), ReportLanguage.German)
            .ShouldNotContain("byte-order mark");

    [Fact]
    public void SeveralTokensAreJoined() =>
        MessageCatalogue.Render(WithArtefacts("lineEndings", "byteOrderMark"), ReportLanguage.English)
            .ShouldContain("line endings, byte-order mark");

    [Fact]
    public void AnUnknownTokenRendersAsItselfRatherThanThrowing() =>
        // A wrong word is a far smaller failure than a report that cannot be produced.
        Should.NotThrow(() =>
            MessageCatalogue.Render(WithArtefacts("somethingNobodyDefined"), ReportLanguage.English)
                .ShouldContain("somethingNobodyDefined"));

    [Fact]
    public void OrdinaryArgumentsAreNeverTouched()
    {
        // A table really could be called "lineEndings"; without the marker it stays verbatim.
        var finding = Finding.Create(FindingCodes.ReferenceDangling, null, "lineEndings", "Kunden");

        MessageCatalogue.Render(finding, ReportLanguage.German).ShouldContain("'lineEndings'");
    }

    [Theory]
    [InlineData("@echo off")]
    [InlineData("@run.bat")]
    [InlineData("@leer")]
    [InlineData("@lineEndings.somethingElse")]
    public void ADocumentValueBeginningWithTheMarkerIsReproducedVerbatim(string declared)
    {
        // Regression: Localise once ran over every argument, so any value starting with '@' was
        // rewritten -- '@echo off' rendered as 'echo off' and '@run.bat' as 'run, bat'. For the
        // Command finding in particular that meant showing a reviewer altered text for the thing
        // the medium asks their machine to run.
        var finding = Finding.Create(FindingCodes.CommandDeclared, null, "DataSet", declared);

        foreach (var language in (ReportLanguage[])[ReportLanguage.English, ReportLanguage.German])
        {
            MessageCatalogue.Render(finding, language).ShouldContain(declared);
        }
    }

    [Fact]
    public void AColumnQualifiedSettingRendersAsAWellFormedSentence()
    {
        // Regression: the column was smuggled into its own optional placeholder and rendered
        // "Table 'Kunden'Betrag: ...".
        var finding = Finding.Create(FindingCodes.NumericSettingInvalid, null, "Kunden", "Betrag/MaxLength", "0");

        var english = MessageCatalogue.Render(finding, ReportLanguage.English);
        english.ShouldContain("Table 'Kunden': Betrag/MaxLength declares '0'");
        english.ShouldNotContain("'Kunden'Betrag");
        MessageCatalogue.Render(finding, ReportLanguage.German).ShouldContain("Tabelle 'Kunden': Betrag/MaxLength");
    }

    [Fact]
    public void EveryTokenTheDtdCheckCanEmitIsKnownToTheCatalogue() =>
        // Guards the seam between the check's vocabulary and the catalogue's.
        new[] { "lineEndings", "byteOrderMark", "trailingWhitespace", "encoding" }
            .ShouldAllBe(name =>
                MessageCatalogue.Render(WithArtefacts(name), ReportLanguage.German)
                    .Contains(name, StringComparison.Ordinal) == false);
}
