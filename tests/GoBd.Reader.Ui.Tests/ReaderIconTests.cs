using Avalonia.Platform;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The reader's icon, as the reader carries it for its window.
/// </summary>
/// <remarks>
/// A file left out of the build would not fail it: the window would simply show the platform's
/// empty icon, and nobody running the tests on a Mac, where windows have no icon, would see it.
/// </remarks>
public sealed class ReaderIconTests : HeadlessTest
{
    [Fact]
    public Task TheReaderCarriesItsIconForItsWindow() => Ui(() =>
    {
        AssetLoader.Exists(ReaderIcon.Ico).ShouldBeTrue();
        AssetLoader.Exists(ReaderIcon.Png).ShouldBeTrue();
    });
}
