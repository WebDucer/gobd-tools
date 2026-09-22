using System.Diagnostics;
using System.IO.Compression;
using GoBd.Validation.Sources;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Sources;

/// <summary>
/// v1 reasons about an export from its listing alone. These tests hold that line against a
/// genuinely multi-gigabyte export: nothing is read, and nothing is extracted.
/// </summary>
public sealed class LargeExportGuardTests : IDisposable
{
    private const long ThreeGigabytes = 3L * 1024 * 1024 * 1024;

    private readonly string directory;

    public LargeExportGuardTests()
    {
        directory = Path.Combine(Path.GetTempPath(), $"gobd-large-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");

        // Sparse: the length is declared, the blocks are never allocated or written. Reading
        // this file would cost real time, which is what makes the assertions below meaningful.
        using var sparse = new FileStream(
            Path.Combine(directory, "buchungen.csv"), FileMode.CreateNew, FileAccess.Write);
        sparse.SetLength(ThreeGigabytes);
    }

    [Fact]
    public void EnumerationReportsTheDeclaredLengthWithoutReadingTheFile()
    {
        using var source = new FolderExportSource(directory);

        source.Find("buchungen.csv")!.Length.ShouldBe(ThreeGigabytes);
    }

    [Fact]
    public void OpeningTheExportNeverOpensADataFile()
    {
        var result = ExportSourceFactory.Open(directory);
        result.CanProceed.ShouldBeTrue();

        using var counting = new CountingExportSource(result.Source!);

        // Opening and enumerating an export must touch no entry at all. v1 goes on to open
        // exactly two -- index.xml and the DTD -- and never a declared data file.
        counting.Entries.Count.ShouldBe(2);
        counting.Opened.ShouldBeEmpty();
    }

    [Fact]
    public void EnumeratingAMultiGigabyteExportIsImmediate()
    {
        var stopwatch = Stopwatch.StartNew();
        using var source = new FolderExportSource(directory);
        var count = source.Entries.Count;
        stopwatch.Stop();

        count.ShouldBe(2);
        // Reading 3 GB could not complete in this budget on any realistic medium.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ReadingAMultiGigabyteArchiveInPlaceExtractsNothing()
    {
        // Scoped to a directory this test owns. Counting the shared system temp directory
        // would race against every other process on the machine.
        var workspace = Path.Combine(Path.GetTempPath(), $"gobd-nozip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var archive = Path.Combine(workspace, "export.zip");
            ZipFile.CreateFromDirectory(directory, archive, CompressionLevel.NoCompression, includeBaseDirectory: false);
            var before = Directory.EnumerateFileSystemEntries(workspace, "*", SearchOption.AllDirectories).ToHashSet(StringComparer.Ordinal);

            using (var source = new ZipExportSource(archive))
            {
                source.Entries.Count.ShouldBe(2);
                source.Find("buchungen.csv")!.Length.ShouldBe(ThreeGigabytes);
            }

            Directory.EnumerateFileSystemEntries(workspace, "*", SearchOption.AllDirectories)
                .ToHashSet(StringComparer.Ordinal)
                .ShouldBe(before, "reading a ZIP export in place must not extract anything");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
