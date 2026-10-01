using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Styling;

namespace GoBd.Reader.Ui;

/// <summary>
/// Semantic palette and theme tokens that resolve dynamically according to the current theme variant
/// (Light or Dark), satisfying WCAG AA contrast.
/// </summary>
internal static class ReaderTheme
{
    // Keys from Semi.Avalonia palette
    public const string Primary = "SemiColorPrimary";
    public const string PrimaryLight = "SemiColorPrimaryLight";
    public const string Success = "SemiColorSuccess";
    public const string Danger = "SemiColorDanger";
    public const string Border = "SemiColorBorder";
    public const string Background = "SemiColorBackground0";
    public const string BackgroundElevated = "SemiColorBackground1";

    /// <summary>The theme variant a preference asks the application for.</summary>
    public static ThemeVariant ToThemeVariant(this ThemePreference preference) => preference switch
    {
        ThemePreference.Light => ThemeVariant.Light,
        ThemePreference.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    /// <summary>The scroll viewer's settings the grid passes on to the one in its template.</summary>
    private static readonly AvaloniaProperty[] ScrollSettings =
    [
        ScrollViewer.HorizontalScrollBarVisibilityProperty,
        ScrollViewer.VerticalScrollBarVisibilityProperty,
        ScrollViewer.AllowAutoHideProperty,
        ScrollViewer.BringIntoViewOnFocusChangeProperty,
        ScrollViewer.IsScrollChainingEnabledProperty,
    ];

    /// <summary>
    /// Semi.Avalonia's TableView theme with its template corrected: Semi's names no
    /// <c>PART_ScrollViewer</c> and binds none of the scroll bar settings, so the grid never
    /// scrolled sideways.
    /// </summary>
    /// <remarks>
    /// Based on Semi's own theme, so its background, border and corner radius still apply and
    /// only the template and the scroll bars are this reader's.
    /// </remarks>
    /// <param name="semi">Semi's theme for the grid, or null when it supplies none.</param>
    public static ControlTheme CreateTableViewTheme(ControlTheme? semi)
    {
        return new ControlTheme(typeof(TableView))
        {
            BasedOn = semi,
            Setters =
            {
                new Setter(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto),
                new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto),
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<TableView>((control, scope) =>
                {
                    var presenter = new ItemsPresenter { Name = "PART_ItemsPresenter" }.RegisterInNameScope(scope);
                    presenter[!ItemsPresenter.ItemsPanelProperty] = control[!ItemsControl.ItemsPanelProperty];

                    var sv = new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter }.RegisterInNameScope(scope);
                    foreach (var property in ScrollSettings)
                    {
                        sv[!property] = control[!property];
                    }

                    // Semi's scroll viewer for the grid, which is what carries the column headers.
                    if (control.TryFindResource("TableViewScrollViewerTheme", out var svTheme) && svTheme is ControlTheme ct)
                    {
                        sv.Theme = ct;
                    }

                    var border = new Avalonia.Controls.Border { Child = sv };
                    border[!Avalonia.Controls.Border.BackgroundProperty] = control[!TemplatedControl.BackgroundProperty];
                    border[!Avalonia.Controls.Border.BorderBrushProperty] = control[!TemplatedControl.BorderBrushProperty];
                    border[!Avalonia.Controls.Border.BorderThicknessProperty] = control[!TemplatedControl.BorderThicknessProperty];
                    border[!Avalonia.Controls.Border.CornerRadiusProperty] = control[!TemplatedControl.CornerRadiusProperty];
                    return border;
                })),
            },
        };
    }
}
