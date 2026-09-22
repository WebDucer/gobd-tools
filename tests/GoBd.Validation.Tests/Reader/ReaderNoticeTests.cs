using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// What the reader says it cannot do, as distinct from what is wrong with the export.
/// </summary>
/// <remarks>
/// A number too long to compute with exactly and a time column holding something that is not a
/// time are both within the standard. The validator reports neither, so neither may appear among
/// the findings: the summary would then carry a claim about the export that the validator would
/// contradict.
/// </remarks>
public sealed class ReaderNoticeTests
{
    private const string Tables = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Betrag</Name><Numeric/></VariableColumn>
                    <VariableColumn>
                      <Name>Zeit</Name><AlphaNumeric/>
                      <Map><From>HHMMSS</From><To>HHMMSS</To></Map>
                    </VariableColumn>
                  </VariableLength>
                </Table>
        """;

    private const string Widest = "123456789012345678901234567890123456789";

    private static ReaderSession Read(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    [Fact]
    public void AColumnTheReaderCannotComputeWithIsStatedAsItsOwnLimit()
    {
        using var harness = StoreHarness.Create(
            Tables,
            ("t.csv", $"A;{Widest};081500\r\nB;12,50;keine Zeit\r\n"));
        using var session = Read(harness);

        var reading = session.Reading;

        // The export is within the standard, so there is nothing wrong with it to report.
        reading.Report.Findings.ShouldBeEmpty();
        session.View(session.DataSet.Tables.First()).Kind.ShouldBe(TableViewKind.Data);

        var notices = reading.Notices;
        notices.Count.ShouldBe(2);

        var amount = notices.First(notice => notice.Column == "Betrag");
        amount.Text.ShouldContain("no filter, sort or figure");
        amount.Text.ShouldContain("still shown");

        var time = notices.First(notice => notice.Column == "Zeit");
        time.Text.ShouldContain("1 of its values");
        time.Text.ShouldContain("absent");
    }

    [Fact]
    public void AnExportTheReaderCanWorkWithEntirelyStatesNoLimits()
    {
        using var harness = StoreHarness.Create(Tables, ("t.csv", "A;12,50;081500\r\nB;3,00;235959\r\n"));
        using var session = Read(harness);

        session.Reading.Notices.ShouldBeEmpty();
    }

    [Fact]
    public void ATableThatIsWithheldStatesNoLimitsAboutItsColumns()
    {
        // Its data is not shown at all, so what could be filtered in it is not the question.
        const string Broken = """
                    <Table>
                      <URL>t.csv</URL>
                      <Name>T</Name>
                      <VariableLength>
                        <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                        <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                      </VariableLength>
                    </Table>
            """;

        using var harness = StoreHarness.Create(Broken, ("t.csv", "A;keine Zahl\r\n"));
        using var session = Read(harness);

        session.Reading.Report.Findings.ShouldNotBeEmpty();
        session.Reading.Notices.ShouldBeEmpty();
    }
}
