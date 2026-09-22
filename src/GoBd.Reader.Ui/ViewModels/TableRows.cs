using GoBd.Reader.Data;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>
/// A table as one tab is looking at it.
/// </summary>
/// <remarks>
/// A table in file order has no view: the records are the table's own, paged as they always were.
/// A filter or a sort makes one, and the tab holds its name so that it can be replaced when the
/// filter changes and removed when the tab closes.
/// </remarks>
/// <param name="Records">The records the grid binds to.</param>
/// <param name="View">The store's view they come from, or none for the table in file order.</param>
/// <param name="Count">How many records they are.</param>
public sealed record TableRows(TablePageList Records, string? View, long Count);

/// <summary>
/// One figure as the reader states it.
/// </summary>
/// <remarks>
/// The store computes exactly or says it cannot. What is added here is the one figure that cannot
/// be exact: an average is a sum divided by a count, and a division that does not come out is
/// rounded — so it is rounded to the column's own decimal places and marked as rounded, rather
/// than presented as though it were the figure itself.
/// </remarks>
/// <param name="Asked">The figure this answers.</param>
/// <param name="Value">What it came to, or null when there was nothing to compute it from.</param>
/// <param name="Missing">Records of the view that hold no value in that column.</param>
/// <param name="Exact">False when the figure could not be computed exactly.</param>
/// <param name="Rounded">True when the value shown is not the figure itself but a rounding of it.</param>
public sealed record FigureReading(
    ColumnFigure Asked,
    object? Value,
    long Missing,
    bool Exact,
    bool Rounded);
