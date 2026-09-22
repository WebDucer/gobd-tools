using GoBd.Reader.Data;
using GoBd.Validation.Content;
using GoBd.Validation.Tests.Content;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// What a filter and a sort become as statements: which records a view holds, in what order, and
/// what is computed over them.
/// </summary>
/// <remarks>
/// Built without a store on purpose. What a filter means is a property of the declaration, and
/// holding it to a statement rather than to a query's answer says which part is wrong when the
/// two disagree.
/// </remarks>
public sealed class ViewStatementsTests
{
    private const string Tables = """
            <Table>
              <URL>t.csv</URL>
              <Name>T</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private static RecordLayout Layout() => ContentHarness.Layout(Tables);

    /// <summary>Text, a number at two decimals, and a date — the three kinds a column can be.</summary>
    private static TableCapabilities Capabilities(ColumnLimitation number = ColumnLimitation.None) =>
        new(
        [
            new ColumnCapability(0, ColumnQueryKind.Text, 0, 0, ColumnLimitation.None),
            new ColumnCapability(1, ColumnQueryKind.Number, 2, 0, number),
            new ColumnCapability(2, ColumnQueryKind.Date, 0, 0, ColumnLimitation.None),
        ]);

    private static ViewStatements.Statement Create(TableQuery query) =>
        ViewStatements.Create("v_1", "t_0_q", Layout(), Capabilities(), query);

    // ---- what a view carries -----------------------------------------------------------------

    [Fact]
    public void AViewCarriesTheRecordsItSelects()
    {
        // Not a list of record numbers pointing back at the table: a sorted page's numbers are
        // scattered, and reading them back through a join costs a page four times its budget.
        var statement = Create(TableQuery.FileOrder);

        statement.Sql.ShouldContain("CREATE OR REPLACE TABLE v_1 AS");
        statement.Sql.ShouldContain("\"c0\", \"q0\"");
        statement.Sql.ShouldContain("\"c2\", \"q2\"");
        statement.Sql.ShouldContain("FROM t_0_q");
        statement.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void RecordsEqualInEverySortedColumnKeepFileOrder()
    {
        var statement = Create(new TableQuery([], [new ColumnSort(1, Ordering.Descending)]));

        statement.Sql.ShouldContain("ORDER BY \"q1\" DESC NULLS LAST, _ordinal ASC");
    }

    [Fact]
    public void SeveralColumnsSortInTheOrderTheyWereChosen()
    {
        var statement = Create(new TableQuery(
            [],
            [
                new ColumnSort(2, Ordering.Ascending),
                new ColumnSort(1, Ordering.Descending),
            ]));

        statement.Sql.ShouldContain("ORDER BY \"q2\" ASC NULLS LAST, \"q1\" DESC NULLS LAST, _ordinal ASC");
    }

    [Fact]
    public void AFilterNamingNoValueToCompareAgainstIsRefused()
    {
        // Refused rather than left out of the statement. A filter silently dropped would show
        // records the banner above them says are filtered away, and a bound of nothing cast to
        // the column's own type is not a statement the store can run at all.
        Should.Throw<ArgumentException>(() =>
            Create(new TableQuery([new ColumnFilter(1, FilterComparison.Equals, [string.Empty])], [])));
        Should.Throw<ArgumentException>(() =>
            Create(new TableQuery([new ColumnFilter(0, FilterComparison.OneOf, [])], [])));
        Should.Throw<ArgumentException>(() =>
            Create(new TableQuery([new ColumnFilter(0, FilterComparison.Contains, [])], [])));
    }

    [Fact]
    public void ARangeIsTheOneComparisonThatMayNameNothing()
    {
        // An absent bound is an open end rather than a missing value, which is what makes a range
        // the exception to the rule above.
        var statement = Create(new TableQuery(
            [new ColumnFilter(1, FilterComparison.Between, [string.Empty, "50"])],
            []));

        statement.Sql.ShouldContain("\"q1\" <=");
        statement.Sql.ShouldNotContain("\"q1\" >=");
        statement.Parameters.ShouldHaveSingleItem().ShouldBe("50");
    }

    [Fact]
    public void TextSortsAlphabeticallyAndPutsEmptyValuesLast()
    {
        // The engine's own order would put Ä after Z and every capital before every small letter.
        // A fixed locale is an alphabet a person recognises, and the same one on every machine.
        var statement = Create(new TableQuery([], [new ColumnSort(0, Ordering.Ascending)]));

        statement.Sql.ShouldContain("CASE WHEN \"q0\" = '' THEN 1 ELSE 0 END ASC");
        statement.Sql.ShouldContain("\"q0\" COLLATE de ASC");
    }

    // ---- what a filter means -----------------------------------------------------------------

    [Fact]
    public void ANumberIsComparedAsANumberAtItsDeclaredScale()
    {
        var statement = Create(new TableQuery(
            [new ColumnFilter(1, FilterComparison.LessThan, ["10"])],
            []));

        statement.Sql.ShouldContain("\"q1\" < CAST(? AS DECIMAL(38, 2))");
        statement.Parameters.ShouldBe(["10"]);
    }

    [Fact]
    public void ARangeWithOneEndOpenBoundsOnlyThatSide()
    {
        var statement = Create(new TableQuery(
            [new ColumnFilter(2, FilterComparison.Between, ["2025-01-01", ""])],
            []));

        statement.Sql.ShouldContain("\"q2\" >= CAST(? AS DATE)");
        statement.Sql.ShouldNotContain("<= CAST(? AS DATE)");
        statement.Parameters.ShouldBe(["2025-01-01"]);
    }

    [Fact]
    public void AnEmptyRangeSelectsEveryRecordThatHoldsAValue()
    {
        var statement = Create(new TableQuery([new ColumnFilter(1, FilterComparison.Between, ["", ""])], []));

        statement.Sql.ShouldContain("\"q1\" IS NOT NULL");
        statement.Parameters.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(FilterComparison.Contains, "%miete%")]
    [InlineData(FilterComparison.StartsWith, "miete%")]
    [InlineData(FilterComparison.EndsWith, "%miete")]
    [InlineData(FilterComparison.Matches, "miete")]
    public void TextIsMatchedByAPatternBuiltFromWhatWasTyped(FilterComparison comparison, string expected)
    {
        var statement = Create(new TableQuery([new ColumnFilter(0, comparison, ["miete"])], []));

        statement.Sql.ShouldContain("\"q0\" LIKE ? ESCAPE '\\'");
        statement.Parameters.ShouldBe([expected]);
    }

    [Fact]
    public void AWildcardTypedByAPersonIsTheirsAndOneInTheDataIsNot()
    {
        // '*' and '?' are the wildcards a person means; '%' and '_' are characters a value may
        // hold, and matching everything because a value contains one would be a filter that lies.
        var statement = Create(new TableQuery([new ColumnFilter(0, FilterComparison.Matches, ["50% *ab?"])], []));

        statement.Parameters.ShouldBe(["50\\% %ab_"]);
    }

    [Fact]
    public void IgnoringCaseIsAskedForRatherThanAssumed()
    {
        var sensitive = Create(new TableQuery([new ColumnFilter(0, FilterComparison.Equals, ["Miete"])], []));
        var insensitive = Create(new TableQuery([new ColumnFilter(0, FilterComparison.Equals, ["Miete"], IgnoreCase: true)], []));

        sensitive.Sql.ShouldContain("\"q0\" = ?");
        insensitive.Sql.ShouldContain("lower(\"q0\") = lower(?)");
    }

    [Fact]
    public void OneOfSeveralValuesMatchesAnyOfThem()
    {
        var statement = Create(new TableQuery(
            [new ColumnFilter(0, FilterComparison.OneOf, ["1200", "1400", "1600"])],
            []));

        statement.Sql.ShouldContain("\"q0\" IN (?, ?, ?)");
        statement.Parameters.ShouldBe(["1200", "1400", "1600"]);
    }

    [Fact]
    public void EmptyMeansTheFileHoldsNoValueHere()
    {
        // A typed column says so with no value at all; text says so with an empty one.
        Create(new TableQuery([new ColumnFilter(1, FilterComparison.IsEmpty, [])], []))
            .Sql.ShouldContain("NOT (\"q1\" IS NOT NULL)");
        Create(new TableQuery([new ColumnFilter(0, FilterComparison.IsNotEmpty, [])], []))
            .Sql.ShouldContain("\"q0\" <> ''");
    }

    [Fact]
    public void EveryFilterMustHoldAndItsValuesAreTakenInOrder()
    {
        var statement = Create(new TableQuery(
            [
                new ColumnFilter(0, FilterComparison.Equals, ["1200"]),
                new ColumnFilter(1, FilterComparison.AtLeast, ["100.00"]),
            ],
            []));

        statement.Sql.ShouldContain(" AND ");
        statement.Parameters.ShouldBe(["1200", "100.00"]);
    }

    [Fact]
    public void NoValueAPersonTypedIsEverWrittenIntoTheStatement()
    {
        var statement = Create(new TableQuery(
            [new ColumnFilter(0, FilterComparison.Equals, ["'; DROP TABLE t_0; --"])],
            []));

        statement.Sql.ShouldNotContain("DROP TABLE t_0");
        statement.Parameters.ShouldBe(["'; DROP TABLE t_0; --"]);
    }

    // ---- what is computed over a view --------------------------------------------------------

    [Fact]
    public void EachFigureIsComputedOverTheValuesTheViewHolds()
    {
        var sql = ViewStatements.Figures(
            "v_1",
            Capabilities(),
            [new ColumnFigure(1, Figure.Sum), new ColumnFigure(0, Figure.DistinctCount)]);

        sql.ShouldContain("sum(\"q1\")");
        sql.ShouldContain("count(DISTINCT \"q0\") FILTER (WHERE \"q0\" <> '')");
        sql.ShouldContain("FROM v_1");
    }

    [Fact]
    public void EveryFigureStatesHowManyValuesItHadNothingToRead()
    {
        var sql = ViewStatements.Figures("v_1", Capabilities(), [new ColumnFigure(1, Figure.Sum)]);

        sql.ShouldContain("count(*) FILTER (WHERE NOT (\"q1\" IS NOT NULL))");
    }

    [Fact]
    public void AnAverageIsAskedForAsASumAndACountSoThatItsRoundingIsStated()
    {
        var sql = ViewStatements.Figures("v_1", Capabilities(), [new ColumnFigure(1, Figure.Average)]);

        sql.ShouldContain("sum(\"q1\")");
        sql.ShouldContain("count(*) FILTER (WHERE \"q1\" IS NOT NULL)");
    }

    // ---- reading a view ----------------------------------------------------------------------

    [Fact]
    public void APageIsReadByPositionAndCarriesTheTextTheGridShows()
    {
        var sql = ViewStatements.Page("v_1", Layout(), 2_000_000, 512);

        sql.ShouldContain("FROM v_1 WHERE pos >= 2000000 AND pos < 2000512");
        sql.ShouldContain("ORDER BY pos");
        sql.ShouldContain("\"c0\", \"c1\", \"c2\"");
    }

    [Fact]
    public void ARecordIsFoundInAViewByItsRecordNumber()
    {
        ViewStatements.Position("v_1", 88_124).ShouldBe("SELECT pos FROM v_1 WHERE _ordinal = 88124");
    }
}
