namespace GoBd.Reader.Data;

/// <summary>Why a directory cannot hold stores.</summary>
public enum StorePrivacyProblem
{
    /// <summary>Other accounts can reach into it.</summary>
    ReadableByOthers,

    /// <summary>It is a symbolic link, which could lead the data anywhere.</summary>
    SymbolicLink,

    /// <summary>
    /// This account cannot use it: it belongs to another, or something that is not a directory is
    /// in its place.
    /// </summary>
    OwnedByAnother,
}

/// <summary>
/// Keeps the directory the stores live in readable by the account running the reader alone.
/// </summary>
/// <remarks>
/// A store holds the export's data, extracted: accounting records, customer names and amounts.
/// Where several people share a machine, a directory every account can read would hand that data
/// to all of them. So the directory is created private, and one that already exists is checked
/// rather than trusted: it is refused if it is a symbolic link, if its mode lets any other
/// account in, or if this account cannot write to it. A directory that fails is refused rather
/// than repaired: it may be someone else's, it may be open by intent, and what was open may
/// already have been read.
/// <para>
/// There is no managed call for a file's owner on Unix, so ownership is not asked. The write
/// probe covers the case that matters: another account's private directory cannot be written to,
/// and another account's open one fails the mode check. On Windows the temporary location is
/// private by its access control, and the Unix mode does not apply. See the
/// prepare-public-release change's design.md D6.
/// </para>
/// </remarks>
public static class StorePrivacy
{
    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode Others =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    /// <summary>
    /// Creates the directory private if it does not exist, and says why it cannot hold stores if
    /// it cannot, or null when it can.
    /// </summary>
    public static StorePrivacyProblem? Ensure(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(root);
            return null;
        }

        try
        {
            // The mode applies only to a directory this creates. One that already exists keeps its
            // own, which is why it is checked afterwards rather than assumed.
            Directory.CreateDirectory(root, Private);
        }
        catch (UnauthorizedAccessException)
        {
            return StorePrivacyProblem.OwnedByAnother;
        }
        catch (IOException)
        {
            // Something that is not a directory is in its place. Opening an export is refused for
            // it like any other location that cannot hold a store, rather than raised at a window
            // that has no answer for it.
            return StorePrivacyProblem.OwnedByAnother;
        }

        return Check(root);
    }

    /// <summary>
    /// Says why an existing directory cannot hold stores, or null when it can. Creates nothing.
    /// </summary>
    public static StorePrivacyProblem? Check(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        if (new DirectoryInfo(root).LinkTarget is not null)
        {
            return StorePrivacyProblem.SymbolicLink;
        }

        if ((File.GetUnixFileMode(root) & Others) != 0)
        {
            return StorePrivacyProblem.ReadableByOthers;
        }

        var probe = Path.Combine(root, ".probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return StorePrivacyProblem.OwnedByAnother;
        }
        catch (IOException)
        {
            return StorePrivacyProblem.OwnedByAnother;
        }
    }
}
