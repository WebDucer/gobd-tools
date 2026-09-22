using System.Globalization;
using GoBd.Validation.Findings;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Findings;

public sealed class FindingCodesTests
{
    [Fact]
    public void EveryCodeIsUnique() =>
        FindingCodes.All
            .GroupBy(info => info.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ShouldBeEmpty();

    [Fact]
    public void EveryCodeSitsInTheNumericRangeOfItsCategory()
    {
        foreach (var info in FindingCodes.All)
        {
            info.Code.ShouldStartWith("GOBD");
            var number = int.Parse(info.Code.AsSpan(4), CultureInfo.InvariantCulture);
            var expected = info.Category switch
            {
                FindingCategory.Source => 1,
                FindingCategory.Grammar => 2,
                FindingCategory.Structure => 3,
                FindingCategory.Content => 4,
                FindingCategory.Integrity => 5,
                FindingCategory.Tool => 9,
                _ => -1,
            };

            (number / 1000).ShouldBe(expected, $"{info.Code} is categorised as {info.Category}");
        }
    }

    [Fact]
    public void EveryCodeHasANonEmptySummary() =>
        FindingCodes.All.ShouldAllBe(info => !string.IsNullOrWhiteSpace(info.Summary));

    [Fact]
    public void ToolFailuresAreNeverAboutTheExportsQuality() =>
        // GOBD9xxx must stay distinguishable from conformance findings, because a pipeline
        // needs to tell "your export is bad" from "the validator could not run".
        FindingCodes.All
            .Where(info => info.Category == FindingCategory.Tool)
            .ShouldAllBe(info => info.Code.StartsWith("GOBD9", StringComparison.Ordinal));

    [Fact]
    public void DescribeRoundTripsEveryCode() =>
        FindingCodes.All.ShouldAllBe(info => FindingCodes.Describe(info.Code) == info);

    [Fact]
    public void UnknownCodeIsNotKnown() => FindingCodes.IsKnown("GOBD4242").ShouldBeFalse();
}
