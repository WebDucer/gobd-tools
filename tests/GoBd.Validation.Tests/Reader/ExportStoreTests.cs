using System.Globalization;
using System.Text;
using GoBd.Reader.Data;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The store: what it imports, where it puts it, what it insists on before starting, and what it
/// leaves behind.
/// </summary>
public sealed class ExportStoreTests
{
    private const string Buchungen = """
            <Table>
              <URL>buchungen.csv</URL>
              <Name>Buchungen</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private static string Rows(int count, Func<int, string>? line = null)
    {
        var builder = new StringBuilder();
        for (var number = 1; number <= count; number++)
        {
            builder.Append(line is null
                ? string.Create(CultureInfo.InvariantCulture, $"B{number:0000};31.12.2024;1,50\r\n")
                : line(number));
        }

        return builder.ToString();
    }

    // ---- 7.2 what the import materialises ----------------------------------------------------

    [Fact]
    public void ADeclaredTableIsImportedWithItsDeclaredColumns()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        var store = harness.Store();

        var import = store.Import(harness.Table("Buchungen"));

        import.Status.ShouldBe(TableImportStatus.Imported);
        import.Findings.ShouldBeEmpty();
        import.Records.ShouldBe(3);
        store.Page(harness.Table("Buchungen"), 0, 3)[0].Values.ShouldBe(["B0001", "31.12.2024", "1,50"]);
    }

    [Fact]
    public void TheOrdinalIsDenseAndStartsAtTheFirstDataRecord()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(200)));
        var store = harness.Store();
        store.Import(harness.Table("Buchungen"));

        var ordinals = store.Page(harness.Table("Buchungen"), 0, 200).Select(record => record.Ordinal);

        ordinals.ShouldBe(Enumerable.Range(1, 200).Select(number => (long)number));
    }

    [Fact]
    public void TheOrdinalIsTheRecordNumberTheFileGivesEvenWhenRecordsAreExcluded()
    {
        const string Skipping = """
                <Table>
                  <URL>buchungen.csv</URL>
                  <Name>Buchungen</Name>
                  <Range><From>3</From></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Skipping, ("buchungen.csv", Rows(5)));
        var store = harness.Store();
        var import = store.Import(harness.Table("Buchungen"));

        import.Records.ShouldBe(3);
        store.Page(harness.Table("Buchungen"), 0, 3).Select(record => record.Ordinal).ShouldBe([3L, 4L, 5L]);
    }

    [Fact]
    public void TheOrdinalMatchesTheRecordNumberTheStreamingEngineReports()
    {
        // Both engines cite records so a person can find them in the file. A record dropped on
        // the way in would silently shift every number after it.
        var content = Rows(50, number => number == 7
            ? "B0007;nicht-ein-datum;1,50\r\n"
            : string.Create(CultureInfo.InvariantCulture, $"B{number:0000};31.12.2024;1,50\r\n"));

        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", content));
        var store = harness.Store();
        var import = store.Import(harness.Table("Buchungen"));

        import.Findings.ShouldHaveSingleItem().Arguments[1].ShouldBe("7");

        using var source = new FolderExportSource(harness.ExportPath);
        var layout = RecordLayout.For(harness.Table("Buchungen"));
        using var stream = source.OpenRead("buchungen.csv");
        RecordReader.Read(stream, layout)
            .Single(record => record.Values[1] == "nicht-ein-datum")
            .Number.ShouldBe(7);
    }

    [Fact]
    public void AValueThatDoesNotMatchItsDeclarationIsFoundByTheStoreToo()
    {
        using var harness = StoreHarness.Create(
            Buchungen,
            ("buchungen.csv", "B0001;31.12.2024;1,505\r\nB0002;2024-12-31;1,50\r\n"));
        var store = harness.Store();

        var codes = store.Import(harness.Table("Buchungen")).Findings.Select(finding => finding.Code);

        codes.ShouldBe([FindingCodes.RecordValueTypeMismatch, FindingCodes.RecordValueAccuracyExceeded], ignoreOrder: true);
    }

    [Fact]
    public void ATableWhoseFileIsAbsentIsReportedAsSuch()
    {
        using var harness = StoreHarness.Create(Buchungen);
        var store = harness.Store();

        store.Import(harness.Table("Buchungen")).Status.ShouldBe(TableImportStatus.FileAbsent);
    }

    // ---- 7.3 where the store lives -----------------------------------------------------------

    [Fact]
    public void TheStoreLivesUnderTheConfiguredRootAndNeverBesideTheExport()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        var before = harness.ExportContents();

        var store = harness.Store();
        store.Import(harness.Table("Buchungen"));

        store.Directory.ShouldStartWith(harness.StoreRoot);
        harness.ExportContents().ShouldBe(before);
    }

    [Fact]
    public void TheLocationIsOverridable()
    {
        // On many Linux distributions the platform temporary location is a tmpfs, so importing a
        // multi-gigabyte export there would consume memory rather than disk.
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        var elsewhere = Path.Combine(harness.StoreRoot, "somewhere-else");

        harness.Store(new StoreOptions(elsewhere)).Directory.ShouldStartWith(elsewhere);
    }

    [Fact]
    public void TwoExportsWithDifferentContentGetDifferentStores()
    {
        using var first = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        using var second = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(4)));

        StoreLocation.KeyFor(first.Source()).ShouldNotBe(StoreLocation.KeyFor(second.Source()));
    }

    // ---- 7.4 room to work in -----------------------------------------------------------------

    [Fact]
    public void AnImportThatWouldNotFitIsRefusedBeforeAnythingIsWritten()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        var options = new StoreOptions(harness.StoreRoot, SpaceHeadroom: 1e12);

        var result = ExportStore.Open(harness.Source(), harness.DataSet, options);

        result.Store.ShouldBeNull();
        var refusal = result.Refusal.ShouldBeOfType<NotEnoughSpace>();
        refusal.RequiredBytes.ShouldBeGreaterThan(refusal.AvailableBytes);
        Directory.EnumerateFileSystemEntries(harness.StoreRoot).ShouldBeEmpty();
    }

    [Fact]
    public void TheVolumeMeasuredIsTheOneThatBacksThePath()
    {
        // Not the root of the filesystem: on a machine whose temporary directory is mounted
        // separately, asking about "/" would report the wrong free space entirely.
        var space = Volumes.For(Path.GetTempPath());

        space.VolumeRoot.ShouldNotBeNullOrEmpty();
        Path.GetFullPath(Path.GetTempPath()).ShouldStartWith(space.VolumeRoot);
        space.AvailableBytes.ShouldBeGreaterThan(0);
    }

    // ---- 7.5 what it leaves behind -----------------------------------------------------------

    [Fact]
    public void ClosingTheStoreRemovesIt()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        var result = ExportStore.Open(harness.Source(), harness.DataSet, harness.Options);
        var store = result.Store.ShouldNotBeNull();
        var directory = store.Directory;
        store.Import(harness.Table("Buchungen"));

        Directory.Exists(directory).ShouldBeTrue();
        store.Dispose();
        Directory.Exists(directory).ShouldBeFalse();
    }

    [Fact]
    public void StartupCleanupRemovesAStoreNobodyHolds()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        var abandoned = Path.Combine(harness.StoreRoot, "abandoned.999999");
        Directory.CreateDirectory(abandoned);
        StoreLock.TryTake(abandoned).ShouldNotBeNull().Dispose();

        ExportStore.RemoveAbandonedStores(harness.Options);

        Directory.Exists(abandoned).ShouldBeFalse();
    }

    [Fact]
    public void StartupCleanupLeavesAStoreThatIsStillHeld()
    {
        // Age cannot tell an abandoned store from one a second instance is importing into right
        // now, and deleting the second kind pulls the ground from under a live session.
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(3)));
        var held = Path.Combine(harness.StoreRoot, "held.999998");
        using var claim = StoreLock.TryTake(held).ShouldNotBeNull();

        ExportStore.RemoveAbandonedStores(harness.Options);

        Directory.Exists(held).ShouldBeTrue();
    }

    // ---- 7.6 paged reads ---------------------------------------------------------------------

    [Fact]
    public void APageRequestMaterialisesOnlyThatPage()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(5_000)));
        var store = harness.Store();
        var table = harness.Table("Buchungen");
        store.Import(table);

        store.RecordCount(table).ShouldBe(5_000);
        var page = store.Page(table, 4_000, 20);

        page.Count.ShouldBe(20);
        page[0].Ordinal.ShouldBe(4_001);
        page[0].Values[0].ShouldBe("B4001");
        page[^1].Ordinal.ShouldBe(4_020);
    }

    [Fact]
    public void APageBeyondTheEndIsEmptyRatherThanAnError()
    {
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", Rows(10)));
        var store = harness.Store();
        var table = harness.Table("Buchungen");
        store.Import(table);

        store.Page(table, 100, 20).ShouldBeEmpty();
    }

    // ---- 7.8 bounded and reproducible --------------------------------------------------------

    [Fact]
    public void DefectsAreReportedInRecordOrderAndBounded()
    {
        var content = Rows(200, number =>
            string.Create(CultureInfo.InvariantCulture, $"B{number:0000};nicht-ein-datum;1,50\r\n"));

        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", content));
        var store = harness.Store();
        var import = store.Import(harness.Table("Buchungen"), bound: 5);

        import.Truncated.ShouldBeTrue();
        import.Findings
            .Where(finding => finding.Code == FindingCodes.RecordValueTypeMismatch)
            .Select(finding => finding.Arguments[1])
            .ShouldBe(["1", "2", "3", "4", "5"]);
        import.Findings.ShouldContain(finding => finding.Code == FindingCodes.TableAnalysisStopped);
    }

    [Fact]
    public void RepeatedRunsOfTheSameDefectiveFileReportTheSameFindings()
    {
        // The store reads CSV in parallel, so "the first fifty errors encountered" would vary
        // between runs of one file. Ordering by record number is what makes a report citable.
        var content = Rows(500, number => number % 3 == 0
            ? string.Create(CultureInfo.InvariantCulture, $"B{number:0000};nicht-ein-datum;1,50\r\n")
            : string.Create(CultureInfo.InvariantCulture, $"B{number:0000};31.12.2024;1,50\r\n"));

        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", content));

        var first = harness.Store().Import(harness.Table("Buchungen"), bound: 20).Findings;
        var second = harness.Store().Import(harness.Table("Buchungen"), bound: 20).Findings;

        second.ShouldBe(first);
    }

    [Fact]
    public void ATableWithinTheBoundIsNotReportedAsTruncated()
    {
        using var harness = StoreHarness.Create(
            Buchungen,
            ("buchungen.csv", "B0001;31.12.2024;1,50\r\nB0002;nicht-ein-datum;1,50\r\n"));
        var store = harness.Store();

        var import = store.Import(harness.Table("Buchungen"), bound: ContentOptions.DefaultMaximumFindingsPerTable);

        import.Truncated.ShouldBeFalse();
        import.Findings.ShouldHaveSingleItem();
    }

    // ---- 14.1 and 14.2 a table with nothing in it --------------------------------------------

    [Fact]
    public void ATableWhoseFileHoldsNoRecordsImportsAsATableOfNoRecords()
    {
        // A crash reported from a real export. The store's CSV reader sniffed the empty extract,
        // found one column where the table declared several, and refused the file — which ended
        // the reader's session.
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", string.Empty));
        var store = harness.Store();
        var table = harness.Table("Buchungen");

        var import = store.Import(table);

        import.Status.ShouldBe(TableImportStatus.Imported);
        import.Findings.ShouldBeEmpty();
        import.Records.ShouldBe(0);
        store.RecordCount(table).ShouldBe(0);
        store.Page(table, 0, 20).ShouldBeEmpty();
    }

    [Fact]
    public void ATableExcludedDownToNothingByItsRangeImportsTheSameWay()
    {
        // The same emptiness reached another way: the file has records, the declaration
        // excludes all of them, and the extract the store reads is empty either way.
        const string Excluding = """
                <Table>
                  <URL>buchungen.csv</URL>
                  <Name>Buchungen</Name>
                  <Range><From>10</From></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(Excluding, ("buchungen.csv", Rows(3)));
        var store = harness.Store();
        var table = harness.Table("Buchungen");

        var import = store.Import(table);

        import.Status.ShouldBe(TableImportStatus.Imported);
        import.Findings.ShouldBeEmpty();
        store.RecordCount(table).ShouldBe(0);
    }
}
