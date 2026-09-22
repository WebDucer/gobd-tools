using System.Globalization;
using System.Text;
using GoBd.Validation.Content;
using GoBd.Validation.Sources;

namespace GoBd.Reader.Data;

/// <summary>One defect found while putting a table where the store can read it.</summary>
/// <param name="Record">The record it was found in, numbered as the file counts records.</param>
/// <param name="Defect">What was wrong.</param>
public sealed record ExtractionDefect(long Record, ContentDefect Defect);

/// <summary>What extraction produced.</summary>
/// <param name="Path">The file the store reads.</param>
/// <param name="Defects">
/// Structural defects found on the way, in record order, collected to one past the bound.
/// </param>
/// <param name="Records">Data records written.</param>
/// <param name="Bytes">Bytes read from the entry, which is what progress is measured in.</param>
public sealed record ExtractedFile(
    string Path,
    IReadOnlyList<ExtractionDefect> Defects,
    long Records,
    long Bytes);

/// <summary>
/// Puts a table's data where the store can read it, numbered as the file numbers it.
/// </summary>
/// <remarks>
/// Extraction is unavoidable rather than a convenience. A ZIP member has no path to hand to the
/// store, three of the six codepages the standard permits are not encodings the store's CSV
/// reader accepts, a fixed-length table has no delimiters at all, and a declared record
/// delimiter that is not a line ending — which the standard permits — is not something that
/// reader can be told to use.
/// <para>
/// <b>The record number is written into the file rather than derived on import.</b> That is the
/// decision this whole class turns on. The store's CSV reader drops a record it cannot parse,
/// so a row number computed during import counts surviving records, not records — one dropped
/// record at the top of a file and every number after it is wrong, and a finding that cites the
/// wrong record sends its reader to the wrong line. Numbering here, where the records are being
/// read one at a time anyway, makes the two engines agree on record numbers by construction.
/// See the change's design.md D3.
/// </para>
/// <para>
/// The consequence, stated plainly: for the store, this tool's reader decides where one record
/// ends and the next begins, and the store reads what it wrote. The store's own parser remains
/// the definition of the readings the declaration does not fix — that is pinned by holding this
/// reader to it on the original files, not by the import path.
/// </para>
/// </remarks>
public static class TableExtraction
{
    /// <summary>Column the record number is written into, ahead of the declared columns.</summary>
    public const string OrdinalColumn = "_ordinal";

    private const char Delimiter = ',';
    private const char Quote = '"';

    /// <summary>Extracts one table's data records into the store's directory.</summary>
    /// <param name="source">The export.</param>
    /// <param name="entryName">Entry holding the table's data.</param>
    /// <param name="layout">The layout resolved from the table's declaration.</param>
    /// <param name="targetPath">Where to write the extracted file.</param>
    /// <param name="bound">Structural defects the table may report; none are collected past it.</param>
    /// <param name="read">Told the bytes read so far, as they are read.</param>
    /// <param name="cancellationToken">Stops extraction between records.</param>
    public static ExtractedFile Extract(
        IExportSource source,
        string entryName,
        RecordLayout layout,
        string targetPath,
        int bound,
        Action<long>? read = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(layout);

        var defects = new List<ExtractionDefect>();
        var records = 0L;

        // Wrapped whether or not anyone is listening, so that the bytes read are reported back
        // either way: the pipeline totals them across tables, and a run that was cancelled has
        // read fewer bytes than the entry is long, which is a difference worth being able to see.
        var counting = new CountingStream(source.OpenRead(entryName), read ?? (_ => { }));

        using (counting)
        using (var writer = new StreamWriter(
            targetPath,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            foreach (var record in RecordReader.Read(counting, layout))
            {
                // Between records, never inside one: a half-written record in the extract would
                // be read back as data. One record is the unit extraction holds at a time, so it
                // is also the granularity at which it can stop. See the change's design.md D2.
                cancellationToken.ThrowIfCancellationRequested();

                if (!record.IsData)
                {
                    continue;
                }

                // Collected to one past the bound, as every other stage reads, so that the budget
                // records "there are further defects" by being asked for one more than it allows
                // rather than being told.
                if (defects.Count <= bound)
                {
                    foreach (var defect in record.Defects)
                    {
                        defects.Add(new ExtractionDefect(record.Number, defect));
                    }
                }

                // Written however defective the table, and to its end. A defective table is shown
                // as a report rather than a grid, but other tables' keys are still checked against
                // it: a store holding only its first records would report every reference to a
                // later one as unresolved, which the streaming engine — reading the whole file —
                // does not.
                Write(writer, record, layout);
                records++;
            }
        }

        return new ExtractedFile(targetPath, defects, records, counting.Total);
    }

    /// <summary>
    /// Writes one record: its number, then exactly as many values as the table declares.
    /// </summary>
    /// <remarks>
    /// Padded or truncated to the declared width on purpose. A record of the wrong width is
    /// already reported as a defect, and letting the store reject the row instead would lose the
    /// record entirely — including the values that are perfectly readable and that a person
    /// looking at the table needs to see.
    /// </remarks>
    private static void Write(TextWriter writer, DataRecord record, RecordLayout layout)
    {
        Span<char> number = stackalloc char[20];
        record.Number.TryFormat(number, out var written, provider: CultureInfo.InvariantCulture);
        writer.Write(number[..written]);

        for (var index = 0; index < layout.Columns.Count; index++)
        {
            writer.Write(Delimiter);
            writer.Write(Quote);
            if (index < record.Values.Count)
            {
                Escape(writer, record.Values[index]);
            }

            writer.Write(Quote);
        }

        writer.Write('\n');
    }

    /// <summary>Writes a value with each quote doubled, in runs rather than character by character.</summary>
    private static void Escape(TextWriter writer, string value)
    {
        var rest = value.AsSpan();
        int quote;
        while ((quote = rest.IndexOf(Quote)) >= 0)
        {
            writer.Write(rest[..(quote + 1)]);
            writer.Write(Quote);
            rest = rest[(quote + 1)..];
        }

        writer.Write(rest);
    }
}
