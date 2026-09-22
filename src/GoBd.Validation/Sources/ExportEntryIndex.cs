namespace GoBd.Validation.Sources;

/// <summary>
/// Entry lookup shared by every export source.
/// </summary>
/// <remarks>
/// Nothing here is ZIP- or folder-specific — it is a pure function of the entry list — so both
/// sources delegate rather than each carrying their own copy. They previously did, and had already
/// drifted: one threw on two entries whose names normalised alike, the other silently kept the
/// last. A backslash is a legal filename character on Unix, so a folder holding both
/// <c>data/x.csv</c> and a file literally named <c>data\x.csv</c> reached that throw.
/// </remarks>
internal sealed class ExportEntryIndex
{
    private readonly Dictionary<string, ExportEntry> byName;
    private readonly Dictionary<string, List<ExportEntry>> byCaseInsensitiveName;
    private readonly Dictionary<string, int> countByName = new(StringComparer.Ordinal);

    internal ExportEntryIndex(IReadOnlyList<ExportEntry> entries)
    {
        byName = new Dictionary<string, ExportEntry>(entries.Count, StringComparer.Ordinal);
        byCaseInsensitiveName = new Dictionary<string, List<ExportEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            // First wins, and never throws. Two entries can share a normalised name -- a folder
            // holding data/x.csv beside a file literally named data\x.csv, or a ZIP with
            // duplicate members -- and throwing there crashed the validator before it could
            // report anything at all.
            //
            // The collision itself is reported as GOBD1007, so the ambiguity reaches the
            // producer rather than being resolved silently in whichever direction this happens
            // to pick. Duplicate ZIP members are a known spoofing vector.
            byName.TryAdd(entry.Name, entry);
            countByName[entry.Name] = countByName.GetValueOrDefault(entry.Name) + 1;

            if (!byCaseInsensitiveName.TryGetValue(entry.Name, out var bucket))
            {
                bucket = [];
                byCaseInsensitiveName[entry.Name] = bucket;
            }

            bucket.Add(entry);
        }
    }

    /// <summary>
    /// Names carried by more than one entry, with how many carry each.
    /// </summary>
    /// <remarks>
    /// Ordered so a report is reproducible whatever order the medium enumerated in.
    /// </remarks>
    internal IReadOnlyList<(string Name, int Count)> Collisions =>
        [.. countByName
            .Where(pair => pair.Value > 1)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (Name: pair.Key, Count: pair.Value))];

    /// <summary>Finds an entry by its normalised name, ordinally.</summary>
    internal ExportEntry? Find(string entryName) =>
        byName.TryGetValue(ExportPath.Normalise(entryName), out var entry) ? entry : null;

    /// <summary>
    /// Entries differing from <paramref name="entryName"/> only by letter case.
    /// </summary>
    /// <remarks>
    /// Bucketed rather than scanned. A Windows-produced export checked on Linux misses on every
    /// table at once, which is exactly when a per-table linear scan over every entry would turn
    /// the fast "no" into the slowest run of all.
    /// </remarks>
    internal IReadOnlyList<ExportEntry> FindCaseInsensitive(string entryName)
    {
        var wanted = ExportPath.Normalise(entryName);
        if (!byCaseInsensitiveName.TryGetValue(wanted, out var bucket))
        {
            return [];
        }

        return [.. bucket.Where(entry => !string.Equals(entry.Name, wanted, StringComparison.Ordinal))];
    }
}
