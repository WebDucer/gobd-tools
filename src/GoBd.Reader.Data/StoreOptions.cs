namespace GoBd.Reader.Data;

/// <summary>
/// Where the store may put its files, and how much room it insists on before starting.
/// </summary>
/// <remarks>
/// The export is a read-only data carrier: an auditor's medium must come back unchanged, so
/// nothing is ever written beside it. The store therefore lives under the platform temporary
/// location — which on many Linux distributions is a RAM-backed tmpfs, so the location is
/// overridable and the space check measures the volume that actually backs it rather than
/// assuming it is a disk. See the change's design.md D9.
/// <para>
/// On Linux the platform temporary location is shared by every account on the machine, so the
/// store's directory there carries the name of the account it belongs to, and is kept readable by
/// that account alone (<see cref="StorePrivacy"/>). On macOS the temporary location already
/// belongs to one account, and the same rule costs nothing there. On Windows it belongs to one
/// account by its access control, and the directory keeps its name. See the
/// prepare-public-release change's design.md D6.
/// </para>
/// </remarks>
/// <param name="Root">Directory the per-export stores live under.</param>
/// <param name="SpaceHeadroom">Multiple of the declared data size the store insists on having free.</param>
public sealed record StoreOptions(string Root, double SpaceHeadroom = 1.5)
{
    /// <summary>The platform temporary location, which is where a store lives unless told otherwise.</summary>
    public static string DefaultRoot { get; } = DefaultRootIn(Path.GetTempPath());

    /// <summary>The default: the platform temporary location, with room to spare.</summary>
    public static StoreOptions Default { get; } = new(DefaultRoot);

    /// <summary>Where the stores of the account running the reader live, under a temporary location.</summary>
    internal static string DefaultRootIn(string temporary) =>
        Path.Combine(temporary, OperatingSystem.IsWindows() ? "gobd-reader" : "gobd-reader-" + Environment.UserName);
}
