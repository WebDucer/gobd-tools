using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using GoBd.Reader.Ui.Controls;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// A table's records from the keyboard: the record's actions, focus that follows a navigation,
/// scrolling sideways, and copying a record.
/// </summary>
/// <remarks>
/// Following a reference is the reader's central act, and it once needed a pointer aimed at a
/// cell. See the improve-reader-accessibility change's design.md D8, D10, D13 and D14.
/// </remarks>
public sealed class RecordKeysTests : HeadlessTest
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
                    <ForeignKey><Name>Kunde</Name><References>Kontakte</References></ForeignKey>
                  </VariableLength>
                </Table>
        """;

    private static ExportHarness Export() =>
        ExportHarness.Create(Tables, ("kontakte.csv", "K1\r\nK2\r\n"), ("rechnungen.csv", "R1;K2\r\nR2;K9\r\n"));

    [Fact]
    public Task EnterOffersTheRecordsActionsAndFollowingFocusesTheRecordReached() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        Driver.Press(window, new KeyGesture(Key.Enter));
        Entries(window).ShouldBe(["Follow Kunde → 'Kontakte'", "Copy Record"]);
        Driver.Focused(window).ShouldBeOfType<MenuItem>().Header.ShouldBe("Follow Kunde → 'Kontakte'");

        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Settle(window);

        Driver.FrontTitle(window).ShouldBe("Kontakte");
        Driver.FocusIsIn(window, Driver.View(window, "Kontakte"));
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(2);
    });

    [Fact]
    public Task TheContextKeyOffersTheSameActions() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Kontakte");

        // The context-menu key the platform's text fields and lists answer, here as on a record.
        var contextKey = Application.Current!.PlatformSettings.ShouldNotBeNull().HotkeyConfiguration.OpenContextMenu[0];
        Driver.Press(window, contextKey);

        Entries(window).ShouldBe(["Records in 'Rechnungen' referring to this", "Copy Record"]);
    });

    [Fact]
    public Task AKeyWhoseValueRefersToNothingSaysSoBeforeItIsFollowed() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        Driver.Press(window, new KeyGesture(Key.Down));
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(2);

        Driver.Press(window, new KeyGesture(Key.Enter));
        Entries(window)[0].ShouldBe("Follow Kunde → 'Kontakte' (refers to nothing)");
    });

    [Fact]
    public Task DismissingTheActionsGivesFocusBackToTheRecord() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Press(window, new KeyGesture(Key.Escape));

        window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().ShouldBeEmpty();
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);
    });

    [Fact]
    public Task ClickingAValueStillFollowsItDirectly() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        // Released on the value itself. The headless platform does not hit-test text, so a pointer
        // aimed at it lands on the row behind it; the release is what the grid acts on.
        var value = Driver.View(window, "Rechnungen").GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == "K2");
        var at = value.TranslatePoint(new Point(2, value.Bounds.Height / 2), window).ShouldNotBeNull();
        value.RaiseEvent(new PointerReleasedEventArgs(
            value,
            new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true),
            window,
            at,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left)
        {
            RoutedEvent = InputElement.PointerReleasedEvent,
        });
        Driver.Settle(window);

        window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().ShouldBeEmpty();
        Driver.FrontTitle(window).ShouldBe("Kontakte");
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(2);
    });

    [Fact]
    public Task LeftAndRightScrollTheRecordsSideways() => Ui(() =>
    {
        var columns = string.Concat(Enumerable.Range(1, 12).Select(column => $"<VariableColumn><Name>Spalte{column}</Name><AlphaNumeric/></VariableColumn>"));
        var wide = $"""
                <Table>
                  <URL>w.csv</URL>
                  <Name>W</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    {columns}
                  </VariableLength>
                </Table>
        """;
        var values = string.Join(';', Enumerable.Range(0, 13).Select(column => $"v{column}"));
        using var harness = ExportHarness.Create(wide, ("w.csv", values + "\r\n" + values + "\r\n"));
        var window = Records(harness, "W");
        var grid = Driver.View(window, "W").GetVisualDescendants().OfType<TableView>().Single();
        var scroll = grid.Scroll.ShouldNotBeNull();
        grid.SelectedIndex.ShouldBe(0);

        Driver.Press(window, new KeyGesture(Key.Right));
        Driver.Press(window, new KeyGesture(Key.Right));
        scroll.Offset.X.ShouldBe(2 * TableTabView.SidewaysStep);
        grid.SelectedIndex.ShouldBe(0);
        Driver.FocusedRecord(window).ShouldNotBeNull().Ordinal.ShouldBe(1);

        Driver.Press(window, new KeyGesture(Key.Left));
        scroll.Offset.X.ShouldBe(TableTabView.SidewaysStep);
    });

    [Fact]
    public Task CopyPutsTheRecordOnTheClipboardAsASpreadsheetPastesIt() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        Driver.Press(window, ReaderKeys.CopyRecord);
        Clipboard(window).ShouldBe($"Record\tNr\tKunde{Environment.NewLine}1\tR1\tK2");

        // From the record's actions too.
        Driver.Press(window, new KeyGesture(Key.Down));
        Driver.Press(window, new KeyGesture(Key.Enter));
        Driver.Press(window, new KeyGesture(Key.Down));
        Driver.Focused(window).ShouldBeOfType<MenuItem>().Header.ShouldBe("Copy Record");
        Driver.Press(window, new KeyGesture(Key.Enter));
        Clipboard(window).ShouldBe($"Record\tNr\tKunde{Environment.NewLine}2\tR2\tK9");
    });

    [Fact]
    public Task CopyInsideATextFieldStillCopiesTheText() => Ui(() =>
    {
        using var harness = Export();
        var window = Records(harness, "Rechnungen");

        var editor = Driver.Editor(Driver.View(window, "Rechnungen"), "Filters");
        Driver.Settle(window);
        var field = editor.OfType<TextBox>().First();
        field.Text = "typed";
        field.Focus();
        field.SelectAll();
        Driver.Settle(window);

        var copy = Application.Current!.PlatformSettings.ShouldNotBeNull().HotkeyConfiguration.Copy[0];
        Driver.Press(window, copy);
        Clipboard(window).ShouldBe("typed");
    });

    [Fact]
    public void AValueThatWouldBreakTheLinesIsQuoted()
    {
        RecordCopy.Cell("plain").ShouldBe("plain");
        RecordCopy.Cell("a\tb").ShouldBe("\"a\tb\"");
        RecordCopy.Cell("two\nlines").ShouldBe("\"two\nlines\"");
        RecordCopy.Cell("say \"so\"").ShouldBe("\"say \"\"so\"\"\"");

        var row = new RecordRow(1, 7, ["x\ty", "\"q\""], []);
        RecordCopy.Text("Record", ["A", "B"], row).ShouldBe($"Record\tA\tB{Environment.NewLine}7\t\"x\ty\"\t\"\"\"q\"\"\"");
    }

    /// <summary>The reader with a table's records in front and focused, as the keyboard leaves them.</summary>
    private static MainWindow Records(ExportHarness harness, string identity)
    {
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, identity));
        Driver.Settle(window);
        Driver.View(window, identity).FocusRecords();
        Driver.Settle(window);
        Driver.FocusedRecord(window).ShouldNotBeNull();
        return window;
    }

    /// <summary>The entries of the record actions shown over the window.</summary>
    private static string[] Entries(MainWindow window) =>
        [.. window.GetVisualDescendants().OfType<MenuFlyoutPresenter>().Single()
            .GetVisualDescendants().OfType<MenuItem>().Select(item => (string)item.Header!)];

    private static string? Clipboard(TopLevel window)
    {
        var reading = window.Clipboard.ShouldNotBeNull().TryGetTextAsync();
        Driver.Wait(reading);
        return reading.Result;
    }
}
