using GoBd.Reader.Data;

namespace GoBd.Reader.Ui;

/// <summary>
/// The native libraries a single-file reader unpacked when it started, and the copies other
/// versions of the reader left beside them.
/// </summary>
/// <remarks>
/// On Windows and Linux the reader is one file, which unpacks its native libraries on first start
/// into <c>&lt;base&gt;/gobd-reader/&lt;bundle&gt;/</c> and reuses them afterwards. Each version
/// unpacks a copy of its own, so every update would otherwise leave one behind.
/// <para>
/// A copy is removed only once no running reader holds it, proven the way a store is proven
/// abandoned: every reader holds a <see cref="StoreLock"/> on its own copy for as long as it runs.
/// Age cannot tell a stale copy from one in use. The store's engine is loaded when an export is
/// opened, not when the reader starts, so a reader that has not opened one yet still needs its
/// copy. See the change's design.md D2.
/// </para>
/// </remarks>
internal static class UnpackedCopies
{
    /// <summary>The directory the runtime unpacks every version of the reader under.</summary>
    internal const string ApplicationDirectory = "gobd-reader";

    /// <summary>How long a copy that no reader has ever claimed is left alone after it was written.</summary>
    /// <remarks>
    /// The runtime unpacks into a directory named after its process and renames it when it is
    /// complete, and the reader that unpacked it claims it only once its application has started,
    /// a moment later. A copy without a claim file that was written this recently may be either,
    /// and deleting it makes that reader fail to start: a second version started just after the
    /// first one's window opened did, with "Failed to open file … for writing". A copy that was
    /// claimed and released is a finished copy of a reader that has quit, and needs no such wait.
    /// </remarks>
    internal static readonly TimeSpan UnclaimedGrace = TimeSpan.FromMinutes(10);

    /// <summary>Where this process's native libraries were unpacked, or null when it unpacked none.</summary>
    public static string? Own() =>
        Own(AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string, AppContext.BaseDirectory);

    /// <summary>The unpacked copy among the directories a process searches for native libraries.</summary>
    /// <remarks>
    /// A single file lists the directory it unpacked to as the place its native libraries are
    /// found. A folder build, the macOS bundle included, lists only its own folder. So the own copy
    /// is the entry that is not the application's folder and sits in the reader's directory.
    /// Measured in the change's design.md, under "What the implementation settled".
    /// </remarks>
    /// <param name="searchDirectories">The runtime's native search directories, as it reports them.</param>
    /// <param name="applicationFolder">The folder the application was started from.</param>
    internal static string? Own(string? searchDirectories, string applicationFolder)
    {
        if (string.IsNullOrEmpty(searchDirectories))
        {
            return null;
        }

        var home = Normalised(applicationFolder);
        foreach (var entry in searchDirectories.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Normalised(entry);
            if (!string.Equals(candidate, home, StringComparison.Ordinal) && IsReaderCopy(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Removes every copy beside this one that no running reader holds.</summary>
    /// <remarks>
    /// Never throws. A copy that cannot be removed now, because a file in it is in use or not ours
    /// to delete, is tried again on a later start. So is a copy no reader has ever claimed that was
    /// written within <see cref="UnclaimedGrace"/>, because it may still be unpacking or starting.
    /// Nothing outside the reader's own directory is looked at, so another application's copies
    /// beside it are never touched.
    /// </remarks>
    /// <param name="own">This process's copy, which is always kept.</param>
    internal static void RemoveOthers(string own)
    {
        ArgumentException.ThrowIfNullOrEmpty(own);

        var kept = Normalised(own);
        var parent = Path.GetDirectoryName(kept);
        if (parent is null || !IsReaderCopy(kept))
        {
            return;
        }

        string[] siblings;
        try
        {
            siblings = Directory.GetDirectories(parent);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var sibling in siblings)
        {
            if (string.Equals(Normalised(sibling), kept, StringComparison.Ordinal) || !StoreLock.IsUnowned(sibling))
            {
                continue;
            }

            try
            {
                var neverClaimed = !File.Exists(Path.Combine(sibling, StoreLock.FileName));
                if (neverClaimed && now - Directory.GetLastWriteTimeUtc(sibling) < UnclaimedGrace)
                {
                    continue;
                }

                Directory.Delete(sibling, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool IsReaderCopy(string directory) =>
        string.Equals(Path.GetFileName(Path.GetDirectoryName(directory)), ApplicationDirectory, StringComparison.Ordinal);

    private static string Normalised(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
