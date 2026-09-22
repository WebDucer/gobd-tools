using System.Text;
using DuckDB.NET.Data;
using GoBd.Validation.Content;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The readings <c>index.xml</c> does not fix, settled against the store's own CSV parser.
/// </summary>
/// <remarks>
/// The declaration fixes the dialect completely — column delimiter, record delimiter, text
/// encapsulator, codepage, and for fixed length the column spans. It says nothing about how an
/// encapsulator is escaped inside an encapsulated value, what one means mid-field in an
/// unencapsulated one, or whether a trailing record delimiter yields a final empty record.
/// Rather than invent answers, the reading a mature parser gives is taken as correct and this
/// tool's reader is held to it — which is what these tests do, by parsing the same original file
/// both ways.
/// </remarks>
public sealed class DialectAgreementTests
{
    private const string TwoColumns = """
            <Table>
              <URL>d.csv</URL>
              <Name>D</Name>
              <VariableLength>
                <VariableColumn><Name>a</Name><AlphaNumeric/></VariableColumn>
                <VariableColumn><Name>b</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    /// <summary>Reads the original file with the store's CSV parser, under the declared dialect.</summary>
    private static IReadOnlyList<string[]> ByTheStore(string path)
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT * FROM read_csv('" + path.Replace("'", "''", StringComparison.Ordinal) + "'"
            + ", columns = {'a': 'VARCHAR', 'b': 'VARCHAR'}"
            + ", delim = ';', quote = '\"', escape = '\"', new_line = '\\r\\n'"
            + ", header = false, all_varchar = true)";

        using var reader = command.ExecuteReader();
        var rows = new List<string[]>();
        while (reader.Read())
        {
            rows.Add(
            [
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            ]);
        }

        return rows;
    }

    private static IReadOnlyList<string[]> ByThisReader(StoreHarness harness)
    {
        using var source = new FolderExportSource(harness.ExportPath);
        var layout = RecordLayout.For(harness.Table("D"));
        using var stream = source.OpenRead("d.csv");
        return [.. RecordReader.Read(stream, layout).Select(record => record.Values.ToArray())];
    }

    private static void BothReadItTheSameWay(string content)
    {
        using var harness = StoreHarness.Create(TwoColumns, Encoding.UTF8, ("d.csv", content));

        var store = ByTheStore(Path.Combine(harness.ExportPath, "d.csv"));
        var ours = ByThisReader(harness);

        ours.Count.ShouldBe(store.Count);
        for (var row = 0; row < store.Count; row++)
        {
            ours[row].ShouldBe(store[row]);
        }
    }

    [Fact]
    public void AnEncapsulatorInsideAnEncapsulatedValueIsOneLiteralEncapsulator() =>
        BothReadItTheSameWay("\"a\"\"b\";x\r\n");

    [Fact]
    public void AnEncapsulatorMidFieldInAnUnencapsulatedValueIsOrdinaryText() =>
        BothReadItTheSameWay("ab\"c;x\r\n");

    [Fact]
    public void ATrailingRecordDelimiterDoesNotYieldAFinalEmptyRecord() =>
        BothReadItTheSameWay("a;b\r\n");

    [Fact]
    public void AFinalRecordWithoutATrailingDelimiterIsStillARecord() =>
        BothReadItTheSameWay("a;b");

    [Fact]
    public void AnEncapsulatedValueMayContainTheRecordDelimiter() =>
        BothReadItTheSameWay("\"a\r\nb\";x\r\n");

    [Fact]
    public void AnEncapsulatedValueMayContainTheColumnDelimiter() =>
        BothReadItTheSameWay("\"a;b\";x\r\n");

    [Fact]
    public void AnEmptyEncapsulatedValueIsAnEmptyValue() =>
        // The store reads "" as null and this reader as an empty string. They mean the same
        // thing, and the store's own import restores the empty string, so the two agree on what
        // the file says even though they disagree about how to spell "nothing".
        BothReadItTheSameWay("\"\";x\r\n");

    [Fact]
    public void ThisReaderIsStricterThanTheStoreAboutTwoThingsOnPurpose()
    {
        // Two readings where this reader deliberately does not follow the store's parser, and
        // where the product never has to choose between them: the store reads an extract this
        // reader produced, so both engines see this reading.
        //
        // Leading whitespace before an encapsulator: the store treats ' "ab"' as an encapsulated
        // value and drops the space. The declaration says the encapsulator encapsulates a value,
        // and a value that begins with a space is not encapsulated — so it is read literally
        // rather than having a character silently removed from it.
        using var whitespace = StoreHarness.Create(TwoColumns, Encoding.UTF8, ("d.csv", " \"ab\";x\r\n"));
        ByThisReader(whitespace)[0][0].ShouldBe(" \"ab\"");

        // Text after a closing encapsulator: the store refuses the file outright. This reader
        // keeps the text, so a person can see what is actually in the record rather than being
        // told the whole table is unreadable because of one row.
        using var trailing = StoreHarness.Create(TwoColumns, Encoding.UTF8, ("d.csv", "\"ab\"cd;x\r\n"));
        ByThisReader(trailing)[0][0].ShouldBe("abcd");
    }
}
