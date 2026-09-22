using GoBd.Reader.Data;
using GoBd.Validation.Content;
using GoBd.Validation.Tests.Content;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Writing a figure the way the export writes its values.
/// </summary>
/// <remarks>
/// A figure shown beside the column it was computed over has to read as the same kind of value:
/// a total of <c>1234.56</c> under a column of <c>1.234,56</c> is a different number to anyone
/// reading the page, and half past two shown as <c>14:30 PM</c> is not a time at all.
/// </remarks>
public sealed class DeclaredTextTests
{
    /// <summary>A table declaring one column besides its key.</summary>
    private static RecordLayout Layout(string column) => ContentHarness.Layout(
        "<Table><URL>t.csv</URL><Name>T</Name><VariableLength>"
        + "<VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>"
        + column
        + "</VariableLength></Table>");

    /// <summary>A Time column, which an alphanumeric declares by mapping a mask to itself.</summary>
    private static RecordLayout Time(string mask) => Layout(
        "<VariableColumn><Name>Zeit</Name><AlphaNumeric/>"
        + $"<Map><From>{mask}</From><To>{mask}</To></Map></VariableColumn>");

    [Fact]
    public void ATimeUnderAMaskNamingHalfTheDayIsWrittenOnATwelveHourClock()
    {
        // The hours run from 1 to 12 where the mask names the half of the day. Written on a
        // 24-hour clock beside a "PM" it would state an hour no clock shows, and it would not be
        // a time the reader itself would accept back as a filter.
        var layout = Time("HH:MM TT");

        DeclaredText.Write(new TimeOnly(14, 30), layout.Columns[1], layout).ShouldBe("02:30 PM");
        DeclaredText.Write(new TimeOnly(9, 5), layout.Columns[1], layout).ShouldBe("09:05 AM");
    }

    [Fact]
    public void ATimeUnderAMaskThatNamesNoHalfKeepsTheHourItHas()
    {
        var layout = Time("HHMMSS");

        DeclaredText.Write(new TimeOnly(14, 30, 0), layout.Columns[1], layout).ShouldBe("143000");
    }

    [Fact]
    public void ANumberIsWrittenUnderTheTablesOwnSymbols()
    {
        var layout = Layout("<VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>");

        DeclaredText.Write(1234567.89m, layout.Columns[1], layout).ShouldBe("1.234.567,89");
        DeclaredText.Write(-50.5m, layout.Columns[1], layout).ShouldBe("-50,5");
    }

    [Fact]
    public void ADateIsWrittenUnderTheColumnsDeclaredMask()
    {
        var layout = Layout("<VariableColumn><Name>Datum</Name><Date><Format>YYYY-MM-DD</Format></Date></VariableColumn>");

        DeclaredText.Write(new DateOnly(2025, 1, 31), layout.Columns[1], layout).ShouldBe("2025-01-31");
    }
}
