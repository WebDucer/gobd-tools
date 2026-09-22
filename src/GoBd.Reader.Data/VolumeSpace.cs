namespace GoBd.Reader.Data;

/// <summary>What is free on the volume that actually backs a path.</summary>
/// <param name="AvailableBytes">Bytes free on that volume.</param>
/// <param name="VolumeRoot">Mount point the path resolves to.</param>
/// <param name="Format">The volume's format, such as <c>apfs</c>, <c>ext4</c> or <c>tmpfs</c>.</param>
public readonly record struct VolumeSpace(long AvailableBytes, string VolumeRoot, string Format)
{
    /// <summary>True when the volume is memory rather than disk, so filling it costs the machine.</summary>
    public bool IsMemoryBacked =>
        Format.Contains("tmpfs", StringComparison.OrdinalIgnoreCase)
        || Format.Contains("ramfs", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Measures the volume a path is really on.
/// </summary>
/// <remarks>
/// Not the root of the filesystem, and not an assumption: the deepest mount point containing the
/// path is the one whose free space governs. On a Linux machine whose <c>/tmp</c> is a tmpfs,
/// asking about <c>/</c> would report the disk and let a multi-gigabyte import consume memory
/// until the machine fell over.
/// </remarks>
public static class Volumes
{
    /// <summary>Measures the volume backing the given path.</summary>
    /// <remarks>
    /// Answers rather than throws. Asking the operating system what is mounted can fail — a
    /// volume appearing or going away as the list is read leaves an entry with no name, and the
    /// whole enumeration throws — and a reader that could not open an export because a disk image
    /// happened to be mounting would be reporting the wrong thing entirely. What it cannot measure
    /// it reports as unmeasured, and the caller refuses on that: room it could not confirm is not
    /// room it may spend.
    /// </remarks>
    public static VolumeSpace For(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var full = Path.GetFullPath(path);
        DriveInfo? best = null;
        var bestLength = -1;

        foreach (var drive in Mounted())
        {
            string root;
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                root = drive.RootDirectory.FullName;
            }
            catch (IOException)
            {
                continue;
            }
            catch (ArgumentException)
            {
                // An entry the operating system named in a way this platform cannot resolve.
                continue;
            }

            // The deepest mount point that contains the path wins: /tmp beats / on a machine
            // where /tmp is mounted separately.
            if (full.StartsWith(root, StringComparison.Ordinal) && root.Length > bestLength)
            {
                best = drive;
                bestLength = root.Length;
            }
        }

        // Nothing matched, so nothing is known: the caller refuses rather than spending room it
        // could not confirm. That is the same answer as a volume list that could not be read.
        return best is null
            ? new VolumeSpace(0, full, "unknown")
            : new VolumeSpace(best.AvailableFreeSpace, best.RootDirectory.FullName, best.DriveFormat);
    }

    /// <summary>
    /// What the machine has mounted, or nothing when that cannot be read.
    /// </summary>
    /// <remarks>
    /// Asked twice before giving up. A volume appearing or going away while the list is built
    /// leaves an entry the platform cannot name, and the whole enumeration throws — by the time
    /// the question is asked again, that is usually over. What still cannot be read stays unread,
    /// and the caller treats an unmeasured volume as one with no room to offer.
    /// </remarks>
    private static DriveInfo[] Mounted()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return DriveInfo.GetDrives();
            }
            catch (ArgumentException)
            {
                // An entry with no name, from a volume that is on its way in or out.
            }
            catch (IOException)
            {
                // The list itself could not be read.
            }
            catch (UnauthorizedAccessException)
            {
                // This process may not ask what is mounted.
            }
        }

        return [];
    }
}
