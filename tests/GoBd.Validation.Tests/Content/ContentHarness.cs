using System.Text;
using GoBd.Validation.Content;
using GoBd.Validation.Model;
using GoBd.Validation.Parsing;
using GoBd.Validation.Tests.Parsing;

namespace GoBd.Validation.Tests.Content;

/// <summary>Builds a table declaration and reads bytes under it.</summary>
public static class ContentHarness
{
    /// <summary>Parses one <c>Table</c> declaration out of a minimal document.</summary>
    public static TableNode Table(string tableXml)
    {
        var document = IndexXml.Document($"""
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
            {tableXml}
              </Media>
            """);

        var outcome = IndexXmlParser.Parse(IndexXml.Stream(document));
        var dataSet = outcome.DataSet
            ?? throw new InvalidOperationException("The table declaration is not well formed.");
        return dataSet.Tables.Single();
    }

    /// <summary>Resolves the layout of one declared table.</summary>
    public static RecordLayout Layout(string tableXml) => RecordLayout.For(Table(tableXml));

    /// <summary>Reads the given bytes under the given table declaration.</summary>
    public static IReadOnlyList<DataRecord> Read(string tableXml, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return [.. RecordReader.Read(stream, Layout(tableXml))];
    }

    /// <summary>Reads the given text, encoded as the declaration says it is stored.</summary>
    public static IReadOnlyList<DataRecord> Read(string tableXml, string text, Encoding? encoding = null)
    {
        var layout = Layout(tableXml);
        return Read(tableXml, (encoding ?? layout.Encoding).GetBytes(text));
    }

    /// <summary>Interprets one value under the declared column at the given position.</summary>
    public static InterpretedValue Interpret(string tableXml, string value, int columnIndex = 0)
    {
        var layout = Layout(tableXml);
        return ValueInterpreter.Interpret(value, layout.Columns[columnIndex], layout);
    }

    /// <summary>The values of the records the declaration counts as data.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> DataValues(IEnumerable<DataRecord> records) =>
        [.. records.Where(record => record.IsData).Select(record => record.Values)];
}
