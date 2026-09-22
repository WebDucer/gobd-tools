using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>How far reading one table has got.</summary>
public enum TableReadingState
{
    /// <summary>Its turn has not come yet.</summary>
    Waiting,

    /// <summary>It is being read now.</summary>
    Reading,

    /// <summary>It was read, it conforms, and its data can be shown.</summary>
    Ready,

    /// <summary>It was read and does not conform to its declaration.</summary>
    Defective,

    /// <summary>Reading it failed for a reason no check anticipated.</summary>
    Failed,
}

/// <summary>One table as reading the export has left it.</summary>
/// <param name="Table">The declared table.</param>
/// <param name="State">How far it has got.</param>
/// <param name="BytesRead">Bytes of its file read so far.</param>
/// <param name="Bytes">Bytes its file holds, as the export's listing reports them.</param>
/// <param name="Records">Data records read, once it has been read.</param>
/// <param name="Findings">What was found about it, in production order.</param>
/// <param name="Truncated">True when analysis of it stopped at its bound.</param>
public sealed record TableReading(
    TableNode Table,
    TableReadingState State,
    long BytesRead,
    long Bytes,
    long Records,
    IReadOnlyList<Finding> Findings,
    bool Truncated)
{
    /// <summary>True once it will not change again.</summary>
    public bool IsFinished =>
        State is TableReadingState.Ready or TableReadingState.Defective or TableReadingState.Failed;

    /// <summary>How much of its file has been read, from 0 to 1.</summary>
    public double Fraction =>
        IsFinished ? 1 : Bytes > 0 ? Math.Min(1, (double)BytesRead / Bytes) : 0;
}

/// <summary>
/// Something this reader cannot do with a column, said in its own words.
/// </summary>
/// <remarks>
/// Not a finding, and carrying no code. The export is not defective: the standard sets no limit
/// on how long a number may be, and a column that says it holds times is not obliged to hold only
/// times. The validator would report neither, so the summary says these apart from what is wrong
/// with the export — a code here would make the two tools appear to disagree.
/// </remarks>
/// <param name="Table">The table it concerns.</param>
/// <param name="Column">The column it concerns, by its declared name.</param>
/// <param name="Text">What the reader cannot do with it, and what it does instead.</param>
public sealed record ReaderNotice(TableNode Table, string Column, string Text);

/// <summary>
/// The export as reading it has left it: the verdict so far, every table's state, and what was
/// found.
/// </summary>
/// <remarks>
/// One type serves both jobs the pipeline has. It is the progress the window shows while an
/// export is being read, and it is the summary the start page presents once it has been — so
/// what a table's readiness says and what the summary says can never disagree, because they are
/// the same value. See the change's design.md D1.
/// </remarks>
/// <param name="Tables">Every declared table, in document order.</param>
/// <param name="BytesRead">Bytes of the export's data files read so far.</param>
/// <param name="Bytes">Bytes those files hold, as the export's listing reports them.</param>
/// <param name="Report">Every finding so far, and the verdict they produce.</param>
/// <param name="KeysChecked">True once the keys have been checked across every table.</param>
/// <param name="Complete">True once nothing is left to read.</param>
public sealed record ExportReading(
    IReadOnlyList<TableReading> Tables,
    long BytesRead,
    long Bytes,
    ValidationReport Report,
    bool KeysChecked,
    bool Complete)
{
    /// <summary>
    /// What this reader cannot do with the export's columns, apart from what is wrong with it.
    /// </summary>
    /// <remarks>
    /// Kept beside the findings rather than among them, because these are the reader's limits and
    /// not the export's defects. The summary presents them as a section of their own.
    /// </remarks>
    public IReadOnlyList<ReaderNotice> Notices { get; init; } = [];

    /// <summary>Whether the export conforms, on everything read so far.</summary>
    public Verdict Verdict => Report.Verdict;

    /// <summary>How much of the export has been read, from 0 to 1.</summary>
    public double Fraction =>
        Complete ? 1 : Bytes > 0 ? Math.Min(1, (double)BytesRead / Bytes) : 0;

    /// <summary>The table being read now, when one is.</summary>
    public TableReading? Current =>
        Tables.FirstOrDefault(table => table.State == TableReadingState.Reading);
}
