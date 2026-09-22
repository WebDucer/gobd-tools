using System.Globalization;
using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Content;

/// <summary>
/// Reports each way in which a record fails the layout its table declares.
/// </summary>
/// <remarks>
/// The check reads each declared data file once, forward only, and holds one record at a time,
/// so a multi-gigabyte export costs memory proportional to a single record. It stops at the
/// configured bound per table, which makes a wholly broken file cheap.
/// <para>
/// Findings arrive in record order because the file is read in record order. That is what the
/// specification requires and what lets the reader's store — which reads the same file in
/// parallel — be held to the same report: it sorts its rejects by line and takes the same first
/// N. See the change's design.md D4.
/// </para>
/// </remarks>
public sealed class RecordConformanceCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.RecordValueTypeMismatch,
        FindingCodes.RecordValueAccuracyExceeded,
        FindingCodes.RecordValueTooLong,
        FindingCodes.RecordColumnCountMismatch,
        FindingCodes.RecordBytesUndecodable,
        FindingCodes.RecordEncapsulatorUnterminated,
        FindingCodes.RecordLengthMismatch,
        FindingCodes.TableAnalysisStopped,
        FindingCodes.TableNotReadable,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var table in context.DataSet.Tables)
        {
            foreach (var finding in ForTable(context, table))
            {
                yield return finding;
            }
        }

        // A table's findings arrive from more than one pass, so the notice that its list was cut
        // short is claimed from the shared budget rather than emitted by whichever pass filled it.
        foreach (var notice in context.Budget.ClaimNotices())
        {
            yield return notice;
        }
    }

    private static IEnumerable<Finding> ForTable(CheckContext context, TableNode table)
    {
        var data = TableData.For(context, table);
        if (data.Status == TableDataStatus.LayoutUnusable)
        {
            yield return ContentFindings.TableNotReadable(table, data.Layout.Fault);
            yield break;
        }

        if (!data.IsReady)
        {
            yield break;
        }

        var layout = data.Layout;
        var budget = context.Budget;

        using var stream = context.Source.OpenRead(data.EntryName);
        foreach (var record in RecordReader.Read(stream, layout))
        {
            if (!record.IsData)
            {
                continue;
            }

            foreach (var defect in Ordered(record, layout))
            {
                // Reading on would cost a scan of the rest of a file that is already known to be
                // defective, and would report nothing further.
                if (!budget.TryReport(table))
                {
                    yield break;
                }

                yield return ContentFindings.For(table, layout, record.Number, defect);
            }
        }
    }

    /// <summary>
    /// Every defect of one record, in a fixed order: what is wrong with the record itself, then
    /// its columns from left to right.
    /// </summary>
    /// <remarks>
    /// The order is part of the contract rather than an accident of implementation. Two engines
    /// must produce the same report for the same file, and a bound that stops mid-record would
    /// otherwise keep whichever defects each engine happened to find first.
    /// </remarks>
    private static IEnumerable<ContentDefect> Ordered(DataRecord record, RecordLayout layout)
    {
        var defects = new List<ContentDefect>(record.Defects);
        var shared = Math.Min(record.Values.Count, layout.Columns.Count);
        for (var index = 0; index < shared; index++)
        {
            if (ValueInterpreter.Interpret(record.Values[index], layout.Columns[index], layout).Defect is { } defect)
            {
                defects.Add(defect);
            }
        }

        // Most records have no defect and nearly all the rest have one, so the order only needs
        // establishing when there is more than one to order.
        return defects.Count <= 1 ? defects : defects.OrderBy(defect => defect.ColumnIndex ?? -1);
    }
}
