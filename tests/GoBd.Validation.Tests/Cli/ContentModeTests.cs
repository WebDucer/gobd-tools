using System.Text.Json;
using GoBd.Validation.Checks;
using GoBd.Validation.Cli;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Parsing;
using GoBd.Validation.Tests.Sources;

namespace GoBd.Validation.Tests.Cli;

/// <summary>
/// The content mode as a caller meets it: opt in, and the report changes what it claims to have
/// checked.
/// </summary>
public sealed class ContentModeTests : IDisposable
{
    private readonly string export;

    public ContentModeTests() =>
        export = CliHarness.CreateExport(
            Declaration,
            includeDtd: true,
            ("kunden.csv", "K1;Meier\r\nK2;Lang\r\n"),
            ("bestellungen.csv", "B1;K1\r\nB2;K9\r\n"));

    public void Dispose() => Directory.Delete(export, recursive: true);

    private static string Declaration => IndexXml.Document("""
          <Version>1.0</Version>
          <Media>
            <Name>Disk 1</Name>
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
            <Table>
              <URL>bestellungen.csv</URL>
              <Name>Bestellungen</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
              </VariableLength>
            </Table>
          </Media>
        """);

    // ---- 6.1 opting in -----------------------------------------------------------------------

    [Fact]
    public void WithoutTheOptionNoDataFileIsOpened()
    {
        var opened = OpenedEntries(CheckRegistry.Tier0);

        opened.ShouldNotContain("kunden.csv");
        opened.ShouldNotContain("bestellungen.csv");
    }

    [Fact]
    public void WithTheOptionTheDataFilesAreOpened()
    {
        var opened = OpenedEntries(CheckRegistry.Content);

        opened.ShouldContain("kunden.csv");
        opened.ShouldContain("bestellungen.csv");
    }

    private IReadOnlyList<string> OpenedEntries(IEnumerable<ICheck> checks)
    {
        var opened = ExportSourceFactory.Open(export);
        using var counting = new CountingExportSource(opened.Source!);
        using (var index = counting.OpenRead(ExportSourceFactory.IndexFileName))
        {
            var dataSet = IndexXmlParser.Parse(index).DataSet!;
            CheckEngine.Run(checks, new CheckContext(dataSet, counting, ContentOptions.Default));
        }

        return counting.Opened;
    }

    [Fact]
    public void WithoutTheOptionTheDanglingReferenceGoesUnnoticed() =>
        // The declaration is coherent; only the data is wrong. That is exactly the gap the
        // content mode fills, and the reason a clean default run promises so little.
        CliHarness.Run(export).ExitCode.ShouldBe((int)ExitCode.Conformant);

    // ---- 6.2 the verdict ---------------------------------------------------------------------

    [Fact]
    public void AContentErrorExitsAsAStructuralErrorDoes()
    {
        var result = CliHarness.Run(export, "--contents", "-l", "en");

        result.ExitCode.ShouldBe((int)ExitCode.Errors);
        result.Output.ShouldContain(FindingCodes.ForeignKeyValueUnresolved);
    }

    [Fact]
    public void StrictModeStillPromotesWarningsWithContentsOn()
    {
        // A reference to a table that cannot be read is a warning. Under --strict it must count
        // against the verdict exactly as a structural warning does.
        var unreadable = CliHarness.CreateExport(
            Declaration, includeDtd: true, ("bestellungen.csv", "B1;K1\r\n"));
        try
        {
            CliHarness.Run(unreadable, "--contents").ExitCode.ShouldBe((int)ExitCode.Errors);
            CliHarness.Run(unreadable, "--contents", "--strict").ExitCode.ShouldBe((int)ExitCode.Errors);
        }
        finally
        {
            Directory.Delete(unreadable, recursive: true);
        }
    }

    // ---- 6.3 the bound -----------------------------------------------------------------------

    [Fact]
    public void TheBoundDefaultsToFiftyWithoutBeingAskedFor() =>
        CommandLine.Parse([export, "--contents"]).Options!.Contents!.MaximumFindingsPerTable
            .ShouldBe(ContentOptions.DefaultMaximumFindingsPerTable);

    [Fact]
    public void AnExplicitBoundIsHonoured() =>
        CommandLine.Parse([export, "--contents", "--max-findings", "7"]).Options!.Contents!
            .MaximumFindingsPerTable.ShouldBe(7);

    [Fact]
    public void WithoutTheContentOptionThereIsNothingToBound() =>
        CommandLine.Parse([export, "--max-findings", "7"]).Options!.Contents.ShouldBeNull();

    [Fact]
    public void ABoundThatIsNotAUsableNumberIsRefused()
    {
        CommandLine.Parse([export, "--max-findings", "zero"]).Error.ShouldNotBeNull();
        CommandLine.Parse([export, "--max-findings", "0"]).Error.ShouldNotBeNull();
        CommandLine.Parse([export, "--max-findings"]).Error.ShouldNotBeNull();
    }

    // ---- 6.5 the report says what it checked -------------------------------------------------

    // The language is pinned on every test that asserts wording: another test in this suite sets
    // the locale environment variables, and tests run in parallel.
    [Fact]
    public void TheTextReportStatesThatContentsWereNotExamined() =>
        CliHarness.Run(export, "-l", "en").Output.ShouldContain("NOT examined");

    [Fact]
    public void TheTextReportStatesThatContentsWereExamined()
    {
        var output = CliHarness.Run(export, "--contents", "-l", "en").Output;

        output.ShouldContain("contents of the data files were checked");
        output.ShouldNotContain("NOT examined");
    }

    [Fact]
    public void TheJsonReportCarriesWhetherContentsWereExamined()
    {
        Examined(CliHarness.Run(export, "--format", "json", "-l", "en").Output).ShouldBeFalse();
        Examined(CliHarness.Run(export, "--contents", "--format", "json", "-l", "en").Output).ShouldBeTrue();
    }

    private static bool Examined(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("contentsExamined").GetBoolean();
    }

    [Fact]
    public void TheGermanReportSaysItInGerman()
    {
        CliHarness.Run(export, "--contents", "-l", "de").Output.ShouldContain("Inhalte der Datendateien wurden geprüft");
        CliHarness.Run(export, "-l", "de").Output.ShouldContain("NICHT untersucht");
    }
}
