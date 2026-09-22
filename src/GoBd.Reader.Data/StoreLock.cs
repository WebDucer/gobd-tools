using System.Globalization;

namespace GoBd.Reader.Data;

/// <summary>
/// A claim on one store directory, held open for as long as the process uses it.
/// </summary>
/// <remarks>
/// Startup cleanup has to remove what a killed run left behind without pulling the ground from
/// under a second instance that is reading right now. Age cannot tell those apart — an import
/// that has been running for an hour looks exactly like an abandoned one. So each store carries
/// a file held open with no sharing: another process can delete the store only when it can take
/// that file itself, which it can only do once the owner is gone. The failure mode if this is
/// wrong is severe, so cleanup proves a store is unowned rather than assuming it.
/// </remarks>
public sealed class StoreLock : IDisposable
{
    /// <summary>Name of the claim file inside a store directory.</summary>
    public const string FileName = ".owner";

    private readonly FileStream stream;

    private StoreLock(FileStream stream) => this.stream = stream;

    /// <summary>Takes the claim on a store directory, or reports that someone else holds it.</summary>
    public static StoreLock? TryTake(string storeDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(storeDirectory);
        Directory.CreateDirectory(storeDirectory);

        try
        {
            var stream = new FileStream(
                Path.Combine(storeDirectory, FileName),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64,
                FileOptions.None);

            // The identity is written for a person looking at a stale directory; the claim itself
            // is the open handle, not the content.
            var writer = new StreamWriter(stream, leaveOpen: true);
            writer.WriteLine(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            writer.Flush();
            return new StoreLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>True when nobody currently holds the claim on this store directory.</summary>
    public static bool IsUnowned(string storeDirectory)
    {
        var path = Path.Combine(storeDirectory, FileName);
        if (!File.Exists(path))
        {
            // A directory with no claim file at all was never fully created, or was left by a
            // build that predates this scheme. Either way nobody is holding it.
            return true;
        }

        try
        {
            using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Releases the claim.</summary>
    public void Dispose() => stream.Dispose();
}
