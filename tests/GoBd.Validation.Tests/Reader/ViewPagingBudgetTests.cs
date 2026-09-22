using System.Diagnostics;
using System.Globalization;
using System.Text;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// What a page of a filtered and sorted view costs, and what it holds while being scrolled.
/// </summary>
/// <remarks>
/// A view's records are scattered through the table it came from, which is what made every other
/// way of reading one too slow: a page fetched through a join cost 46–98 ms against a budget of
/// 27, and only a view that carries its records came in at 1.3 ms. This holds that to the same
/// budget as a page in file order, on the same three-million-record export, and holds the reader
/// to materialising a window rather than the view.
/// </remarks>
[Collection(typeof(Measured))]
public sealed class ViewPagingBudgetTests
{
    /// <summary>The scrollbar step measured in the finished reader, and what a page may cost.</summary>
    private static readonly TimeSpan PageBudget = TimeSpan.FromMilliseconds(2 * 13.6);

    private const int Customers = 100_000;
    private const int Orders = 3_000_000;

    private const string Shop = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
        """;

    [Fact]
    public void APageOfAFilteredAndSortedViewCostsWhatAPageInFileOrderCosts()
    {
        var customers = new StringBuilder(Customers * 20);
        for (var number = 1; number <= Customers; number++)
        {
            customers.Append(string.Create(CultureInfo.InvariantCulture, $"K{number:0000000};Firma {number}\r\n"));
        }

        // Amounts that repeat, so the sort has ties to break by record number, and two thirds of
        // them above the filter's bound, so the view is large enough to scroll through.
        var orders = new StringBuilder(Orders * 26);
        for (var number = 1; number <= Orders; number++)
        {
            orders.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"N{number:0000000};K{(number % Customers) + 1:0000000};{number % 300},{number % 100:00}\r\n"));
        }

        using var harness = StoreHarness.Create(
            Shop,
            ("kunden.csv", customers.ToString()),
            ("bestellungen.csv", orders.ToString()));

        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        using (session)
        {
            session.Read(cancellationToken: TestContext.Current.CancellationToken);

            var table = session.DataSet.Tables.First(candidate =>
                string.Equals(candidate.Identity, "Bestellungen", StringComparison.Ordinal));

            var query = new TableQuery(
                [new ColumnFilter(2, FilterComparison.AtLeast, ["100"])],
                [new ColumnSort(2, Ordering.Descending)]);

            var built = Stopwatch.StartNew();
            var view = session.Build(table, query, 0, TestContext.Current.CancellationToken);
            built.Stop();

            var rows = view.Records;
            rows.Count.ShouldBeGreaterThan(1_000_000);

            // A scrollbar drag over the view: each step lands somewhere new, so each costs a page
            // that is not resident.
            var random = new Random(7);
            var steps = new List<TimeSpan>(200);
            for (var step = 0; step < 200; step++)
            {
                var index = random.Next(0, rows.Count);
                var clock = Stopwatch.StartNew();
                var row = rows[index];
                clock.Stop();

                row.Ordinal.ShouldBeGreaterThan(0);
                steps.Add(clock.Elapsed);
            }

            // The 95th percentile rather than the slowest step, for the reason given in
            // PagingBudgetTests: the slowest of 200 measures the machine.
            var usual = Measured.Percentile(steps, 0.95);
            usual.ShouldBeLessThan(
                PageBudget,
                $"95th percentile {usual.TotalMilliseconds:F1} ms, slowest {steps.Max().TotalMilliseconds:F1} ms,"
                + $" after a {built.ElapsedMilliseconds} ms build, over {rows.PageLoads} page loads");

            // Scrolling a view of a million records materialises a window of them, not the view:
            // the pages held at once are what the list was told to hold.
            rows.PeakResident.ShouldBeLessThanOrEqualTo(8 * 512);
        }
    }
}
