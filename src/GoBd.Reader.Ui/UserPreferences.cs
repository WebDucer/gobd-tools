using System.Text.Json.Serialization;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>Appearance theme options supported by the reader.</summary>
/// <remarks>
/// The numbers are what earlier builds wrote, before choices were written by name, and are still
/// read; a new choice takes the next number.
/// </remarks>
public enum ThemePreference
{
    /// <summary>Follows the operating system: light or dark, and its high contrast setting.</summary>
    System = 0,

    /// <summary>Forces Light appearance.</summary>
    Light = 1,

    /// <summary>Forces Dark appearance.</summary>
    Dark = 2,

    /// <summary>Forces high contrast, light text on a dark background.</summary>
    HighContrastDark = 3,

    /// <summary>Forces high contrast, dark text on a light background.</summary>
    HighContrastLight = 4,
}

/// <summary>
/// User preferences persisted across application launches.
/// </summary>
/// <param name="Theme">The selected appearance theme variant.</param>
/// <param name="Language">The selected display and reporting language.</param>
/// <param name="Zoom">How large the interface is drawn, in percent of its actual size.</param>
/// <param name="NavigatorWidth">The navigator's width, at actual size.</param>
/// <param name="NavigatorCollapsed">Whether the navigator is collapsed to a narrow strip.</param>
public sealed record UserPreferences(
    ThemePreference Theme = ThemePreference.System,
    ReportLanguage Language = ReportLanguage.English,
    int Zoom = UserPreferences.ActualSize,
    double NavigatorWidth = UserPreferences.DefaultNavigatorWidth,
    bool NavigatorCollapsed = false)
{
    /// <summary>The zoom level at which the interface is drawn at its actual size.</summary>
    public const int ActualSize = 100;

    /// <summary>The largest zoom level, twice the actual size.</summary>
    public const int LargestZoom = 200;

    /// <summary>The navigator's width until a person changes it.</summary>
    public const double DefaultNavigatorWidth = 300;

    /// <summary>The narrowest the navigator can be made and still name a table.</summary>
    public const double NarrowestNavigator = 160;

    /// <summary>
    /// The widest width the preferences keep; the window bounds it further, to half its own width.
    /// </summary>
    public const double WidestNavigator = 1600;

    /// <summary>
    /// Default preferences: System appearance, OS-detected language, actual size and an expanded
    /// navigator of its default width.
    /// </summary>
    public static UserPreferences Default => new(
        ThemePreference.System,
        LanguageResolver.Resolve(null).Language);
}

/// <summary>
/// The preferences as the file holds them: every choice by name, so the file says what it means to
/// whoever opens it.
/// </summary>
/// <remarks>
/// Only written. Reading takes the file apart field by field instead, so that one value a reader
/// does not know — an appearance a later version added — costs that one choice and not every
/// other. See the improve-reader-accessibility change's design.md D4.
/// </remarks>
internal sealed record StoredPreferences(
    ThemePreference Theme,
    ReportLanguage Language,
    int Zoom,
    double NavigatorWidth,
    bool NavigatorCollapsed);

/// <summary>
/// Trimming-safe JSON serialization context for user preferences.
/// </summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true)]
[JsonSerializable(typeof(StoredPreferences))]
internal sealed partial class PreferencesJsonContext : JsonSerializerContext
{
}
