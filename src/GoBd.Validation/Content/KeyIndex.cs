namespace GoBd.Validation.Content;

/// <summary>
/// Every key of one table, as a sorted array of 64-bit hashes.
/// </summary>
/// <remarks>
/// Eight bytes a record, and nothing else. A hash set of the key strings would cost several
/// times that — the strings themselves, a node per entry, and the bucket array — which decides
/// whether a table of tens of millions of records can be checked at all on an ordinary machine.
/// <para>
/// The same array answers both key questions. Sorted, adjacent equal entries are candidate
/// duplicate primary keys; searched, it says whether a foreign key value has a match. So it is
/// built once per table and every foreign key pointing at that table probes the same array,
/// which keeps peak memory at the largest single table rather than the sum of them. See the
/// change's design.md D5.
/// </para>
/// <para>
/// A hash is not a key. A duplicate candidate is confirmed against the actual key values before
/// anything is reported, so a collision cannot produce a false accusation. In the other
/// direction a collision could mask one dangling reference; at 64 bits and the table sizes this
/// engine admits, that is far below the rate at which the file itself is misread.
/// </para>
/// </remarks>
internal sealed class KeyIndex : IDisposable
{
    [ThreadStatic]
    private static int live;

    [ThreadStatic]
    private static int peak;

    private readonly int capacity;
    private long[] hashes = [];
    private int count;
    private bool sealedOff;

    internal KeyIndex(int capacity)
    {
        this.capacity = capacity;
        live++;
        peak = Math.Max(peak, live);
    }

    /// <summary>
    /// The most indexes alive at once on this thread since <see cref="ResetPeak"/>.
    /// </summary>
    /// <remarks>
    /// An observation seam. "Peak memory is the largest table rather than the sum" is a claim
    /// about how many of these exist at one time, and a claim that is never observed is a claim
    /// that quietly stops being true. Per-thread so that tests running in parallel do not read
    /// each other's counts.
    /// </remarks>
    internal static int PeakLive => peak;

    /// <summary>Starts a fresh observation of how many indexes are alive at once.</summary>
    internal static void ResetPeak()
    {
        peak = live;
    }

    /// <summary>Releases the keys, so the next table's index is not built beside this one.</summary>
    public void Dispose()
    {
        if (hashes.Length > 0 || count > 0)
        {
            hashes = [];
            count = 0;
        }

        live--;
    }

    /// <summary>True when the table holds more keys than this engine admits.</summary>
    public bool CapacityExceeded { get; private set; }

    /// <summary>Keys held.</summary>
    public int Count => count;

    /// <summary>Bytes the sealed index occupies.</summary>
    public long BytesHeld => hashes.LongLength * sizeof(long);

    /// <summary>Adds one key's hash, or notes that the table is beyond capacity.</summary>
    public bool Add(long hash)
    {
        if (CapacityExceeded)
        {
            return false;
        }

        if (count == capacity)
        {
            CapacityExceeded = true;
            hashes = [];
            count = 0;
            return false;
        }

        if (count == hashes.Length)
        {
            var grown = hashes.Length == 0 ? 1024 : hashes.Length * 2;
            Array.Resize(ref hashes, Math.Min(Math.Max(grown, 1024), capacity));
        }

        hashes[count++] = hash;
        return true;
    }

    /// <summary>Sorts the index and trims it to exactly eight bytes a key.</summary>
    public void Seal()
    {
        if (sealedOff || CapacityExceeded)
        {
            return;
        }

        // Trimming first is what makes the eight-bytes-a-record claim true of the index that is
        // actually held: doubling while building overshoots, and that overshoot is transient.
        Array.Resize(ref hashes, count);
        Array.Sort(hashes);
        sealedOff = true;
    }

    /// <summary>True when some key hashes to this value.</summary>
    public bool Contains(long hash) => Array.BinarySearch(hashes, 0, count, hash) >= 0;

    /// <summary>Hashes carried by more than one key, each reported once.</summary>
    public IEnumerable<long> Repeated()
    {
        for (var index = 1; index < count; index++)
        {
            if (hashes[index] == hashes[index - 1]
                && (index == 1 || hashes[index - 2] != hashes[index]))
            {
                yield return hashes[index];
            }
        }
    }
}
