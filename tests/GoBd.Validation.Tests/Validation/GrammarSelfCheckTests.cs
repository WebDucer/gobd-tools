using GoBd.Validation;
using GoBd.Validation.Cli;
using GoBd.Validation.Cli.Reporting;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;
using GoBd.Validation.Tests.Fixtures;
using CanonicalDtd = GoBd.Validation.Dtd.CanonicalDtd;

namespace GoBd.Validation.Tests.Validation;

/// <summary>
/// The validator refuses to judge an export when its own grammar is not the canonical one.
/// </summary>
public sealed class GrammarSelfCheckTests
{
    private static ValidationReport ValidateWithBrokenGrammar(string exportPath) =>
        ExportValidator.Validate(exportPath, strict: false, additionalFindings: null, grammarIsCanonical: false);

    [Fact]
    public void AWrongGrammarProducesTheSelfCheckFindingAndNothingElse()
    {
        var report = ValidateWithBrokenGrammar(FixtureLibrary.FolderPath(FixtureLibrary.BrokenExport));

        // The export is genuinely defective, yet not one of its defects is reported: a verdict
        // reached with the wrong grammar would be worthless, so none is offered.
        report.Findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.EmbeddedGrammarNotCanonical);
    }

    [Fact]
    public void TheFindingNamesTheExpectedAndObservedFingerprints()
    {
        var finding = ValidateWithBrokenGrammar(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport))
            .Findings.ShouldHaveSingleItem();

        finding.Arguments.ShouldBe([CanonicalDtd.ExpectedSha256, CanonicalDtd.Sha256]);
        MessageCatalogue.Render(finding, ReportLanguage.English)
            .ShouldContain(CanonicalDtd.ExpectedSha256);
    }

    [Fact]
    public void ItIsAToolFailureNotAConformanceVerdict() =>
        // GOBD9xxx: "the validator could not run", never "your export is bad".
        FindingCodes.Describe(FindingCodes.EmbeddedGrammarNotCanonical)
            .Category.ShouldBe(FindingCategory.Tool);

    [Fact]
    public void ItExitsWithTheToolFailureCodeNotTheErrorCode()
    {
        var report = ValidateWithBrokenGrammar(FixtureLibrary.FolderPath(FixtureLibrary.BrokenExport));

        // 3, not 2 -- a pipeline must not be told the export is bad when the tool is.
        ExitCodes.For(report).ShouldBe(ExitCode.ToolFailure);
    }

    [Fact]
    public void StrictModeDoesNotChangeTheOutcome() =>
        ExitCodes.For(ExportValidator.Validate(
            FixtureLibrary.FolderPath(FixtureLibrary.GoodExport), strict: true,
            additionalFindings: null, grammarIsCanonical: false))
            .ShouldBe(ExitCode.ToolFailure);

    [Fact]
    public void TheRenderedReportSaysNoValidationWasPerformed() =>
        MessageCatalogue.Render(
            ValidateWithBrokenGrammar(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport)).Findings[0],
            ReportLanguage.English)
            .ShouldContain("validation was not performed");

    [Fact]
    public void TheRealGrammarPassesSoNormalRunsAreUnaffected() =>
        ExitCodes.For(ExportValidator.Validate(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport)))
            .ShouldBe(ExitCode.Conformant);
}
