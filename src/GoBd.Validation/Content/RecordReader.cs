using System.Globalization;
using System.Text;

namespace GoBd.Validation.Content;

/// <summary>
/// Reads a table's data file as the declaration defines it, and never as the file suggests.
/// </summary>
/// <remarks>
/// Nothing here is sniffed. Columns, their order, their types, the delimiters, the encapsulator,
/// the codepage and the excluded records all come from <c>index.xml</c>; a first record carrying
/// plausible column names is data like any other, and whether it is a header is a question the
/// header check answers by comparing it against the declaration.
/// <para>
/// Reading is forward-only and holds one record at a time, so a multi-gigabyte file costs the
/// same as a small one. That is what lets the CLI check contents without extracting a ZIP.
/// </para>
/// </remarks>
public static class RecordReader
{
    /// <summary>Reads every record the declaration reaches, excluded ones included.</summary>
    /// <param name="stream">The data file, positioned at its beginning.</param>
    /// <param name="layout">The layout resolved from the table's declaration.</param>
    public static IEnumerable<DataRecord> Read(Stream stream, RecordLayout layout)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(layout);

        return layout.IsReadable ? Records(stream, layout) : [];
    }

    private static IEnumerable<DataRecord> Records(Stream stream, RecordLayout layout)
    {
        Skip(stream, layout.SkipNumBytes);

        using var reader = new StreamReader(
            stream,
            layout.Encoding,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 64 * 1024,
            leaveOpen: true);

        var lookahead = Math.Max(
            layout.RecordDelimiter.Length,
            Math.Max(layout.ColumnDelimiter.Length, layout.TextEncapsulator?.Length ?? 1));
        var cursor = new CharCursor(reader, lookahead + 1);
        DiscardByteOrderMark(cursor);

        var number = 0L;
        while (!cursor.EndOfInput)
        {
            number++;
            var record = layout.IsFixedLength
                ? ReadFixed(cursor, layout, number)
                : ReadVariable(cursor, layout, number);

            yield return record;

            if (layout.LastRecord is { } last && number >= last)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Adds the undecodable-bytes defect when the declared codepage could not represent what the
    /// file held at some position in this record.
    /// </summary>
    private static void CheckDecoding(List<string> values, List<ContentDefect> defects, RecordLayout layout)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index].Contains(DeclaredEncoding.Sentinel, StringComparison.Ordinal))
            {
                defects.Add(new ContentDefect(
                    ContentDefectKind.UndecodableBytes,
                    index,
                    null,
                    DeclaredEncoding.NameOf(layout.Table.Codepage)));
                return;
            }
        }
    }

    private static DataRecord ReadVariable(CharCursor cursor, RecordLayout layout, long number)
    {
        var values = new List<string>(layout.Columns.Count);
        var defects = new List<ContentDefect>();
        var value = new StringBuilder();
        var encapsulator = layout.TextEncapsulator;

        while (true)
        {
            value.Clear();

            if (encapsulator is not null && cursor.StartsWith(encapsulator))
            {
                cursor.Skip(encapsulator.Length);
                var closed = false;
                while (!cursor.EndOfInput)
                {
                    if (cursor.StartsWith(encapsulator))
                    {
                        cursor.Skip(encapsulator.Length);

                        // A doubled encapsulator stands for one literal encapsulator; a single
                        // one ends the value.
                        if (cursor.StartsWith(encapsulator))
                        {
                            cursor.Skip(encapsulator.Length);
                            value.Append(encapsulator);
                            continue;
                        }

                        closed = true;
                        break;
                    }

                    value.Append(cursor.Read());
                }

                if (!closed)
                {
                    defects.Add(new ContentDefect(
                        ContentDefectKind.UnterminatedEncapsulator,
                        values.Count,
                        null,
                        encapsulator));
                }
            }

            // Whether the value was encapsulated or not, everything up to the next delimiter
            // belongs to it. Text after a closing encapsulator is not something the declaration
            // describes; keeping it is what the store's reader does, so the two agree.
            while (!cursor.EndOfInput
                && !cursor.StartsWith(layout.ColumnDelimiter)
                && !cursor.StartsWith(layout.RecordDelimiter))
            {
                value.Append(cursor.Read());
            }

            values.Add(value.ToString());

            if (cursor.StartsWith(layout.ColumnDelimiter))
            {
                cursor.Skip(layout.ColumnDelimiter.Length);
                continue;
            }

            if (cursor.StartsWith(layout.RecordDelimiter))
            {
                cursor.Skip(layout.RecordDelimiter.Length);
            }

            break;
        }

        CheckDecoding(values, defects, layout);

        if (values.Count != layout.Columns.Count)
        {
            defects.Add(new ContentDefect(
                ContentDefectKind.ColumnCountMismatch,
                null,
                values.Count.ToString(CultureInfo.InvariantCulture),
                layout.Columns.Count.ToString(CultureInfo.InvariantCulture)));
        }

        return new DataRecord(number, layout.IsDataRecord(number), values, defects);
    }

    private static DataRecord ReadFixed(CharCursor cursor, RecordLayout layout, long number)
    {
        var declaredLength = layout.FixedRecordLength ?? 0;
        var defects = new List<ContentDefect>();
        var text = new StringBuilder(declaredLength);

        if (layout.RecordDelimiter.Length > 0)
        {
            while (!cursor.EndOfInput && !cursor.StartsWith(layout.RecordDelimiter))
            {
                text.Append(cursor.Read());
            }

            if (cursor.StartsWith(layout.RecordDelimiter))
            {
                cursor.Skip(layout.RecordDelimiter.Length);
            }
        }
        else
        {
            // With no delimiter the declared length is the only thing separating one record from
            // the next, so a short final record is both the end of the file and a defect.
            while (text.Length < declaredLength && !cursor.EndOfInput)
            {
                text.Append(cursor.Read());
            }
        }

        var record = text.ToString();
        if (record.Length != declaredLength)
        {
            defects.Add(new ContentDefect(
                ContentDefectKind.FixedRecordLengthMismatch,
                null,
                record.Length.ToString(CultureInfo.InvariantCulture),
                declaredLength.ToString(CultureInfo.InvariantCulture)));
        }

        var values = new List<string>(layout.Columns.Count);
        foreach (var column in layout.Columns)
        {
            var span = column.Span!.Value;
            var from = span.From - 1;
            if (from >= record.Length)
            {
                values.Add(string.Empty);
                continue;
            }

            var available = Math.Min(span.Length, record.Length - from);
            values.Add(record.Substring(from, available));
        }

        CheckDecoding(values, defects, layout);
        return new DataRecord(number, layout.IsDataRecord(number), values, defects);
    }

    private static void DiscardByteOrderMark(CharCursor cursor)
    {
        // The declared codepage decides how the file is read; a byte order mark is a leftover of
        // how it was written. Left in place it would prefix the first value of the first record.
        if (cursor.TryPeek(0, out var first) && first == '\uFEFF')
        {
            cursor.Skip(1);
        }
    }

    private static void Skip(Stream stream, long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        // A ZIP member is not seekable, and the CLI reads exports without extracting them, so
        // the skip is a read that discards rather than a seek.
        var buffer = new byte[Math.Min(bytes, 64 * 1024)];
        var remaining = bytes;
        while (remaining > 0)
        {
            var read = stream.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));
            if (read <= 0)
            {
                return;
            }

            remaining -= read;
        }
    }
}
