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
    /// Each choice is read on its own: one the file leaves out, names a value this reader does not
    /// have, or holds in a form it cannot read, takes its default — the detected language rather
    /// than English — and every other choice is kept. Only a file that is not JSON at all gives the
    /// defaults throughout. Choices are read by name, and as the numbers earlier builds wrote; zoom
    /// and width are kept within the range the reader offers.
    /// </remarks>
    public UserPreferences Load()
    {
        var fallback = UserPreferences.Default;
        try
        {
            if (!File.Exists(FilePath))
            {
                return fallback;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return fallback;
            }

            var file = document.RootElement;
            return new UserPreferences(
                Choice(file, nameof(UserPreferences.Theme), fallback.Theme),
                Choice(file, nameof(UserPreferences.Language), fallback.Language),
                Math.Clamp(Number(file, nameof(UserPreferences.Zoom), fallback.Zoom), UserPreferences.ActualSize, UserPreferences.LargestZoom),
                Math.Clamp(Number(file, nameof(UserPreferences.NavigatorWidth), fallback.NavigatorWidth), UserPreferences.NarrowestNavigator, UserPreferences.WidestNavigator),
                Flag(file, nameof(UserPreferences.NavigatorCollapsed), fallback.NavigatorCollapsed));
        }
#pragma warning disable CA1031 // Fallback to defaults on any read/parse failure
        catch
#pragma warning restore CA1031
        {
            return fallback;
        }
    }

    /// <summary>A choice, by name or by the number an earlier build wrote, or its default.</summary>
    private static T Choice<T>(JsonElement file, string name, T fallback)
        where T : struct, Enum
    {
        if (!file.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            // By name only: Enum.TryParse would also take a number written as a string, which no
            // build wrote.
            var written = value.GetString();
            var known = Enum.GetNames<T>().FirstOrDefault(candidate => string.Equals(candidate, written, StringComparison.OrdinalIgnoreCase));
            return known is null ? fallback : Enum.Parse<T>(known);
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            var read = (T)Enum.ToObject(typeof(T), number);
            return Enum.IsDefined(read) ? read : fallback;
        }

        return fallback;
    }

    private static int Number(JsonElement file, string name, int fallback) =>
        file.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : fallback;

    private static double Number(JsonElement file, string name, double fallback) =>
        file.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? number
            : fallback;

    private static bool Flag(JsonElement file, string name, bool fallback) =>
        file.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

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

            var stored = new StoredPreferences(
                preferences.Theme,
                preferences.Language,
                preferences.Zoom,
                preferences.NavigatorWidth,
                preferences.NavigatorCollapsed);
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
