namespace GoBd.Validation.Sources;

/// <summary>
/// A GoBD export presented as a uniform set of entries, whether it arrived as a ZIP archive or
/// as an unpacked folder.
/// </summary>
/// <remarks>
/// v1 enumerates entries and opens exactly two of them — <c>index.xml</c> and the DTD. The
/// stream-opening member exists now because the v2 Tier 1 checks are a forward-only pass per
/// table, which works inside a ZIP without extraction; committing to the shape now means v2
/// adds checks rather than re-plumbing access. See design.md D2.
/// </remarks>
public interface IExportSource : IDisposable
{
    /// <summary>The path this export was opened from, for reporting.</summary>
    string Location { get; }

    /// <summary>Every entry in the export, ordered by name.</summary>
    IReadOnlyList<ExportEntry> Entries { get; }

    /// <summary>
    /// Finds an entry by its normalised relative name, using ordinal case-sensitive matching.
    /// </summary>
    ExportEntry? Find(string entryName);

    /// <summary>
    /// Finds entries differing from <paramref name="entryName"/> only by letter case. Used to
    /// explain a miss rather than to satisfy it: an export that resolves only on a
    /// case-insensitive filesystem is defective, because the auditor's environment is not the
    /// producer's.
    /// </summary>
    IReadOnlyList<ExportEntry> FindCaseInsensitive(string entryName);

    /// <summary>
    /// Names carried by more than one entry, with how many carry each.
    /// </summary>
    /// <remarks>
    /// Two entries can resolve to one name — a folder holding <c>data/x.csv</c> beside a file
    /// literally named <c>data\x.csv</c>, or a ZIP with duplicate members. Which one an importer
    /// reads is undefined, so the medium is ambiguous and the producer needs telling.
    /// </remarks>
    IReadOnlyList<(string Name, int Count)> NameCollisions { get; }

    /// <summary>Opens an entry as a forward-only stream.</summary>
    Stream OpenRead(string entryName);
}
