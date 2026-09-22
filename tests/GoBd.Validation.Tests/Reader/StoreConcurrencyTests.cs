using System.Diagnostics;
using System.Globalization;
using System.Text;
using GoBd.Reader.Data;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The one assumption the design could not settle by reading: that the grid may page a finished
/// table while the worker is still importing a later one, and that a statement already inside
/// the store can be stopped without waiting it out.
/// </summary>
/// <remarks>
/// These are the spike of the change's design.md D2 and D3, kept as tests rather than thrown
/// away, because what they establish is what the opening pipeline rests on. If DuckDB ever
/// serialised the two connections, or lost the ability to interrupt, the pipeline would freeze
/// the window again and these would say so.
/// </remarks>
[Collection(typeof(Measured))]
public sealed class StoreConcurrencyTests
{
    /// <summary>
    /// Dragging the scrollbar of the three-million-record export cost 13.6 ms a step, measured in
    /// the finished reader. Contention between the two connections may cost a page at most twice
    /// that; beyond it the design's fallback — paging from the extract files — would be needed.
    /// </summary>
    /// <remarks>
    /// A budget for the pages a person actually meets, not for the single slowest of thousands:
    /// see <see cref="AFinishedTableIsPagedWhileALaterOneIsStillBeingImported"/>.
    /// </remarks>
    private static readonly TimeSpan PageBudget = TimeSpan.FromMilliseconds(2 * 13.6);

    private const int SmallRecords = 20_000;
    private const int LargeRecords = 1_000_000;

    private const string Tables = """
            <Table>
              <URL>klein.csv</URL>
              <Name>Klein</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
              </VariableLength>
            </Table>
            <Table>
              <URL>gross.csv</URL>
              <Name>Gross</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private static string Rows(int count)
    {
        var builder = new StringBuilder(count * 24);
        for (var number = 1; number <= count; number++)
        {
            builder.Append(string.Create(CultureInfo.InvariantCulture, $"B{number:0000000};31.12.2024;1,50\r\n"));
        }

        return builder.ToString();
    }

    private static StoreHarness Export() =>
        StoreHarness.Create(
            Tables,
            ("klein.csv", Rows(SmallRecords)),
            ("gross.csv", Rows(LargeRecords)));

    // ---- 2.1 two connections to one store ----------------------------------------------------

    [Fact]
    public void AFinishedTableIsPagedWhileALaterOneIsStillBeingImported()
    {
        using var harness = Export();
        var store = harness.Store();
        var small = harness.Table("Klein");
        var large = harness.Table("Gross");

        store.Import(small).Status.ShouldBe(TableImportStatus.Imported);

        // The same paging, with nothing else running, as the measurement to hold the contended
        // one against. An absolute budget alone would be a claim about this machine and this
        // build; the two together say what the design actually asked — whether the second
        // connection costs anything.
        var quiet = Timings(store, small, 200);

        Exception? failure = null;
        var importing = new Thread(() =>
        {
            try
            {
                store.Import(large);
            }
#pragma warning disable CA1031 // Whatever the import threw is the test's verdict, reported below.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failure = exception;
            }
        });

        var contended = new List<TimeSpan>();
        var short_ = 0;
        var random = new Random(7);

        importing.Start();
        while (importing.IsAlive)
        {
            var offset = random.Next(0, SmallRecords - 512);
            var clock = Stopwatch.StartNew();
            var page = store.Page(small, offset, 512);
            clock.Stop();

            if (page.Count != 512)
            {
                short_++;
            }

            contended.Add(clock.Elapsed);
        }

        importing.Join();

        // Judged by where the pages fall rather than by the one that fell furthest. Thousands are
        // read while the import runs, and on a shared machine one of them always meets a garbage
        // collection or a descheduled thread: the slowest page measured the machine, not the
        // store, and failed a run in three for it. What the design asked is whether paging stays
        // fast while an import runs, which is what the median and the 95th percentile say.
        var median = Measured.Percentile(contended, 0.50);
        var usual = Measured.Percentile(contended, 0.95);
        var quietMedian = Measured.Percentile(quiet, 0.50);

        var measured = string.Create(
            CultureInfo.InvariantCulture,
            $"{contended.Count} pages: median {median.TotalMilliseconds:F1} ms, "
            + $"95th percentile {usual.TotalMilliseconds:F1} ms, slowest {contended.Max().TotalMilliseconds:F1} ms; "
            + $"with nothing else running: median {quietMedian.TotalMilliseconds:F1} ms, "
            + $"95th percentile {Measured.Percentile(quiet, 0.95).TotalMilliseconds:F1} ms");

        failure.ShouldBeNull();
        short_.ShouldBe(0);

        // A run that paged nothing would assert nothing: the import has to have been slow enough
        // for the two to actually have overlapped.
        contended.Count.ShouldBeGreaterThan(10);
        usual.ShouldBeLessThan(PageBudget, measured);
        median.ShouldBeLessThan(quietMedian * 3 + TimeSpan.FromMilliseconds(2), measured);
    }

    private static List<TimeSpan> Timings(ExportStore store, TableNode table, int pages)
    {
        var random = new Random(11);
        var timings = new List<TimeSpan>(pages);
        for (var page = 0; page < pages; page++)
        {
            var clock = Stopwatch.StartNew();
            store.Page(table, random.Next(0, SmallRecords - 512), 512);
            clock.Stop();
            timings.Add(clock.Elapsed);
        }

        return timings;
    }

    [Fact]
    public void ATableImportedByTheWorkerBecomesVisibleToThePagingConnection()
    {
        // The grid's connection is opened when the export is, long before the later tables are
        // created. Nothing is reopened, so the connection has to see what the worker commits.
        using var harness = Export();
        var store = harness.Store();
        var large = harness.Table("Gross");

        var importing = new Thread(() => store.Import(large));
        importing.Start();
        importing.Join();

        store.RecordCount(large).ShouldBe(LargeRecords);
        store.Page(large, LargeRecords - 1, 1)[0].Values[0].ShouldBe("B1000000");
    }

    // ---- 2.2 interrupting a statement already inside the store -------------------------------

    [Fact]
    public void AStatementRunningInTheStoreIsStoppedFromAnotherThread()
    {
        using var harness = StoreHarness.Create(Tables, ("klein.csv", Rows(1)), ("gross.csv", Rows(1)));
        var store = harness.Store();

        Exception? thrown = null;
        var running = new Thread(() =>
        {
            try
            {
                // Long enough that it cannot finish on its own inside the budget below, and
                // entirely inside the store, so only an interrupt can end it.
                store.Query(
                    "SELECT count(*) FROM range(0, 200000000000) t(i) WHERE md5(i::VARCHAR) LIKE '%abcdef%'",
                    _ => { });
            }
#pragma warning disable CA1031 // Which exception ends it is exactly what this test reports.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                thrown = exception;
            }
        });

        running.Start();

        // Interrupted repeatedly rather than after a sleep: an interrupt arriving before the
        // statement starts is simply lost, and guessing how long that takes is what makes such a
        // test flaky.
        var clock = Stopwatch.StartNew();
        while (running.IsAlive && clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            store.Interrupt();
            running.Join(TimeSpan.FromMilliseconds(50));
        }

        clock.Stop();

        // The guarantee is that it stopped, not which exception says so. An interrupt that lands
        // while the connection is executing raises OperationCanceledException; one that lands
        // between statements is reported by the store as its own error on the next. The reader
        // consults the cancellation token rather than the exception type for exactly this reason.
        running.IsAlive.ShouldBeFalse($"the statement was still running after {clock.Elapsed.TotalSeconds:F0} s");
        thrown.ShouldNotBeNull();
    }

    // ---- 2.3 a third connection, for filters, sorts and figures -------------------------------

    [Fact]
    public void AViewIsBuiltAndReadWhileAnotherTableIsStillBeingImported()
    {
        // Three connections, three threads, one store: the import writes on the first, the view
        // is built on the second, and the grid pages what it built on the third. A person must be
        // able to filter the table they already have while the rest of the export is still being
        // read. See the change's design.md D5.
        using var harness = Export();
        var store = harness.Store();
        var small = harness.Table("Klein");
        var large = harness.Table("Gross");

        store.Import(small).Conforms.ShouldBeTrue();

        Exception? failure = null;
        var importing = new Thread(() =>
        {
            try
            {
                store.Import(large);
            }
#pragma warning disable CA1031 // Whatever the import threw is what this test has to report.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failure = exception;
            }
        });

        importing.Start();

        var view = store.CreateView(
            small,
            new TableQuery([], [new ColumnSort(0, Ordering.Descending)]),
            0,
            TestContext.Current.CancellationToken);
        store.CountView(view).ShouldBe(SmallRecords);
        store.PageView(small, view, 0, 1)[0].Values[0].ShouldBe("B0020000");

        importing.Join();

        failure.ShouldBeNull();
        store.RecordCount(large).ShouldBe(LargeRecords);
    }
}
