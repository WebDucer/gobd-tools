namespace GoBd.Validation.Content;

/// <summary>
/// A stable 64-bit hash of a key tuple.
/// </summary>
/// <remarks>
/// The hash is FNV-1a rather than <see cref="string.GetHashCode()"/>, because .NET randomises
/// string hashing per process: the same export would produce differently ordered candidates
/// between runs, and two runs must agree. A separator is folded in between the parts of a
/// composite key so that <c>("AB", "C")</c> and <c>("A", "BC")</c> are different keys.
/// </remarks>
internal static class KeyHash
{
    private const ulong Offset = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    /// <summary>Hashes the parts of one key, in order.</summary>
    internal static long Of(IReadOnlyList<string> parts)
    {
        var hash = Offset;
        for (var index = 0; index < parts.Count; index++)
        {
            if (index > 0)
            {
                hash = Fold(hash, 0x1F);
            }

            var part = parts[index];
            for (var character = 0; character < part.Length; character++)
            {
                var value = part[character];
                hash = Fold(hash, (byte)value);
                hash = Fold(hash, (byte)(value >> 8));
            }
        }

        return unchecked((long)hash);
    }

    private static ulong Fold(ulong hash, byte value) => (hash ^ value) * Prime;
}
