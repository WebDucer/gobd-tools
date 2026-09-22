using GoBd.Validation.Collections;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Model;

/// <summary><c>Map (Description?, From, To)</c> — a value redefinition, or a Time declaration.</summary>
/// <param name="Description">Optional commentary.</param>
/// <param name="From">Source value, verbatim.</param>
/// <param name="To">Target value, verbatim.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record MapNode(TextNode? Description, TextNode From, TextNode To, SourceLocation Location);

/// <summary>A column or primary key of either table format.</summary>
/// <param name="Name">Declared column name.</param>
/// <param name="Description">Optional commentary.</param>
/// <param name="Type">Declared datatype.</param>
/// <param name="Maps">Declared value redefinitions.</param>
/// <param name="IsPrimaryKey">True when declared as a primary key rather than a plain column.</param>
/// <param name="FixedRange">Span within the record; present only for fixed-length tables.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record ColumnNode(
    TextNode Name,
    TextNode? Description,
    ColumnType Type,
    EquatableArray<MapNode> Maps,
    bool IsPrimaryKey,
    FixedRangeNode? FixedRange,
    SourceLocation Location);

/// <summary><c>Alias (From, To)</c> — remaps one foreign key column onto a target key column.</summary>
/// <param name="From">A column of the foreign key that declares this alias.</param>
/// <param name="To">A primary key column of the referenced table.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record AliasNode(TextNode From, TextNode To, SourceLocation Location);

/// <summary><c>ForeignKey (Name+, References, Alias*)</c>.</summary>
/// <remarks>
/// A foreign key never introduces a column: each <paramref name="Names"/> entry must already be
/// declared in the same table. <paramref name="References"/> names a <i>table</i>, and the join
/// target is that table's primary key — positionally, unless an alias remaps it.
/// </remarks>
/// <param name="Names">Columns of this table forming the key, in declaration order.</param>
/// <param name="References">Identity of the referenced table.</param>
/// <param name="Aliases">Explicit column mappings.</param>
/// <param name="Location">Where the element begins.</param>
public sealed record ForeignKeyNode(
    EquatableArray<TextNode> Names,
    TextNode References,
    EquatableArray<AliasNode> Aliases,
    SourceLocation Location);
