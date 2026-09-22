using Avalonia;
using Avalonia.Headless;
using GoBd.Reader.Ui;
using GoBd.Reader.Ui.Tests;

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
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
