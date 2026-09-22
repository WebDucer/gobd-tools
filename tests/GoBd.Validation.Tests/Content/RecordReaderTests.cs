using System.Text;
using GoBd.Validation.Content;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Content;

/// <summary>
/// Reading a data file as its declaration defines it.
/// </summary>
/// <remarks>
/// Every fixture here is bytes plus a declaration, because that is the whole of what a GoBD
/// export offers: the file says nothing about its own structure, and these tests fail if the
/// reader ever starts believing it.
/// </remarks>
public sealed class RecordReaderTests
{
    private const string TwoColumns = """
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    // ---- 2.1 the declared record layout ------------------------------------------------------

    [Fact]
    public void TheStandardsOwnDefaultsApplyWhenNothingIsDeclared()
    {
        var layout = ContentHarness.Layout(TwoColumns);

        layout.ColumnDelimiter.ShouldBe(";");
        layout.RecordDelimiter.ShouldBe("\r\n");
        layout.TextEncapsulator.ShouldBe("\"");
        layout.DecimalSymbol.ShouldBe(",");
        layout.DigitGroupingSymbol.ShouldBe(".");
        layout.Epoch.ShouldBe(30);
        layout.SkipNumBytes.ShouldBe(0);
    }

    [Fact]
    public void RecordsAndColumnsAreSplitAtTheDeclaredDelimiters()
    {
        var records = ContentHarness.Read(TwoColumns, "K1;Meier\r\nK2;Schulz\r\n");

        records.Count.ShouldBe(2);
        records[0].Values.ShouldBe(["K1", "Meier"]);
        records[1].Values.ShouldBe(["K2", "Schulz"]);
        records.SelectMany(record => record.Defects).ShouldBeEmpty();
    }

    [Fact]
    public void AnEncapsulatedValueMayContainTheColumnDelimiter()
    {
        var records = ContentHarness.Read(TwoColumns, "K1;\"Meier; Schulz & Co\"\r\n");

        records.Single().Values.ShouldBe(["K1", "Meier; Schulz & Co"]);
        records.Single().Defects.ShouldBeEmpty();
    }

    [Fact]
    public void AnEncapsulatedValueMayContainTheRecordDelimiter()
    {
        var records = ContentHarness.Read(TwoColumns, "K1;\"Meier\r\nSchulz\"\r\nK2;Lang\r\n");

        records.Count.ShouldBe(2);
        records[0].Values.ShouldBe(["K1", "Meier\r\nSchulz"]);
        records[1].Values.ShouldBe(["K2", "Lang"]);
    }

    [Fact]
    public void ADoubledEncapsulatorIsOneLiteralEncapsulator()
    {
        var records = ContentHarness.Read(TwoColumns, "K1;\"Meier \"\"der Ältere\"\"\"\r\n");

        records.Single().Values.ShouldBe(["K1", "Meier \"der Ältere\""]);
        records.Single().Defects.ShouldBeEmpty();
    }

    [Fact]
    public void DeclaredDelimitersOverrideTheDefaults()
    {
        const string Declared = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <ColumnDelimiter>|</ColumnDelimiter>
                    <RecordDelimiter>~</RecordDelimiter>
                    <TextEncapsulator>'</TextEncapsulator>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        var records = ContentHarness.Read(Declared, "K1|'Meier|Schulz'~K2|Lang~");

        records.Count.ShouldBe(2);
        records[0].Values.ShouldBe(["K1", "Meier|Schulz"]);
        records[1].Values.ShouldBe(["K2", "Lang"]);
    }

    [Fact]
    public void ATrailingRecordDelimiterDoesNotProduceAnEmptyRecord() =>
        ContentHarness.Read(TwoColumns, "K1;Meier\r\n").Count.ShouldBe(1);

    [Fact]
    public void AFinalRecordWithoutATrailingDelimiterIsStillRead() =>
        ContentHarness.Read(TwoColumns, "K1;Meier\r\nK2;Lang").Count.ShouldBe(2);

    // ---- 2.2 fixed-length spans --------------------------------------------------------------

    private const string FixedTable = """
            <Table>
              <URL>konten.txt</URL>
              <Name>Konten</Name>
              <FixedLength>
                <Length>18</Length>
                <FixedPrimaryKey>
                  <Name>Konto</Name><AlphaNumeric/>
                  <FixedRange><From>1</From><To>4</To></FixedRange>
                </FixedPrimaryKey>
                <FixedColumn>
                  <Name>Bezeichnung</Name><AlphaNumeric/>
                  <FixedRange><From>5</From><Length>10</Length></FixedRange>
                </FixedColumn>
                <FixedColumn>
                  <Name>Kennung</Name><AlphaNumeric/>
                  <FixedRange><From>15</From><To>18</To></FixedRange>
                </FixedColumn>
              </FixedLength>
            </Table>
    """;

    [Fact]
    public void EachColumnIsReadAtItsDeclaredPosition()
    {
        var records = ContentHarness.Read(FixedTable, "1000Kasse     AKTV1200Bank      AKTV");

        records.Count.ShouldBe(2);
        records[0].Values.ShouldBe(["1000", "Kasse     ", "AKTV"]);
        records[1].Values.ShouldBe(["1200", "Bank      ", "AKTV"]);
        records.SelectMany(record => record.Defects).ShouldBeEmpty();
    }

    [Fact]
    public void ASpanThatRunsToTheRecordEndIsRead() =>
        // The last column ends exactly at the declared record length; an off-by-one in the span
        // arithmetic would truncate it and nothing else would notice.
        ContentHarness.Read(FixedTable, "1000Kasse     AKTV").Single().Values[2].ShouldBe("AKTV");

    [Fact]
    public void AFixedRecordOfTheWrongLengthIsReported()
    {
        var records = ContentHarness.Read(FixedTable, "1000Kasse     AKT");

        records.Single().Defects.Single().Kind.ShouldBe(ContentDefectKind.FixedRecordLengthMismatch);
        records.Single().Defects.Single().Value.ShouldBe("17");
        records.Single().Defects.Single().Expected.ShouldBe("18");
    }

    [Fact]
    public void AFixedTableMayDelimitItsRecordsInstead()
    {
        const string Delimited = """
                <Table>
                  <URL>konten.txt</URL>
                  <Name>Konten</Name>
                  <FixedLength>
                    <RecordDelimiter>&#13;&#10;</RecordDelimiter>
                    <FixedPrimaryKey>
                      <Name>Konto</Name><AlphaNumeric/>
                      <FixedRange><From>1</From><To>4</To></FixedRange>
                    </FixedPrimaryKey>
                    <FixedColumn>
                      <Name>Bezeichnung</Name><AlphaNumeric/>
                      <FixedRange><From>5</From><To>9</To></FixedRange>
                    </FixedColumn>
                  </FixedLength>
                </Table>
        """;

        var records = ContentHarness.Read(Delimited, "1000Kasse\r\n1200Bank \r\n");

        records.Count.ShouldBe(2);
        records[0].Values.ShouldBe(["1000", "Kasse"]);
        records[1].Values.ShouldBe(["1200", "Bank "]);
    }

    // ---- 2.3 the declared codepage -----------------------------------------------------------

    [Fact]
    public void ANonUnicodeFileIsDecodedByItsDeclaredCodepage()
    {
        const string Ansi = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <ANSI/>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        // 0xE4 is 'ä' in windows-1252 and an invalid byte in UTF-8: reading it correctly proves
        // the declared codepage was used rather than a guess.
        var bytes = new byte[] { (byte)'K', (byte)'1', (byte)';', (byte)'M', 0xE4, (byte)'r', (byte)'z' };

        ContentHarness.Read(Ansi, bytes).Single().Values.ShouldBe(["K1", "März"]);
    }

    [Fact]
    public void AnsiIsAppliedWhenNoCodepageIsDeclared() =>
        ContentHarness.Layout(TwoColumns).Encoding.CodePage.ShouldBe(1252);

    [Fact]
    public void BytesThatTheDeclaredCodepageCannotRepresentAreReportedRatherThanReplaced()
    {
        const string Utf8Table = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <UTF8/>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        // 0xC3 begins a two-byte sequence that 0x28 cannot continue. The second record is sound,
        // and must still be read: a file does not stop being checkable at its first bad byte.
        var bytes = new List<byte> { (byte)'K', (byte)'1', (byte)';', 0xC3, 0x28, (byte)'\r', (byte)'\n' };
        bytes.AddRange(Encoding.UTF8.GetBytes("K2;Lang\r\n"));
        var records = ContentHarness.Read(Utf8Table, [.. bytes]);

        records.Count.ShouldBe(2);
        var defect = records[0].Defects.Single(defect => defect.Kind == ContentDefectKind.UndecodableBytes);
        defect.ColumnIndex.ShouldBe(1);
        defect.Expected.ShouldBe("UTF8");
        records[1].Defects.ShouldBeEmpty();
    }

    [Fact]
    public void ACodepageThisBuildCannotSupplyMakesTheTableUnreadable()
    {
        // Every codepage the standard names is supported, so the unreadable case is reached by
        // asking the resolver directly rather than by inventing a declaration the DTD rejects.
        DeclaredEncoding.TryResolve(Codepage.Utf7, out _).ShouldBeTrue();
        DeclaredEncoding.TryResolve(Codepage.Macintosh, out _).ShouldBeTrue();
        DeclaredEncoding.TryResolve(Codepage.Oem, out var oem).ShouldBeTrue();
        oem.CodePage.ShouldBe(850);
        DeclaredEncoding.TryResolve((Codepage)99, out _).ShouldBeFalse();
    }

    [Fact]
    public void AByteOrderMarkIsNotPartOfTheFirstValue()
    {
        const string Utf8Table = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <UTF8/>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        var bytes = new List<byte>([0xEF, 0xBB, 0xBF]);
        bytes.AddRange(Encoding.UTF8.GetBytes("K1;Meier\r\n"));

        ContentHarness.Read(Utf8Table, [.. bytes]).Single().Values[0].ShouldBe("K1");
    }

    // ---- 2.4 excluded records ----------------------------------------------------------------

    [Fact]
    public void RecordsBeforeTheDeclaredRangeAreNotData()
    {
        const string FromSecond = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <Range><From>2</From></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        var records = ContentHarness.Read(FromSecond, "Code;Firma\r\nK1;Meier\r\nK2;Lang\r\n");

        records[0].IsData.ShouldBeFalse();
        records[0].Number.ShouldBe(1);
        ContentHarness.DataValues(records).ShouldBe([["K1", "Meier"], ["K2", "Lang"]]);
    }

    [Fact]
    public void TheFirstDataRecordKeepsTheRecordNumberTheFileGivesIt() =>
        // A finding cites a record so that a person can find it in the file. Renumbering data
        // records from one would send them to the wrong line.
        ContentHarness.Read("""
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <Range><From>2</From></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """, "Code;Firma\r\nK1;Meier\r\n").Single(record => record.IsData).Number.ShouldBe(2);

    [Fact]
    public void ADeclaredRangeEndExcludesWhatFollowsIt()
    {
        const string TwoRecords = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <Range><From>1</From><To>2</To></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        ContentHarness.DataValues(ContentHarness.Read(TwoRecords, "K1;A\r\nK2;B\r\nK3;C\r\n"))
            .ShouldBe([["K1", "A"], ["K2", "B"]]);
    }

    [Fact]
    public void ADeclaredLengthBoundsTheRangeJustAsAnEndDoes()
    {
        var layout = ContentHarness.Layout("""
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <Range><From>3</From><Length>2</Length></Range>
                  <VariableLength>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """);

        layout.FirstRecord.ShouldBe(3);
        layout.LastRecord.ShouldBe(4);
    }

    [Fact]
    public void DeclaredBytesAreSkippedBeforeTheFirstRecord()
    {
        const string Skipping = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <SkipNumBytes>7</SkipNumBytes>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        ContentHarness.Read(Skipping, "PREAMBLK1;Meier\r\n").Single().Values.ShouldBe(["K1", "Meier"]);
    }

    // ---- 2.7 the file never describes itself -------------------------------------------------

    [Fact]
    public void AFirstRecordCarryingTheColumnNamesIsStillReadAsData()
    {
        // Nothing in the declaration excludes it, so it is data. Whether it is a header the
        // producer forgot to declare is a question for the header check, not for the reader.
        var records = ContentHarness.Read(TwoColumns, "Code;Firma\r\nK1;Meier\r\n");

        records.Count.ShouldBe(2);
        records[0].IsData.ShouldBeTrue();
        records[0].Values.ShouldBe(["Code", "Firma"]);
    }

    [Fact]
    public void ARecordWithTheWrongNumberOfColumnsIsReportedWithBothCounts()
    {
        var defect = ContentHarness.Read(TwoColumns, "K1;Meier;extra\r\n").Single().Defects.Single();

        defect.Kind.ShouldBe(ContentDefectKind.ColumnCountMismatch);
        defect.Value.ShouldBe("3");
        defect.Expected.ShouldBe("2");
    }

    [Fact]
    public void AnEncapsulatorThatIsNeverClosedIsReported()
    {
        var record = ContentHarness.Read(TwoColumns, "K1;\"Meier").Single();

        record.Defects.ShouldContain(defect => defect.Kind == ContentDefectKind.UnterminatedEncapsulator);
    }

    [Fact]
    public void AFixedRecordLengthOfZeroWithNoDelimiterCannotBeRead()
    {
        var layout = ContentHarness.Layout("""
                <Table>
                  <URL>konten.txt</URL>
                  <Name>Konten</Name>
                  <FixedLength>
                    <Length>0</Length>
                    <FixedColumn>
                      <Name>Konto</Name><AlphaNumeric/>
                      <FixedRange><From>1</From><To>8</To></FixedRange>
                    </FixedColumn>
                  </FixedLength>
                </Table>
        """);

        // Reading it would never advance past the first character, so it is not read at all.
        layout.Fault.ShouldBe(LayoutFault.RecordLengthUnusable);
        using var stream = new MemoryStream("10000000"u8.ToArray());
        RecordReader.Read(stream, layout).ShouldBeEmpty();
    }

    [Fact]
    public void AnUnreadableDeclarationYieldsNoRecordsRatherThanThrowing()
    {
        var layout = ContentHarness.Layout("""
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <ColumnDelimiter></ColumnDelimiter>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """);

        layout.IsReadable.ShouldBeFalse();
        layout.Fault.ShouldBe(LayoutFault.DelimiterEmpty);
        using var stream = new MemoryStream("A\r\n"u8.ToArray());
        RecordReader.Read(stream, layout).ShouldBeEmpty();
    }
}
