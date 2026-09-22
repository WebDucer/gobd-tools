using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Checks that the positional spans of a <c>FixedLength</c> table describe a coherent record.
/// </summary>
/// <remarks>
/// Spans are 1-based and inclusive. Overlaps and gaps are legal but suspicious — a composite
/// field exposed both whole and in parts is a real pattern — so they are reported below error.
/// </remarks>
public sealed class FixedRangeCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.FixedRangeInvalid,
        FindingCodes.FixedRangeOverlap,
        FindingCodes.FixedRangeGap,
        FindingCodes.FixedRangeExceedsRecordLength,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var table in context.DataSet.Tables)
        {
            if (table.Format is not FixedLengthFormat format)
            {
                continue;
            }

            foreach (var finding in Inspect(table, format))
            {
                yield return finding;
            }
        }
    }

    private sealed record Span(ColumnNode Column, int Start, int End);

    private static IEnumerable<Finding> Inspect(TableNode table, FixedLengthFormat format)
    {
        var spans = new List<Span>();

        foreach (var column in format.Columns)
        {
            if (column.FixedRange is not { } range)
            {
                continue;
            }

            if (!Masks.TryNonNegativeInteger(range.From.Value, out var start) || start < 1)
            {
                yield return Finding.Create(
                    FindingCodes.FixedRangeInvalid, range.Location,
                    table.Identity, column.Name.Value, range.From.Value).About(FindingScope.Table(table.Identity));
                continue;
            }

            int end;
            if (range.To is { } to)
            {
                if (!Masks.TryNonNegativeInteger(to.Value, out end) || end < start)
                {
                    yield return Finding.Create(
                        FindingCodes.FixedRangeInvalid, range.Location,
                        table.Identity, column.Name.Value, to.Value).About(FindingScope.Table(table.Identity));
                    continue;
                }
            }
            else if (range.Length is { } length)
            {
                if (!Masks.TryNonNegativeInteger(length.Value, out var size) || size < 1)
                {
                    yield return Finding.Create(
                        FindingCodes.FixedRangeInvalid, range.Location,
                        table.Identity, column.Name.Value, length.Value).About(FindingScope.Table(table.Identity));
                    continue;
                }

                end = start + size - 1;
            }
            else
            {
                yield return Finding.Create(
                    FindingCodes.FixedRangeInvalid, range.Location,
                    table.Identity, column.Name.Value, string.Empty).About(FindingScope.Table(table.Identity));
                continue;
            }

            spans.Add(new Span(column, start, end));
        }

        var ordered = spans.OrderBy(span => span.Start).ThenBy(span => span.End).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];

            if (current.Start <= previous.End)
            {
                yield return Finding.Create(
                    FindingCodes.FixedRangeOverlap, current.Column.Location,
                    table.Identity, previous.Column.Name.Value, current.Column.Name.Value).About(FindingScope.Table(table.Identity));
            }
            else if (current.Start > previous.End + 1)
            {
                yield return Finding.Create(
                    FindingCodes.FixedRangeGap, current.Column.Location,
                    table.Identity,
                    Invariant.Integer(previous.End + 1),
                    Invariant.Integer(current.Start - 1)).About(FindingScope.Table(table.Identity));
            }
        }

        if (format.Length is { } declared
            && Masks.TryNonNegativeInteger(declared.Value, out var recordLength)
            && recordLength > 0)
        {
            foreach (var span in ordered.Where(candidate => candidate.End > recordLength))
            {
                yield return Finding.Create(
                    FindingCodes.FixedRangeExceedsRecordLength, span.Column.Location,
                    table.Identity, span.Column.Name.Value,
                    Invariant.Integer(span.End), declared.Value).About(FindingScope.Table(table.Identity));
            }
        }
    }
}
