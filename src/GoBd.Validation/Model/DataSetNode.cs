using GoBd.Validation.Collections;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Model;

/// <summary><c>Extension (Name, URL)</c> — an application-specific supplement.</summary>
/// <param name="Name">Extension identifier.</param>
/// <param name="Url">Relative location of the supplementary file.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record ExtensionNode(TextNode Name, TextNode Url, SourceLocation Location);

/// <summary><c>DataSupplier (Name, Location, Comment)</c>.</summary>
/// <param name="Name">Supplying entity.</param>
/// <param name="Origin">Where the supplier is located; the standard calls this <c>Location</c>.</param>
/// <param name="Comment">Free commentary.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record DataSupplierNode(TextNode Name, TextNode Origin, TextNode Comment, SourceLocation Location);

/// <summary><c>Media (Name, Command*, Table*, Command*, AcceptNoTables?)</c>.</summary>
/// <remarks>
/// <c>Table</c> is <c>*</c>, not <c>+</c>: since 1.4 a medium may legitimately carry no table,
/// but only when it says so through <see cref="AcceptNoTables"/>. The specification PDF's prose
/// section still prints <c>Table+</c>; the authoritative DTD file settles it.
/// </remarks>
/// <param name="Name">Medium name.</param>
/// <param name="Commands">Declared operating-system commands. Reported, never executed.</param>
/// <param name="Tables">Tables carried on this medium.</param>
/// <param name="AcceptNoTables">Present when an empty medium is deliberate.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record MediaNode(
    TextNode Name,
    EquatableArray<TextNode> Commands,
    EquatableArray<TableNode> Tables,
    TextNode? AcceptNoTables,
    SourceLocation Location);

/// <summary><c>DataSet</c> — the document element of <c>index.xml</c>.</summary>
/// <param name="Extensions">Declared extensions.</param>
/// <param name="Version">Version of the data carrier cession — not of the standard.</param>
/// <param name="DataSupplier">Who supplied the data.</param>
/// <param name="Commands">Declared operating-system commands. Reported, never executed.</param>
/// <param name="Media">The media described.</param>
/// <param name="DoctypeSystemId">System identifier the DOCTYPE declared, when present.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record DataSetNode(
    EquatableArray<ExtensionNode> Extensions,
    TextNode Version,
    DataSupplierNode? DataSupplier,
    EquatableArray<TextNode> Commands,
    EquatableArray<MediaNode> Media,
    string? DoctypeSystemId,
    SourceLocation Location)
{
    /// <summary>Every table across every medium, in document order.</summary>
    public IEnumerable<TableNode> Tables => Media.SelectMany(medium => medium.Tables);

    /// <summary>Every declared foreign key, paired with the table that declares it, in document order.</summary>
    public IEnumerable<(TableNode Table, ForeignKeyNode ForeignKey)> ForeignKeys =>
        Tables
            .Where(table => table.Format is not null)
            .SelectMany(table => table.Format!.ForeignKeys.Select(key => (table, key)));
}
