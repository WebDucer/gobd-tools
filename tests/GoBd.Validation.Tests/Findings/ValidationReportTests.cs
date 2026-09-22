using GoBd.Validation.Findings;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Findings;

public sealed class ValidationReportTests
{
    private static Finding Info() => Finding.Create(FindingCodes.DtdLegacySystemIdentifier, null);
    private static Finding Warning() => Finding.Create(FindingCodes.MediaWithoutTables, null, "Disk 1");
    private static Finding Error() => Finding.Create(FindingCodes.ReferenceDangling, null, "Bestellungen", "Kunden");

    [Fact]
    public void CleanRunIsConformant()
    {
        var report = new ValidationReport("export.zip", []);

        report.Verdict.ShouldBe(Verdict.Conformant);
        report.IsClean.ShouldBeTrue();
        report.ErrorCount.ShouldBe(0);
        report.WarningCount.ShouldBe(0);
    }

    [Fact]
    public void InfoFindingsDoNotMakeARunUnclean()
    {
        var report = new ValidationReport("export.zip", [Info()]);

        report.Verdict.ShouldBe(Verdict.Conformant);
        report.IsClean.ShouldBeTrue();
        report.InfoCount.ShouldBe(1);
    }

    [Fact]
    public void WarningsAloneAreStillConformant()
    {
        var report = new ValidationReport("export.zip", [Warning(), Warning()]);

        report.Verdict.ShouldBe(Verdict.Conformant);
        report.IsClean.ShouldBeFalse();
        report.WarningCount.ShouldBe(2);
    }

    [Fact]
    public void ErrorsMakeARunNonConformant()
    {
        var report = new ValidationReport("export.zip", [Info(), Warning(), Error()]);

        report.Verdict.ShouldBe(Verdict.NonConformant);
        report.InfoCount.ShouldBe(1);
        report.WarningCount.ShouldBe(1);
        report.ErrorCount.ShouldBe(1);
    }

    [Fact]
    public void StrictModeCountsWarningsAgainstTheVerdict() =>
        new ValidationReport("export.zip", [Warning()], strict: true)
            .Verdict.ShouldBe(Verdict.NonConformant);

    [Fact]
    public void StrictModeLeavesTheFindingsOwnSeverityUntouched()
    {
        var report = new ValidationReport("export.zip", [Warning()], strict: true);

        // The verdict changes; the finding is still reported as the warning it is.
        report.Findings.ShouldHaveSingleItem().Severity.ShouldBe(Severity.Warning);
        report.WarningCount.ShouldBe(1);
        report.ErrorCount.ShouldBe(0);
    }

    [Fact]
    public void StrictModeDoesNotDisturbACleanRun() =>
        new ValidationReport("export.zip", [Info()], strict: true)
            .Verdict.ShouldBe(Verdict.Conformant);

    [Fact]
    public void WithStrictReevaluatesTheSameFindings()
    {
        var lenient = new ValidationReport("export.zip", [Warning()]);
        var strict = lenient.WithStrict(true);

        lenient.Verdict.ShouldBe(Verdict.Conformant);
        strict.Verdict.ShouldBe(Verdict.NonConformant);
        strict.Findings.ShouldBe(lenient.Findings);
        lenient.WithStrict(false).ShouldBeSameAs(lenient);
    }

    [Fact]
    public void FindingsAreKeptInProductionOrder() =>
        new ValidationReport("export.zip", [Error(), Info(), Warning()])
            .Findings.Select(finding => finding.Code)
            .ShouldBe([FindingCodes.ReferenceDangling, FindingCodes.DtdLegacySystemIdentifier, FindingCodes.MediaWithoutTables]);

    [Fact]
    public void OfSeverityFiltersWithoutReordering()
    {
        var report = new ValidationReport("export.zip", [Error(), Warning(), Error()]);

        report.OfSeverity(Severity.Error).Count().ShouldBe(2);
        report.OfSeverity(Severity.Warning).ShouldHaveSingleItem();
        report.OfSeverity(Severity.Info).ShouldBeEmpty();
    }
}
