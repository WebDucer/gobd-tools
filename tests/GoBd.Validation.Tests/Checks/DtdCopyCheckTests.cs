using System.Text;
using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Cli;
using GoBd.Validation.Dtd;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Cli;
using GoBd.Validation.Tests.Parsing;

namespace GoBd.Validation.Tests.Checks;

public sealed class DtdCopyCheckTests
{
    private static Finding RunAgainst(byte[] dtd)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gobd-dtdcheck-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "index.xml"), IndexXml.Document("""
                  <Version>1.0</Version>
                  <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
                """));
            File.WriteAllBytes(Path.Combine(directory, CanonicalDtd.FileName), dtd);

            var dataSet = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
                  <Version>1.0</Version>
                  <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
                """))).DataSet!;

            using var source = new FolderExportSource(directory);
            return CheckEngine.Run([new DtdCopyCheck()], new CheckContext(dataSet, source))
                .ShouldHaveSingleItem();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] AsLf() => Encoding.ASCII.GetBytes(
        Encoding.ASCII.GetString(CanonicalDtd.Bytes).Replace("\r\n", "\n", StringComparison.Ordinal));

    [Fact]
    public void ANormalisationDifferenceNamesBothFingerprints()
    {
        var finding = RunAgainst(AsLf());
        var message = MessageCatalogue.Render(finding, ReportLanguage.English);

        finding.Code.ShouldBe(FindingCodes.DtdNormalisationDifference);
        // Without both, a reader cannot tell a re-encoded export from a defective validator.
        message.ShouldContain(CanonicalDtd.ExpectedSha256);
        message.ShouldContain(finding.Arguments[3]);
        finding.Arguments[3].ShouldNotBe(CanonicalDtd.ExpectedSha256);
    }

    [Fact]
    public void ANormalisationDifferenceNamesTheArtefactNotAllThree()
    {
        var message = MessageCatalogue.Render(RunAgainst(AsLf()), ReportLanguage.English);

        message.ShouldContain("line endings");
        message.ShouldNotContain("byte-order mark");
        message.ShouldNotContain("trailing whitespace");
    }

    [Fact]
    public void AnAlteredDtdNamesBothFingerprints()
    {
        var edited = Encoding.ASCII.GetBytes(
            Encoding.ASCII.GetString(CanonicalDtd.Bytes)
                .Replace("Table*, Command*", "Table+, Command*", StringComparison.Ordinal));

        var finding = RunAgainst(edited);
        var message = MessageCatalogue.Render(finding, ReportLanguage.English);

        finding.Code.ShouldBe(FindingCodes.DtdAltered);
        message.ShouldContain(CanonicalDtd.ExpectedSha256);
        message.ShouldContain(finding.Arguments[2]);
    }

    [Fact]
    public void SeveritiesAreUnchanged()
    {
        // Deliberately out of scope for this change: a re-encoded DTD is a real defect in the
        // export, because the standard requires the shipped copy to be byte-identical.
        FindingCodes.SeverityOf(FindingCodes.DtdNormalisationDifference).ShouldBe(Severity.Warning);
        FindingCodes.SeverityOf(FindingCodes.DtdAltered).ShouldBe(Severity.Error);
    }

    [Fact]
    public void ARepackagedExportStillExitsOne()
    {
        var export = CliHarness.CreateExport(IndexXml.Document("""
              <Version>1.0</Version>
              <Media><Name>Disk 1</Name><AcceptNoTables>true</AcceptNoTables></Media>
            """), includeDtd: false);
        try
        {
            File.WriteAllBytes(Path.Combine(export, CanonicalDtd.FileName), AsLf());

            var result = CliHarness.Run(export, "--language", "en");

            result.ExitCode.ShouldBe((int)ExitCode.Warnings);
            result.Output.ShouldContain("GOBD2004");
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }
}
