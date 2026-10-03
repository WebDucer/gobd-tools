using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// What shows where keyboard focus is, and the buttons that show only a symbol.
/// </summary>
/// <remarks>
/// Semi draws one indicator around whichever control has keyboard focus, in a colour of its own
/// that in light and dark falls short of the 3:1 a focus indicator needs. The reader points it at
/// its own focus colour there. See the improve-reader-accessibility change's design.md D16.
/// </remarks>
public sealed class FocusVisualsTests : HeadlessTest
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

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task SemisFocusIndicatorIsDrawnInTheReadersFocusColour(string appearance) => Ui(() =>
    {
        var variant = appearance == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        var application = Application.Current!;

        application.TryGetResource("AdornerLayerBorderBrush", variant, out var drawn).ShouldBeTrue();
        application.TryGetResource(ReaderTheme.Focus, variant, out var focus).ShouldBeTrue();
        drawn.ShouldBeSameAs(focus);
    });

    [Fact]
    public Task ButtonsThatShowOnlyASymbolKeepTheirThemesStates() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A;10,00\r\nB;20,00\r\n"));
        var window = Driver.Reader(harness);
        window.ShowTable(Driver.Table(window, "T"));

        // A chip to close as well as a tab: Betrag greater than 15.
        Driver.Filter(window, Driver.View(window, "T"), column: 1, comparison: 5, "15");
        Driver.Settle(window);

        var glyphs = window.GetVisualDescendants().OfType<Button>().Where(button => (button.Content as string) == "✕").ToArray();
        glyphs.Length.ShouldBe(2);
        foreach (var glyph in glyphs)
        {
            glyph.GetDiagnostic(TemplatedControl.BackgroundProperty).Priority.ShouldNotBe(BindingPriority.LocalValue);
            glyph.GetDiagnostic(TemplatedControl.BorderThicknessProperty).Priority.ShouldNotBe(BindingPriority.LocalValue);
            glyph.Opacity.ShouldBe(1);
            glyph.Theme.ShouldNotBeNull();
        }
    });
}
