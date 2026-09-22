using System.Globalization;
using System.Text;
using GoBd.Validation.Content;

namespace GoBd.Reader.Data;

/// <summary>
/// The statements a view is made and read by: which records it holds, in what order, and what is
/// computed over them.
/// </summary>
/// <remarks>
/// A view is a table of the records it selects, carrying their values, rather than a list of
/// record numbers pointing back at the table. A sorted page's record numbers are scattered, so
/// reading them back through a join costs 46–98 ms a page against a 27 ms budget, while reading
/// a carried page costs 1.3 ms — the same as a page in file order. See the change's design.md D4.
/// <para>
/// Nothing here touches the store: it builds statements and the values they take, so that what a
/// filter means can be tested without a database. No value a person typed is ever written into a
/// statement; every one of them is a parameter.
/// </para>
/// </remarks>
internal static class ViewStatements
{
    /// <summary>The column carrying a record's position in the view, counting from zero.</summary>
    internal const string PositionColumn = "pos";

    /// <summary>
    /// The collation text is compared and ordered by.
    /// </summary>
    /// <remarks>
    /// The engine ships ICU, so this is a real alphabetical order — <c>Äpfel</c> between
    /// <c>apfel</c> and <c>Ofen</c> rather than after <c>Zebra</c> — and a fixed locale rather
    /// than the machine's, which is what the spec requires and what the rest of this reader does
    /// with every declared rule.
    /// </remarks>
    internal const string Collation = "de";

    /// <summary>A statement and the values it takes, in order.</summary>
    /// <param name="Sql">The statement, with a <c>?</c> where each value belongs.</param>
    /// <param name="Parameters">The values, in the order the statement takes them.</param>
    internal sealed record Statement(string Sql, IReadOnlyList<string> Parameters);

    /// <summary>Builds the view a filter and a sort select.</summary>
    /// <param name="view">Name for the view's table.</param>
    /// <param name="source">The table's query view, which carries the declared readings.</param>
    /// <param name="layout">The declared layout.</param>
    /// <param name="capabilities">What each column can be queried as.</param>
    /// <param name="query">What the person asked to see.</param>
    internal static Statement Create(
        string view,
        string source,
        RecordLayout layout,
        TableCapabilities capabilities,
        TableQuery query)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(query);

        var values = new List<string>();
        var where = Predicates(capabilities, query, values);
        var carried = new List<string> { TableExtraction.OrdinalColumn };
        for (var index = 0; index < layout.Columns.Count; index++)
        {
            carried.Add(DeclaredSql.Identifier(ExportStore.Column(index)));
            carried.Add(DeclaredSql.Identifier(QueryView.Column(index)));
        }

        // Numbered by the order asked for, then by record number: a sort that leaves records equal
        // leaves them in the order the file delivers them, which is what makes the order
        // reproducible and a record citable.
        var sql = $"CREATE OR REPLACE TABLE {view} AS SELECT row_number() OVER (ORDER BY {Order(capabilities, query)}) - 1"
            + $" AS {PositionColumn}, {string.Join(", ", carried)} FROM {source}"
            + (where.Length > 0 ? " WHERE " + where : string.Empty)
            + $" ORDER BY {PositionColumn}";

        return new Statement(sql, values);
    }

    /// <summary>Reads one page of a view, in view order.</summary>
    internal static string Page(string view, RecordLayout layout, long offset, int count)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var columns = string.Join(
            ", ",
            Enumerable.Range(0, layout.Columns.Count).Select(index => DeclaredSql.Identifier(ExportStore.Column(index))));

        return $"SELECT {TableExtraction.OrdinalColumn}, {columns} FROM {view}"
            + $" WHERE {PositionColumn} >= {offset.ToString(CultureInfo.InvariantCulture)}"
            + $" AND {PositionColumn} < {(offset + count).ToString(CultureInfo.InvariantCulture)}"
            + $" ORDER BY {PositionColumn}";
    }

    /// <summary>Where a record sits in a view, or nothing when the view does not hold it.</summary>
    internal static string Position(string view, long ordinal) =>
        $"SELECT {PositionColumn} FROM {view} WHERE {TableExtraction.OrdinalColumn}"
        + $" = {ordinal.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>How many records a view holds.</summary>
    internal static string Count(string view) => $"SELECT count(*) FROM {view}";

    /// <summary>Removes a view's table.</summary>
    internal static string Drop(string view) => $"DROP TABLE IF EXISTS {view}";

    /// <summary>
    /// The figures asked for, and for each the values it left out because the file holds none.
    /// </summary>
    /// <remarks>
    /// One statement for every figure on screen, so that what they say is what one reading of the
    /// records says. Every figure is exact: a sum is decimal arithmetic, a distinct count counts
    /// values rather than estimating them, and an average is left to the caller to divide so that
    /// it can be rounded and marked as rounded rather than quietly approximated here.
    /// </remarks>
    internal static string Figures(string source, TableCapabilities capabilities, IReadOnlyList<ColumnFigure> figures)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(figures);

        var selected = new List<string>();
        foreach (var figure in figures)
        {
            var capability = capabilities[figure.Column];
            var value = DeclaredSql.Identifier(QueryView.Column(figure.Column));
            var present = Present(capability, figure.Column);

            selected.Add(figure.Figure switch
            {
                Figure.Count => $"count(*) FILTER (WHERE {present})",
                Figure.DistinctCount => $"count(DISTINCT {value}) FILTER (WHERE {present})",
                Figure.Sum or Figure.Average => $"sum({value})",
                Figure.Minimum => $"min({value})",
                Figure.Maximum => $"max({value})",
                _ => "NULL",
            });

            // Every figure states how many records it had nothing to read here, because a figure
            // over fewer values than it appears to cover is a figure that misleads.
            selected.Add($"count(*) FILTER (WHERE NOT ({present}))");

            if (figure.Figure == Figure.Average)
            {
                // Divided by the caller, in decimal, so the rounding is stated rather than done
                // here in whatever the engine's division would produce.
                selected.Add($"count(*) FILTER (WHERE {present})");
            }
        }

        return selected.Count == 0
            ? $"SELECT 1 FROM {source} LIMIT 0"
            : $"SELECT {string.Join(", ", selected)} FROM {source}";
    }

    /// <summary>True of a record that holds a value in this column at all.</summary>
    /// <remarks>
    /// An empty field is an absent value, and for a Time column so is a value that is not a time:
    /// nothing has checked such a column against its mask, and a figure cannot compute with what
    /// it cannot read.
    /// </remarks>
    private static string Present(ColumnCapability capability, int column) =>
        capability.IsTyped
            ? $"{DeclaredSql.Identifier(QueryView.Column(column))} IS NOT NULL"
            : $"{DeclaredSql.Identifier(QueryView.Column(column))} <> ''";

    /// <summary>The order a view's records are numbered in.</summary>
    private static string Order(TableCapabilities capabilities, TableQuery query)
    {
        var keys = new List<string>();
        foreach (var sort in query.Sorts)
        {
            var capability = capabilities[sort.Column];
            var value = DeclaredSql.Identifier(QueryView.Column(sort.Column));
            var direction = sort.Direction == Ordering.Descending ? " DESC" : " ASC";

            // Records holding no value here come after every record that holds one, whichever way
            // the sort runs. A typed column says so with NULL; text says so with an empty value,
            // which orders before everything unless it is asked not to.
            if (capability.IsTyped)
            {
                keys.Add($"{value}{direction} NULLS LAST");
            }
            else
            {
                keys.Add($"CASE WHEN {value} = '' THEN 1 ELSE 0 END ASC");
                keys.Add($"{value} COLLATE {Collation}{direction}");
            }
        }

        keys.Add(TableExtraction.OrdinalColumn + " ASC");
        return string.Join(", ", keys);
    }

    /// <summary>Every filter, joined by "and", with its values added in the order they are taken.</summary>
    private static string Predicates(TableCapabilities capabilities, TableQuery query, List<string> values)
    {
        var predicates = new List<string>();
        foreach (var filter in query.Filters)
        {
            predicates.Add("(" + Predicate(capabilities[filter.Column], filter, values) + ")");
        }

        return string.Join(" AND ", predicates);
    }

    /// <summary>One filter, as the comparison its column's kind supports.</summary>
    private static string Predicate(ColumnCapability capability, ColumnFilter filter, List<string> values)
    {
        // Never quietly: a filter naming nothing to compare against would otherwise be left out of
        // the statement while the banner above the grid went on claiming it.
        if (!filter.NamesValue)
        {
            throw new ArgumentException(
                $"A '{filter.Comparison}' filter names no value to compare against.",
                nameof(filter));
        }

        var value = DeclaredSql.Identifier(QueryView.Column(filter.Column));
        var present = Present(capability, filter.Column);

        switch (filter.Comparison)
        {
            case FilterComparison.IsEmpty:
                return $"NOT ({present})";

            case FilterComparison.IsNotEmpty:
                return present;

            case FilterComparison.Between:
                var bounds = new List<string>();
                if (filter.Values.Count > 0 && filter.Values[0].Length > 0)
                {
                    bounds.Add($"{value} >= {Parameter(capability, filter.Values[0], values)}");
                }

                if (filter.Values.Count > 1 && filter.Values[1].Length > 0)
                {
                    bounds.Add($"{value} <= {Parameter(capability, filter.Values[1], values)}");
                }

                // Both ends open selects everything that holds a value, which is what an empty
                // range asks for.
                return bounds.Count == 0 ? present : string.Join(" AND ", bounds);

            case FilterComparison.OneOf:
                var listed = filter.Values.Select(entry => Compared(capability, filter, entry, values));
                return $"{Comparable(capability, filter, value)} IN ({string.Join(", ", listed)})";

            case FilterComparison.Contains:
            case FilterComparison.StartsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.Matches:
                values.Add(Pattern(filter));
                return $"{value} {(filter.IgnoreCase ? "ILIKE" : "LIKE")} ? ESCAPE '\\'";

            default:
                var comparison = filter.Comparison switch
                {
                    FilterComparison.Equals => "=",
                    FilterComparison.NotEquals => "<>",
                    FilterComparison.LessThan => "<",
                    FilterComparison.AtMost => "<=",
                    FilterComparison.GreaterThan => ">",
                    FilterComparison.AtLeast => ">=",
                    _ => "=",
                };

                return $"{Comparable(capability, filter, value)} {comparison}"
                    + $" {Compared(capability, filter, filter.Values[0], values)}";
        }
    }

    /// <summary>The column as it is compared: lowered when case is to be ignored.</summary>
    private static string Comparable(ColumnCapability capability, ColumnFilter filter, string value) =>
        filter.IgnoreCase && !capability.IsTyped ? $"lower({value})" : value;

    /// <summary>One value to compare against, added as a parameter and cast to the column's type.</summary>
    private static string Compared(
        ColumnCapability capability,
        ColumnFilter filter,
        string entry,
        List<string> values)
    {
        if (!capability.IsTyped && filter.IgnoreCase)
        {
            values.Add(entry);
            return "lower(?)";
        }

        return Parameter(capability, entry, values);
    }

    /// <summary>
    /// One value as a parameter, read as the column's kind.
    /// </summary>
    /// <remarks>
    /// The value arrives already read under the declaration — a number as its digits with a
    /// <c>.</c> for its point, a date as <c>YYYY-MM-DD</c>, a time as <c>HH:MM:SS</c> — so the
    /// cast here is the store's own reading of a form it defines, not an interpretation of what a
    /// person typed.
    /// </remarks>
    private static string Parameter(ColumnCapability capability, string entry, List<string> values)
    {
        values.Add(entry);
        return capability.Kind switch
        {
            ColumnQueryKind.Number when capability.IsQueryable =>
                $"CAST(? AS DECIMAL(38, {capability.Scale.ToString(CultureInfo.InvariantCulture)}))",
            ColumnQueryKind.Date when capability.IsQueryable => "CAST(? AS DATE)",
            ColumnQueryKind.Time when capability.IsQueryable => "CAST(? AS TIME)",
            _ => "?",
        };
    }

    /// <summary>
    /// The pattern a text filter matches by.
    /// </summary>
    /// <remarks>
    /// Built here rather than written into the statement, because it is made from what a person
    /// typed. Every character the store would read as a wildcard is escaped first, so a literal
    /// <c>%</c> matches only itself; then <c>*</c> and <c>?</c> become the wildcards the person
    /// means by them.
    /// </remarks>
    private static string Pattern(ColumnFilter filter)
    {
        var entry = filter.Values[0];
        var escaped = new StringBuilder(entry.Length + 8);
        foreach (var character in entry)
        {
            switch (character)
            {
                case '\\':
                case '%':
                case '_':
                    escaped.Append('\\').Append(character);
                    break;

                case '*' when filter.Comparison == FilterComparison.Matches:
                    escaped.Append('%');
                    break;

                case '?' when filter.Comparison == FilterComparison.Matches:
                    escaped.Append('_');
                    break;

                default:
                    escaped.Append(character);
                    break;
            }
        }

        return filter.Comparison switch
        {
            FilterComparison.Contains => "%" + escaped + "%",
            FilterComparison.StartsWith => escaped + "%",
            FilterComparison.EndsWith => "%" + escaped,
            _ => escaped.ToString(),
        };
    }
}
