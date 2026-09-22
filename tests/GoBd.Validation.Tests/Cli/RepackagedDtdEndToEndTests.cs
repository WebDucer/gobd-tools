using System.Text.Json;
using GoBd.Validation.Cli;
using GoBd.Validation.Dtd;
using GoBd.Validation.Findings;
using GoBd.Validation.Tests.Fixtures;

namespace GoBd.Validation.Tests.Cli;

/// <summary>
/// The whole tool over the case that started this change: a conformant export whose DTD was
/// re-encoded in transit.
/// </summary>
public sealed class RepackagedDtdEndToEndTests
{
    private static string Export => FixtureLibrary.FolderPath(FixtureLibrary.RepackagedExport);

    [Fact]
    public void TheReportNamesLineEndingsAndBothFingerprints()
    {
        var output = CliHarness.Run(Export, "--language", "en").Output;

        output.ShouldContain("GOBD2004");
        output.ShouldContain("line endings");
        output.ShouldContain(CanonicalDtd.ExpectedSha256);
        // The observed hash must differ, or the finding would not have fired.
        output.ShouldNotContain("byte-order mark");
    }

    [Fact]
    public void ItRemainsAWarningAndStillExitsOne()
    {
        var result = CliHarness.Run(Export, "--language", "en");

        result.ExitCode.ShouldBe((int)ExitCode.Warnings);
        result.Output.ShouldContain("CONFORMANT");
    }

    [Fact]
    public void TheJsonReportIsReadableAndCarriesBothFingerprints()
    {
        var raw = CliHarness.Run(Export, "--format", "json", "--language", "en").Output;

        raw.ShouldNotContain("\\u0027");
        var message = JsonDocument.Parse(raw).RootElement.GetProperty("findings").EnumerateArray()
            .Single(finding => finding.GetProperty("code").GetString() == FindingCodes.DtdNormalisationDifference)
            .GetProperty("message").GetString().ShouldNotBeNull();

        message.ShouldContain("'gdpdu-01-03-2019.dtd'");
        message.ShouldContain(CanonicalDtd.ExpectedSha256);
    }

    [Fact]
    public void TheSameExportAsAZipBehavesIdentically()
    {
        var zip = FixtureLibrary.ZipPath(FixtureLibrary.RepackagedExport);
        try
        {
            var fromZip = CliHarness.Run(zip, "--format", "json", "--language", "en");
            var fromFolder = CliHarness.Run(Export, "--format", "json", "--language", "en");

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
    public void GermanReportIsEquallyReadable()
    {
        var raw = CliHarness.Run(Export, "--format", "json", "--language", "de").Output;

        raw.ShouldContain("Zeilenenden");
        raw.ShouldNotContain("line endings");
        raw.ShouldNotContain("\\u00");
    }
}
