using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Semi.Avalonia;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// Which appearance the reader asks for, from what the person chose and what the platform reports.
/// </summary>
/// <remarks>
/// System follows the platform's high contrast setting as well as its light or dark one, which
/// Avalonia does not do by itself. See the improve-reader-accessibility change's design.md D3.
/// </remarks>
public sealed class ThemeResolutionTests : HeadlessTest
{
    [Theory]
    [InlineData(PlatformThemeVariant.Dark, "Aquatic")]
    [InlineData(PlatformThemeVariant.Light, "Desert")]
    public void SystemFollowsThePlatformsHighContrast(PlatformThemeVariant platform, string expected) =>
        ReaderTheme.Resolve(ThemePreference.System, ColorContrastPreference.High, platform).Key.ShouldBe(expected);

    [Theory]
    [InlineData(PlatformThemeVariant.Dark)]
    [InlineData(PlatformThemeVariant.Light)]
    public void WithoutHighContrastSystemLeavesLightAndDarkToThePlatform(PlatformThemeVariant platform) =>
        ReaderTheme.Resolve(ThemePreference.System, ColorContrastPreference.NoPreference, platform).ShouldBe(ThemeVariant.Default);

    [Theory]
    [InlineData(ThemePreference.Light, "Light")]
    [InlineData(ThemePreference.Dark, "Dark")]
    [InlineData(ThemePreference.HighContrastDark, "Aquatic")]
    [InlineData(ThemePreference.HighContrastLight, "Desert")]
    public void AnExplicitChoiceIgnoresThePlatform(ThemePreference preference, string expected)
    {
        foreach (var contrast in new[] { ColorContrastPreference.NoPreference, ColorContrastPreference.High })
        {
            foreach (var platform in new[] { PlatformThemeVariant.Light, PlatformThemeVariant.Dark })
            {
                ReaderTheme.Resolve(preference, contrast, platform).Key.ShouldBe(expected);
            }
        }
    }

    [Theory]
    [InlineData("Aquatic")]
    [InlineData("Desert")]
    public Task SemiResolvesItsHighContrastColoursWithoutFurtherIncludes(string appearance) => Ui(() =>
    {
        var variant = appearance == "Aquatic" ? SemiTheme.Aquatic : SemiTheme.Desert;
        foreach (var key in new[] { "SemiColorWindow", "SemiColorWindowText", "SemiColorHotlight", "SemiColorHighlight", "SemiColorHighlightText", "SemiColorGrayText" })
        {
            Application.Current!.TryGetResource(key, variant, out _).ShouldBeTrue($"{key} in {appearance}");
        }
    });
}
