using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Baseline accessibility tests verifying that controls have accessible names for screen readers
/// and assistive technology.
/// </summary>
public sealed class AccessibilityTests : HeadlessTest
{
    private const string Tables = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
        """;

    private const string Records = "A;10,00\r\nB;20,00\r\n";

    [Fact]
    public Task InteractiveButtonsExposeAccessibleNames() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var (view, _) = Driver.Show(harness);

        var buttons = view.GetLogicalDescendants().OfType<Button>().ToArray();
        foreach (var button in buttons)
        {
            var name = AutomationProperties.GetName(button);
            // Every action button (Previous, Next, Reset, Adder) must have an accessible name
            name.ShouldNotBeNullOrWhiteSpace();
        }
    });
}
