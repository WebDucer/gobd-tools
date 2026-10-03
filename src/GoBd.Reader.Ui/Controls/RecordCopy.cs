using System.Text;
using GoBd.Reader.Ui.ViewModels;

namespace GoBd.Reader.Ui.Controls;

/// <summary>
/// A record as it goes to the clipboard: a line of column names and a line of values, separated
/// by tabs, as a spreadsheet pastes them.
/// </summary>
/// <remarks>
/// The values as they are shown, which is as the file stores them with only a declared value
/// redefinition applied; quoting someone else's figure must not change it. The record number leads,
/// because it is what cites the record; the position in the view does not, because it is not data.
/// See the improve-reader-accessibility change's design.md D13.
/// </remarks>
internal static class RecordCopy
{
    /// <summary>The two lines that stand for a record on the clipboard.</summary>
    /// <param name="recordColumn">What the record number's column is called, in the display language.</param>
    /// <param name="columns">The declared columns' names.</param>
    /// <param name="row">The record.</param>
    public static string Text(string recordColumn, IReadOnlyList<string> columns, RecordRow row)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(row);

        var text = new StringBuilder();
        Line(text, [recordColumn, .. columns]);
        text.Append(Environment.NewLine);
        Line(text, [row.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), .. row.Values]);
        return text.ToString();
    }

    private static void Line(StringBuilder text, IReadOnlyList<string> cells)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (index > 0)
            {
                text.Append('\t');
            }

            text.Append(Cell(cells[index]));
        }
    }

    /// <summary>
    /// A cell as a spreadsheet reads it: quoted, with its quotation marks doubled, wherever it holds
    /// what would otherwise end the cell or the line.
    /// </summary>
    internal static string Cell(string value) =>
        value.AsSpan().IndexOfAny("\t\r\n\"") < 0
            ? value
            : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
