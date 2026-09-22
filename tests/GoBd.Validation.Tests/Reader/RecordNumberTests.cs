using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Model;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The two numbers a row carries: where it sits in what is shown, and which record it is.
/// </summary>
/// <remarks>
/// They are not the same number and must never be shown as one. A declaration that excludes a
/// header row already separates them before anyone filters anything: the first record a person
/// sees is the file's second. Sorting separates them further, and it is the file's number that a
/// finding cites and a person quotes.
/// </remarks>
public sealed class RecordNumberTests
{
    /// <summary>A table whose declaration excludes the first record, as a header row is excluded.</summary>
    private const string WithHeader = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <Range><From>2</From></Range>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
        """;

    private static StoreHarness Export() => StoreHarness.Create(
        WithHeader,
        ("t.csv", "Id;Firma\r\nK1;Meier\r\nK2;Lang\r\nK3;Wolf\r\n"));

    private static ReaderSession Read(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    private static TableNode Table(ReaderSession session) => session.DataSet.Tables.First();

    [Fact]
    public void TheFirstRecordShownIsTheFilesSecondWhenAHeaderRowIsExcluded()
    {
        using var harness = Export();
        using var session = Read(harness);
        var table = Table(session);

        var rows = session.Build(table, TableQuery.FileOrder, 0, TestContext.Current.CancellationToken).Records;

        rows.Count.ShouldBe(3);
        rows[0].Position.ShouldBe(1);
        rows[0].Ordinal.ShouldBe(2);
        rows[0].Values[0].ShouldBe("K1");

        rows[2].Position.ShouldBe(3);
        rows[2].Ordinal.ShouldBe(4);
    }

    [Fact]
    public void SortingRenumbersThePositionsAndLeavesTheRecordNumbersAlone()
    {
        using var harness = Export();
        using var session = Read(harness);
        var table = Table(session);

        var sorted = session.Build(
            table,
            new TableQuery([], [new ColumnSort(0, Ordering.Descending)]),
            0,
            TestContext.Current.CancellationToken).Records;

        sorted.Select(row => row.Position).ShouldBe([1, 2, 3]);
        sorted.Select(row => row.Ordinal).ShouldBe([4, 3, 2]);
        sorted[0].Values[0].ShouldBe("K3");
    }

    [Fact]
    public void AFilteredViewCountsFromOneAndStillNamesTheFilesRecords()
    {
        using var harness = Export();
        using var session = Read(harness);
        var table = Table(session);

        var filtered = session.Build(
            table,
            new TableQuery([new ColumnFilter(0, FilterComparison.OneOf, ["K1", "K3"])], []),
            0,
            TestContext.Current.CancellationToken).Records;

        filtered.Select(row => row.Position).ShouldBe([1, 2]);
        filtered.Select(row => row.Ordinal).ShouldBe([2, 4]);
    }
}
