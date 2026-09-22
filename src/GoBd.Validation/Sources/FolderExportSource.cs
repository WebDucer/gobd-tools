namespace GoBd.Validation.Sources;

/// <summary>An export delivered as an unpacked folder.</summary>
public sealed class FolderExportSource : IExportSource
{
    private readonly string root;
    private readonly ExportEntryIndex index;
    private readonly Dictionary<string, string> pathByName;

    /// <summary>Opens the folder at <paramref name="path"/> as an export.</summary>
    public FolderExportSource(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Trailing separator trimmed here, where the root is made, rather than in each caller:
        // the validator, the CLI and the reader all construct this source, and shell completion
        // writes `export/` routinely. On Unix Path.GetFullPath keeps that slash, so the
        // containment check below would compare against `export//` and reject every entry,
        // index.xml included. TrimEndingDirectorySeparator leaves a filesystem root alone, so
        // `/` stays `/`. See the change's design.md D9.
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        Location = path;

        var collected = new List<(ExportEntry Entry, string FullPath)>();

        // A symbolic link is a pointer out of the export, not a file the medium carries.
        // Skipping ReparsePoint stops the walk at a linked directory, which matters more than
        // the per-file check below: the files under such a directory are not links themselves,
        // so they arrive with ordinary in-root names and pass every other test. A directory
        // named data linked at /etc is all it takes. Everything else about the enumeration is
        // left as the SearchOption overload had it, so which ordinary files are listed does not
        // change: hidden files stay listed, and an unreadable directory still throws.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };

        // EnumerateFiles yields FileInfo objects whose status the enumeration already fetched, so
        // reading Length costs no second lookup. The file itself is never opened.
        foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", options))
        {
            // Belt and braces for a file that is itself a link, independently of how the
            // enumeration maps link attributes on this platform.
            if (file.LinkTarget is not null)
            {
                continue;
            }

            var name = ExportPath.Normalise(Path.GetRelativePath(root, file.FullName));

            // A literal backslash in a filename normalises into a path separator, so a name can
            // escape the root even though the file itself sits inside it.
            if (!ExportPath.IsContainedName(name))
            {
                continue;
            }

            collected.Add((new ExportEntry(name, file.Length), file.FullName));
        }

        // OrderBy is stable, so which of two entries with equal normalised names comes first is
        // reproducible. List.Sort is an unstable introsort and would vary with directory order.
        var ordered = collected.OrderBy(item => item.Entry.Name, StringComparer.Ordinal).ToList();
        Entries = [.. ordered.Select(item => item.Entry)];
        index = new ExportEntryIndex(Entries);

        // Keyed off the same ordered sequence with the same first-wins rule as the index, so the
        // entry a lookup returns is always the file an open reads. Rebuilding a path from the
        // normalised name instead would open a different file whenever two entries collide --
        // data/x.csv and a file literally named data\x.csv both rebuild to the former.
        pathByName = new Dictionary<string, string>(ordered.Count, StringComparer.Ordinal);
        foreach (var (entry, fullPath) in ordered)
        {
            pathByName.TryAdd(entry.Name, fullPath);
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
        var entry = Find(entryName)
            ?? throw new FileNotFoundException($"Entry '{entryName}' is not present in the export.", entryName);

        // The path the enumeration actually walked to, not one rebuilt from the entry name.
        //
        // Belt and braces: the listing already excludes escaping names, but every open is
        // confined again so that no future way of getting a name into the index can reach
        // outside the export root.
        var full = Path.GetFullPath(pathByName[entry.Name]);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                $"Entry '{entryName}' resolves outside the export root and was not opened.");
        }

        return new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // A folder source holds no unmanaged state; streams are owned by their callers.
    }
}
