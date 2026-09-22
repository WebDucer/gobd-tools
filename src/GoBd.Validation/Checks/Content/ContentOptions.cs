namespace GoBd.Validation.Checks.Content;

/// <summary>
/// How much a content check reports about one table before it stops.
/// </summary>
/// <remarks>
/// The bound is a stop, not a display cap: analysis of a defective table halts when it is
/// reached, so a wholly broken multi-gigabyte file is rejected in seconds rather than read to
/// its end. It has a default rather than being merely available, because that saving only holds
/// if the stop applies without anyone having asked for it.
/// <para>
/// Raising the bound trades time for completeness on a file already known to be defective. It
/// does not change which findings are reported first: those are the first by record order under
/// any bound, so a larger bound yields a superset of a smaller one and two runs at the same
/// bound agree. See the change's design.md D7.
/// </para>
/// </remarks>
/// <param name="MaximumFindingsPerTable">Findings after which analysis of a table stops.</param>
/// <param name="MaximumKeyRecords">Keys of one table this engine will hold at once.</param>
public sealed record ContentOptions(
    int MaximumFindingsPerTable = ContentOptions.DefaultMaximumFindingsPerTable,
    int MaximumKeyRecords = ContentOptions.DefaultMaximumKeyRecords)
{
    /// <summary>Findings per table after which analysis stops unless the caller says otherwise.</summary>
    public const int DefaultMaximumFindingsPerTable = 50;

    /// <summary>
    /// Keys of a single table this engine holds at once: about 512 MB at eight bytes a key.
    /// </summary>
    /// <remarks>
    /// A table beyond this is reported as unchecked rather than checked badly. Saying "the check
    /// did not run" costs a pipeline an investigation; reporting an unchecked table as clean
    /// costs it the point of running the check at all.
    /// </remarks>
    public const int DefaultMaximumKeyRecords = 64_000_000;

    /// <summary>The bound as it applies when the caller expresses no preference.</summary>
    public static ContentOptions Default { get; } = new();
}
