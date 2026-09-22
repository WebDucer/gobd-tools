using GoBd.Validation.Checks;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Fixtures;
using GoBd.Validation.Tests.Parsing;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Checks;

public sealed class CheckEngineTests : IDisposable
{
    private readonly IExportSource source = new FolderExportSource(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport));

    private CheckContext Context()
    {
        var dataSet = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Minimal())).DataSet!;
        return new CheckContext(dataSet, source);
    }

    private sealed class StubCheck(string code, int count) : ICheck
    {
        public bool Ran { get; private set; }

        public IReadOnlyList<string> Codes => [code];

        public IEnumerable<Finding> Run(CheckContext context)
        {
            Ran = true;
            return Enumerable.Range(0, count).Select(_ => Finding.Create(code, null, "x"));
        }
    }

    private sealed class ThrowingCheck : ICheck
    {
        public IReadOnlyList<string> Codes => [FindingCodes.ReferenceDangling];

        public IEnumerable<Finding> Run(CheckContext context) =>
            throw new InvalidOperationException("check is broken");
    }

    [Fact]
    public void EveryCheckRunsAndFindingsAreConcatenated()
    {
        var first = new StubCheck(FindingCodes.ReferenceDangling, 2);
        var second = new StubCheck(FindingCodes.MediaWithoutTables, 1);

        var findings = CheckEngine.Run([first, second], Context());

        first.Ran.ShouldBeTrue();
        second.Ran.ShouldBeTrue();
        findings.Select(finding => finding.Code)
            .ShouldBe([FindingCodes.ReferenceDangling, FindingCodes.ReferenceDangling, FindingCodes.MediaWithoutTables]);
    }

    [Fact]
    public void ABrokenCheckDoesNotAbortTheOthers()
    {
        var before = new StubCheck(FindingCodes.ReferenceDangling, 1);
        var after = new StubCheck(FindingCodes.MediaWithoutTables, 1);

        var findings = CheckEngine.Run([before, new ThrowingCheck(), after], Context());

        // One broken check must not cost the producer every other finding in the report.
        before.Ran.ShouldBeTrue();
        after.Ran.ShouldBeTrue();
        findings.Select(finding => finding.Code).ShouldBe(
            [FindingCodes.ReferenceDangling, FindingCodes.CheckFailed, FindingCodes.MediaWithoutTables]);
    }

    [Fact]
    public void ABrokenCheckIsReportedAsAToolFailureNotAConformanceProblem()
    {
        var finding = CheckEngine.Run([new ThrowingCheck()], Context()).ShouldHaveSingleItem();

        // GOBD9xxx: "the validator misbehaved", never "your export is bad".
        FindingCodes.Describe(finding.Code).Category.ShouldBe(FindingCategory.Tool);
        finding.Arguments.ShouldContain(nameof(ThrowingCheck));
    }

    [Fact]
    public void NoChecksYieldNoFindings() => CheckEngine.Run([], Context()).ShouldBeEmpty();

    public void Dispose() => source.Dispose();
}

public sealed class CheckRegistryTests
{
    [Fact]
    public void TheRegistryIsNotEmpty() => CheckRegistry.Tier0.ShouldNotBeEmpty();

    [Fact]
    public void EveryRegisteredCheckDeclaresCodesFromTheCatalogue() =>
        CheckRegistry.Tier0
            .SelectMany(check => check.Codes)
            .ShouldAllBe(code => FindingCodes.IsKnown(code));

    [Fact]
    public void NoTwoChecksClaimTheSameCode()
    {
        // Codes are the interface downstream pipelines suppress by, so ownership must be clear.
        var duplicates = CheckRegistry.Tier0
            .SelectMany(check => check.Codes)
            .GroupBy(code => code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        duplicates.ShouldBeEmpty();
    }
}
