using GoBd.Validation.Sources;

namespace GoBd.Validation.Dtd;

/// <summary>Finds and compares the DTD file an export ships alongside <c>index.xml</c>.</summary>
public static class DtdCopyLocator
{
    /// <summary>
    /// Locates the export's DTD copy and compares it to the canonical grammar.
    /// </summary>
    /// <remarks>
    /// Selection is deterministic when an export carries more than one candidate: the
    /// canonically named file wins, then the legacy name, then the first in ordinal order.
    /// </remarks>
    public static DtdCopyComparison Inspect(IExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var candidates = source.Entries
            .Where(entry => CanonicalDtd.IsDtdFileName(entry.Name))
            .OrderBy(entry => Rank(entry.Name))
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return DtdCopyComparison.Missing();
        }

        var chosen = candidates[0];
        using var stream = source.OpenRead(chosen.Name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return DtdCopyComparison.Compare(chosen.Name, buffer.ToArray());
    }

    private static int Rank(string entryName) => entryName switch
    {
        _ when string.Equals(entryName, CanonicalDtd.FileName, StringComparison.Ordinal) => 0,
        _ when string.Equals(entryName, CanonicalDtd.LegacyFileName, StringComparison.Ordinal) => 1,
        _ => 2,
    };
}
