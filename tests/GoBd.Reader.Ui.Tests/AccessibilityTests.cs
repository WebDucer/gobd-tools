using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Every control a person can reach with the keyboard tells assistive technology what it is.
/// </summary>
/// <remarks>
/// A sweep rather than a list: a control added later without a name fails here without anyone
/// having to remember to add it. Read from each control's automation peer, which is what a screen
/// reader asks, so a button named by its own text passes and a box named only by its placeholder
/// does not. See the improve-reader-accessibility change's design.md D15 and D19.
/// </remarks>
public sealed class AccessibilityTests : HeadlessTest
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

    [Theory]
    [InlineData(ReportLanguage.English)]
    [InlineData(ReportLanguage.German)]
    public Task EveryControlTheKeyboardReachesHasAName(ReportLanguage language) => Ui(() =>
    {
        using var harness = ExportHarness.Create(
            Tables,
            ("kontakte.csv", "K1\r\nK2\r\n"),
            ("rechnungen.csv", "R1;K1;10,00\r\nR2;K9;20,00\r\n"));
        using var preferences = new TemporaryPreferences();

        RestoringApplication(() =>
        {
            var window = Driver.Reader(harness, preferences.Store);
            window.ApplyLanguage(language);
            var unnamed = new List<string>();

            // The summary, with the navigator beside it.
            Sweep(window, "the summary", unnamed);

            // A table, with a filter in force so its chip and the way back are there too.
            window.ShowTable(Driver.Table(window, "Rechnungen"));
            var view = Driver.View(window, "Rechnungen");
            Driver.Filter(window, view, column: 2, comparison: 5, "15,00");
            Sweep(window, "a table", unnamed);

            // Each editor, and Go to Record, open in turn.
            foreach (var (open, what) in new (Action, string)[]
            {
                (view.OpenFilterEditor, "the filter editor"),
                (view.OpenSortEditor, "the sort editor"),
                (view.OpenFigureEditor, "the figure editor"),
                (() => view.AskForRecord(_ => true), "Go to Record"),
            })
            {
                open();
                Driver.Settle(window);
                Sweep(window, what, unnamed);
                Driver.Press(window, new KeyGesture(Key.Escape));
            }

            // The windows beside the reader.
            window.ShowSettings();
            window.ShowAbout();
            window.ShowShortcuts();
            foreach (var beside in window.OwnedWindows.ToArray())
            {
                Sweep(beside, beside.GetType().Name, unnamed);
                beside.Close();
            }

            // The navigator collapsed, with the control that brings it back.
            Driver.Press(window, ReaderKeys.ToggleNavigator);
            Sweep(window, "the collapsed navigator", unnamed);

            unnamed.ShouldBeEmpty();
        });
    });

    /// <summary>Notes every control the keyboard can reach that has no name.</summary>
    private static void Sweep(TopLevel window, string where, List<string> unnamed)
    {
        Driver.Settle(window);
        foreach (var control in window.GetVisualDescendants().OfType<Control>())
        {
            if (!control.Focusable || !control.IsEffectivelyVisible || !control.IsEffectivelyEnabled || control is Announcer)
            {
                continue;
            }

            // A part of a template that takes focus for its control, as a tree entry's border
            // does: assistive technology reads the control it belongs to.
            var named = TemplatedControl.GetIsTemplateFocusTarget(control) && control.TemplatedParent is Control owner ? owner : control;
            var name = ControlAutomationPeer.CreatePeerForElement(named).GetName();
            if (string.IsNullOrWhiteSpace(name))
            {
                unnamed.Add($"{control.GetType().Name} in {where}");
            }
        }
    }
}
