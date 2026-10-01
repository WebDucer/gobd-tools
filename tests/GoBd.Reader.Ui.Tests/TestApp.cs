using Avalonia;
using Avalonia.Headless;
using GoBd.Reader.Ui;
using GoBd.Reader.Ui.Tests;
using GoBd.Validation.Localisation;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The reader's own application, on a windowing platform that draws nothing.
/// </summary>
/// <remarks>
/// The real <see cref="App"/> rather than a stand-in, so the controls are templated by the theme
/// the reader actually ships: a button that never got its template would answer differently to a
/// click than the one a person meets. It creates no window here — it only opens one under a
/// desktop lifetime, and a headless session provides none.
/// </remarks>
public static class TestApp
{
    /// <summary>Builds the application every headless test runs inside.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        // The tests read what the reader says in English. Without this the language would be the
        // machine's, and a German one would fail every test that looks for an English text.
        Environment.SetEnvironmentVariable(LanguageResolver.EnvironmentVariable, "en");
        PreferencesStore.DefaultPathResolver = TemporaryPreferences.NewPath;
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
