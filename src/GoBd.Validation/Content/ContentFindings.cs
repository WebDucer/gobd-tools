using System.Globalization;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Content;

/// <summary>
/// Turns what a content check found into the finding that reports it.
/// </summary>
/// <remarks>
/// Shared by both engines deliberately. The streaming reader and the store find defects by
/// entirely different means, but a defect must reach a report as the same code carrying the same
/// arguments whichever of them found it — otherwise "the two engines agree" would be a claim
/// about two independent pieces of formatting rather than about what was found. See the change's
/// design.md D4. That holds for the key findings as much as for the record ones, so they are
/// built here too.
/// </remarks>
public static class ContentFindings
{
    /// <summary>Separates the parts of a composite key where a finding quotes it.</summary>
    public const string KeySeparator = "|";

    /// <summary>Separates the record numbers a duplicated key occurs in.</summary>
    public const string RecordSeparator = ", ";

    /// <summary>Reports one defect found in one record of one table.</summary>
    public static Finding For(TableNode table, RecordLayout layout, long recordNumber, ContentDefect defect)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(defect);

        var number = Number(recordNumber);
        var column = defect.ColumnIndex is { } index ? ColumnName(layout, index) : string.Empty;

        // A column defect is quoted against the declaration it failed, which is where a reader
        // will look; a record defect has no column, so the table's own position serves.
        var location = defect.ColumnIndex is { } declared && declared < layout.Columns.Count
            ? layout.Columns[declared].Declaration.Location
            : table.Location;

        var finding = defect.Kind switch
        {
            ContentDefectKind.TypeOrFormatMismatch => Finding.Create(
                FindingCodes.RecordValueTypeMismatch, location,
                table.Identity, number, column, defect.Value ?? string.Empty, defect.Expected ?? string.Empty),

            ContentDefectKind.AccuracyExceeded => Finding.Create(
                FindingCodes.RecordValueAccuracyExceeded, location,
                table.Identity, number, column, defect.Value ?? string.Empty, defect.Expected ?? string.Empty),

            ContentDefectKind.MaxLengthExceeded => Finding.Create(
                FindingCodes.RecordValueTooLong, location,
                table.Identity, number, column, defect.Value ?? string.Empty, defect.Expected ?? string.Empty),

            ContentDefectKind.ColumnCountMismatch => Finding.Create(
                FindingCodes.RecordColumnCountMismatch, location,
                table.Identity, number, defect.Value ?? string.Empty, defect.Expected ?? string.Empty),

            ContentDefectKind.UndecodableBytes => Finding.Create(
                FindingCodes.RecordBytesUndecodable, location,
                table.Identity, number, column, defect.Expected ?? string.Empty),

            ContentDefectKind.UnterminatedEncapsulator => Finding.Create(
                FindingCodes.RecordEncapsulatorUnterminated, location,
                table.Identity, number, column, defect.Expected ?? string.Empty),

            _ => Finding.Create(
                FindingCodes.RecordLengthMismatch, location,
                table.Identity, number, defect.Value ?? string.Empty, defect.Expected ?? string.Empty),
        };

        return finding.About(FindingScope.Table(table.Identity));
    }

    /// <summary>Reports a table whose declaration could not be turned into a reading of its file.</summary>
    public static Finding TableNotReadable(TableNode table, LayoutFault fault)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Finding.Create(
            FindingCodes.TableNotReadable,
            table.Location,
            table.Identity,
            table.Url.Value,
            fault.ToString())
            .About(FindingScope.Table(table.Identity));
    }

    /// <summary>Reports a reference into a table that could not be read, and so was never compared.</summary>
    public static Finding ReferenceNotChecked(TableNode referring, ForeignKeyNode foreignKey, TableNode referenced)
    {
        ArgumentNullException.ThrowIfNull(referring);
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(referenced);
        return Finding.Create(
            FindingCodes.ReferenceNotChecked,
            foreignKey.References.Location,
            referring.Identity,
            referenced.Identity)
            .About(FindingScope.Table(referring.Identity));
    }

    /// <summary>Reports a record whose declared primary key is empty or only partly present.</summary>
    public static Finding PrimaryKeyIncomplete(TableNode table, long recordNumber, string key)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Finding.Create(
            FindingCodes.PrimaryKeyIncomplete,
            table.Location,
            table.Identity,
            Number(recordNumber),
            key)
            .About(FindingScope.Table(table.Identity));
    }

    /// <summary>Reports a primary key value that occurs in more than one record.</summary>
    /// <param name="table">The table carrying the key.</param>
    /// <param name="key">The key, as <see cref="Key"/> quotes it.</param>
    /// <param name="records">The records carrying it, as <see cref="Records"/> lists them.</param>
    public static Finding PrimaryKeyDuplicated(TableNode table, string key, string records)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Finding.Create(
            FindingCodes.PrimaryKeyDuplicated,
            table.Location,
            table.Identity,
            key,
            records)
            .About(FindingScope.Table(table.Identity));
    }

    /// <summary>Reports a foreign key value that matches no record of the table it references.</summary>
    public static Finding ForeignKeyValueUnresolved(
        TableNode referring,
        ForeignKeyNode foreignKey,
        long recordNumber,
        string key,
        TableNode referenced)
    {
        ArgumentNullException.ThrowIfNull(referring);
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(referenced);
        return Finding.Create(
            FindingCodes.ForeignKeyValueUnresolved,
            foreignKey.Location,
            referring.Identity,
            Number(recordNumber),
            key,
            referenced.Identity)
            .About(FindingScope.Table(referring.Identity));
    }

    /// <summary>A key as a finding quotes it: its parts, in declared order.</summary>
    public static string Key(IEnumerable<string> parts) => string.Join(KeySeparator, parts);

    /// <summary>Record numbers as a finding lists them.</summary>
    public static string Records(IEnumerable<long> numbers) => string.Join(RecordSeparator, numbers.Select(Number));

    /// <summary>
    /// The column's declared name, or its position when the record produced more values than the
    /// table declares columns for.
    /// </summary>
    public static string ColumnName(RecordLayout layout, int index)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return index < layout.Columns.Count
            ? layout.Columns[index].Name
            : (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
