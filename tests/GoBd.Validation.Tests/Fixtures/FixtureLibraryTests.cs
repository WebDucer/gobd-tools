using System.IO.Compression;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Fixtures;

public sealed class FixtureLibraryTests
{
    [Fact]
    public void GoodExportFolderContainsIndexXmlAtItsRoot()
    {
        var folder = FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);

        File.Exists(Path.Combine(folder, "index.xml")).ShouldBeTrue();
    }

    [Fact]
    public void GoodExportZipContainsIndexXmlAtItsRoot()
    {
        var zip = FixtureLibrary.ZipPath(FixtureLibrary.GoodExport);
        try
        {
            using var archive = ZipFile.OpenRead(zip);
            archive.GetEntry("index.xml").ShouldNotBeNull();
        }
        finally
        {
            File.Delete(zip);
        }
    }

    [Fact]
    public void FolderAndZipDescribeTheSameEntrySet()
    {
        var folder = FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);
        var fromFolder = Directory
            .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var zip = FixtureLibrary.ZipPath(FixtureLibrary.GoodExport);
        try
        {
            using var archive = ZipFile.OpenRead(zip);
            var fromZip = archive.Entries
                .Select(entry => entry.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            fromZip.ShouldBe(fromFolder);
        }
        finally
        {
            File.Delete(zip);
        }
    }
}
