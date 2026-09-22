using System.Reflection;
using Avalonia.Headless;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// A test whose body runs where controls may be touched: Avalonia's dispatcher thread.
/// </summary>
/// <remarks>
/// A control may only be built, laid out or clicked on the thread its application runs on, and
/// xUnit runs a test wherever it likes. The session started here owns that thread for the whole
/// assembly — starting one per test would stand up a windowing platform for each — and
/// <see cref="Ui"/> hands a test body to it.
/// <para>
/// This is what <c>Avalonia.Headless.XUnit</c> would otherwise provide. Its 12.1.2 release is
/// built against xUnit v3 3.2.2 and fails at discovery against 4.0.0, and one xUnit version
/// across the repository is worth more than an attribute.
/// </para>
/// </remarks>
public abstract class HeadlessTest
{
    /// <summary>
    /// The application every test in this assembly runs inside.
    /// </summary>
    /// <remarks>
    /// Started from <c>AvaloniaTestApplication</c>, which names the reader's own application, so
    /// the controls under test are templated by the theme the reader ships.
    /// </remarks>
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    /// <summary>Runs a test body on the dispatcher, and reports what it threw.</summary>
    protected static Task Ui(Action body) =>
        Session.Dispatch(body, TestContext.Current.CancellationToken);
}
