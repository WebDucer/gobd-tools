using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Semi.Avalonia;

namespace GoBd.Reader.Ui;

/// <summary>
/// The reader's own colours, defined for every appearance it offers, and the theme for its grid.
/// </summary>
/// <remarks>
/// The reader refers to these keys only, never to Semi's palette directly. Semi's colours come in
/// threes — a colour, its hover and its pressed state — that a single override would collapse into
/// one, several of its templates reach them through aliases a later override does not reach, and
/// its high contrast variants define none of the status colours at all. So the reader keeps a key
/// for each thing it marks, gives every key a value in every appearance, and a test checks each
/// value's contrast against the background it is drawn on. See the improve-reader-accessibility
/// change's design.md D1 and D2.
/// </remarks>
internal static class ReaderTheme
{
    /// <summary>A value that can be followed.</summary>
    public const string Link = "ReaderLink";

    /// <summary>A value that can be followed, in a row drawn in the selection or hover colour.</summary>
    public const string LinkOnSelection = "ReaderLinkOnSelection";

    /// <summary>A value that refers to nothing.</summary>
    public const string Dangling = "ReaderDangling";

    /// <summary>A value that refers to nothing, in a row drawn in the selection or hover colour.</summary>
    public const string DanglingOnSelection = "ReaderDanglingOnSelection";

    /// <summary>An export, a table or a value that conforms to its declaration.</summary>
    public const string Conformant = "ReaderConformant";

    /// <summary>Something that does not conform, or an input that was refused.</summary>
    public const string Defective = "ReaderDefective";

    /// <summary>Text that says less than the text around it, but must still be read.</summary>
    public const string MutedText = "ReaderMutedText";

    /// <summary>The background the window itself is drawn on.</summary>
    public const string Background = "ReaderBackground";

    /// <summary>A card or strip raised from the background.</summary>
    public const string Surface = "ReaderSurface";

    /// <summary>The edge of a card, a strip or a chip.</summary>
    public const string SurfaceBorder = "ReaderSurfaceBorder";

    /// <summary>A chip naming something in force.</summary>
    public const string Chip = "ReaderChip";

    /// <summary>The indicator around whichever control has keyboard focus.</summary>
    public const string Focus = "ReaderFocus";

    /// <summary>The class a text that says less carries.</summary>
    public const string MutedClass = "muted";

    /// <summary>The class a value that can be followed carries.</summary>
    public const string LinkClass = "link";

    /// <summary>The class a value that refers to nothing carries.</summary>
    public const string DanglingClass = "dangling";

    /// <summary>Semi's key for the focus indicator it draws around every focused control.</summary>
    private const string SemiFocusBorder = "AdornerLayerBorderBrush";

    /// <summary>
    /// Puts the reader's colours into the application's resources, one dictionary per appearance.
    /// </summary>
    /// <remarks>
    /// Light and dark values are the reader's own, chosen to reach 4.5:1 on every background they
    /// are drawn on, including a selected or hovered row. High contrast values are Semi's system
    /// colours for that variant, taken from Semi itself, so the two can never disagree. Semi's own
    /// focus indicator is pointed at the reader's focus colour in light and dark, where Semi's
    /// pale blue does not reach the 3:1 a focus indicator needs; its high contrast one already
    /// does.
    /// </remarks>
    /// <param name="resources">The application's resources.</param>
    /// <param name="semi">Semi's theme, to take the colours the reader shares with it from.</param>
    public static void AddColours(IResourceDictionary resources, IResourceNode semi)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(semi);

        var light = new ResourceDictionary
        {
            [Link] = Brush("#004FB3"),
            [LinkOnSelection] = Brush("#004FB3"),
            [Dangling] = Brush("#B2140C"),
            [DanglingOnSelection] = Brush("#B2140C"),
            [Conformant] = Brush("#25772F"),
            [Defective] = Brush("#B2140C"),
            [MutedText] = Brush("#1C1F23", 0.8),
            [Focus] = Brush("#0064FA"),
        };
        Share(light, semi, ThemeVariant.Light, Background, "SemiColorBackground0");
        Share(light, semi, ThemeVariant.Light, Surface, "SemiColorBackground1");
        Share(light, semi, ThemeVariant.Light, SurfaceBorder, "SemiColorBorder");
        Share(light, semi, ThemeVariant.Light, Chip, "SemiColorPrimaryLight");
        light[SemiFocusBorder] = light[Focus];

        var dark = new ResourceDictionary
        {
            [Link] = Brush("#7FC1FF"),
            [LinkOnSelection] = Brush("#A9D7FF"),
            [Dangling] = Brush("#FD9983"),
            [DanglingOnSelection] = Brush("#FDBEAC"),
            [Conformant] = Brush("#5DC264"),
            [Defective] = Brush("#FD9983"),
            [MutedText] = Brush("#F9F9F9", 0.8),
            [Focus] = Brush("#54A9FF"),
        };
        Share(dark, semi, ThemeVariant.Dark, Background, "SemiColorBackground0");
        Share(dark, semi, ThemeVariant.Dark, Surface, "SemiColorBackground1");
        Share(dark, semi, ThemeVariant.Dark, SurfaceBorder, "SemiColorBorder");
        Share(dark, semi, ThemeVariant.Dark, Chip, "SemiColorPrimaryLight");
        dark[SemiFocusBorder] = dark[Focus];

        resources.ThemeDictionaries[ThemeVariant.Light] = light;
        resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
        resources.ThemeDictionaries[SemiTheme.Aquatic] = HighContrast(semi, SemiTheme.Aquatic);
        resources.ThemeDictionaries[SemiTheme.Desert] = HighContrast(semi, SemiTheme.Desert);
    }

    /// <summary>
    /// The reader's colours in a high contrast variant: Semi's system colours, and no colour of
    /// their own for a status, which the wording, an underline or a strikethrough carries instead.
    /// </summary>
    private static ResourceDictionary HighContrast(IResourceNode semi, ThemeVariant variant)
    {
        var colours = new ResourceDictionary();
        Share(colours, semi, variant, Link, "SemiColorHotlight");
        Share(colours, semi, variant, LinkOnSelection, "SemiColorHighlightText");
        Share(colours, semi, variant, Dangling, "SemiColorWindowText");
        Share(colours, semi, variant, DanglingOnSelection, "SemiColorHighlightText");
        Share(colours, semi, variant, Conformant, "SemiColorWindowText");
        Share(colours, semi, variant, Defective, "SemiColorWindowText");
        Share(colours, semi, variant, MutedText, "SemiColorGrayText");
        Share(colours, semi, variant, Background, "SemiColorWindow");
        Share(colours, semi, variant, Surface, "SemiColorWindow");
        Share(colours, semi, variant, SurfaceBorder, "SemiColorWindowText");
        Share(colours, semi, variant, Chip, "SemiColorWindow");
        Share(colours, semi, variant, Focus, "SemiColorWindowText");
        return colours;
    }

    /// <summary>Gives a reader key the value Semi holds for one of its own in that appearance.</summary>
    private static void Share(ResourceDictionary into, IResourceNode semi, ThemeVariant variant, string key, string semiKey)
    {
        if (!semi.TryGetResource(semiKey, variant, out var value))
        {
            throw new InvalidOperationException($"Semi defines no '{semiKey}' for the {variant} appearance.");
        }

        into[key] = value;
    }

    private static SolidColorBrush Brush(string colour, double opacity = 1) =>
        new(Color.Parse(colour), opacity);

    /// <summary>
    /// The styles that give the reader's classes their colours: muted text, and the values that can
    /// be followed or refer to nothing.
    /// </summary>
    /// <remarks>
    /// A cell's colour comes from a class rather than from a value set on the cell. A value set on
    /// the cell outranks every style, so the selected row could never recolour it, and in high
    /// contrast a link drawn on the selection colour measured 1:1 — invisible. The row's hover
    /// counts as a selection here, because high contrast draws both the same. See the
    /// improve-reader-accessibility change's design.md D1.
    /// </remarks>
    public static Styles CreateStyles()
    {
        return
        [
            Coloured(selector => selector.Is<TextBlock>().Class(MutedClass), MutedText),
            Coloured(selector => selector.OfType<TextBlock>().Class(LinkClass), Link),
            Coloured(selector => selector.OfType<TextBlock>().Class(DanglingClass), Dangling),
            Coloured(selector => selector.OfType<TableViewRow>().Class(":selected").Descendant().OfType<TextBlock>().Class(LinkClass), LinkOnSelection),
            Coloured(selector => selector.OfType<TableViewRow>().Class(":pointerover").Descendant().OfType<TextBlock>().Class(LinkClass), LinkOnSelection),
            Coloured(selector => selector.OfType<TableViewRow>().Class(":selected").Descendant().OfType<TextBlock>().Class(DanglingClass), DanglingOnSelection),
            Coloured(selector => selector.OfType<TableViewRow>().Class(":pointerover").Descendant().OfType<TextBlock>().Class(DanglingClass), DanglingOnSelection),
        ];
    }

    private static Style Coloured(Func<Selector?, Selector> selector, string key) => new(selector)
    {
        Setters = { new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension(key)) },
    };

    /// <summary>
    /// Draws an entry of a menu that pops up at the reader's zoom level.
    /// </summary>
    /// <remarks>
    /// A popup opens outside the window that zooms, and a menu's presenter cannot be wrapped in the
    /// zoom without copying Semi's template, so its entries take a text size scaled by the zoom
    /// level instead. See the improve-reader-accessibility change's design.md D5.
    /// </remarks>
    public static void Zoom(MenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var regular = Application.Current?.TryGetResource("SemiFontSizeRegular", null, out var size) == true && size is double value ? value : 14;
        item.FontSize = regular * ZoomLevel.Current.Scale;
    }

    /// <summary>
    /// A button that shows only a symbol, such as a tab's or a chip's close button.
    /// </summary>
    /// <remarks>
    /// Semi's borderless theme rather than a transparent background and no border set on the
    /// button: values set on the button outrank the theme's own states, so it kept neither its
    /// hover nor its pressed look. The symbol is muted rather than made translucent, which kept it
    /// below the contrast a control needs.
    /// </remarks>
    public static Button Glyph(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        button[!StyledElement.ThemeProperty] = new DynamicResourceExtension("BorderlessButton");
        button[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension(MutedText);
        return button;
    }

    /// <summary>The theme variant a preference asks the application for.</summary>
    /// <remarks>
    /// An explicit choice is taken as it stands. System follows the platform: Avalonia applies its
    /// light or dark preference by itself, but not its high contrast setting, so when the platform
    /// reports high contrast the reader asks for Semi's high contrast variant that is light or dark
    /// as the platform is. Two of Semi's four, because the platform says only that high contrast is
    /// on and whether it is light, not which of Windows' schemes is in use. See the
    /// improve-reader-accessibility change's design.md D3.
    /// </remarks>
    /// <param name="preference">What the person chose.</param>
    /// <param name="contrast">Whether the platform reports high contrast.</param>
    /// <param name="platform">Whether the platform is light or dark.</param>
    public static ThemeVariant Resolve(ThemePreference preference, ColorContrastPreference contrast, PlatformThemeVariant platform) => preference switch
    {
        ThemePreference.Light => ThemeVariant.Light,
        ThemePreference.Dark => ThemeVariant.Dark,
        ThemePreference.HighContrastDark => SemiTheme.Aquatic,
        ThemePreference.HighContrastLight => SemiTheme.Desert,
        _ when contrast == ColorContrastPreference.High =>
            platform == PlatformThemeVariant.Light ? SemiTheme.Desert : SemiTheme.Aquatic,
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
