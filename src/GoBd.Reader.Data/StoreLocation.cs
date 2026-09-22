using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GoBd.Validation.Sources;

namespace GoBd.Reader.Data;

/// <summary>
/// Names the directory one export's store lives in.
/// </summary>
/// <remarks>
/// The key is derived from the export's listing — every entry's name and length — rather than
/// from its path alone. Two exports at the same path in succession are then two different
/// stores, which matters because the second one's data would otherwise be read out of the
/// first one's tables.
/// <para>
/// Hashing the listing rather than the bytes is deliberate: an export runs to gigabytes, and
/// reading all of it to decide where to put it would cost more than the import it precedes. Any
/// change to a file's length changes the key, and a change that preserves every length exactly
/// is not something a re-exported data carrier does.
/// </para>
/// </remarks>
public static class StoreLocation
{
    /// <summary>The directory this export's store belongs in.</summary>
    public static string For(IExportSource source, StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        return Path.Combine(options.Root, KeyFor(source));
    }

    /// <summary>A stable name for this export's store.</summary>
    public static string KeyFor(IExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new StringBuilder();
        builder.Append(Path.GetFullPath(source.Location)).Append('\n');
        foreach (var entry in source.Entries)
        {
            builder.Append(entry.Name).Append('\t')
                .Append(entry.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash)[..32];
    }
}
