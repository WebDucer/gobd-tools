using System.IO.Compression;

namespace GoBd.Validation.Sources;

/// <summary>
/// An export delivered as a ZIP archive, read in place.
/// </summary>
/// <remarks>
/// Entries come from the archive's central directory, so enumerating a multi-gigabyte export
/// costs nothing and nothing is extracted to disk. See design.md D2.
/// </remarks>
public sealed class ZipExportSource : IExportSource
{
    private readonly FileStream file;
    private readonly ZipArchive archive;
    private readonly ExportEntryIndex index;
    private readonly Dictionary<string, ZipArchiveEntry> archiveEntries;

    /// <summary>Opens the archive at <paramref name="path"/> as an export.</summary>
    public ZipExportSource(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Location = path;
        file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            archive = new ZipArchive(file, ZipArchiveMode.Read);
        }
        catch
        {
            file.Dispose();
            throw;
        }

        var entries = archive.Entries
            // A directory marker has an empty name after its trailing separator.
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .Select(entry => new ExportEntry(ExportPath.Normalise(entry.FullName), entry.Length))
            // Nothing is extracted, so an escaping name cannot reach the filesystem today — but
            // it has no legitimate meaning either, and v2 opens data streams from this listing.
            .Where(entry => ExportPath.IsContainedName(entry.Name))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();

        Entries = entries;
        index = new ExportEntryIndex(entries);

        // A lookup rather than a scan of every entry on each open.
        //
        // Duplicate member names are legal in a ZIP and are a known spoofing vector; they are
        // reported as GOBD1007. First-wins here matches ExportEntryIndex -- whose input is this
        // same sequence, stably sorted -- so Find and OpenRead always describe the same member;
        // otherwise the hash reported for the DTD could belong to a different member than the
        // one the listing selected.
        archiveEntries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var name = ExportPath.Normalise(entry.FullName);
            if (ExportPath.IsContainedName(name))
            {
                archiveEntries.TryAdd(name, entry);
            }
        }
    }

    /// <inheritdoc />
    public string Location { get; }

    /// <inheritdoc />
    public IReadOnlyList<ExportEntry> Entries { get; }

    /// <inheritdoc />
    public ExportEntry? Find(string entryName) => index.Find(entryName);

    /// <inheritdoc />
    public IReadOnlyList<ExportEntry> FindCaseInsensitive(string entryName) =>
        index.FindCaseInsensitive(entryName);

    /// <inheritdoc />
    public IReadOnlyList<(string Name, int Count)> NameCollisions => index.Collisions;

    /// <inheritdoc />
    public Stream OpenRead(string entryName)
    {
        var entry = archiveEntries.GetValueOrDefault(ExportPath.Normalise(entryName))
            ?? throw new FileNotFoundException($"Entry '{entryName}' is not present in the export.", entryName);
        return entry.Open();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        archive.Dispose();
        file.Dispose();
    }
}
