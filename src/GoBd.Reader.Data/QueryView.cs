using System.Data;
using System.Globalization;
using GoBd.Validation.Checks;
using GoBd.Validation.Content;
using GoBd.Validation.Model;

namespace GoBd.Reader.Data;

/// <summary>
/// The query view over an imported table: what each column can be queried as, and the SQL that
/// reads it that way.
/// </summary>
/// <remarks>
/// A view rather than stored columns. Typing a value costs about a second over four million
/// records, which is paid when a person filters rather than by every import of every export,
/// and a filtered or sorted view carries its typed values with it so no figure pays twice. The
/// name is the seam: were this ever to become a stored table, nothing that queries it would
/// change. See the change's design.md D1 and D2.
/// <para>
/// Nothing here reads the store. It builds statements, so that what a column is read as can be
/// tested without a database.
/// </para>
/// </remarks>
internal static class QueryView
{
    /// <summary>Widest number a <c>DECIMAL</c> can hold, digits either side of the point.</summary>
    private const int MaximumDigits = 38;

    /// <summary>The name a table's query view carries.</summary>
    internal static string NameOf(string table) => table + "_q";

    /// <summary>The name of a column's queryable value in the view.</summary>
    /// <remarks>
    /// One per declared column, whatever its type, so that a filter, a sort or a figure names a
    /// column the same way regardless of what it is read as.
    /// </remarks>
    internal static string Column(int index) =>
        "q" + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// What to ask a table's records before deciding what its columns can be queried as.
    /// </summary>
    /// <remarks>
    /// One statement for the whole table rather than one per column: a table may declare dozens,
    /// and each would otherwise be another pass over every record. The plan records where each
    /// column's answer lands in the row that comes back.
    /// </remarks>
    /// <param name="table">The table's name in the store.</param>
    /// <param name="layout">Its declared layout.</param>
    internal static MeasurePlan Measure(string table, RecordLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var selects = new List<string>();
        var read = new List<string>();
        var numbers = new Dictionary<int, int>();
        var times = new Dictionary<int, int>();

        for (var index = 0; index < layout.Columns.Count; index++)
        {
            var column = layout.Columns[index];
            var quoted = DeclaredSql.Identifier(ExportStore.Column(index));

            // Each reading is named once and asked about twice or more. Inlined instead, the whole
            // trim-and-replace pipeline would be evaluated again for every value of every column.
            var named = DeclaredSql.Identifier("m" + index.ToString(CultureInfo.InvariantCulture));

            if (column.Type is NumericType numeric)
            {
                // Measured from the same digits the cast will meet, so that a column judged to fit
                // does fit. Integer digits first, then decimals.
                read.Add($"{DeclaredSql.NumberDigits(quoted, layout, numeric)} AS {named}");
                numbers[index] = selects.Count;
                selects.Add($"max(length(split_part({named}, '.', 1))) FILTER (WHERE {named} <> '')");
                selects.Add($"max(length(split_part({named}, '.', 2))) FILTER (WHERE {named} <> '')");
            }
            else if (DeclaredSql.Time(quoted, column) is { } time)
            {
                // A Time column is alphanumeric, so nothing has checked its values against its
                // mask. How many are not times is what the column has to be able to state.
                read.Add($"{time} AS {named}");
                times[index] = selects.Count;
                selects.Add($"count(*) FILTER (WHERE {quoted} <> '' AND {named} IS NULL)");
            }
        }

        return new MeasurePlan(
            selects.Count == 0
                ? null
                : "SELECT " + string.Join(", ", selects)
                    + " FROM (SELECT *, " + string.Join(", ", read) + " FROM " + table + ")",
            numbers,
            times);
    }

    /// <summary>Turns what was measured into what each column can be queried as.</summary>
    /// <param name="layout">The declared layout.</param>
    /// <param name="plan">What was asked, and where each answer lands.</param>
    /// <param name="measured">The answers, or null when there was nothing to ask.</param>
    internal static TableCapabilities Capabilities(RecordLayout layout, MeasurePlan plan, IDataReader? measured)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(plan);

        var columns = new List<ColumnCapability>(layout.Columns.Count);
        for (var index = 0; index < layout.Columns.Count; index++)
        {
            var column = layout.Columns[index];
            columns.Add(column.Type switch
            {
                NumericType numeric => Number(index, numeric, plan, measured),

                // A mask naming no usable date is already reported as a defect, and a column read
                // under it would answer for every record. Such a column stays text.
                DateType date => new ColumnCapability(
                    index,
                    Masks.IsUsableDateMask(date.Format?.Value ?? Masks.DefaultDateMask)
                        ? ColumnQueryKind.Date
                        : ColumnQueryKind.Text,
                    0,
                    0,
                    ColumnLimitation.None),

                _ when DeclaredSql.TimeMaskOf(column) is not null => new ColumnCapability(
                    index,
                    ColumnQueryKind.Time,
                    0,
                    plan.Times.TryGetValue(index, out var at) ? Answer(at, measured) : 0,
                    ColumnLimitation.None),

                _ => new ColumnCapability(index, ColumnQueryKind.Text, 0, 0, ColumnLimitation.None),
            });
        }

        return new TableCapabilities(columns);
    }

    /// <summary>
    /// What a numeric column can be queried as, from the widest value the table holds.
    /// </summary>
    /// <remarks>
    /// The scale is the declared accuracy where there is one, and otherwise the most decimals any
    /// value carries: the standard's default accuracy is zero, but a column that declares none may
    /// still hold decimals, and reading those at scale zero would round a value the file states
    /// exactly. A column too wide for the store's widest number is turned off whole, because a
    /// figure that quietly left a value out would be a guess presented as a fact.
    /// </remarks>
    private static ColumnCapability Number(int index, NumericType numeric, MeasurePlan plan, IDataReader? measured)
    {
        var at = plan.Numbers.TryGetValue(index, out var position) ? position : -1;
        var integers = (int)Answer(at, measured);
        var decimals = (int)Answer(at < 0 ? -1 : at + 1, measured);

        // Implied decimals are the fewest a value has, not the most: one that also prints decimals
        // of its own carries both, and a scale of only the declared places would round away the
        // digits the file states.
        var scale = numeric.Accuracy is { } accuracy && Masks.TryNonNegativeInteger(accuracy.Value, out var declared)
            ? declared
            : numeric.ImpliedAccuracy is { } implied && Masks.TryNonNegativeInteger(implied.Value, out var carried)
                ? Math.Max(carried, decimals)
                : decimals;

        return integers + scale > MaximumDigits || scale > MaximumDigits
            ? new ColumnCapability(index, ColumnQueryKind.Number, 0, 0, ColumnLimitation.NumberTooLarge)
            : new ColumnCapability(index, ColumnQueryKind.Number, scale, 0, ColumnLimitation.None);
    }

    /// <summary>One measured answer, or zero where a table holds no value to measure.</summary>
    private static long Answer(int at, IDataReader? measured) =>
        at < 0 || measured is null || measured.IsDBNull(at)
            ? 0
            : Convert.ToInt64(measured.GetValue(at), CultureInfo.InvariantCulture);

    /// <summary>
    /// The statement creating a table's query view: its text as stored, and its values as declared.
    /// </summary>
    /// <remarks>
    /// Every declared column contributes one queryable value, named by <see cref="Column"/>.
    /// A number, date or time is read as itself; text is read as the grid shows it, which is the
    /// declared redefinition applied and nothing else. An empty value is absent, never zero and
    /// never the earliest date.
    /// </remarks>
    internal static string Create(string table, RecordLayout layout, TableCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(capabilities);

        var selected = new List<string> { TableExtraction.OrdinalColumn };
        for (var index = 0; index < layout.Columns.Count; index++)
        {
            selected.Add(DeclaredSql.Identifier(ExportStore.Column(index)));
        }

        for (var index = 0; index < layout.Columns.Count; index++)
        {
            var column = layout.Columns[index];
            var quoted = DeclaredSql.Identifier(ExportStore.Column(index));
            var capability = capabilities[index];

            var value = capability switch
            {
                { IsQueryable: false } => Text(quoted, column),
                { Kind: ColumnQueryKind.Number } when column.Type is NumericType numeric =>
                    Empty(quoted, DeclaredSql.Number(quoted, layout, numeric, capability.Scale)),
                { Kind: ColumnQueryKind.Date } when column.Type is DateType date
                    && DeclaredSql.Date(quoted, date, layout) is { } read => Empty(quoted, read),
                { Kind: ColumnQueryKind.Time } when DeclaredSql.Time(quoted, column) is { } read =>
                    Empty(quoted, read),
                _ => Text(quoted, column),
            };

            selected.Add(value + " AS " + DeclaredSql.Identifier(Column(index)));
        }

        return "CREATE OR REPLACE VIEW " + NameOf(table) + " AS SELECT "
            + string.Join(", ", selected) + " FROM " + table;
    }

    /// <summary>
    /// Text as the grid shows it, and never nothing.
    /// </summary>
    /// <remarks>
    /// The store's CSV reader hands back an empty field as no value at all, and the rest of the
    /// store has always read that as the empty string — a page shows it as one, and a key
    /// comparison coalesces it. A filter has to agree: left as nothing, an empty value would
    /// satisfy neither "is empty" nor anything else, and a record would drop out of every answer.
    /// </remarks>
    private static string Text(string quoted, ColumnLayout column) =>
        $"coalesce({DeclaredSql.Presented(quoted, column)}, '')";

    /// <summary>Reads a value only when the file holds one, because empty is absent.</summary>
    private static string Empty(string quoted, string value) =>
        $"CASE WHEN coalesce({quoted}, '') <> '' THEN {value} END";
}

/// <summary>
/// What to measure before a table's columns can be judged, and where each answer lands.
/// </summary>
/// <param name="Sql">The statement to run, or null when no column needs measuring.</param>
/// <param name="Numbers">
/// Numeric columns, each naming the position of its integer-digit answer; its decimals follow in
/// the next position.
/// </param>
/// <param name="Times">
/// Time columns, each naming the position of its answer: how many values are not times.
/// </param>
internal sealed record MeasurePlan(
    string? Sql,
    IReadOnlyDictionary<int, int> Numbers,
    IReadOnlyDictionary<int, int> Times);
