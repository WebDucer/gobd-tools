using System.Text.Json;
using GoBd.Validation.Cli;
using GoBd.Validation.Localisation;
using GoBd.Validation.Tests.Fixtures;
using GoBd.Validation.Tests.Parsing;

namespace GoBd.Validation.Tests.Cli;

public sealed class CliRunnerTests
{
    private static string GoodFolder => FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);

    // ---- Task 11.1: the command surface -----------------------------------------------------

    [Fact]
    public void ValidatesAFolderExport()
    {
        var result = CliHarness.Run(GoodFolder, "--language", "en");

        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
        result.Output.ShouldContain("CONFORMANT");
    }

    [Fact]
    public void ValidatesAZipExport()
    {
        var zip = FixtureLibrary.ZipPath(FixtureLibrary.GoodExport);
        try
        {
            var result = CliHarness.Run(zip, "--language", "en");

            result.ExitCode.ShouldBe((int)ExitCode.Conformant);
            result.Output.ShouldContain("CONFORMANT");
        }
        finally
        {
            File.Delete(zip);
        }
    }

    [Fact]
    public void TextIsTheDefaultFormat() =>
        CliHarness.Run(GoodFolder, "--language", "en").Output
            .ShouldStartWith("GoBD export validation");

    [Fact]
    public void JsonFormatIsSelectable()
    {
        var result = CliHarness.Run(GoodFolder, "--format", "json", "--language", "en");

        // Nothing may be interleaved, or the stream cannot be parsed directly.
        Should.NotThrow(() => JsonDocument.Parse(result.Output));
        JsonDocument.Parse(result.Output).RootElement.GetProperty("verdict").GetString().ShouldBe("conformant");
    }

    [Fact]
    public void UnknownFormatIsRejected()
    {
        var result = CliHarness.Run(GoodFolder, "--format", "xml");

        result.ExitCode.ShouldBe((int)ExitCode.ToolFailure);
        result.Error.ShouldContain("Unknown report format");
    }

    [Fact]
    public void ReportCanBeWrittenToAFileLeavingStandardOutputForASummary()
    {
        var destination = Path.Combine(Path.GetTempPath(), $"gobd-report-{Guid.NewGuid():N}.json");
        try
        {
            var result = CliHarness.Run(GoodFolder, "--format", "json", "--output", destination, "--language", "en");

            result.ExitCode.ShouldBe((int)ExitCode.Conformant);
            File.Exists(destination).ShouldBeTrue();
            Should.NotThrow(() => JsonDocument.Parse(File.ReadAllText(destination)));

            // Standard output carries a short human summary, not the report.
            result.Output.ShouldContain("Report written to");
            result.Output.ShouldNotContain("\"schemaVersion\"");
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public void NoArgumentsShowsUsageAndFails()
    {
        var result = CliHarness.Run();

        result.ExitCode.ShouldBe((int)ExitCode.ToolFailure);
        result.Output.ShouldContain("Usage:");
    }

    [Fact]
    public void TwoExportPathsAreRejected() =>
        CliHarness.Run("one", "two").Error.ShouldContain("Only one export path");

    // ---- Task 11.2: exit codes --------------------------------------------------------------

    [Fact]
    public void ConformantExportExitsZero() =>
        CliHarness.Run(GoodFolder, "--language", "en").ExitCode.ShouldBe(0);

    [Fact]
    public void WarningsOnlyExitOne()
    {
        // A medium with no tables and no AcceptNoTables is a warning and nothing worse.
        var export = CliHarness.CreateExport(IndexXml.Document("""
              <Version>1.0</Version>
              <Media><Name>Disk 1</Name></Media>
            """));
        try
        {
            var result = CliHarness.Run(export, "--language", "en");

            result.ExitCode.ShouldBe((int)ExitCode.Warnings);
            result.Output.ShouldContain("CONFORMANT");
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }

    [Fact]
    public void StrictModePromotesWarningsToErrors()
    {
        var export = CliHarness.CreateExport(IndexXml.Document("""
              <Version>1.0</Version>
              <Media><Name>Disk 1</Name></Media>
            """));
        try
        {
            var result = CliHarness.Run(export, "--strict", "--language", "en");

            result.ExitCode.ShouldBe((int)ExitCode.Errors);
            result.Output.ShouldContain("NOT CONFORMANT");
            result.Output.ShouldContain("Strict mode");
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }

    [Fact]
    public void ErrorsExitTwo()
    {
        var export = CliHarness.CreateExport(IndexXml.Document("""
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Id</Name><References>Nirgends</References></ForeignKey>
                  </VariableLength>
                </Table>
              </Media>
            """), includeDtd: true, ("bestellungen.csv", "1\n"));
        try
        {
            var result = CliHarness.Run(export, "--language", "en");

            result.ExitCode.ShouldBe((int)ExitCode.Errors);
            result.Output.ShouldContain("GOBD3002");
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }

    [Fact]
    public void MissingPathIsAToolFailureNotAConformanceVerdict()
    {
        var result = CliHarness.Run(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}"), "--language", "en");

        // A pipeline must be able to tell "your export is bad" from "the validator could not run".
        result.ExitCode.ShouldBe((int)ExitCode.ToolFailure);
        result.Output.ShouldContain("GOBD9001");
    }

    // ---- Task 11.3: language --------------------------------------------------------------

    [Fact]
    public void GermanIsProducedOnRequest() =>
        CliHarness.Run(GoodFolder, "--language", "de").Output.ShouldContain("GoBD-Exportprüfung: KONFORM");

    [Fact]
    public void ExplicitOptionOverridesTheEnvironmentVariable() =>
        CliHarness.RunWithEnvironment(
            new Dictionary<string, string?> { ["GOBD_LANG"] = "de" },
            GoodFolder, "--language", "en")
            .Output.ShouldContain("GoBD export validation");

    [Fact]
    public void EnvironmentVariableIsUsedWhenNoOptionIsGiven() =>
        CliHarness.RunWithEnvironment(
            new Dictionary<string, string?> { ["GOBD_LANG"] = "de" }, GoodFolder)
            .Output.ShouldContain("GoBD-Exportprüfung");

    [Fact]
    public void OperatingSystemLanguageIsUsedWhenNothingElseIsSet()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Windows resolves through NLS, not the POSIX variables.
        }

        CliHarness.RunWithEnvironment(
            new Dictionary<string, string?> { ["GOBD_LANG"] = null, ["LC_ALL"] = "de_DE.UTF-8" }, GoodFolder)
            .Output.ShouldContain("GoBD-Exportprüfung");
    }

    [Fact]
    public void UnsupportedLanguageFallsBackToEnglishAndSaysSo()
    {
        var result = CliHarness.Run(GoodFolder, "--language", "fr");

        result.Output.ShouldContain("GoBD export validation");
        result.Output.ShouldContain("GOBD9003");
        // An unavailable language must not cost the caller their report.
        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
    }

    // ---- Task 11.4: help --------------------------------------------------------------------

    [Fact]
    public void HelpStatesWhatIsAndIsNotChecked()
    {
        var result = CliHarness.Run("--help");

        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
        result.Output.ShouldContain("Without --contents");
        result.Output.ShouldContain("contents of the data files are NOT examined");
        result.Output.ShouldContain("With --contents");
    }

    [Fact]
    public void HelpStatesTheBoundAndItsDefault()
    {
        var output = CliHarness.Run("--help").Output;

        output.ShouldContain("--max-findings");
        output.ShouldContain("Default: 50");
    }

    [Fact]
    public void HelpMakesTheKeyCheckCeilingDiscoverable() =>
        // A limit nobody can find is a limit that surprises somebody in a pipeline.
        CliHarness.Run("--help").Output.ShouldContain("64 million records");

    [Fact]
    public void HelpDocumentsTheExitCodes()
    {
        var output = CliHarness.Run("--help").Output;

        output.ShouldContain("0  conformant");
        output.ShouldContain("3  the validator could not run");
    }
}
