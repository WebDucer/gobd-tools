using System.Text.Json.Serialization;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>Appearance theme options supported by the reader.</summary>
public enum ThemePreference
{
    /// <summary>Follows the operating system dark/light mode dynamically.</summary>
    System = 0,

    /// <summary>Forces Light appearance.</summary>
    Light = 1,

    /// <summary>Forces Dark appearance.</summary>
    Dark = 2,
}

/// <summary>
/// User preferences persisted across application launches.
/// </summary>
/// <param name="Theme">The selected appearance theme variant.</param>
/// <param name="Language">The selected display and reporting language.</param>
public sealed record UserPreferences(
    ThemePreference Theme = ThemePreference.System,
    ReportLanguage Language = ReportLanguage.English)
{
    /// <summary>
    /// Default preferences: System appearance and OS-detected language.
    /// </summary>
    public static UserPreferences Default => new(
        ThemePreference.System,
        LanguageResolver.Resolve(null).Language);
}

/// <summary>
/// The preferences as the file holds them: by name, so the file says what it means to whoever
/// opens it, and each optional, so a file that leaves one out is still read for what it does say.
/// </summary>
/// <param name="Theme">The appearance theme, or null when the file names none.</param>
/// <param name="Language">The display language, or null when the file names none.</param>
internal sealed record StoredPreferences(ThemePreference? Theme, ReportLanguage? Language);

/// <summary>
/// Trimming-safe JSON serialization context for user preferences.
/// </summary>
/// <remarks>
/// Enums are written by name. Numbers, which earlier builds wrote, are still read.
/// </remarks>
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true)]
[JsonSerializable(typeof(StoredPreferences))]
internal sealed partial class PreferencesJsonContext : JsonSerializerContext
{
}
