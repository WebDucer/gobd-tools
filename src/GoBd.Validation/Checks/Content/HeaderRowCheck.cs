using GoBd.Validation.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Content;

/// <summary>
/// Compares a file's header row, where it has one, against the columns declared for it.
/// </summary>
/// <remarks>
/// A header row is the one place a data file states its own structure, which makes it the one
/// place the file and <c>index.xml</c> can be caught disagreeing. The disagreement that matters
/// is order: a header carrying the declared names in a different sequence means every value is
/// read into the wrong column, every value is individually plausible, and no other check sees
/// anything wrong.
/// <para>
/// Only the first record is read, so this costs nothing on a large export.
/// </para>
/// </remarks>
public sealed class HeaderRowCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes => [FindingCodes.HeaderOrderDiffers, FindingCodes.HeaderUndeclared];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var table in context.DataSet.Tables)
        {
            if (ForTable(context, table) is { } finding)
            {
                yield return finding;
            }
        }
    }

    /// <summary>
    /// The header finding for one table, or none when its first record is not a header.
    /// </summary>
    /// <remarks>
    /// Public because the reader runs this check one table at a time, as it reads that table,
    /// rather than over the whole export in one pass. The check reads a single record either
    /// way; what differs is when the finding arrives, and the reader needs it to arrive with the
    /// table it concerns.
    /// </remarks>
    public static Finding? ForTable(CheckContext context, TableNode table)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(table);

        var data = TableData.For(context, table);
        if (!data.IsReady)
        {
            return null;
        }

        var layout = data.Layout;
        using var stream = context.Source.OpenRead(data.EntryName);
        if (RecordReader.Read(stream, layout).FirstOrDefault() is not { } first)
        {
            return null;
        }

        var declared = layout.Columns.Select(column => column.Name).ToArray();
        var found = first.Values.Select(value => value.Trim()).ToArray();

        // A record that is not the declared names, in some order, is not a header. A declaration
        // may legitimately exclude a preamble, and reporting one as a mis-ordered header would
        // be a false accusation about a file that is correct.
        if (found.Length != declared.Length || !IsPermutation(declared, found))
        {
            return null;
        }

        if (first.IsData)
        {
            return Finding.Create(
                FindingCodes.HeaderUndeclared,
                table.Location,
                table.Identity,
                string.Join(", ", found))
                .About(FindingScope.Table(table.Identity));
        }

        return declared.SequenceEqual(found, StringComparer.Ordinal)
            ? null
            : Finding.Create(
                FindingCodes.HeaderOrderDiffers,
                table.Format?.Location ?? table.Location,
                table.Identity,
                string.Join(", ", declared),
                string.Join(", ", found))
                .About(FindingScope.Table(table.Identity));
    }

    private static bool IsPermutation(string[] declared, string[] found) =>
        declared.Order(StringComparer.Ordinal).SequenceEqual(found.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
