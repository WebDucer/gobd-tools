using GoBd.Validation.Checks;
using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Fixtures;
using GoBd.Validation.Tests.Parsing;

namespace GoBd.Validation.Tests.Checks;

/// <summary>Runs one check over an inline index.xml body.</summary>
public static class CheckHarness
{
    /// <summary>Parses the body and runs the given check over it.</summary>
    public static IReadOnlyList<Finding> Run(ICheck check, string body)
    {
        var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document(body)));
        var dataSet = outcome.DataSet
            ?? throw new InvalidOperationException("Fixture document is not well formed.");

        using var source = new FolderExportSource(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport));
        return CheckEngine.Run([check], new CheckContext(dataSet, source));
    }

    /// <summary>Codes produced by running the check, in order.</summary>
    public static IReadOnlyList<string> Codes(ICheck check, string body) =>
        [.. Run(check, body).Select(finding => finding.Code)];

    /// <summary>A table declaring a single alphanumeric primary key plus one column.</summary>
    public static string MasterTable(string identity, string keyName = "Id", string keyType = "<AlphaNumeric/>") => $"""
            <Table>
              <URL>{identity.ToLowerInvariant()}.csv</URL>
              <Name>{identity}</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>{keyName}</Name>{keyType}</VariablePrimaryKey>
                <VariableColumn><Name>{identity}Label</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    /// <summary>Wraps table declarations in a single medium.</summary>
    public static string Medium(params string[] tables) => $"""
          <Version>1.0</Version>
          <Media>
            <Name>Disk 1</Name>
        {string.Join("\n", tables)}
          </Media>
    """;
}
