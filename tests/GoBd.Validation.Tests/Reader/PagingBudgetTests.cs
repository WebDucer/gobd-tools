using System.Diagnostics;
using System.Globalization;
using System.Text;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// What a page costs once every foreign key value on it is checked as well as shown.
/// </summary>
/// <remarks>
/// Marking a dangling value is an anti-join per key per page. That is the one part of D7 that
/// could have been too expensive to do at all, so it is measured on the export the reader's
/// paging was measured on: three million records, where dragging the scrollbar cost 13.6 ms a
/// step before any of this existed.
/// </remarks>
[Collection(typeof(Measured))]
public sealed class PagingBudgetTests
{
    /// <summary>The scrollbar step measured in the finished reader, and what a page may now cost.</summary>
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
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    [Fact]
    public void APageOfTheThreeMillionRecordExportStillCostsLessThanAScrollbarStep()
    {
        var customers = new StringBuilder(Customers * 20);
        for (var number = 1; number <= Customers; number++)
        {
            customers.Append(string.Create(CultureInfo.InvariantCulture, $"K{number:0000000};Firma {number}\r\n"));
        }

        // Every third order refers to a customer that is not in the export, so the anti-join has
        // something to find on every page rather than being able to stop early.
        var orders = new StringBuilder(Orders * 26);
        for (var number = 1; number <= Orders; number++)
        {
            var customer = number % 3 == 0 ? Customers + number : (number % Customers) + 1;
            orders.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"N{number:0000000};K{customer:0000000};31.12.2024\r\n"));
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
            var rows = session.View(table).Rows.ShouldNotBeNull();
            rows.Count.ShouldBe(Orders);

            // A scrollbar drag: each step lands somewhere new, so each costs a page that is not
            // resident, fetched and marked.
            var random = new Random(7);
            var steps = new List<TimeSpan>(200);
            var marked = 0;
            for (var step = 0; step < 200; step++)
            {
                var index = random.Next(0, Orders);
                var clock = Stopwatch.StartNew();
                var row = rows[index];
                clock.Stop();

                if (row.RefersToNothing(1))
                {
                    marked++;
                }

                steps.Add(clock.Elapsed);
            }

            marked.ShouldBeGreaterThan(0);

            // Judged by where the steps fall rather than by the one that fell furthest: on a
            // machine that is also running the reader's test assembly, the slowest of 200 steps
            // measures a garbage collection or a descheduled thread. A page that has actually
            // become expensive moves nearly every step, and with it the 95th percentile.
            var usual = Measured.Percentile(steps, 0.95);
            usual.ShouldBeLessThan(
                PageBudget,
                $"95th percentile {usual.TotalMilliseconds:F1} ms, slowest {steps.Max().TotalMilliseconds:F1} ms,"
                + $" over {rows.PageLoads} page loads");
        }
    }
}
