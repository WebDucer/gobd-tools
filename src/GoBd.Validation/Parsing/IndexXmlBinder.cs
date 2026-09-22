using System.Xml.Linq;
using GoBd.Validation.Collections;
using GoBd.Validation.Findings;
using GoBd.Validation.Model;

namespace GoBd.Validation.Parsing;

/// <summary>
/// Builds the immutable model from a parsed document, recording each node's source position as
/// the tree is walked.
/// </summary>
/// <remarks>
/// Positions are captured here because they cannot be recovered afterwards. The binder is
/// deliberately tolerant: it shapes whatever the document contains without judging it, so that a
/// grammar-invalid document still yields a model for the semantic checks to report on.
/// </remarks>
internal static class IndexXmlBinder
{
    internal static DataSetNode BindDataSet(XElement root, string? systemId) => new(
        Extensions: Bind(root, "Extension", element => new ExtensionNode(
            Text(element, "Name") ?? Empty(element),
            Text(element, "URL") ?? Empty(element),
            Location(element))),
        Version: Text(root, "Version") ?? Empty(root),
        DataSupplier: root.Element("DataSupplier") is { } supplier
            ? new DataSupplierNode(
                Text(supplier, "Name") ?? Empty(supplier),
                Text(supplier, "Location") ?? Empty(supplier),
                Text(supplier, "Comment") ?? Empty(supplier),
                Location(supplier))
            : null,
        Commands: Bind(root, "Command", element => new TextNode(element.Value, Location(element))),
        Media: Bind(root, "Media", BindMedia),
        DoctypeSystemId: systemId,
        Location: Location(root));

    private static MediaNode BindMedia(XElement element) => new(
        Text(element, "Name") ?? Empty(element),
        Bind(element, "Command", command => new TextNode(command.Value, Location(command))),
        Bind(element, "Table", BindTable),
        Text(element, "AcceptNoTables"),
        Location(element));

    private static TableNode BindTable(XElement element) => new(
        Url: Text(element, "URL") ?? Empty(element),
        Name: Text(element, "Name"),
        Description: Text(element, "Description"),
        Validity: element.Element("Validity") is { } validity
            ? new ValidityNode(
                BindRange(validity.Element("Range")) ?? new RangeNode(Empty(validity), null, null, Location(validity)),
                Text(validity, "Format"),
                Location(validity))
            : null,
        Codepage: BindCodepage(element),
        DecimalSymbol: Text(element, "DecimalSymbol"),
        DigitGroupingSymbol: Text(element, "DigitGroupingSymbol"),
        SkipNumBytes: Text(element, "SkipNumBytes"),
        Range: BindRange(element.Element("Range")),
        Epoch: Text(element, "Epoch"),
        Format: BindFormat(element),
        Location: Location(element));

    private static Codepage BindCodepage(XElement table)
    {
        foreach (var (name, codepage) in Codepages)
        {
            if (table.Element(name) is not null)
            {
                return codepage;
            }
        }

        return Codepage.Unspecified;
    }

    private static readonly (string Name, Codepage Value)[] Codepages =
    [
        ("ANSI", Codepage.Ansi),
        ("Macintosh", Codepage.Macintosh),
        ("OEM", Codepage.Oem),
        ("UTF16", Codepage.Utf16),
        ("UTF7", Codepage.Utf7),
        ("UTF8", Codepage.Utf8),
    ];

    private static TableFormat? BindFormat(XElement table)
    {
        if (table.Element("VariableLength") is { } variable)
        {
            return new VariableLengthFormat(
                Text(variable, "ColumnDelimiter"),
                Text(variable, "RecordDelimiter"),
                Text(variable, "TextEncapsulator"),
                BindColumns(variable, "VariablePrimaryKey", "VariableColumn"),
                Bind(variable, "ForeignKey", BindForeignKey),
                Location(variable));
        }

        if (table.Element("FixedLength") is { } fixedLength)
        {
            return new FixedLengthFormat(
                Text(fixedLength, "Length"),
                Text(fixedLength, "RecordDelimiter"),
                BindColumns(fixedLength, "FixedPrimaryKey", "FixedColumn"),
                Bind(fixedLength, "ForeignKey", BindForeignKey),
                Location(fixedLength));
        }

        return null;
    }

    private static EquatableArray<ColumnNode> BindColumns(XElement format, string keyName, string columnName)
    {
        // Document order matters: the standard requires columns to be declared in the order the
        // file delivers them, and fixed-length spans are checked against that order.
        var columns = format.Elements()
            .Where(element => element.Name.LocalName == keyName || element.Name.LocalName == columnName)
            .Select(element => BindColumn(element, element.Name.LocalName == keyName));
        return new EquatableArray<ColumnNode>(columns);
    }

    private static ColumnNode BindColumn(XElement element, bool isPrimaryKey) => new(
        Text(element, "Name") ?? Empty(element),
        Text(element, "Description"),
        BindColumnType(element),
        Bind(element, "Map", map => new MapNode(
            Text(map, "Description"),
            Text(map, "From") ?? Empty(map),
            Text(map, "To") ?? Empty(map),
            Location(map))),
        isPrimaryKey,
        BindFixedRange(element.Element("FixedRange")),
        Location(element));

    private static ColumnType BindColumnType(XElement column)
    {
        if (column.Element("Numeric") is { } numeric)
        {
            return new NumericType(Text(numeric, "Accuracy"), Text(numeric, "ImpliedAccuracy"));
        }

        if (column.Element("Date") is { } date)
        {
            return new DateType(Text(date, "Format"));
        }

        // AlphaNumeric is EMPTY, so MaxLength is a sibling of it rather than a child.
        return new AlphaNumericType(Text(column, "MaxLength"));
    }

    private static ForeignKeyNode BindForeignKey(XElement element) => new(
        new EquatableArray<TextNode>(element.Elements("Name").Select(name => new TextNode(name.Value, Location(name)))),
        Text(element, "References") ?? Empty(element),
        Bind(element, "Alias", alias => new AliasNode(
            Text(alias, "From") ?? Empty(alias),
            Text(alias, "To") ?? Empty(alias),
            Location(alias))),
        Location(element));

    private static RangeNode? BindRange(XElement? element) => element is null
        ? null
        : new RangeNode(Text(element, "From") ?? Empty(element), Text(element, "To"), Text(element, "Length"), Location(element));

    private static FixedRangeNode? BindFixedRange(XElement? element) => element is null
        ? null
        : new FixedRangeNode(Text(element, "From") ?? Empty(element), Text(element, "To"), Text(element, "Length"), Location(element));

    private static EquatableArray<T> Bind<T>(XElement parent, string name, Func<XElement, T> bind)
        where T : IEquatable<T> =>
        new(parent.Elements(name).Select(bind));

    private static TextNode? Text(XElement parent, string name) =>
        parent.Element(name) is { } element ? new TextNode(element.Value, Location(element)) : null;

    private static TextNode Empty(XElement context) => new(string.Empty, Location(context));

    private static SourceLocation Location(XElement element) => IndexXmlParser.LocationOf(element);
}
