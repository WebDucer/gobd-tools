using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Parsing;
using GoBd.Validation.Tests.Sources;

namespace GoBd.Validation.Tests.Checks;

public sealed class IdentityAmbiguityCheckTests
{
    private const string Duplicate = """
        <Table>
          <URL>kunden-alt.csv</URL>
          <Name>Kunden</Name>
          <VariableLength>
            <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
          </VariableLength>
        </Table>
        """;

    [Fact]
    public void UnreferencedDuplicateNameIsAWarning()
    {
        var finding = CheckHarness
            .Run(new IdentityAmbiguityCheck(), CheckHarness.Medium(CheckHarness.MasterTable("Kunden"), Duplicate))
            .Single(candidate => candidate.Code == FindingCodes.TableNameDuplicated);

        finding.Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void DuplicateNameThatSomethingReferencesIsAnError()
    {
        var finding = CheckHarness
            .Run(new IdentityAmbiguityCheck(), CheckHarness.Medium(
                CheckHarness.MasterTable("Kunden"),
                Duplicate,
                """
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Id</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
                """))
            .Single(candidate => candidate.Code == FindingCodes.TableNameDuplicated);

        // Ambiguity only bites once something actually resolves through it.
        finding.Severity.ShouldBe(Severity.Error);
    }

    [Fact]
    public void DuplicateUrlIsReported() =>
        CheckHarness.Codes(new IdentityAmbiguityCheck(), CheckHarness.Medium(
            CheckHarness.MasterTable("Kunden"),
            """
            <Table>
              <URL>kunden.csv</URL>
              <Name>KundenZweitbeschreibung</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
              </VariableLength>
            </Table>
            """)).ShouldContain(FindingCodes.TableUrlDuplicated);

    [Fact]
    public void DuplicateColumnNameWithinATableIsAnError()
    {
        var finding = CheckHarness.Run(new IdentityAmbiguityCheck(), CheckHarness.Medium("""
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                <VariableColumn><Name>Id</Name><Numeric/></VariableColumn>
              </VariableLength>
            </Table>
            """)).ShouldHaveSingleItem();

        // Foreign keys and aliases resolve columns by name, so this is never benign.
        finding.Code.ShouldBe(FindingCodes.ColumnNameDuplicated);
        finding.Severity.ShouldBe(Severity.Error);
    }
}

public sealed class NamingLimitsCheckTests
{
    [Fact]
    public void ReservedCharacterInAColumnNameIsReported() =>
        // The document escapes it, so the check sees the decoded character an importer would.
        CheckHarness.Codes(new NamingLimitsCheck(), CheckHarness.Medium("""
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariableColumn><Name>Firma &amp; Co</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
            """)).ShouldContain(FindingCodes.ReservedCharacterInName);

    [Fact]
    public void OverlongDescriptionIsMerelyNoted()
    {
        var finding = CheckHarness.Run(new NamingLimitsCheck(), CheckHarness.Medium($"""
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <Description>{new string('x', 256)}</Description>
              <VariableLength>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
            """)).ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.DescriptionTooLong);
        finding.Severity.ShouldBe(Severity.Info);
        finding.Arguments.ShouldContain("256");
    }

    [Fact]
    public void DescriptionAtTheLimitIsAccepted() =>
        CheckHarness.Codes(new NamingLimitsCheck(), CheckHarness.Medium($"""
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <Description>{new string('x', 255)}</Description>
              <VariableLength>
                <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
            """)).ShouldBeEmpty();
}

public sealed class CommandCheckTests
{
    [Fact]
    public void EveryCommandIsReportedAndQuoted()
    {
        var findings = CheckHarness.Run(new CommandCheck(), """
              <Version>1.0</Version>
              <Command>uncompress.bat</Command>
              <Media>
                <Name>Disk 1</Name>
                <Command>mount.sh</Command>
                <AcceptNoTables>true</AcceptNoTables>
              </Media>
            """);

        findings.Select(finding => finding.Code)
            .ShouldBe([FindingCodes.CommandDeclared, FindingCodes.CommandDeclared]);
        findings.SelectMany(finding => finding.Arguments).ShouldContain("uncompress.bat");
        findings.SelectMany(finding => finding.Arguments).ShouldContain("mount.sh");
        findings.ShouldAllBe(finding => finding.Severity == Severity.Warning);
    }

    [Fact]
    public void CommandsAreNeverExecutedAndTheirFilesAreNeverOpened()
    {
        // The named script exists in the export. It must still never be resolved or read --
        // this is a security boundary, not an unimplemented feature.
        var directory = Path.Combine(Path.GetTempPath(), $"gobd-cmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var marker = Path.Combine(directory, "executed.txt");
            File.WriteAllText(Path.Combine(directory, "uncompress.bat"), $"echo pwned > \"{marker}\"");
            File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");

            var dataSet = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
                  <Version>1.0</Version>
                  <Command>uncompress.bat</Command>
                  <Media><Name>D</Name><AcceptNoTables>true</AcceptNoTables></Media>
                """))).DataSet!;

            using var source = new CountingExportSource(new FolderExportSource(directory));
            var findings = CheckEngine.Run([new CommandCheck()], new CheckContext(dataSet, source));

            findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.CommandDeclared);
            source.Opened.ShouldBeEmpty();
            File.Exists(marker).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
