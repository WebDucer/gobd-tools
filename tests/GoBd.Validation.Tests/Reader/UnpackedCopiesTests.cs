using GoBd.Reader.Data;
using GoBd.Reader.Ui;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Finding the reader's own unpacked copy, and removing the copies other versions left behind.
/// </summary>
/// <remarks>
/// A single file unpacks its native libraries on first start, a copy per version, so every update
/// would leave one behind. Removing one is only safe when no running reader holds it: a reader that
/// has not opened an export has not loaded its store's engine yet, and still needs its copy. See the
/// change's design.md D2.
/// </remarks>
public sealed class UnpackedCopiesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gobd-unpacked-" + Guid.NewGuid().ToString("N"));

    private string Readers => Path.Combine(root, UnpackedCopies.ApplicationDirectory);

    private string Application => Path.Combine(root, "app") + Path.DirectorySeparatorChar;

    [Fact]
    public void ASingleFileFindsTheCopyItUnpacked()
    {
        var own = Copy("GT9LYrg0pAVU");

        // As the runtime reports it: the directory with its trailing separator, then a separator
        // between entries.
        var searched = own + Path.DirectorySeparatorChar + Path.PathSeparator;

        UnpackedCopies.Own(searched, Application).ShouldBe(own);
    }

    [Fact]
    public void AFolderBuildHasNoCopy()
    {
        // A folder build, the macOS bundle included, searches only the folder it was started from.
        UnpackedCopies.Own(Application + Path.PathSeparator, Application).ShouldBeNull();
    }

    [Fact]
    public void NothingSearchedIsNoCopy()
    {
        UnpackedCopies.Own(null, Application).ShouldBeNull();
        UnpackedCopies.Own(string.Empty, Application).ShouldBeNull();
    }

    [Fact]
    public void ADirectoryOutsideTheReadersOwnIsNotItsCopy()
    {
        var foreign = Copy("X1", Path.Combine(root, "other-app"));

        UnpackedCopies.Own(foreign + Path.PathSeparator, Application).ShouldBeNull();
    }

    [Fact]
    public void ACopyWhoseReaderHasQuitIsRemoved()
    {
        var own = Copy("own");
        var released = Released("released");

        UnpackedCopies.RemoveOthers(own);

        Directory.Exists(released).ShouldBeFalse();
    }

    [Fact]
    public void ACopyARunningReaderHoldsIsKept()
    {
        var own = Copy("own");
        var held = Copy("held");
        using var claim = StoreLock.TryTake(held).ShouldNotBeNull();

        UnpackedCopies.RemoveOthers(own);

        File.Exists(Path.Combine(held, "libduckdb.so")).ShouldBeTrue();
    }

    [Fact]
    public void ACopyNeverClaimedIsKeptWhileItMayStillBeUnpackingOrStarting()
    {
        // Another version unpacks into a directory named after its process, and claims the result
        // only once it has started. Deleting either makes that reader fail to start.
        var own = Copy("own");
        var unpacking = Copy("7ab");

        UnpackedCopies.RemoveOthers(own);

        File.Exists(Path.Combine(unpacking, "libduckdb.so")).ShouldBeTrue();
    }

    [Fact]
    public void ACopyNeverClaimedIsRemovedOnceItIsOld()
    {
        // Left by a build from before copies were claimed, or by an unpacking that never finished.
        var own = Copy("own");
        var old = Copy("old");
        Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow - UnpackedCopies.UnclaimedGrace - TimeSpan.FromMinutes(1));

        UnpackedCopies.RemoveOthers(own);

        Directory.Exists(old).ShouldBeFalse();
    }

    [Fact]
    public void TheReadersOwnCopyIsKeptEvenUnclaimed()
    {
        var own = Copy("own");
        Directory.SetLastWriteTimeUtc(own, DateTime.UtcNow - TimeSpan.FromDays(1));

        UnpackedCopies.RemoveOthers(own);

        File.Exists(Path.Combine(own, "libduckdb.so")).ShouldBeTrue();
    }

    [Fact]
    public void AnotherApplicationsCopiesBesideTheReadersAreUntouched()
    {
        var own = Copy("own");
        var foreign = Copy("released", Path.Combine(root, "other-app"));
        StoreLock.TryTake(foreign).ShouldNotBeNull().Dispose();

        UnpackedCopies.RemoveOthers(own);

        Directory.Exists(foreign).ShouldBeTrue();
    }

    [Fact]
    public void WithoutACopyOfItsOwnNothingIsRemoved()
    {
        var released = Released("released");

        UnpackedCopies.RemoveOthers(Application);

        Directory.Exists(released).ShouldBeTrue();
    }

    [Fact]
    public void ACopyThatCannotBeRemovedRaisesNothingAndTheOthersAreStillRemoved()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The copy that cannot be removed is made with Unix permissions.");
        }

        var own = Copy("own");
        var stuck = Released("stuck");
        var inner = Path.Combine(stuck, "inner");
        Directory.CreateDirectory(inner);
        File.WriteAllText(Path.Combine(inner, "libstuck.so"), "native");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(inner, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        var released = Released("released");

        Should.NotThrow(() => UnpackedCopies.RemoveOthers(own));
        Directory.Exists(released).ShouldBeFalse();
    }

    public void Dispose()
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        // A test may have taken the write permission away from a directory to make it stick.
        if (!OperatingSystem.IsWindows())
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        Directory.Delete(root, recursive: true);
    }

    private string Copy(string name, string? under = null)
    {
        var directory = Path.Combine(under ?? Readers, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "libduckdb.so"), "native");
        return directory;
    }

    /// <summary>A copy a reader claimed and released when it quit.</summary>
    private string Released(string name)
    {
        var directory = Copy(name);
        StoreLock.TryTake(directory).ShouldNotBeNull().Dispose();
        return directory;
    }
}
