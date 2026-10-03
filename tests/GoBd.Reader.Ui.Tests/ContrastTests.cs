using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Semi.Avalonia;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Every colour the reader chooses, against every background it is drawn on, in every appearance.
/// </summary>
/// <remarks>
/// The spec once said the reader's colours met WCAG AA, and in the light theme two of them did
/// not. A ratio that is computed rather than claimed cannot drift that way: a colour added or
/// changed later fails here before anyone has to read it. The backgrounds are the ones Semi
/// actually resolves — the window, the grid, a selected and a hovered row, a card, a chip, an
/// editor's flyout — composited the way they are drawn. See the improve-reader-accessibility
/// change's design.md D2.
/// </remarks>
public sealed class ContrastTests : HeadlessTest
{
    /// <summary>What WCAG AA asks of text.</summary>
    private const double Text = 4.5;

    /// <summary>What WCAG AA asks of a focus indicator and of a control's edge.</summary>
    private const double NonText = 3.0;

    public static TheoryData<string> Appearances => ["Light", "Dark", "Aquatic", "Desert"];

    [Theory]
    [MemberData(nameof(Appearances))]
    public Task EveryReaderColourResolvesInEveryAppearance(string appearance) => Ui(() =>
    {
        var variant = Variant(appearance);
        foreach (var key in ReaderKeys())
        {
            Application.Current!.TryGetResource(key, variant, out var value).ShouldBeTrue($"{key} in {appearance}");
            value.ShouldBeAssignableTo<ISolidColorBrush>($"{key} in {appearance}");
        }
    });

    [Theory]
    [MemberData(nameof(Appearances))]
    public Task TextMeetsAaOnEveryBackgroundItIsDrawnOn(string appearance) => Ui(() =>
    {
        var surfaces = new Surfaces(Variant(appearance));
        var failures = new List<string>();

        void Check(string key, string on, Color background) =>
            Expect(failures, surfaces.Ratio(key, background), Text, $"{key} on {on}");

        Check(ReaderTheme.Conformant, "the window", surfaces.Window);
        Check(ReaderTheme.Defective, "the window", surfaces.Window);
        Check(ReaderTheme.Defective, "an editor", surfaces.Flyout);
        Check(ReaderTheme.MutedText, "the window", surfaces.Window);
        Check(ReaderTheme.MutedText, "a card", surfaces.Surface);
        Check(ReaderTheme.MutedText, "an editor", surfaces.Flyout);
        Check(ReaderTheme.Link, "the grid", surfaces.Grid);
        Check(ReaderTheme.Dangling, "the grid", surfaces.Grid);

        foreach (var (row, background) in surfaces.Rows)
        {
            Check(ReaderTheme.LinkOnSelection, row, background);
            Check(ReaderTheme.DanglingOnSelection, row, background);
        }

        failures.ShouldBeEmpty();
    });

    [Theory]
    [MemberData(nameof(Appearances))]
    public Task FocusIsVisibleAgainstWhatItIsDrawnOn(string appearance) => Ui(() =>
    {
        var surfaces = new Surfaces(Variant(appearance));
        var failures = new List<string>();

        Expect(failures, surfaces.Ratio(ReaderTheme.Focus, surfaces.Window), NonText, "focus on the window");
        Expect(failures, surfaces.Ratio(ReaderTheme.Focus, surfaces.Grid), NonText, "focus on the grid");

        // The indicator Semi actually draws around a focused control, which the reader points at
        // its own focus colour where Semi's is too pale.
        Expect(failures, surfaces.Ratio("AdornerLayerBorderBrush", surfaces.Window), NonText, "Semi's focus indicator on the window");

        failures.ShouldBeEmpty();
    });

    [Theory]
    [InlineData("Aquatic")]
    [InlineData("Desert")]
    public Task InHighContrastTheEdgesOfCardsAndChipsCanBeSeen(string appearance) => Ui(() =>
    {
        // In light and dark a card is told apart by its own background and its edge is a hairline.
        // High contrast draws cards, chips and the window in one colour, so there the edge is what
        // identifies them.
        var surfaces = new Surfaces(Variant(appearance));
        surfaces.Ratio(ReaderTheme.SurfaceBorder, surfaces.Surface).ShouldBeGreaterThanOrEqualTo(NonText);
        surfaces.Ratio(ReaderTheme.SurfaceBorder, surfaces.Chip).ShouldBeGreaterThanOrEqualTo(NonText);
    });

    [Fact]
    public void TheRatioIsTheOneWcagDefines()
    {
        Ratio(Colors.Black, Colors.White).ShouldBe(21, 0.01);
        Ratio(Colors.White, Colors.White).ShouldBe(1, 0.01);

        // Semi's own danger red, which the reader once used for a value that refers to nothing.
        Ratio(Color.Parse("#F93920"), Colors.White).ShouldBe(3.73, 0.01);
        Ratio(Color.Parse("#F93920"), Colors.White).ShouldBeLessThan(Text);
    }

    [Fact]
    public void ATranslucentColourIsMeasuredAsItIsDrawn()
    {
        // Text at 80 % over white is lighter than the text itself.
        var drawn = Composite(Color.Parse("#1C1F23"), 0.8, Colors.White);
        Ratio(drawn, Colors.White).ShouldBe(8.65, 0.02);
    }

    private static void Expect(List<string> failures, double ratio, double needed, string what)
    {
        if (ratio < needed)
        {
            failures.Add($"{what}: {ratio:F2}:1, needs {needed}:1");
        }
    }

    private static ThemeVariant Variant(string appearance) => appearance switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        "Aquatic" => SemiTheme.Aquatic,
        "Desert" => SemiTheme.Desert,
        _ => throw new ArgumentOutOfRangeException(nameof(appearance), appearance, null),
    };

    /// <summary>Every key the reader defines a colour for.</summary>
    private static IEnumerable<string> ReaderKeys() =>
        typeof(ReaderTheme)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.Name != nameof(ReaderTheme.MutedClass)
                && field.Name != nameof(ReaderTheme.LinkClass) && field.Name != nameof(ReaderTheme.DanglingClass))
            .Select(field => (string)field.GetRawConstantValue()!);

    /// <summary>The contrast ratio of two opaque colours, as WCAG 2 defines it.</summary>
    internal static double Ratio(Color a, Color b)
    {
        var (lighter, darker) = (Luminance(a), Luminance(b)) is var (x, y) && x > y ? (x, y) : (y, x);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color colour)
    {
        static double Channel(byte value)
        {
            var c = value / 255d;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));
    }

    /// <summary>A colour drawn at an opacity over another, as it reaches the eye.</summary>
    internal static Color Composite(Color colour, double opacity, Color under)
    {
        var alpha = colour.A / 255d * opacity;
        byte Mix(byte top, byte bottom) => (byte)Math.Round((top * alpha) + (bottom * (1 - alpha)));
        return Color.FromRgb(Mix(colour.R, under.R), Mix(colour.G, under.G), Mix(colour.B, under.B));
    }

    /// <summary>The backgrounds of one appearance, as they are drawn on top of one another.</summary>
    private sealed class Surfaces
    {
        private readonly ThemeVariant variant;

        public Surfaces(ThemeVariant variant)
        {
            this.variant = variant;
            Window = Over("WindowDefaultBackground", Colors.White);
            Grid = Over("TableViewBackground", Window);
            Surface = Over(ReaderTheme.Surface, Window);
            Chip = Over(ReaderTheme.Chip, Window);
            Flyout = Over("FlyoutBackground", Window);
            Rows =
            [
                ("a selected row", Over("TableViewRowBackgroundSelected", Grid)),
                ("a hovered row", Over("TableViewRowBackgroundPointerover", Grid)),
                ("a hovered selected row", Over("TableViewRowBackgroundSelectedPointerover", Grid)),
            ];
        }

        public Color Window { get; }

        public Color Grid { get; }

        public Color Surface { get; }

        public Color Chip { get; }

        public Color Flyout { get; }

        public IReadOnlyList<(string Row, Color Background)> Rows { get; }

        /// <summary>The ratio of a key's colour, drawn over a background, to that background.</summary>
        public double Ratio(string key, Color background) => ContrastTests.Ratio(Over(key, background), background);

        private Color Over(string key, Color under)
        {
            Application.Current!.TryGetResource(key, variant, out var value).ShouldBeTrue($"{key} in {variant}");
            var brush = value.ShouldBeAssignableTo<ISolidColorBrush>().ShouldNotBeNull();
            return Composite(brush.Color, brush.Opacity, under);
        }
    }
}
