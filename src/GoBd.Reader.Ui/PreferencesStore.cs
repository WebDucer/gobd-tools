using System.Text.Json;

namespace GoBd.Reader.Ui;

/// <summary>
/// Loads and saves user preferences to the platform's standard configuration directory.
/// </summary>
/// <remarks>
/// Never touches the export medium, and isolates any I/O or security failures gracefully so the
/// application remains functional even in restricted or read-only environments.
/// </remarks>
public sealed class PreferencesStore
{
    /// <summary>Allows test hosts to isolate preferences across tests.</summary>
    internal static Func<string>? DefaultPathResolver { get; set; }

    /// <summary>Creates a store using the standard platform configuration path.</summary>
    public PreferencesStore()
        : this(DefaultPathResolver?.Invoke() ?? ResolveDefaultPath())
    {
    }

    /// <summary>Creates a store using the given file path, for testing.</summary>
    public PreferencesStore(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        FilePath = filePath;
    }

    /// <summary>The file path this store reads and writes.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Loads the stored preferences, or returns default preferences if missing or unreadable.
    /// </summary>
    /// <remarks>
    /// Each choice is checked on its own: one the file leaves out, or names a value this reader
    /// does not have, takes its default — the detected language rather than English — and the
    /// other choice is kept.
    /// </remarks>
    public UserPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return UserPreferences.Default;
            }

            var stored = JsonSerializer.Deserialize(File.ReadAllText(FilePath), PreferencesJsonContext.Default.StoredPreferences);
            var fallback = UserPreferences.Default;
            return new UserPreferences(
                stored?.Theme is { } theme && Enum.IsDefined(theme) ? theme : fallback.Theme,
                stored?.Language is { } language && Enum.IsDefined(language) ? language : fallback.Language);
        }
#pragma warning disable CA1031 // Fallback to defaults on any read/parse failure
        catch
#pragma warning restore CA1031
        {
            return UserPreferences.Default;
        }
    }

    /// <summary>
    /// Saves the given preferences, suppressing any I/O or permission failures silently.
    /// </summary>
    public void Save(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        try
        {
            if (Path.GetDirectoryName(FilePath) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            var stored = new StoredPreferences(preferences.Theme, preferences.Language);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(stored, PreferencesJsonContext.Default.StoredPreferences));
        }
#pragma warning disable CA1031 // Safe suppression of write failures
        catch
#pragma warning restore CA1031
        {
            // Retained in-memory during the session; nothing thrown.
        }
    }

    /// <summary>Resolves the standard platform user configuration file path.</summary>
    /// <remarks>
    /// The roaming application data folder on Windows, <c>~/Library/Application Support</c> on
    /// macOS, and <c>$XDG_CONFIG_HOME</c> (or <c>~/.config</c>) elsewhere — which is what .NET
    /// resolves <see cref="Environment.SpecialFolder.ApplicationData"/> to on each.
    /// </remarks>
    public static string ResolveDefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gobd-reader",
            "preferences.json");
}
