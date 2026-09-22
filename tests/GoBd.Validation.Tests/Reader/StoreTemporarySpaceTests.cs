using System.Globalization;
using System.Text;
using GoBd.Reader.Data;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Where the store spills when a statement outgrows memory.
/// </summary>
/// <remarks>
/// Sorting a table of millions is what filtering and sorting a view costs, and beyond the
/// engine's memory limit it goes to disk. That disk must be the store's own directory: it is the
/// volume the space check measured before importing, and it is the directory that is removed when
/// the export is closed. An auditor's medium must not leave a copy of itself somewhere else.
/// </remarks>
[Collection(typeof(Measured))]
public sealed class StoreTemporarySpaceTests
{
    private const string Table = """
            <Table>
              <URL>buchungen.csv</URL>
              <Name>Buchungen</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Text</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private const int Records = 1_000_000;

    private static string Rows(int count)
    {
        var builder = new StringBuilder(count * 110);
        for (var number = 1; number <= count; number++)
        {
            // Wide and barely repeating, so that a hundred megabytes of values have to be sorted
            // and the engine cannot hold them under the limit set below.
            builder.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"B{number:0000000};{number * 7919 % 1000000:000000} Beleg für Vorgang {number:0000000} "));
            builder.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"Kostenstelle {number % 997:000}\r\n"));
        }

        return builder.ToString();
    }

    [Fact]
    public async Task TheStoreSpillsInsideItsOwnDirectoryAndTakesItWithItWhenClosed()
    {
        using var harness = StoreHarness.Create(Table, ("buchungen.csv", Rows(Records)));
        var store = harness.Store();
        store.Import(harness.Table("Buchungen"));

        var temporary = string.Empty;
        store.Query("SELECT current_setting('temp_directory')", row => temporary = row.GetString(0));

        // Where the engine says it will spill, before it is asked to.
        temporary.ShouldStartWith(store.Directory);

        // Room to work in, but far less than the values being sorted occupy.
        store.Query("SET memory_limit = '100MB'", _ => { });

        // The directory is the engine's to create and to clean up, so it is watched while the
        // sort runs rather than looked for afterwards.
        using var sorting = new CancellationTokenSource();
        var spilled = false;
        var watch = Task.Run(
            async () =>
            {
                while (!sorting.Token.IsCancellationRequested)
                {
                    spilled |= Directory.Exists(temporary);
                    await Task.Delay(5, CancellationToken.None);
                }
            },
            CancellationToken.None);

        // What building a view costs: every record ordered by a key and numbered.
        store.Query(
            "CREATE OR REPLACE TABLE v_spill AS SELECT row_number() OVER (ORDER BY c1, _ordinal) - 1 AS pos,"
            + " _ordinal, c0, c1 FROM " + store.NameOf(harness.Table("Buchungen")),
            _ => { });

        sorting.Cancel();
        await watch;

        var sorted = 0L;
        store.Query("SELECT count(*) FROM v_spill", row => sorted = row.GetInt64(0));
        sorted.ShouldBe(Records);
        (spilled || Directory.Exists(temporary))
            .ShouldBeTrue("the engine spilled somewhere other than its own directory");

        var directory = store.Directory;
        store.Dispose();

        Directory.Exists(directory).ShouldBeFalse();
    }
}
