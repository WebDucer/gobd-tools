using GoBd.Validation.Findings;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Sources;

public sealed class ExportSourceFactoryTests
{
    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gobd-factory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void DirectoryPathOpensAsAFolderExport()
    {
        var result = ExportSourceFactory.Open(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport));
        using var source = result.Source;

        result.CanProceed.ShouldBeTrue();
        source.ShouldBeOfType<FolderExportSource>();
        result.Findings.ShouldBeEmpty();
    }

    [Fact]
    public void FilePathOpensAsAZipExport()
    {
        var zip = FixtureLibrary.ZipPath(FixtureLibrary.GoodExport);
        try
        {
            var result = ExportSourceFactory.Open(zip);
            using var source = result.Source;

            result.CanProceed.ShouldBeTrue();
            source.ShouldBeOfType<ZipExportSource>();
            result.Findings.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(zip);
        }
    }

    [Fact]
    public void MissingPathIsAToolFailureNotAConformanceFinding()
    {
        var result = ExportSourceFactory.Open(Path.Combine(Path.GetTempPath(), "definitely-not-here-" + Guid.NewGuid().ToString("N")));

        result.CanProceed.ShouldBeFalse();
        // GOBD9xxx: the validator could not run, which is distinct from "your export is bad".
        result.Findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.ExportPathNotFound);
    }

    [Fact]
    public void CorruptArchiveIsAToolFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gobd-corrupt-{Guid.NewGuid():N}.zip");
        File.WriteAllText(path, "this is not a zip archive");
        try
        {
            var result = ExportSourceFactory.Open(path);

            result.CanProceed.ShouldBeFalse();
            result.Findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.ExportUnreadable);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportWithoutIndexXmlStopsTheRun()
    {
        var directory = NewTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "kunden.csv"), "K001;Beispiel\n");

            var result = ExportSourceFactory.Open(directory);

            result.CanProceed.ShouldBeFalse();
            result.Findings.ShouldHaveSingleItem().Code.ShouldBe(FindingCodes.IndexMissing);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IndexXmlBuriedInASubdirectoryIsReportedDistinctly()
    {
        var directory = NewTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "export"));
            File.WriteAllText(Path.Combine(directory, "export", "index.xml"), "<DataSet/>");

            var result = ExportSourceFactory.Open(directory);

            result.CanProceed.ShouldBeFalse();
            var finding = result.Findings.ShouldHaveSingleItem();
            // Distinct from IndexMissing: the producer nested the export one level too deep,
            // which is a different mistake with a different fix.
            finding.Code.ShouldBe(FindingCodes.IndexNotAtRoot);
            finding.Arguments.ShouldContain("export/index.xml");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ACollisionIsReportedWithoutStoppingTheRun()
    {
        var directory = NewTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "data"));
            File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
            File.WriteAllText(Path.Combine(directory, "data", "x.csv"), "a");
            File.WriteAllText(Path.Combine(directory, "data" + '\\' + "x.csv"), "b");

            var result = ExportSourceFactory.Open(directory);
            using var source = result.Source;

            // The collision is an error, but index.xml is readable, so the run goes on. Gating
            // the abort on "any error was reported" instead of on the index meant a single
            // collision suppressed every other finding the export would have produced.
            result.CanProceed.ShouldBeTrue();
            var finding = result.Findings.ShouldHaveSingleItem();
            finding.Code.ShouldBe(FindingCodes.EntryNameCollision);
            // An entry in the medium, not the export as a whole: the JSON scope reads the same
            // either way, but the kind is what the deferred UI groups by.
            finding.Scope.ShouldNotBeNull().Kind.ShouldBe(FindingScopeKind.Entry);
            finding.Scope.Name.ShouldBe("data/x.csv");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ACollisionAlongsideAMissingIndexStillStopsTheRun()
    {
        var directory = NewTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "data"));
            File.WriteAllText(Path.Combine(directory, "data", "x.csv"), "a");
            File.WriteAllText(Path.Combine(directory, "data" + '\\' + "x.csv"), "b");

            var result = ExportSourceFactory.Open(directory);

            result.CanProceed.ShouldBeFalse();
            result.Findings.Select(finding => finding.Code)
                .ShouldBe([FindingCodes.EntryNameCollision, FindingCodes.IndexMissing]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindingsAboutTheExportAsAWholeCarryNoLocation() =>
        ExportSourceFactory.Open(Path.Combine(Path.GetTempPath(), "absent-" + Guid.NewGuid().ToString("N")))
            .Findings.ShouldHaveSingleItem().Location.ShouldBeNull();
}
