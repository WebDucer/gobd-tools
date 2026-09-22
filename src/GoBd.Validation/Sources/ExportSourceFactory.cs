using System.Collections.Immutable;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Sources;

/// <summary>Result of opening an export: the source when usable, plus any findings raised.</summary>
/// <param name="Source">The opened export, or <see langword="null"/> when validation cannot proceed.</param>
/// <param name="Findings">Findings raised while opening.</param>
public sealed record ExportOpenResult(IExportSource? Source, ImmutableArray<Finding> Findings)
{
    /// <summary>True when a source was produced and validation can continue.</summary>
    public bool CanProceed => Source is not null;
}

/// <summary>
/// Opens an export from a path, choosing the folder or ZIP implementation, and performs the
/// checks that must pass before anything else can run.
/// </summary>
public static class ExportSourceFactory
{
    /// <summary>The description file every export must carry at its root.</summary>
    public const string IndexFileName = "index.xml";

    /// <summary>
    /// Opens the export at <paramref name="path"/>. A directory is treated as a folder export
    /// and a regular file as a ZIP archive.
    /// </summary>
    public static ExportOpenResult Open(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            return new ExportOpenResult(null, [Finding.Create(FindingCodes.ExportPathNotFound, null, path)]);
        }

        IExportSource source;
        try
        {
            source = isDirectory ? new FolderExportSource(path) : new ZipExportSource(path);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return new ExportOpenResult(null, [Finding.Create(FindingCodes.ExportUnreadable, null, path, exception.Message)]);
        }

        var findings = Preflight(source);
        if (source.Find(IndexFileName) is null)
        {
            // A document that is not there cannot be parsed, so the run stops here. The test is
            // the index itself rather than "any error was reported": a colliding entry name is
            // an error too, and it says nothing about whether index.xml can be read. Gating on
            // severity meant one collision suppressed every other finding in the export.
            source.Dispose();
            return new ExportOpenResult(null, [.. findings]);
        }

        return new ExportOpenResult(source, [.. findings]);
    }

    /// <summary>
    /// Checks that the export carries <c>index.xml</c> at its root, distinguishing an export
    /// that lacks one entirely from one that buried it in a subdirectory.
    /// </summary>
    private static List<Finding> Preflight(IExportSource source)
    {
        var findings = new List<Finding>();

        findings.AddRange(source.NameCollisions.Select(collision => Finding.Create(
            FindingCodes.EntryNameCollision,
            null,
            collision.Name,
            collision.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)).About(FindingScope.Entry(collision.Name))));

        if (source.Find(IndexFileName) is not null)
        {
            return findings;
        }

        var elsewhere = source.Entries
            .Where(entry => entry.Name.EndsWith('/' + IndexFileName, StringComparison.Ordinal))
            .Select(entry => entry.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        findings.Add(elsewhere.Length > 0
            ? Finding.Create(FindingCodes.IndexNotAtRoot, null, source.Location, elsewhere[0])
            : Finding.Create(FindingCodes.IndexMissing, null, source.Location));

        return findings;
    }
}
