using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.Controls;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// What a screen reader is told: each record's name, and the messages announced without moving
/// focus.
/// </summary>
/// <remarks>
/// Read from the record rows' automation names and from the window's announcer, which is where a
/// screen reader reads them. See the improve-reader-accessibility change's design.md D15.
/// </remarks>
public sealed class AnnouncementTests : HeadlessTest
{
    private const string Tables = """
                <Table>
                  <URL>kontakte.csv</URL>
                  <Name>Kontakte</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nr</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
                <Table>
                  <URL>rechnungen.csv</URL>
                  <Name>Rechnungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Nr</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Kunde</Name><AlphaNumeric/></VariableColumn>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                    <ForeignKey><Name>Kunde</Name><References>Kontakte</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private static ExportHarness Export() => ExportHarness.Create(
        Tables,
        ("kontakte.csv", "K1\r\nK2\r\n"),
        ("rechnungen.csv", "R1;K1;10,00\r\nR2;K1;20,00\r\nR3;K9;30,00\r\n"));

    [Fact]
    public Task ARecordIsNamedByItsNumberAndItsValues() => Ui(() =>
    {
        using var harness = Export();
        var (view, _) = Driver.Show(harness, "Rechnungen");

        RecordName(view, 1).ShouldBe("Record 1: Nr R1, Kunde K1, Betrag 10,00");
        RecordName(view, 3).ShouldBe("Record 3: Nr R3, Kunde K9 (refers to nothing), Betrag 30,00");

        view.SetLanguage(ReportLanguage.German);
        RecordName(view, 3).ShouldBe("Datensatz 3: Nr R3, Kunde K9 (verweist auf nichts), Betrag 30,00");
    });

    [Fact]
    public Task ARowReusedForAnotherRecordTakesThatRecordsName() => Ui(() =>
    {
        var rows = string.Concat(Enumerable.Range(1, 400).Select(row => $"R{row};K1;1,00\r\n"));
        using var harness = ExportHarness.Create(Tables, ("kontakte.csv", "K1\r\n"), ("rechnungen.csv", rows));
        var (view, _) = Driver.Show(harness, "Rechnungen");
        var grid = view.GetVisualDescendants().OfType<TableView>().Single();

        grid.ScrollIntoView(350);
        view.UpdateLayout();
        grid.ScrollIntoView(10);
        view.UpdateLayout();

        var realised = view.GetVisualDescendants().OfType<TableViewRow>().Where(row => row.DataContext is RecordRow).ToArray();
        realised.ShouldNotBeEmpty();
        foreach (var row in realised)
        {
            var record = (RecordRow)row.DataContext!;
            AutomationProperties.GetName(row).ShouldStartWith($"Record {record.Ordinal}: Nr R{record.Ordinal},");
        }
    });

    [Fact]
    public Task ReadingIsAnnouncedInTenthsTablesAsTheyFinishAndTheOutcomeOnce() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);

        // The last of the progress reaches the window through its dispatcher, after the reading.
        Driver.Settle(window);
        var said = window.Announcer.Said.Select(entry => entry.Text).ToArray();
        said.ShouldContain("'Kontakte': read.", string.Join(" | ", said));
        said.ShouldContain("'Rechnungen': read.");
        said.Count(text => text.StartsWith("Conformant", StringComparison.Ordinal) || text.StartsWith("Not conformant", StringComparison.Ordinal)).ShouldBe(1);
        window.Announcer.Said.ShouldAllBe(entry => !entry.Assertive);
    });

    [Fact]
    public void ProgressIsNotAnnouncedAtEveryUpdate()
    {
        var announcements = new ReadingAnnouncements();
        var report = new ValidationReport("export", [], strict: false, contentsExamined: false);
        var said = new List<string>();

        // A hundred updates, as the display receives them while an export is read.
        for (var read = 0; read < 100; read++)
        {
            said.AddRange(announcements.Next(new ExportReading([], read, 100, report, KeysChecked: false, Complete: false), ReportLanguage.English));
        }

        said.ShouldBe([.. Enumerable.Range(1, 9).Select(tenth => $"Export {tenth * 10}% read.")]);

        var done = new ExportReading([], 100, 100, report, KeysChecked: true, Complete: true);
        announcements.Next(done, ReportLanguage.English).ShouldBe(["Conformant. 0 table(s) read. 0 error(s), 0 warning(s)."]);
        announcements.Next(done, ReportLanguage.English).ShouldBeEmpty();
    }

    [Fact]
    public Task WhereANavigationLandedIsAnnouncedWithoutMovingFocus() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "Kontakte"));
        Driver.Settle(window);
        Driver.View(window, "Kontakte").FocusRecords();
        Driver.Settle(window);

        // K1's referring invoices: the walk's position is announced, and focus is on the record.
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Settle(window);
        window.Announcer.Said[^1].Text.ShouldBe("Record 1 of 2 referring to 'K1'.");
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);

        Driver.Press(window, ReaderKeys.NextReferring);
        window.Announcer.Said[^1].Text.ShouldBe("Record 2 of 2 referring to 'K1'.");

        // A value that refers to nothing, followed from its actions.
        Driver.Press(window, new KeyGesture(Key.Down));
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Settle(window);
        window.Announcer.Said[^1].Text.ShouldBe("'K9' matches no record of 'Kontakte'. The reference does not resolve.");
    });

    [Fact]
    public Task AFilterThatGaveWayIsAnnouncedWithTheRecordReached() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "Rechnungen"));
        Driver.Settle(window);
        var view = Driver.View(window, "Rechnungen");
        Driver.Filter(window, view, column: 2, comparison: 3, "15,00");

        Driver.Press(window, ReaderKeys.GoToRecord);
        Driver.Focused(window).ShouldBeOfType<TextBox>().Text = "3";
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Until(window, () => window.Announcer.Said.Any(entry => entry.Text.Contains("Filter removed", StringComparison.Ordinal)));

        window.Announcer.Said[^1].Text.ShouldBe("Record 3. Filter removed to show record 3.");
    });

    [Fact]
    public Task AnEditorsRefusalIsAnnouncedAtOnce() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "Rechnungen"));
        Driver.Settle(window);

        var editor = Driver.Editor(Driver.View(window, "Rechnungen"), "Filters");
        Driver.Choose(editor, 0, 2);
        Driver.Choose(editor, 1, 1);
        Driver.Type(editor, 0, "ungefähr hundert");
        Driver.Apply(editor);

        var refusal = window.Announcer.Said[^1];
        refusal.Assertive.ShouldBeTrue();
        refusal.Text.ShouldStartWith("'ungefähr hundert' is not");
    });

    [Fact]
    public Task ATableStillBeingReadIsAnnouncedOnce() => Ui(() =>
    {
        using var harness = Export();
        var session = harness.Open();
        var table = ExportHarness.Table(session, "Kontakte");
        var said = new List<string>();
        var view = new TableTabView(session, new ReaderTabs(session).Open(table), (_, _, _) => { }, (_, _, _, _) => { }, _ => { })
        {
            Announce = (text, _) => said.Add(text),
        };

        view.Refresh();
        view.Refresh();

        said.ShouldBe(["This table is still being read. It will be shown as soon as it has been."]);
    });

    private static string? RecordName(TableTabView view, long ordinal)
    {
        view.UpdateLayout();
        var row = view.GetVisualDescendants().OfType<TableViewRow>().First(candidate => candidate.DataContext is RecordRow record && record.Ordinal == ordinal);
        return AutomationProperties.GetName(row);
    }
}
