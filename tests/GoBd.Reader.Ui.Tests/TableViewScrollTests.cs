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

    [Fact]
    public Task AtTwiceTheSizeTheGridStillRealisesOnlyTheRowsItShows() => Ui(() => RestoringApplication(() =>
    {
        // Ten thousand rows of ten columns: realising them all would show at once in the count.
        var rows = Enumerable.Range(1, 10_000).Select(row => $"Row {row}").ToArray();

        int Realised(int percent, out TimeSpan scrolling)
        {
            ZoomLevel.Current.Set(percent);
            var tv = new TableView { ItemsSource = rows };
            for (var column = 0; column < 10; column++)
            {
                tv.Columns.Add(new TableViewColumn { Header = $"Col {column}", Width = new GridLength(120) });
            }

            var window = new Window { Content = new Zoomed(tv), Width = 1000, Height = 700 };
            window.Show();
            try
            {
                window.UpdateLayout();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (var page = 0; page < 50; page++)
                {
                    tv.ScrollIntoView(page * 200);
                    window.UpdateLayout();
                }

                scrolling = clock.Elapsed;
                return tv.GetVisualDescendants().OfType<TableViewRow>().Count();
            }
            finally
            {
                window.Close();
            }
        }

        var actual = Realised(100, out var atActual);
        var doubled = Realised(200, out var atDouble);
        TestContext.Current.SendDiagnosticMessage($"Rows realised: {actual} at 100 %, {doubled} at 200 %. Fifty jumps: {atActual.TotalMilliseconds:F0} ms at 100 %, {atDouble.TotalMilliseconds:F0} ms at 200 %.");

        doubled.ShouldBeLessThan(60);
        doubled.ShouldBeLessThanOrEqualTo(actual);
    }));
}
