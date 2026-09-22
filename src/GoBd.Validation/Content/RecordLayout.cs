using System.Globalization;
using System.Text;
using GoBd.Validation.Checks;
using GoBd.Validation.Model;

namespace GoBd.Validation.Content;

/// <summary>Why a declared table cannot be read at all.</summary>
public enum LayoutFault
{
    /// <summary>The layout is usable.</summary>
    None,

    /// <summary>The table declares neither a variable-length nor a fixed-length format.</summary>
    NoFormat,

    /// <summary>The declared codepage cannot be supplied by this build.</summary>
    CodepageUnavailable,

    /// <summary>A declared delimiter is empty, so records or columns cannot be separated.</summary>
    DelimiterEmpty,

    /// <summary>A fixed-length column declares a span that cannot be read.</summary>
    FixedSpanUnusable,

    /// <summary>
    /// A fixed-length table's records are zero characters long and not delimited, so one record
    /// cannot be told from the next.
    /// </summary>
    RecordLengthUnusable,
}

/// <summary>A column's span within a fixed-length record, in characters.</summary>
/// <param name="From">One-based position of the first character.</param>
/// <param name="Length">Number of characters the column occupies.</param>
public readonly record struct FixedSpan(int From, int Length)
{
    /// <summary>One-based position of the last character.</summary>
    public int To => From + Length - 1;
}

/// <summary>One column, as the reader needs it.</summary>
/// <param name="Declaration">The column as <c>index.xml</c> declares it.</param>
/// <param name="Index">Zero-based position among the table's declared columns.</param>
/// <param name="Span">Span within a fixed-length record; absent for a variable-length table.</param>
public sealed record ColumnLayout(ColumnNode Declaration, int Index, FixedSpan? Span)
{
    /// <summary>Declared column name — the only place a name exists when the file has no header.</summary>
    public string Name => Declaration.Name.Value;

    /// <summary>Declared datatype.</summary>
    public ColumnType Type => Declaration.Type;
}

/// <summary>
/// Everything needed to read a table's data file, resolved from its declaration.
/// </summary>
/// <remarks>
/// A GoBD data file carries no self-describing structure — frequently not even a header row — so
/// <c>index.xml</c> is the sole authority on how to read it. Every default applied here is the
/// one the standard's own DTD documents: <c>;</c> between columns, CRLF between records, <c>"</c>
/// around text, <c>,</c> for decimals and <c>.</c> for digit grouping, an epoch of 30, no bytes
/// skipped, and ANSI when no codepage is declared.
/// <para>
/// A declared scalar that cannot be read as a number is left at its default here rather than
/// reported: the structural checks already report it (<c>GOBD3019</c> and its neighbours), and a
/// second finding for one mistake helps nobody.
/// </para>
/// </remarks>
public sealed class RecordLayout
{
    private const string DefaultColumnDelimiter = ";";
    private const string DefaultRecordDelimiter = "\r\n";
    private const string DefaultTextEncapsulator = "\"";
    private const string DefaultDecimalSymbol = ",";
    private const string DefaultDigitGroupingSymbol = ".";
    private const int DefaultEpoch = 30;

    private RecordLayout(TableNode table, LayoutFault fault)
    {
        Table = table;
        Fault = fault;
        Encoding = Encoding.UTF8;
        ColumnDelimiter = DefaultColumnDelimiter;
        RecordDelimiter = DefaultRecordDelimiter;
        TextEncapsulator = DefaultTextEncapsulator;
        DecimalSymbol = DefaultDecimalSymbol;
        DigitGroupingSymbol = DefaultDigitGroupingSymbol;
        Epoch = DefaultEpoch;
        Columns = [];
        FirstRecord = 1;
    }

    private RecordLayout(
        TableNode table,
        bool isFixedLength,
        Encoding encoding,
        string columnDelimiter,
        string recordDelimiter,
        string? textEncapsulator,
        int? fixedRecordLength,
        IReadOnlyList<ColumnLayout> columns,
        long skipNumBytes,
        long firstRecord,
        long? lastRecord,
        string decimalSymbol,
        string digitGroupingSymbol,
        int epoch)
    {
        Table = table;
        Fault = LayoutFault.None;
        IsFixedLength = isFixedLength;
        Encoding = encoding;
        ColumnDelimiter = columnDelimiter;
        RecordDelimiter = recordDelimiter;
        TextEncapsulator = textEncapsulator;
        FixedRecordLength = fixedRecordLength;
        Columns = columns;
        SkipNumBytes = skipNumBytes;
        FirstRecord = firstRecord;
        LastRecord = lastRecord;
        DecimalSymbol = decimalSymbol;
        DigitGroupingSymbol = digitGroupingSymbol;
        Epoch = epoch;
    }

    /// <summary>The table this layout reads.</summary>
    public TableNode Table { get; }

    /// <summary>How a <c>References</c> names this table.</summary>
    public string Identity => Table.Identity;

    /// <summary>Why the table cannot be read, or <see cref="LayoutFault.None"/>.</summary>
    public LayoutFault Fault { get; }

    /// <summary>True when the declaration is complete enough to read the file.</summary>
    public bool IsReadable => Fault == LayoutFault.None;

    /// <summary>True when records are positional rather than delimited.</summary>
    public bool IsFixedLength { get; }

    /// <summary>The encoding the declared codepage resolves to.</summary>
    public Encoding Encoding { get; }

    /// <summary>Text between columns of a variable-length record.</summary>
    public string ColumnDelimiter { get; }

    /// <summary>Text between records.</summary>
    public string RecordDelimiter { get; }

    /// <summary>Text surrounding an encapsulated value, or <see langword="null"/> when none is declared.</summary>
    public string? TextEncapsulator { get; }

    /// <summary>Declared length of a fixed-length record, in characters.</summary>
    public int? FixedRecordLength { get; }

    /// <summary>Declared columns, in the order the file delivers them.</summary>
    public IReadOnlyList<ColumnLayout> Columns { get; }

    /// <summary>Bytes to discard before the first record begins.</summary>
    public long SkipNumBytes { get; }

    /// <summary>One-based number of the first record that counts as data.</summary>
    public long FirstRecord { get; }

    /// <summary>One-based number of the last record that counts as data, when bounded.</summary>
    public long? LastRecord { get; }

    /// <summary>Declared decimal separator.</summary>
    public string DecimalSymbol { get; }

    /// <summary>Declared digit grouping separator.</summary>
    public string DigitGroupingSymbol { get; }

    /// <summary>Two-digit-year window: a year below this belongs to the twenty-first century.</summary>
    public int Epoch { get; }

    /// <summary>True when the record at this one-based position is data rather than excluded.</summary>
    public bool IsDataRecord(long recordNumber) =>
        recordNumber >= FirstRecord && (LastRecord is not { } last || recordNumber <= last);

    /// <summary>Resolves a table's declaration into a layout, or states why it cannot be read.</summary>
    public static RecordLayout For(TableNode table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.Format is not { } format)
        {
            return new RecordLayout(table, LayoutFault.NoFormat);
        }

        if (!DeclaredEncoding.TryResolve(table.Codepage, out var encoding))
        {
            return new RecordLayout(table, LayoutFault.CodepageUnavailable);
        }

        var (first, last) = ResolveRange(table.Range);
        var skip = Scalar(table.SkipNumBytes, 0);
        var epoch = Scalar(table.Epoch, DefaultEpoch);
        var decimalSymbol = Declared(table.DecimalSymbol, DefaultDecimalSymbol);
        var groupingSymbol = Declared(table.DigitGroupingSymbol, DefaultDigitGroupingSymbol);

        return format switch
        {
            VariableLengthFormat variable => Variable(table, variable, encoding, skip, first, last, decimalSymbol, groupingSymbol, epoch),
            FixedLengthFormat fixedLength => Fixed(table, fixedLength, encoding, skip, first, last, decimalSymbol, groupingSymbol, epoch),
            _ => new RecordLayout(table, LayoutFault.NoFormat),
        };
    }

    private static RecordLayout Variable(
        TableNode table,
        VariableLengthFormat format,
        Encoding encoding,
        long skip,
        long first,
        long? last,
        string decimalSymbol,
        string groupingSymbol,
        int epoch)
    {
        var columnDelimiter = Declared(format.ColumnDelimiter, DefaultColumnDelimiter);
        var recordDelimiter = Declared(format.RecordDelimiter, DefaultRecordDelimiter);
        if (columnDelimiter.Length == 0 || recordDelimiter.Length == 0)
        {
            return new RecordLayout(table, LayoutFault.DelimiterEmpty);
        }

        // An explicitly empty TextEncapsulator is a deliberate "no encapsulation", which is not
        // the same as declaring nothing and inheriting the double quote.
        var encapsulator = format.TextEncapsulator is { } declared
            ? (declared.Value.Length == 0 ? null : declared.Value)
            : DefaultTextEncapsulator;

        var columns = format.Columns
            .Select((column, index) => new ColumnLayout(column, index, null))
            .ToArray();

        return new RecordLayout(
            table,
            isFixedLength: false,
            encoding,
            columnDelimiter,
            recordDelimiter,
            encapsulator,
            fixedRecordLength: null,
            columns,
            skip,
            first,
            last,
            decimalSymbol,
            groupingSymbol,
            epoch);
    }

    private static RecordLayout Fixed(
        TableNode table,
        FixedLengthFormat format,
        Encoding encoding,
        long skip,
        long first,
        long? last,
        string decimalSymbol,
        string groupingSymbol,
        int epoch)
    {
        var columns = new List<ColumnLayout>(format.Columns.Count);
        for (var index = 0; index < format.Columns.Count; index++)
        {
            var column = format.Columns[index];
            if (ResolveSpan(column.FixedRange) is not { } span)
            {
                return new RecordLayout(table, LayoutFault.FixedSpanUnusable);
            }

            columns.Add(new ColumnLayout(column, index, span));
        }

        // A fixed-length table declares a record length, a record delimiter, or neither. With
        // neither, the record is exactly as long as the declared columns reach: the last span's
        // end is the only length the document states.
        var declaredLength = format.Length is { } length && Masks.TryNonNegativeInteger(length.Value, out var parsed)
            ? parsed
            : (int?)null;
        var spanned = columns.Count == 0 ? 0 : columns.Max(column => column.Span!.Value.To);
        var recordLength = declaredLength ?? spanned;
        var recordDelimiter = format.RecordDelimiter?.Value;

        // With no delimiter, the length is the only thing that separates one record from the
        // next, so a length of zero separates nothing: reading would never get past the start of
        // the file, and would report an empty record there for as long as it was let run.
        if (recordLength < 1 && string.IsNullOrEmpty(recordDelimiter))
        {
            return new RecordLayout(table, LayoutFault.RecordLengthUnusable);
        }

        return new RecordLayout(
            table,
            isFixedLength: true,
            encoding,
            DefaultColumnDelimiter,
            string.IsNullOrEmpty(recordDelimiter) ? string.Empty : recordDelimiter,
            textEncapsulator: null,
            recordLength,
            columns,
            skip,
            first,
            last,
            decimalSymbol,
            groupingSymbol,
            epoch);
    }

    private static FixedSpan? ResolveSpan(FixedRangeNode? range)
    {
        if (range is null || !Masks.TryNonNegativeInteger(range.From.Value, out var from) || from < 1)
        {
            return null;
        }

        if (range.To is { } to)
        {
            return Masks.TryNonNegativeInteger(to.Value, out var end) && end >= from
                ? new FixedSpan(from, end - from + 1)
                : null;
        }

        return range.Length is { } length && Masks.TryNonNegativeInteger(length.Value, out var count) && count > 0
            ? new FixedSpan(from, count)
            : null;
    }

    private static (long First, long? Last) ResolveRange(RangeNode? range)
    {
        if (range is null || !long.TryParse(range.From.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var from) || from < 1)
        {
            return (1, null);
        }

        if (range.To is { } to && long.TryParse(to.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var last))
        {
            return (from, last);
        }

        return range.Length is { } length && long.TryParse(length.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? (from, from + count - 1)
            : (from, null);
    }

    private static long Scalar(TextNode? declared, long fallback) =>
        declared is not null && long.TryParse(declared.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static int Scalar(TextNode? declared, int fallback) =>
        declared is not null && Masks.TryNonNegativeInteger(declared.Value, out var parsed) ? parsed : fallback;

    private static string Declared(TextNode? node, string fallback) => node?.Value ?? fallback;
}
