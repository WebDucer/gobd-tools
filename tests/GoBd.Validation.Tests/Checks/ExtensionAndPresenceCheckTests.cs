using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Fixtures;
using GoBd.Validation.Tests.Parsing;
using GoBd.Validation.Tests.Sources;

namespace GoBd.Validation.Tests.Checks;

public sealed class ExtensionCheckTests
{
    private static IReadOnlyList<Finding> Run(string url)
    {
        var dataSet = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document($"""
              <Extension><Name>SmartExporter</Name><URL>{url}</URL></Extension>
              <Version>1.0</Version>
              <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
            """))).DataSet!;

        using var source = new FolderExportSource(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport));
        return CheckEngine.Run([new ExtensionCheck()], new CheckContext(dataSet, source));
    }

    [Fact]
    public void ResolvingExtensionIsMerelyNoted()
    {
        var finding = Run("kunden.csv").ShouldHaveSingleItem();

        // An extension's meaning is outside this standard, so its presence is an observation.
        finding.Code.ShouldBe(FindingCodes.ExtensionDeclared);
        finding.Severity.ShouldBe(Severity.Info);
        finding.Arguments.ShouldContain("SmartExporter");
    }

    [Fact]
    public void ExtensionNamingAnAbsentFileIsAnError() =>
        Run("nirgends.xml").Select(finding => finding.Code)
            .ShouldBe([FindingCodes.ExtensionDeclared, FindingCodes.ExtensionFileMissing]);

    [Fact]
    public void ExtensionWithAnAbsoluteUrlIsAnError() =>
        Run("http://example.invalid/ext.xml").Select(finding => finding.Code)
            .ShouldContain(FindingCodes.ExtensionFileMissing);
}

public sealed class TableFilePresenceCheckTests
{
    private static IReadOnlyList<Finding> Run(string url, IExportSource source)
    {
        var dataSet = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document($"""
              <Version>1.0</Version>
              <Media>
                <Name>D</Name>
                <Table>
                  <URL>{url}</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
              </Media>
            """))).DataSet!;

        return CheckEngine.Run([new TableFilePresenceCheck()], new CheckContext(dataSet, source));
    }

    private static IReadOnlyList<Finding> RunOnFixture(string url)
    {
        using var source = new FolderExportSource(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport));
        return Run(url, source);
    }

    [Fact]
    public void PresentFileRaisesNothing() => RunOnFixture("kunden.csv").ShouldBeEmpty();

    [Fact]
    public void NestedPresentFileRaisesNothing() => RunOnFixture("data/bestellungen.csv").ShouldBeEmpty();

    [Fact]
    public void AbsentFileIsReported() =>
        RunOnFixture("nirgends.csv").ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.TableFileMissing);

    [Fact]
    public void AbsoluteUrlIsReportedAsSuch() =>
        RunOnFixture("file:///kunden.csv").ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.UrlNotRelative);

    [Fact]
    public void TraversalOutsideTheRootIsReportedAsSuch() =>
        RunOnFixture("../kunden.csv").ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.UrlEscapesRoot);

    [Fact]
    public void CaseOnlyMismatchNamesTheNearMiss()
    {
        var finding = RunOnFixture("Kunden.csv").ShouldHaveSingleItem();

        // An export that resolves only on a case-insensitive filesystem is defective, and
        // naming the near-miss turns "not found" into something actionable.
        finding.Code.ShouldBe(FindingCodes.EntryCaseMismatch);
        finding.Arguments.ShouldContain("kunden.csv");
    }

    [Fact]
    public void PresenceIsDecidedFromTheListingWithoutReadingTheFile()
    {
        using var counting = new CountingExportSource(
            new FolderExportSource(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport)));

        Run("kunden.csv", counting).ShouldBeEmpty();

        counting.Opened.ShouldBeEmpty();
    }
}
