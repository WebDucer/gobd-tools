using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Checks.Structure;

/// <summary>
/// Checks that the scalar values <c>index.xml</c> declares are usable.
/// </summary>
/// <remarks>
/// A grammar-valid document can still declare values no importer can act on — an
/// <c>Accuracy</c> of <c>"two"</c>, a decimal symbol identical to the grouping symbol, a date
/// mask with no year. The model keeps these verbatim precisely so they can be reported here.
/// </remarks>
public sealed class MetadataLintCheck : ICheck
{
    /// <inheritdoc />
    public IReadOnlyList<string> Codes =>
    [
        FindingCodes.NumericSettingInvalid,
        FindingCodes.SymbolCollision,
        FindingCodes.DelimiterCollision,
        FindingCodes.DateMaskUnusable,
        FindingCodes.RecordRangeInvalid,
    ];

    /// <inheritdoc />
    public IEnumerable<Finding> Run(CheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var table in context.DataSet.Tables)
        {
            foreach (var finding in InspectTable(table))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<Finding> InspectTable(TableNode table)
    {
        foreach (var finding in NonNegative(table.Identity, "SkipNumBytes", table.SkipNumBytes))
        {
            yield return finding;
        }

        foreach (var finding in NonNegative(table.Identity, "Epoch", table.Epoch))
        {
            yield return finding;
        }

        if (table.DecimalSymbol is { } decimalSymbol && table.DigitGroupingSymbol is { } grouping
            && string.Equals(decimalSymbol.Value, grouping.Value, StringComparison.Ordinal))
        {
            yield return Finding.Create(
                FindingCodes.SymbolCollision, decimalSymbol.Location, table.Identity, decimalSymbol.Value).About(FindingScope.Table(table.Identity));
        }

        if (table.Range is { } range)
        {
            // Range counts records from one; zero or negative cannot be acted on.
            if (!Masks.TryNonNegativeInteger(range.From.Value, out var from) || from < 1)
            {
                yield return Finding.Create(
                    FindingCodes.RecordRangeInvalid, range.Location, table.Identity, range.From.Value).About(FindingScope.Table(table.Identity));
            }

            foreach (var finding in NonNegative(table.Identity, "Range/To", range.To))
            {
                yield return finding;
            }

            foreach (var finding in NonNegative(table.Identity, "Range/Length", range.Length))
            {
                yield return finding;
            }
        }

        // A fixed record length counts characters, and a record of none holds nothing. Without a
        // record delimiter it also separates nothing, so no reading of the file could advance.
        if (table.Format is FixedLengthFormat { Length: { } recordLength }
            && (!Masks.TryNonNegativeInteger(recordLength.Value, out var characters) || characters < 1))
        {
            yield return Finding.Create(
                FindingCodes.NumericSettingInvalid, recordLength.Location,
                table.Identity, "Length", recordLength.Value).About(FindingScope.Table(table.Identity));
        }

        if (table.Validity?.Format is { } validityFormat && !Masks.IsUsableDateMask(validityFormat.Value))
        {
            yield return Finding.Create(
                FindingCodes.DateMaskUnusable, validityFormat.Location, table.Identity, "Validity", validityFormat.Value).About(FindingScope.Table(table.Identity));
        }

        if (table.Format is VariableLengthFormat variable)
        {
            foreach (var finding in InspectDelimiters(table, variable))
            {
                yield return finding;
            }
        }

        foreach (var column in table.Format?.Columns ?? [])
        {
            foreach (var finding in InspectColumn(table, column))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<Finding> InspectDelimiters(TableNode table, VariableLengthFormat format)
    {
        var column = format.ColumnDelimiter?.Value;
        var record = format.RecordDelimiter?.Value;
        var encapsulator = format.TextEncapsulator?.Value;

        if (!string.IsNullOrEmpty(column) && string.Equals(column, record, StringComparison.Ordinal))
        {
            yield return Finding.Create(
                FindingCodes.DelimiterCollision, format.Location,
                table.Identity, "ColumnDelimiter", "RecordDelimiter").About(FindingScope.Table(table.Identity));
        }

        // An empty TextEncapsulator means "none", which cannot collide with anything.
        if (!string.IsNullOrEmpty(column) && !string.IsNullOrEmpty(encapsulator)
            && string.Equals(column, encapsulator, StringComparison.Ordinal))
        {
            yield return Finding.Create(
                FindingCodes.DelimiterCollision, format.Location,
                table.Identity, "ColumnDelimiter", "TextEncapsulator").About(FindingScope.Table(table.Identity));
        }
    }

    private static IEnumerable<Finding> InspectColumn(TableNode table, ColumnNode column)
    {
        switch (column.Type)
        {
            case NumericType numeric:
                foreach (var finding in NonNegative(table.Identity, "Accuracy", numeric.Accuracy, column.Name.Value))
                {
                    yield return finding;
                }

                foreach (var finding in NonNegative(table.Identity, "ImpliedAccuracy", numeric.ImpliedAccuracy, column.Name.Value))
                {
                    yield return finding;
                }

                break;

            case AlphaNumericType { MaxLength: { } maxLength }:
                // A zero-length column could hold nothing, so zero is rejected here.
                if (!Masks.TryNonNegativeInteger(maxLength.Value, out var length) || length < 1)
                {
                    yield return Finding.Create(
                        FindingCodes.NumericSettingInvalid, maxLength.Location,
                        table.Identity, Qualify(column.Name.Value, "MaxLength"), maxLength.Value).About(FindingScope.Table(table.Identity));
                }

                break;

            case DateType { Format: { } mask } when !Masks.IsUsableDateMask(mask.Value):
                yield return Finding.Create(
                    FindingCodes.DateMaskUnusable, mask.Location,
                    table.Identity, column.Name.Value, mask.Value).About(FindingScope.Table(table.Identity));
                break;

            default:
                break;
        }
    }

    private static IEnumerable<Finding> NonNegative(string table, string element, TextNode? value, string? column = null)
    {
        if (value is null || Masks.TryNonNegativeInteger(value.Value, out _))
        {
            yield break;
        }

        yield return Finding.Create(
            FindingCodes.NumericSettingInvalid, value.Location,
            table, Qualify(column, element), value.Value).About(FindingScope.Table(table));
    }

    /// <summary>
    /// Names the offending setting, qualified by its column when it has one.
    /// </summary>
    /// <remarks>
    /// Both halves are identifiers copied from the document, joined by a separator, so this
    /// carries no prose and needs no translation. An earlier version passed the column as its own
    /// optional placeholder and rendered "Table 'Kunden'Betrag:" whenever it was present.
    /// </remarks>
    private static string Qualify(string? column, string element) =>
        string.IsNullOrEmpty(column) ? element : $"{column}/{element}";
}
