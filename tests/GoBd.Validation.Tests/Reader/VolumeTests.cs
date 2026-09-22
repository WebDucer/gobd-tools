using GoBd.Reader.Data;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Measuring the volume a store would live on.
/// </summary>
/// <remarks>
/// The space check runs before a byte is imported, so it runs on every export anyone opens. It
/// therefore has to answer for whatever the machine has mounted at that moment, including a
/// volume that is appearing or going away, rather than throwing and taking the open with it.
/// </remarks>
public sealed class VolumeTests
{
    [Fact]
    public void TheTemporaryLocationIsMeasuredWithARootAndSomeRoom()
    {
        var volume = Volumes.For(Path.GetTempPath());

        volume.VolumeRoot.ShouldNotBeNullOrEmpty();
        volume.AvailableBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void APathThatDoesNotExistYetIsMeasuredByTheVolumeItWouldBeOn()
    {
        // A store's directory is measured before it is created.
        var unborn = Path.Combine(Path.GetTempPath(), "gobd-" + Guid.NewGuid().ToString("N"), "store");

        var volume = Volumes.For(unborn);

        volume.VolumeRoot.ShouldBe(Volumes.For(Path.GetTempPath()).VolumeRoot);
        volume.AvailableBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ARelativePathIsMeasuredWhereItActuallyPoints()
    {
        Volumes.For(".").VolumeRoot.ShouldNotBeNullOrEmpty();
    }
}
