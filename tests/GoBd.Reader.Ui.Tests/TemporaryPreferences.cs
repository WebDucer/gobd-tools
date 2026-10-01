namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// A preferences file of a test's own, so no test reads or writes the person's real settings,
/// and removed when the test is done with it.
/// </summary>
internal sealed class TemporaryPreferences : IDisposable
{
    /// <summary>A store whose file no other test uses.</summary>
    public PreferencesStore Store { get; } = new(NewPath());

    /// <summary>A path in the temporary location that nothing else names.</summary>
    public static string NewPath() =>
        Path.Combine(Path.GetTempPath(), "gobd-test-" + Guid.NewGuid().ToString("N") + ".json");

    /// <inheritdoc />
    public void Dispose() => File.Delete(Store.FilePath);
}
