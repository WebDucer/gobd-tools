using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using GoBd.Reader.Ui.Controls;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Back and Forward through navigations, and going to a record by its number.
/// </summary>
/// <remarks>
/// See the improve-reader-accessibility change's design.md D11 and D12.
/// </remarks>
public sealed class HistoryAndGoToTests : HeadlessTest
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

    private const int Betrag = 2;
    private const int LessThan = 3;

    private static ExportHarness Export() => ExportHarness.Create(
        Tables,
        ("kontakte.csv", "K1\r\nK2\r\n"),
        ("rechnungen.csv", "R1;K1;10,00\r\nR2;K1;20,00\r\nR3;K2;30,00\r\n"));

    [Fact]
    public Task BackReturnsToTheRecordANavigationStartedFromAndForwardRepeatsIt() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");
        Driver.Press(window, new KeyGesture(Key.Down));
        Driver.Press(window, new KeyGesture(Key.Down));
        FollowKunde(window);
        Driver.FrontTitle(window).ShouldBe("Kontakte");
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(2);

        Driver.Press(window, ReaderKeys.Back);
        Driver.FrontTitle(window).ShouldBe("Rechnungen");
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(3);
        Command(window, "Back").IsEnabled.ShouldBeFalse();
        Command(window, "Forward").IsEnabled.ShouldBeTrue();

        Driver.Press(window, ReaderKeys.Forward);
        Driver.FrontTitle(window).ShouldBe("Kontakte");
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(2);
        Command(window, "Forward").IsEnabled.ShouldBeFalse();
    });

    [Fact]
    public Task BackIntoATableWhoseTabWasClosedOpensItAgain() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");
        FollowKunde(window);

        Driver.Press(window, ReaderKeys.TabAt(2));
        Driver.FrontTitle(window).ShouldBe("Rechnungen");
        Driver.Press(window, ReaderKeys.CloseTab);
        Driver.TabTitles(window).ShouldBe(["Summary", "Kontakte"]);

        Driver.Press(window, ReaderKeys.Back);
        Driver.FrontTitle(window).ShouldBe("Rechnungen");
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);
    });

    [Fact]
    public Task BackIntoAViewWhoseFilterHidesTheRecordLiftsTheFilter() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");
        Driver.Press(window, new KeyGesture(Key.Down));
        Driver.Press(window, new KeyGesture(Key.Down));
        FollowKunde(window);

        // Back in Rechnungen, a filter that hides R3; then over to Kontakte again.
        Driver.Press(window, ReaderKeys.TabAt(2));
        var view = Driver.View(window, "Rechnungen");
        Driver.Filter(window, view, Betrag, LessThan, "15,00");
        Driver.Banner(view).ShouldBe("Showing 1 of 3 records");
        Driver.Press(window, ReaderKeys.TabAt(3));

        Driver.Press(window, ReaderKeys.Back);
        Driver.Until(window, () => view.InFileOrder && Driver.FocusedRecord(window)?.Ordinal == 3);
        Driver.FrontTitle(window).ShouldBe("Rechnungen");
        Driver.Texts(view).ShouldContain("Filter removed to show record 3.");
    });

    [Fact]
    public Task AnotherExportStartsANewHistory() => Ui(() =>
    {
        using var harness = Export();
        using var other = Export();
        var window = Records(harness, "Rechnungen");
        FollowKunde(window);
        Command(window, "Back").IsEnabled.ShouldBeTrue();

        window.OpenExport(other.ExportPath);
        Driver.Wait(window.Reading);
        Driver.Settle(window);

        Command(window, "Back").IsEnabled.ShouldBeFalse();
        Command(window, "Forward").IsEnabled.ShouldBeFalse();
    });

    [Fact]
    public Task GoToRecordPositionsAndFocusesTheRecordAndIsRememberedForBack() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        Driver.Press(window, ReaderKeys.GoToRecord);
        var field = Driver.Focused(window).ShouldBeOfType<TextBox>();
        field.Text = "3";
        Driver.Press(window, new KeyGesture(Key.Enter));

        window.GetVisualDescendants().OfType<FlyoutPresenter>().ShouldBeEmpty();
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(3);
        Driver.Texts(Driver.View(window, "Rechnungen")).ShouldContain("Record 3.");

        Driver.Press(window, ReaderKeys.Back);
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);
    });

    [Fact]
    public Task ANumberTheTableDoesNotHoldIsSaidWhereItWasTyped() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        Driver.Press(window, ReaderKeys.GoToRecord);
        Driver.Focused(window).ShouldBeOfType<TextBox>().Text = "9";
        Driver.Press(window, new KeyGesture(Key.Enter));

        var editor = window.GetVisualDescendants().OfType<FlyoutPresenter>().Single();
        Driver.Texts(editor).ShouldContain("'9' is not a record number of this table. It holds records 1 to 3.");
        Driver.View(window, "Rechnungen").CurrentOrdinal.ShouldBe(1);
    });

    [Fact]
    public Task GoingToARecordAFilterHidesLiftsTheFilter() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");
        var view = Driver.View(window, "Rechnungen");
        Driver.Filter(window, view, Betrag, LessThan, "15,00");

        Driver.Press(window, ReaderKeys.GoToRecord);
        Driver.Focused(window).ShouldBeOfType<TextBox>().Text = "3";
        Driver.Press(window, new KeyGesture(Key.Enter));

        Driver.Until(window, () => view.InFileOrder && Driver.FocusedRecord(window)?.Ordinal == 3);
        Driver.Texts(view).ShouldContain("Filter removed to show record 3.");
    });

    [Fact]
    public Task GoingToARecordIsUnavailableOnTheSummary() => Ui(() =>
    {
        using var harness = Export();
        var window = Driver.Reader(harness);
        Command(window, "Go to Record…").IsEnabled.ShouldBeFalse();
    });

    [Theory]
    [InlineData("1.234", ReportLanguage.German, 1234L)]
    [InlineData("1,234", ReportLanguage.English, 1234L)]
    [InlineData(" 17 ", ReportLanguage.English, 17L)]
    [InlineData("1,5", ReportLanguage.German, null)]
    [InlineData("12a", ReportLanguage.English, null)]
    [InlineData("-3", ReportLanguage.English, null)]
    public void ARecordNumberIsReadAsTheLanguageWritesNumbers(string typed, ReportLanguage language, long? expected) =>
        TableTabView.ReadRecordNumber(typed.Trim(), language).ShouldBe(expected);

    /// <summary>The reader with a table's records in front and focused.</summary>
    private static MainWindow Records(ExportHarness harness, string identity)
    {
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, identity));
        Driver.Settle(window);
        Driver.View(window, identity).FocusRecords();
        Driver.Settle(window);
        return window;
    }

    /// <summary>Follows the focused invoice's customer, from its actions.</summary>
    private static void FollowKunde(MainWindow window)
    {
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Focused(window).ShouldBeOfType<MenuItem>().Header.ShouldBe("Follow Kunde → 'Kontakte'");
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Settle(window);
    }

    private static NativeMenuItem Command(MainWindow window, string entry) =>
        NativeMenu.GetMenu(window).ShouldNotBeNull().Items.OfType<NativeMenuItem>()
            .Single(item => Driver.Plain(item.Header) == "Go").Menu.ShouldNotBeNull()
            .Items.OfType<NativeMenuItem>().Single(item => Driver.Plain(item.Header) == entry);
}
