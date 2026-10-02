using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace GoBd.Reader.Ui.Tests;

public sealed class TableViewScrollTests : HeadlessTest
{
    [Fact]
    public Task AppThemeFixEnablesHorizontalScrollingAutomatically() => Ui(() =>
    {
        var tv = new TableView
        {
            ItemsSource = new[] { "Row 1", "Row 2", "Row 3" },
        };
        tv.Columns.Add(new TableViewColumn { Header = "Col 1", Width = new GridLength(500) });
        tv.Columns.Add(new TableViewColumn { Header = "Col 2", Width = new GridLength(500) });
        tv.Columns.Add(new TableViewColumn { Header = "Col 3", Width = new GridLength(500) });

        WithWindow(new Window { Content = tv, Width = 400, Height = 400 }, win =>
        {
            win.UpdateLayout();

            // If App has registered the theme fix, tv.Scroll will be populated automatically
            tv.Scroll.ShouldNotBeNull();

            var svFound = tv.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            svFound.ShouldNotBeNull();
            svFound.Extent.Width.ShouldBe(1500d);
            svFound.Viewport.Width.ShouldBeLessThan(1500d);

            var hsb = svFound.GetVisualDescendants().OfType<ScrollBar>().FirstOrDefault(s => s.Orientation == Orientation.Horizontal);
            hsb.ShouldNotBeNull();
            hsb.IsVisible.ShouldBeTrue();
        });
    });

    [Fact]
    public Task TheCorrectedThemeKeepsSemisLookAndColumnHeaders() => Ui(() =>
    {
        var tv = new TableView { ItemsSource = new[] { "Row 1" } };
        tv.Columns.Add(new TableViewColumn { Header = "Col 1", Width = new GridLength(100) });

        WithWindow(new Window { Content = tv, Width = 400, Height = 400 }, win =>
        {
            win.UpdateLayout();

            // Semi's border and corners, which a theme not based on Semi's left unset.
            tv.BorderThickness.ShouldNotBe(default);
            tv.CornerRadius.ShouldNotBe(default);

            // Semi's scroll viewer for the grid, which is what carries the column headers.
            tv.GetVisualDescendants().OfType<TableViewColumnHeadersPresenter>().ShouldNotBeEmpty();
        });
    });
}
