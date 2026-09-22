using System.Text.Json;
using GoBd.Validation.Cli;
using GoBd.Validation.Findings;
using GoBd.Validation.Tests.Fixtures;

namespace GoBd.Validation.Tests.Cli;

/// <summary>
/// Drives the whole tool over a deliberately defective export, from the command line down.
/// </summary>
public sealed class EndToEndTests
{
    /// <summary>Exactly what the broken fixture is built to provoke — no more, no less.</summary>
    private static readonly string[] Expected =
    [
        FindingCodes.TableFileMissing,            // bestellungen.csv is not in the export
        FindingCodes.DtdAltered,                  // the shipped grammar was edited
        FindingCodes.ForeignKeyColumnUndeclared,  // KundenCode is never declared
        FindingCodes.ReferenceDangling,           // no table called Nirgends
    ];

    private static string Broken => FixtureLibrary.FolderPath(FixtureLibrary.BrokenExport);

    [Fact]
    public void BrokenExportProducesExactlyTheExpectedFindings()
    {
        var result = CliHarness.Run(Broken, "--format", "json", "--language", "en");

        var codes = JsonDocument.Parse(result.Output).RootElement
            .GetProperty("findings").EnumerateArray()
            .Select(finding => finding.GetProperty("code").GetString()!)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        codes.ShouldBe([.. Expected.OrderBy(code => code, StringComparer.Ordinal)]);
    }

    [Fact]
    public void BrokenExportExitsWithTheErrorCode() =>
        CliHarness.Run(Broken, "--language", "en").ExitCode.ShouldBe((int)ExitCode.Errors);

    [Fact]
    public void BrokenExportIsReportedAsNonConformant() =>
        CliHarness.Run(Broken, "--language", "en").Output.ShouldContain("NOT CONFORMANT");

    [Fact]
    public void EditedGrammarDoesNotStopTheOtherChecksRunning()
    {
        // Validation used the canonical embedded grammar, so an edited DTD in the export is
        // reported without being allowed to influence anything else. See design.md D1.
        var output = CliHarness.Run(Broken, "--language", "en").Output;

        output.ShouldContain(FindingCodes.DtdAltered);
        output.ShouldContain(FindingCodes.ReferenceDangling);
        output.ShouldNotContain(FindingCodes.GrammarViolation);
    }

    [Fact]
    public void BrokenExportBehavesIdenticallyAsAZipArchive()
    {
        var zip = FixtureLibrary.ZipPath(FixtureLibrary.BrokenExport);
        try
        {
            var fromZip = CliHarness.Run(zip, "--format", "json", "--language", "en");
            var fromFolder = CliHarness.Run(Broken, "--format", "json", "--language", "en");

            static string[] Codes(string json) =>
                [.. JsonDocument.Parse(json).RootElement.GetProperty("findings").EnumerateArray()
                    .Select(finding => finding.GetProperty("code").GetString()!)
                    .OrderBy(code => code, StringComparer.Ordinal)];

            fromZip.ExitCode.ShouldBe(fromFolder.ExitCode);
            Codes(fromZip.Output).ShouldBe(Codes(fromFolder.Output));
        }
        finally
        {
            File.Delete(zip);
        }
    }

    [Fact]
    public void GoodExportRemainsClean() =>
        CliHarness.Run(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport), "--language", "en")
            .ExitCode.ShouldBe((int)ExitCode.Conformant);

    /// <summary>
    /// `gobd-validate export/` is what shell completion produces, and it used to refuse every
    /// entry of the export rather than validate it.
    /// </summary>
    [Fact]
    public void AFolderNamedWithATrailingSeparatorValidatesIdentically()
    {
        var folder = FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);
        var withSeparator = CliHarness.Run(
            folder + Path.DirectorySeparatorChar, "--format", "json", "--language", "en");
        var withoutSeparator = CliHarness.Run(folder, "--format", "json", "--language", "en");

        static string[] Codes(string json) =>
            [.. JsonDocument.Parse(json).RootElement.GetProperty("findings").EnumerateArray()
                .Select(finding => finding.GetProperty("code").GetString()!)
                .OrderBy(code => code, StringComparer.Ordinal)];

        withSeparator.ExitCode.ShouldBe((int)ExitCode.Conformant);
        Codes(withSeparator.Output).ShouldBe(Codes(withoutSeparator.Output));
    }

    [Fact]
    public void ABrokenFolderNamedWithATrailingSeparatorFindsTheSameDefects()
    {
        var withSeparator = CliHarness.Run(
            Broken + Path.DirectorySeparatorChar, "--format", "json", "--language", "en");

        var codes = JsonDocument.Parse(withSeparator.Output).RootElement
            .GetProperty("findings").EnumerateArray()
            .Select(finding => finding.GetProperty("code").GetString()!)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        codes.ShouldBe([.. Expected.OrderBy(code => code, StringComparer.Ordinal)]);
    }
}
