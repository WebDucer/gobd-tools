using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The summary the start page presents: the verdict, every table's state, and what was found,
/// grouped by the table it concerns.
/// </summary>
/// <remarks>
/// It is not built beside the pipeline but out of it — the same value the window reads progress
/// from — so that what a table's readiness says and what the summary says cannot disagree.
/// </remarks>
public sealed class ExportSummaryTests
{
    /// <summary>
    /// One clean table, one whose records do not conform, and one whose reference does not
    /// resolve. Three states worth telling apart in one summary.
    /// </summary>
    private const string Tables = """
                <Table>
                  <URL>kunden.csv</URL>
                  <Name>Kunden</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>buchungen.csv</URL>
                  <Name>Buchungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>bestellungen.csv</URL>
                  <Name>Bestellungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nummer</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kunden</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private static StoreHarness Export() => StoreHarness.Create(
        Tables,
        ("kunden.csv", "K1;Meier\r\nK2;Lang\r\n"),
        ("buchungen.csv", "B1;nicht-ein-datum\r\nB2;01.01.2025\r\n"),
        ("bestellungen.csv", "N1;K1\r\nN2;K9\r\n"));

    private static ReaderSession Read(StoreHarness harness)
    {
        var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);
        return session;
    }

    private static TableReading Of(ExportReading reading, string identity) =>
        reading.Tables.Single(table => string.Equals(table.Table.Identity, identity, StringComparison.Ordinal));

    // ---- 4.1 the verdict, the tables, and the findings grouped by table -----------------------

    [Fact]
    public void TheSummaryReportsTheVerdictEveryTableAndWhatWasFoundAboutIt()
    {
        using var harness = Export();
        using var session = Read(harness);
        var summary = session.Reading;

        summary.Complete.ShouldBeTrue();
        summary.Verdict.ShouldBe(Verdict.NonConformant);

        var clean = Of(summary, "Kunden");
        clean.State.ShouldBe(TableReadingState.Ready);
        clean.Records.ShouldBe(2);
        clean.Findings.ShouldBeEmpty();

        var defective = Of(summary, "Buchungen");
        defective.State.ShouldBe(TableReadingState.Defective);
        defective.Findings.Select(finding => finding.Code)
            .ShouldBe([FindingCodes.RecordValueTypeMismatch]);

        // A reference that does not resolve is a finding about the table that carries it, and
        // that table still shows its data. See design.md D8.
        var dangling = Of(summary, "Bestellungen");
        dangling.State.ShouldBe(TableReadingState.Ready);
        dangling.Records.ShouldBe(2);
        dangling.Findings.Select(finding => finding.Code)
            .ShouldBe([FindingCodes.ForeignKeyValueUnresolved]);

        // Every finding in the summary belongs to one of the tables it groups, and no table's
        // findings were invented for it.
        summary.Report.Findings.Length.ShouldBe(summary.Tables.Sum(table => table.Findings.Count));
    }

    // ---- 4.2 the same codes and messages as the validator --------------------------------------

    [Fact]
    public void TheSummarysFindingsAreTheOnesTheValidatorReportsForTheSameExport()
    {
        using var harness = Export();
        using var session = Read(harness);

        var validator = ExportValidator.Validate(
            harness.ExportPath,
            contents: new ContentOptions(ContentOptions.DefaultMaximumFindingsPerTable));

        Rendered(session.Reading.Report.Findings).ShouldBe(Rendered(validator.Findings));
        session.Reading.Verdict.ShouldBe(validator.Verdict);
    }

    [Fact]
    public void TheSummaryAgreesWithTheValidatorOnAConformantExport()
    {
        using var harness = StoreHarness.Create(
            Tables,
            ("kunden.csv", "K1;Meier\r\nK2;Lang\r\n"),
            ("buchungen.csv", "B1;31.12.2024\r\n"),
            ("bestellungen.csv", "N1;K1\r\n"));
        using var session = Read(harness);

        var validator = ExportValidator.Validate(
            harness.ExportPath,
            contents: new ContentOptions(ContentOptions.DefaultMaximumFindingsPerTable));

        Rendered(session.Reading.Report.Findings).ShouldBeEmpty();
        Rendered(validator.Findings).ShouldBeEmpty();
        session.Reading.Verdict.ShouldBe(Verdict.Conformant);
    }

    // ---- 4.3 the summary while tables are still being read -------------------------------------

    [Fact]
    public void BeforeAnythingIsReadEveryTableIsWaitingAndNothingIsComplete()
    {
        using var harness = Export();
        using var session = ReaderSession.Open(harness.ExportPath, harness.Options).Session.ShouldNotBeNull();

        var summary = session.Reading;

        summary.Complete.ShouldBeFalse();
        summary.KeysChecked.ShouldBeFalse();
        summary.Current.ShouldBeNull();
        summary.Tables.Select(table => table.State)
            .ShouldAllBe(state => state == TableReadingState.Waiting);
        summary.BytesRead.ShouldBe(0);
        summary.Bytes.ShouldBeGreaterThan(0);
        summary.Fraction.ShouldBe(0);
    }

    [Fact]
    public async Task TheSummaryShowsWhichTablesAreFinishedWhichIsBeingReadAndWhichAreWaiting()
    {
        using var harness = Export();
        using var source = new HoldingExportSource(new FolderExportSource(harness.ExportPath))
        {
            Held = "buchungen.csv",
        };
        using var session = ReaderSession.Open(
            source,
            harness.ExportPath,
            harness.Options,
            ContentOptions.DefaultMaximumFindingsPerTable,
            [])
            .Session.ShouldNotBeNull();

        var reading = Task.Run(() => session.Read(), TestContext.Current.CancellationToken);
        try
        {
            Until(() => source.Reached);

            var partial = session.Reading;
            Of(partial, "Kunden").State.ShouldBe(TableReadingState.Ready);
            Of(partial, "Buchungen").State.ShouldBe(TableReadingState.Reading);
            Of(partial, "Bestellungen").State.ShouldBe(TableReadingState.Waiting);
            partial.Complete.ShouldBeFalse();
            partial.Current.ShouldNotBeNull().Table.Identity.ShouldBe("Buchungen");
        }
        finally
        {
            source.Release();
            await reading;
        }

        // And it completes itself as they finish, without being asked again.
        var summary = session.Reading;
        summary.Complete.ShouldBeTrue();
        summary.Tables.ShouldAllBe(table => table.IsFinished);
        Of(summary, "Bestellungen").State.ShouldBe(TableReadingState.Ready);
    }

    private static IReadOnlyList<string> Rendered(IEnumerable<Finding> findings) =>
        [.. findings
            .Select(finding => finding.Code + "  " + MessageCatalogue.Render(finding, ReportLanguage.English))
            .Order(StringComparer.Ordinal)];

    private static void Until(Func<bool> condition)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            Thread.Sleep(1);
        }

        condition().ShouldBeTrue("the worker never reached the state this test waits for");
    }
}
