using System.Text;

namespace GoBd.Validation.Tests.Parsing;

/// <summary>Builds small index.xml documents for tests.</summary>
public static class IndexXml
{
    /// <summary>Wraps a body in a DataSet declaring the canonical 1.6 grammar.</summary>
    public static string Document(string body, string systemId = "gdpdu-01-03-2019.dtd") =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <!DOCTYPE DataSet SYSTEM "{systemId}">
         <DataSet>
         {body}
         </DataSet>
         """;

    /// <summary>A minimal conformant document with one variable-length table.</summary>
    public static string Minimal() => Document("""
          <Version>1.0</Version>
          <Media>
            <Name>Disk 1</Name>
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariablePrimaryKey>
                  <Name>Kunden-Code</Name>
                  <AlphaNumeric/>
                </VariablePrimaryKey>
                <VariableColumn>
                  <Name>Firma</Name>
                  <AlphaNumeric/>
                </VariableColumn>
              </VariableLength>
            </Table>
          </Media>
        """);

    /// <summary>Opens a document as a stream, as the parser consumes it.</summary>
    public static Stream Stream(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));
}
