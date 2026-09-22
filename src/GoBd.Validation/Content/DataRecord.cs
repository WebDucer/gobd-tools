namespace GoBd.Validation.Content;

/// <summary>
/// One record as the declared layout yields it.
/// </summary>
/// <param name="Number">One-based position in the file, counting every record the file holds.</param>
/// <param name="IsData">False for a record the declaration excludes, such as a skipped header.</param>
/// <param name="Values">Column values in declared order, exactly as many as the record produced.</param>
/// <param name="Defects">Ways in which reading the record disagreed with the declaration.</param>
/// <remarks>
/// The number counts physical records rather than data records, so that a finding cites the line
/// a person will find in the file. Excluded records are yielded too — the header check exists
/// precisely to look at the record the declaration told the reader to skip.
/// </remarks>
public sealed record DataRecord(
    long Number,
    bool IsData,
    IReadOnlyList<string> Values,
    IReadOnlyList<ContentDefect> Defects);
