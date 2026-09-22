using System.Runtime.Versioning;
using GoBd.Reader.Data;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The store keeps an export's data where only the account running the reader can read it, and
/// refuses to open rather than put it anywhere else.
/// </summary>
/// <remarks>
/// On Windows the temporary location is private by its access control and the Unix mode does not
/// apply, so the tests of the mode run on Linux and macOS only: they skip themselves on Windows,
/// and say so to the platform analyzer. See the prepare-public-release change's design.md D6.
/// </remarks>
public sealed class StorePrivacyTests
{
    private const string Buchungen = """
                <Table>
                  <URL>buchungen.csv</URL>
                  <Name>Buchungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Belegnummer</Name><AlphaNumeric/></VariablePrimaryKey>
                  </VariableLength>
                </Table>
        """;

    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static void UnixOnly() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows keeps the temporary location private by its access control.");

    private static StoreOpenResult Open(StoreHarness harness, string root) =>
        ExportStore.Open(harness.Source(), harness.DataSet, new StoreOptions(root));

    [Fact]
    public void TheDefaultRootBelongsToTheAccountRunningTheReader()
    {
        var expected = OperatingSystem.IsWindows() ? "gobd-reader" : "gobd-reader-" + Environment.UserName;

        StoreOptions.DefaultRoot.ShouldBe(Path.Combine(Path.GetTempPath(), expected));
    }

    [Fact]
    public void TheDefaultRootIsInsideWhicheverTemporaryLocationTheEnvironmentNames()
    {
        // Path.GetTempPath() is where TMPDIR arrives on Linux and macOS; the root is made inside it.
        var temporary = Path.Combine(Path.GetTempPath(), "somewhere-of-my-own");

        Path.GetDirectoryName(StoreOptions.DefaultRootIn(temporary)).ShouldBe(temporary);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ARootThatDoesNotExistIsCreatedPrivate()
    {
        UnixOnly();
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "A\r\n"));
        var root = Path.Combine(harness.StoreRoot, "new");

        using var store = Open(harness, root).Store.ShouldNotBeNull();

        File.GetUnixFileMode(root).ShouldBe(Private);
        store.Directory.ShouldStartWith(root);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ARootOthersCanReadIsRefusedAndNothingIsWrittenThere()
    {
        UnixOnly();
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "A\r\n"));
        var root = Path.Combine(harness.StoreRoot, "open");
        Directory.CreateDirectory(root, Private | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        var result = Open(harness, root);

        result.Store.ShouldBeNull();
        result.Refusal.ShouldBe(new LocationNotPrivate(root, StorePrivacyProblem.ReadableByOthers));
        Directory.EnumerateFileSystemEntries(root).ShouldBeEmpty();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ASymbolicLinkIsRefusedEvenToAPrivateDirectory()
    {
        UnixOnly();
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "A\r\n"));
        var target = Path.Combine(harness.StoreRoot, "target");
        Directory.CreateDirectory(target, Private);
        var root = Path.Combine(harness.StoreRoot, "link");
        Directory.CreateSymbolicLink(root, target);

        var result = Open(harness, root);

        result.Store.ShouldBeNull();
        result.Refusal.ShouldBe(new LocationNotPrivate(root, StorePrivacyProblem.SymbolicLink));
        Directory.EnumerateFileSystemEntries(target).ShouldBeEmpty();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ARootThisAccountCannotWriteToIsRefused()
    {
        UnixOnly();
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "A\r\n"));

        // Private, but not writable: what another account's private directory looks like from
        // here, without needing a second account to make one.
        var root = Path.Combine(harness.StoreRoot, "theirs");
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var result = Open(harness, root);

            result.Store.ShouldBeNull();
            result.Refusal.ShouldBe(new LocationNotPrivate(root, StorePrivacyProblem.OwnedByAnother));
        }
        finally
        {
            File.SetUnixFileMode(root, Private);
        }
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void StartupCleanupLeavesADirectoryThatIsNotPrivateAlone()
    {
        UnixOnly();
        using var harness = StoreHarness.Create(Buchungen, ("buchungen.csv", "A\r\n"));
        var root = Path.Combine(harness.StoreRoot, "open");
        Directory.CreateDirectory(root, Private | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        var somebodysStore = Path.Combine(root, "abandoned.999999");
        Directory.CreateDirectory(somebodysStore);

        ExportStore.RemoveAbandonedStores(new StoreOptions(root));

        Directory.Exists(somebodysStore).ShouldBeTrue();
    }
}
