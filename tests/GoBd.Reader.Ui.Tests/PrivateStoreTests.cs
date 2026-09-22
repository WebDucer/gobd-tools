using System.Runtime.Versioning;
using GoBd.Reader.Data;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// What the window says when the directory its store would live in is not private.
/// </summary>
/// <remarks>
/// The way out is to remove the directory or to point the reader elsewhere, and a person can do
/// neither unless the window names the directory and the variable that moves it. The mode is a
/// Unix one, so this runs on Linux and macOS. See the prepare-public-release change's design.md D6.
/// </remarks>
public sealed class PrivateStoreTests : HeadlessTest
{
    private const string Tables = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public Task AnExportIsNotOpenedWhereOtherAccountsCouldReadItsStore()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows keeps the temporary location private by its access control.");

        return Ui(() =>
        {
            using var harness = ExportHarness.Create(Tables, ("t.csv", "A\r\n"));
            var open = Path.Combine(Path.GetDirectoryName(harness.Options.Root)!, "open");
            Directory.CreateDirectory(open, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

            var window = new MainWindow(harness.ExportPath, new StoreOptions(open));
            window.Show();
            try
            {
                var texts = Driver.Texts(window);

                texts.ShouldContain("This export could not be opened");
                texts.ShouldContain(MainWindow.Describe(new LocationNotPrivate(open, StorePrivacyProblem.ReadableByOthers)));
                texts.ShouldContain(text => text.Contains("other accounts can read", StringComparison.Ordinal)
                    && text.Contains("TMPDIR", StringComparison.Ordinal));
                window.Session.ShouldBeNull();
                Directory.EnumerateFileSystemEntries(open).ShouldBeEmpty();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
