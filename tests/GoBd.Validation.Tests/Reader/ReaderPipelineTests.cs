using System.Diagnostics;
using System.Globalization;
using System.Text;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Opening an export reads and checks every table, on a worker, with progress that is measured
/// and an outcome that can be abandoned.
/// </summary>
public sealed class ReaderPipelineTests
{
    private static string Table(string name, string url) => $"""
                <Table>
                  <URL>{url}</URL>
                  <Name>{name}</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
        """;

    private static string Rows(int count, string date = "31.12.2024")
    {
        var builder = new StringBuilder(count * 20);
        for (var number = 1; number <= count; number++)
        {
            builder.Append(string.Create(CultureInfo.InvariantCulture, $"A{number:0000000};{date}\r\n"));
        }

        return builder.ToString();
    }

    private static ReaderSession Session(StoreHarness harness, IExportSource source) =>
        ReaderSession.Open(
            source,
            harness.ExportPath,
            harness.Options,
            ContentOptions.DefaultMaximumFindingsPerTable,
            [])
            .Session.ShouldNotBeNull();

    // ---- 3.1 progress is bytes read against the entry's length --------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExtractionReportsTheBytesItReadAndNeverMoreThanTheEntryHolds(bool zipped)
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", Rows(5_000)));
        var path = harness.ExportPath;
        var archive = zipped ? Zip(harness) : null;

        using IExportSource source = zipped
            ? new ZipExportSource(archive!)
            : new FolderExportSource(path);

        var entry = source.Find("a.csv").ShouldNotBeNull();
        var reported = new List<long>();
        var extracted = TableExtraction.Extract(
            source,
            "a.csv",
            RecordLayout.For(harness.Table("A")),
            Path.Combine(harness.StoreRoot, "extract.csv"),
            bound: 50,
            reported.Add,
            TestContext.Current.CancellationToken);

        extracted.Records.ShouldBe(5_000);
        reported.ShouldNotBeEmpty();
        reported.ShouldBeInOrder(SortDirection.Ascending);
        reported[^1].ShouldBe(entry.Length);
        reported.ShouldAllBe(bytes => bytes <= entry.Length);
        extracted.Bytes.ShouldBe(entry.Length);
    }

    // ---- 3.2 extraction and import are cancellable --------------------------------------------

    [Fact]
    public void ExtractionStopsAtTheNextRecordWhenItIsCancelled()
    {
        const int Records = 200_000;
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", Rows(Records)));
        using var source = new FolderExportSource(harness.ExportPath);
        using var cancellation = new CancellationTokenSource();
        var target = Path.Combine(harness.StoreRoot, "extract.csv");

        // Cancelled from the extraction's own progress, part way through the file, so the token
        // is set while records are still being written rather than before any were.
        Should.Throw<OperationCanceledException>(() => TableExtraction.Extract(
            source,
            "a.csv",
            RecordLayout.For(harness.Table("A")),
            target,
            bound: 50,
            bytes =>
            {
                // Past the reader's first buffer, so records have already been written when the
                // token is set rather than none at all.
                if (bytes >= 256 * 1024)
                {
                    cancellation.Cancel();
                }
            },
            cancellation.Token));

        // Stopped well short of reading the file out, and what it wrote is whole records:
        // extraction checks the token between records, never inside one, so a partial record
        // can never reach the extract for the store to read back as data.
        var written = File.ReadAllLines(target);
        written.Length.ShouldBeGreaterThan(0);
        written.Length.ShouldBeLessThan(Records);
        written.ShouldAllBe(line => line.EndsWith('"'));
    }

    [Fact]
    public async Task CancellingInTheMiddleOfATableStopsItAndLeavesNoStoreBehind()
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", Rows(400_000)));
        using var source = new FolderExportSource(harness.ExportPath);
        using var cancellation = new CancellationTokenSource();

        var session = Session(harness, source);
        var directory = session.Store.Directory;

        var reading = Task.Run(
            () => session.Read(null, cancellation.Token),
            TestContext.Current.CancellationToken);
        Until(() => session.Reading.Current is not null);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(reading);

        var table = session.Reading.Tables.ShouldHaveSingleItem();
        table.State.ShouldBe(TableReadingState.Reading);
        table.BytesRead.ShouldBeLessThan(table.Bytes);
        session.View(harness.Table("A")).Kind.ShouldBe(TableViewKind.NotReady);

        session.Dispose();
        Directory.Exists(directory).ShouldBeFalse();
    }

    // ---- 3.3 the pipeline, in document order --------------------------------------------------

    [Fact]
    public void EveryTableMovesFromWaitingThroughReadingToAnOutcomeInDocumentOrder()
    {
        using var harness = StoreHarness.Create(
            Table("A", "a.csv") + Table("B", "b.csv") + Table("C", "c.csv"),
            ("a.csv", Rows(50)),
            ("b.csv", Rows(50, "nicht-ein-datum")),
            ("c.csv", Rows(50)));
        using var source = new FolderExportSource(harness.ExportPath);
        using var session = Session(harness, source);

        var seen = new List<(string Table, TableReadingState State)>();
        session.Read(
            new Progress(reading =>
        {
            foreach (var table in reading.Tables)
            {
                if (seen.Count == 0 || seen[^1] != (table.Table.Identity, table.State))
                {
                    if (!seen.Contains((table.Table.Identity, table.State)))
                    {
                        seen.Add((table.Table.Identity, table.State));
                    }
                }
            }
        }),
            TestContext.Current.CancellationToken);

        seen.ShouldBe(
        [
            ("A", TableReadingState.Waiting),
            ("B", TableReadingState.Waiting),
            ("C", TableReadingState.Waiting),
            ("A", TableReadingState.Reading),
            ("A", TableReadingState.Ready),
            ("B", TableReadingState.Reading),
            ("B", TableReadingState.Defective),
            ("C", TableReadingState.Reading),
            ("C", TableReadingState.Ready),
        ]);

        var final = session.Reading;
        final.Complete.ShouldBeTrue();
        final.KeysChecked.ShouldBeTrue();
        final.Bytes.ShouldBe(Declared(source, "a.csv", "b.csv", "c.csv"));
        final.BytesRead.ShouldBe(final.Bytes);
        final.Fraction.ShouldBe(1);
    }

    [Fact]
    public void ATableWhoseImportFailsIsReportedAsFailedAndTheOthersAreStillRead()
    {
        using var harness = StoreHarness.Create(
            Table("A", "a.csv") + Table("B", "b.csv"),
            ("a.csv", Rows(10)),
            ("b.csv", Rows(10)));
        using var source = new FolderExportSource(harness.ExportPath);
        using var session = Session(harness, source);

        // Something already occupies the path B's extract is written to, which no finding code
        // anticipates. It has to stay with B.
        Directory.CreateDirectory(
            Path.Combine(session.Store.Directory, session.Store.NameOf(harness.Table("B")) + ".csv"));
        session.Read(cancellationToken: TestContext.Current.CancellationToken);

        var states = session.Reading.Tables.ToDictionary(table => table.Table.Identity, table => table.State);
        states["A"].ShouldBe(TableReadingState.Ready);
        states["B"].ShouldBe(TableReadingState.Failed);
        session.View(harness.Table("A")).Rows.ShouldNotBeNull().Count.ShouldBe(10);
    }

    // ---- 3.4, 3.5, 3.7 a table is openable as soon as it is ready ------------------------------

    [Fact]
    public async Task AReadyTableIsOpenedAndPagedWhileALaterOneIsHeldMidImport()
    {
        using var harness = StoreHarness.Create(
            Table("A", "a.csv") + Table("B", "b.csv"),
            ("a.csv", Rows(2_000)),
            ("b.csv", Rows(2_000)));
        using var source = new HoldingExportSource(new FolderExportSource(harness.ExportPath)) { Held = "b.csv" };
        using var session = Session(harness, source);

        var reading = Task.Run(() => session.Read(), TestContext.Current.CancellationToken);
        try
        {
            Until(() => source.Reached);

            // A is ready and readable, and B, whose turn has come but whose file will not open,
            // says it is still being read rather than blocking or lying about its contents.
            var ready = session.View(harness.Table("A"));
            ready.Kind.ShouldBe(TableViewKind.Data);
            ready.Rows.ShouldNotBeNull().Count.ShouldBe(2_000);
            ready.Rows![1_999].Values[0].ShouldBe("A0002000");

            var held = session.View(harness.Table("B"));
            held.Kind.ShouldBe(TableViewKind.NotReady);
            held.Rows.ShouldBeNull();
            held.Columns.ShouldBe(["Id", "Datum"]);

            session.Reading.Current.ShouldNotBeNull().Table.Identity.ShouldBe("B");
        }
        finally
        {
            source.Release();
            await reading;
        }

        session.View(harness.Table("B")).Kind.ShouldBe(TableViewKind.Data);
    }

    [Fact]
    public async Task AReadyTableIsPagedOnTheUiConnectionWhileTheWorkerImportsTheNext()
    {
        // The other half of the spike, at the session's level: not merely that a ready table can
        // be asked for, but that its pages keep arriving while the worker is inside the store.
        using var harness = StoreHarness.Create(
            Table("A", "a.csv") + Table("B", "b.csv"),
            ("a.csv", Rows(5_000)),
            ("b.csv", Rows(400_000)));
        using var source = new FolderExportSource(harness.ExportPath);
        using var session = Session(harness, source);

        var reading = Task.Run(() => session.Read(), TestContext.Current.CancellationToken);
        Until(() => session.Reading.Current?.Table.Identity == "B");

        var rows = session.View(harness.Table("A")).Rows.ShouldNotBeNull();
        var pages = 0;
        while (session.Reading.Current?.Table.Identity == "B")
        {
            rows[pages % rows.Count].Values[0].ShouldNotBeNullOrEmpty();
            rows[(pages * 977) % rows.Count].Values[0].ShouldNotBeNullOrEmpty();
            pages++;
        }

        await reading;

        pages.ShouldBeGreaterThan(10);
        session.Reading.Tables.Select(table => table.State)
            .ShouldAllBe(state => state == TableReadingState.Ready);
    }

    // ---- 3.6 opening can be abandoned ---------------------------------------------------------

    [Fact]
    public void ClosingTheReaderWhileAnExportIsBeingReadStopsItAndRemovesTheStore()
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", Rows(400_000)));
        var workspace = new ReaderWorkspace(harness.Options);

        workspace.Open(harness.ExportPath).Outcome.ShouldBe(OpenOutcome.Opened);
        var session = workspace.Session.ShouldNotBeNull();
        var directory = session.Store.Directory;
        Until(() => session.Reading.Current is not null);

        workspace.Dispose();

        workspace.Session.ShouldBeNull();
        workspace.Reading.IsCompleted.ShouldBeTrue();
        Directory.Exists(directory).ShouldBeFalse();
        Directory.EnumerateDirectories(harness.StoreRoot).ShouldBeEmpty();
    }

    [Fact]
    public async Task OpeningAnotherExportStopsReadingTheFirstAndReleasesItsStore()
    {
        using var first = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", Rows(400_000)));
        using var second = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", Rows(10)));
        using var workspace = new ReaderWorkspace(first.Options);

        workspace.Open(first.ExportPath);
        var abandoned = workspace.Session.ShouldNotBeNull().Store.Directory;
        Until(() => workspace.Session!.Reading.Current is not null);

        workspace.Open(second.ExportPath).Outcome.ShouldBe(OpenOutcome.Opened);
        await workspace.Reading;

        Directory.Exists(abandoned).ShouldBeFalse();
        workspace.Session.ShouldNotBeNull().Reading.Complete.ShouldBeTrue();
    }

    // ---- 3.8 progress is reported at most every 100 ms -----------------------------------------

    [Fact]
    public void ProgressIsReportedOnEveryStateChangeAndAtMostTenTimesASecondBetweenThem()
    {
        using var harness = StoreHarness.Create(Table("A", "a.csv"), ("a.csv", Rows(1_000_000)));
        using var source = new FolderExportSource(harness.ExportPath);
        using var session = Session(harness, source);

        var notifications = 0;
        var clock = Stopwatch.StartNew();
        session.Read(new Progress(_ => notifications++), TestContext.Current.CancellationToken);
        clock.Stop();

        // One table: after the header check, entering Reading, leaving it, and the completion
        // that follows the key checks. Everything beyond those is byte progress.
        const int StateChanges = 4;
        var allowed = StateChanges + (int)(clock.Elapsed.TotalMilliseconds / 100) + 1;

        notifications.ShouldBeGreaterThanOrEqualTo(StateChanges);
        notifications.ShouldBeLessThanOrEqualTo(allowed, $"{notifications} in {clock.ElapsedMilliseconds} ms");
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static long Declared(IExportSource source, params string[] entries) =>
        entries.Sum(entry => source.Find(entry)!.Length);

    private static string Zip(StoreHarness harness)
    {
        var path = Path.Combine(harness.StoreRoot, "export.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(harness.ExportPath, path);
        return path;
    }

    [Fact]
    public void AStoreErrorWhileCheckingKeysStaysWithItsTableAndTheReadStillCompletes()
    {
        const string Tables = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

        using var harness = StoreHarness.Create(
            Tables,
            ("kunden.csv", "K1\r\n"),
            ("bestellungen.csv", "B1\r\nB1\r\n"));
        using var source = new FolderExportSource(harness.ExportPath);
        using var session = Session(harness, source);
        var kunden = session.DataSet.Tables.Single(table => table.Identity == "Kunden");
        var bestellungen = session.DataSet.Tables.Single(table => table.Identity == "Bestellungen");
        var broken = false;

        // Once every table is in, the first one checked is taken out of the store from under the
        // key phase: the same error a full disk or a vanished file raises there, on demand.
        session.Read(new Progress(reading =>
        {
            if (!broken && reading.Tables.All(table => table.State == TableReadingState.Ready))
            {
                session.Store.Query($"DROP TABLE {session.Store.NameOf(kunden)}", _ => { });
                broken = true;
            }
        }), TestContext.Current.CancellationToken);

        broken.ShouldBeTrue();
        session.Reading.Complete.ShouldBeTrue();
        session.Reading.KeysChecked.ShouldBeTrue();

        // The failure is Kunden's alone. Bestellungen, checked after it, still has its duplicate
        // key reported, and neither table stops showing its data over a key.
        var states = session.Reading.Tables.ToDictionary(table => table.Table.Identity);
        states["Kunden"].Findings.ShouldContain(finding => finding.Code == FindingCodes.CheckFailed);
        states["Bestellungen"].Findings.ShouldContain(finding => finding.Code == FindingCodes.PrimaryKeyDuplicated);
        session.View(bestellungen).Kind.ShouldBe(TableViewKind.Data);
    }

    /// <summary>Waits for something the worker is about to do, or gives up loudly.</summary>
    private static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            Thread.Sleep(1);
        }

        condition().ShouldBeTrue("the worker never reached the state this test waits for");
    }

    /// <summary>
    /// A progress sink that runs on the reporting thread.
    /// </summary>
    /// <remarks>
    /// <see cref="System.Progress{T}"/> posts to the captured synchronisation context, which in a
    /// test is none, so its callbacks land on the thread pool and arrive after the assertions.
    /// The window wants exactly that indirection — it marshals to the dispatcher — and a test
    /// counting notifications wants none of it.
    /// </remarks>
    private sealed class Progress(Action<ExportReading> report) : IProgress<ExportReading>
    {
        public void Report(ExportReading value) => report(value);
    }
}
