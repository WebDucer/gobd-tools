using GoBd.Validation.Checks.Structure;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>How following a reference ended.</summary>
public enum NavigationKind
{
    /// <summary>The referenced records were found and the view is positioned at one.</summary>
    Positioned,

    /// <summary>The value refers to nothing in the referenced table.</summary>
    Unresolved,

    /// <summary>The value is empty, so it refers to nothing on purpose.</summary>
    NoReference,

    /// <summary>The referenced table has no data to show, so there is nowhere to go.</summary>
    Unreadable,

    /// <summary>The referenced table has not been read yet, so there is nowhere to go yet.</summary>
    NotReady,
}

/// <summary>
/// Where following a reference leads.
/// </summary>
/// <param name="Kind">How it ended.</param>
/// <param name="Table">The table to show, when there is one.</param>
/// <param name="Ordinal">Record number to position at.</param>
/// <param name="Position">Index of that record in the table's rows, for a virtualised list.</param>
/// <param name="Columns">Columns forming the key, to be indicated together.</param>
/// <param name="Matches">How many records match, which is more than one only going backwards.</param>
/// <param name="MatchIndex">Which of those this navigation landed on.</param>
/// <param name="Value">The key value followed, as the file holds it.</param>
public sealed record Navigation(
    NavigationKind Kind,
    TableNode? Table,
    long Ordinal,
    int Position,
    IReadOnlyList<int> Columns,
    long Matches,
    long MatchIndex,
    string Value)
{
    /// <summary>A navigation that did not lead anywhere.</summary>
    internal static Navigation Nowhere(NavigationKind kind, TableNode? table, string value) =>
        new(kind, table, 0, -1, [], 0, 0, value);
}

/// <summary>A declared foreign key, resolved into column positions on both sides.</summary>
/// <param name="ForeignKey">The declaration.</param>
/// <param name="Referring">The table that declares it.</param>
/// <param name="Referenced">The table it points at.</param>
/// <param name="ReferringColumns">Positions of the key's columns in the referring table.</param>
/// <param name="ReferencedColumns">Positions of the matching key columns in the referenced table.</param>
public sealed record ResolvedForeignKey(
    ForeignKeyNode ForeignKey,
    TableNode Referring,
    TableNode Referenced,
    IReadOnlyList<int> ReferringColumns,
    IReadOnlyList<int> ReferencedColumns)
{
    /// <summary>
    /// Resolves a declared foreign key into the column positions on both sides.
    /// </summary>
    /// <remarks>
    /// A composite key is one key, so the columns are ordered to correspond: the n-th referring
    /// column joins the n-th referenced column. <see cref="ForeignKeyResolver"/> settles which
    /// target column each key column joins to, aliases and positional fallback included.
    /// </remarks>
    public static ResolvedForeignKey? Resolve(
        ForeignKeyNode foreignKey,
        TableNode referring,
        TableNode referenced)
    {
        ArgumentNullException.ThrowIfNull(foreignKey);
        ArgumentNullException.ThrowIfNull(referring);
        ArgumentNullException.ThrowIfNull(referenced);

        if (referring.Format is not { } referringFormat || referenced.Format is not { } referencedFormat)
        {
            return null;
        }

        var referringColumns = new List<int>(foreignKey.Names.Count);
        var referencedColumns = new List<int>(foreignKey.Names.Count);

        foreach (var mapping in ForeignKeyResolver.Map(foreignKey, referenced))
        {
            var source = Position(referringFormat.Columns, mapping.Source.Value);
            var target = Position(referencedFormat.Columns, mapping.TargetName);
            if (source < 0 || target < 0)
            {
                return null;
            }

            referringColumns.Add(source);
            referencedColumns.Add(target);
        }

        return referringColumns.Count == 0
            ? null
            : new ResolvedForeignKey(foreignKey, referring, referenced, referringColumns, referencedColumns);
    }

    private static int Position(IReadOnlyList<ColumnNode> columns, string name)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (string.Equals(columns[index].Name.Value, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
