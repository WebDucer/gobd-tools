using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using GoBd.Reader.Ui.Controls;

namespace GoBd.Reader.Ui.Tests;

public sealed class StartPageViewScrollTests : HeadlessTest
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
    public Task StartPageViewScrollsWhenHeightIsConstrained() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", Records));
        var session = harness.Read();
        var page = new StartPageView();
        page.Show(session.Reading, harness.ExportPath);

        var win = new Window { Content = page, Width = 500, Height = 200 };
        harness.Track(win);
        win.Show();
        win.UpdateLayout();

        page.Extent.Height.ShouldBeGreaterThan(page.Viewport.Height);

        var vsb = page.GetVisualDescendants().OfType<ScrollBar>().FirstOrDefault(s => s.Orientation == Avalonia.Layout.Orientation.Vertical);
        vsb.ShouldNotBeNull();
        vsb.IsVisible.ShouldBeTrue();
    });
}
