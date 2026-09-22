using System.IO.Compression;
using System.Reflection;

namespace GoBd.Validation.Tests.Fixtures;

/// <summary>
/// Resolves the on-disk fixture exports and can project any of them into an equivalent ZIP,
/// so that a test can assert ZIP and folder sources behave identically.
/// </summary>
public static class FixtureLibrary
{
    /// <summary>A conformant export: valid index.xml, canonical DTD, every declared file present.</summary>
    public const string GoodExport = "good-export";

    /// <summary>
    /// A deliberately defective export: a dangling reference, a foreign key naming an
    /// undeclared column, an edited DTD, and a declared file that is not present.
    /// </summary>
    public const string BrokenExport = "broken-export";

    /// <summary>
    /// A conformant export whose DTD was re-encoded to LF, as a Linux zip/unzip cycle or a
    /// normalising tool leaves it. The standard requires the shipped DTD to be byte-identical to
    /// the original, so this is a real defect and not merely cosmetic.
    /// </summary>
    public const string RepackagedExport = "repackaged-export";

    /// <summary>Root of the checked-in fixture folders.</summary>
    public static string Root { get; } = LocateRoot();

    /// <summary>Absolute path of the named fixture folder.</summary>
    public static string FolderPath(string name)
    {
        var path = Path.Combine(Root, name);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Fixture '{name}' not found at '{path}'.");
        }

        return path;
    }

    /// <summary>Zips the named fixture folder into a temporary file and returns its path.</summary>
    public static string ZipPath(string name)
    {
        var target = Path.Combine(Path.GetTempPath(), $"gobd-fixture-{name}-{Guid.NewGuid():N}.zip");
        ZipFile.CreateFromDirectory(FolderPath(name), target, CompressionLevel.Fastest, includeBaseDirectory: false);
        return target;
    }

    private static string LocateRoot()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        for (var candidate = new DirectoryInfo(directory); candidate is not null; candidate = candidate.Parent)
        {
            var fixtures = Path.Combine(candidate.FullName, "Fixtures");
            if (Directory.Exists(Path.Combine(fixtures, GoodExport)))
            {
                return fixtures;
            }
        }

        throw new DirectoryNotFoundException($"Could not locate the Fixtures directory from '{directory}'.");
    }
}
