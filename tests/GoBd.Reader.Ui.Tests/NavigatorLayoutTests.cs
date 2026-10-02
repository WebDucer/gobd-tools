using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The navigator's width, which a person adjusts, and the navigator collapsed to a strip.
/// </summary>
/// <remarks>
/// Driven from the keyboard, the way someone who cannot drag a boundary adjusts it. See the
/// improve-reader-accessibility change's design.md D6.
/// </remarks>
public sealed class NavigatorLayoutTests : HeadlessTest
{
    private const string Tables = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

    [Fact]
    public Task TheArrowKeysMoveTheBoundaryWithinItsBoundsAndTheWidthIsKept() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            Driver.Settle(window);
            var splitter = Splitter(window);
            AutomationProperties.GetName(splitter).ShouldBe("Navigator width");
            splitter.Focus(NavigationMethod.Tab);

            Driver.Press(window, new KeyGesture(Key.Right));
            Width(window).ShouldBe(310);
            Columns(window)[2].Width.IsStar.ShouldBeTrue("the tables take whatever the navigator leaves");
            preferences.Store.Load().NavigatorWidth.ShouldBe(310);

            for (var press = 0; press < 30; press++)
            {
                Driver.Press(window, new KeyGesture(Key.Left));
            }

            Width(window).ShouldBe(UserPreferences.NarrowestNavigator);

            for (var press = 0; press < 80; press++)
            {
                Driver.Press(window, new KeyGesture(Key.Right));
            }

            Width(window).ShouldBeLessThanOrEqualTo(window.Bounds.Width / 2);
        }));
    });

    [Fact]
    public Task AWindowOpensWithTheWidthAndTheStateKept() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        preferences.Store.Save(UserPreferences.Default with { NavigatorWidth = 420 });
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
            Width(window).ShouldBe(420)));

        preferences.Store.Save(UserPreferences.Default with { NavigatorCollapsed = true });
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
            Navigator(window).IsEffectivelyVisible.ShouldBeFalse()));
    });

    [Fact]
    public Task TheNavigatorCollapsesToAStripAndComesBackAtItsWidth() => Ui(() =>
    {
        using var preferences = new TemporaryPreferences();
        preferences.Store.Save(UserPreferences.Default with { NavigatorWidth = 350 });
        RestoringApplication(() => WithWindow(new MainWindow(null, null, preferences.Store), window =>
        {
            Driver.Press(window, ReaderKeys.ToggleNavigator);
            Navigator(window).IsEffectivelyVisible.ShouldBeFalse();
            Splitter(window).IsEffectivelyVisible.ShouldBeFalse();
            ShowItem(window).IsChecked.ShouldBeFalse();
            preferences.Store.Load().NavigatorCollapsed.ShouldBeTrue();

            // Zooming leaves the person's choice alone.
            Driver.Press(window, ReaderKeys.ZoomIn);
            Navigator(window).IsEffectivelyVisible.ShouldBeFalse();

            // The strip's own control brings it back, at the width it had.
            var show = window.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == "Show Navigator");
            show.IsEffectivelyVisible.ShouldBeTrue();
            Driver.Click(show);
            Driver.Settle(window);

            Navigator(window).IsEffectivelyVisible.ShouldBeTrue();
            Width(window).ShouldBe(350);
            ShowItem(window).IsChecked.ShouldBeTrue();
            preferences.Store.Load().NavigatorCollapsed.ShouldBeFalse();
        }));
    });

    [Fact]
    public Task CollapsingTheNavigatorItsFocusIsOnMovesFocusToTheTabInFront() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"));
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() =>
        {
            var window = Driver.Reader(harness, preferences.Store);
            Driver.Settle(window);
            var entry = Navigator(window).GetVisualDescendants().OfType<TreeViewItem>().First();
            entry.Focus(NavigationMethod.Tab).ShouldBeTrue();

            Driver.Press(window, ReaderKeys.ToggleNavigator);

            var focused = Driver.Focused(window).ShouldBeAssignableTo<Visual>().ShouldNotBeNull();
            Navigator(window).IsVisualAncestorOf(focused).ShouldBeFalse();
            focused.IsEffectivelyVisible.ShouldBeTrue();
        });
    });

    [Fact]
    public Task ACollapsedNavigatorStillFollowsTheTableInView() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"));
        using var preferences = new TemporaryPreferences();
        RestoringApplication(() =>
        {
            var window = Driver.Reader(harness, preferences.Store);
            Driver.Press(window, ReaderKeys.ToggleNavigator);

            window.ShowTable(Driver.Table(window, "T"));
            Driver.Press(window, ReaderKeys.ToggleNavigator);

            (Navigator(window).SelectedItem as TreeViewItem).ShouldNotBeNull().Header.ShouldBe("T");
        });
    });

    private static TreeView Navigator(MainWindow window) => window.GetVisualDescendants().OfType<TreeView>().Single();

    private static GridSplitter Splitter(MainWindow window) => window.GetVisualDescendants().OfType<GridSplitter>().Single();

    private static ColumnDefinitions Columns(MainWindow window) =>
        Splitter(window).GetVisualParent().ShouldBeOfType<Grid>().ColumnDefinitions;

    private static double Width(MainWindow window)
    {
        Driver.Settle(window);
        return Columns(window)[0].ActualWidth;
    }

    private static NativeMenuItem ShowItem(MainWindow window) =>
        NativeMenu.GetMenu(window).ShouldNotBeNull().Items.OfType<NativeMenuItem>()
            .Single(item => Driver.Plain(item.Header) == "View").Menu.ShouldNotBeNull()
            .Items.OfType<NativeMenuItem>().Single(item => Driver.Plain(item.Header) == "Show Navigator");
}
