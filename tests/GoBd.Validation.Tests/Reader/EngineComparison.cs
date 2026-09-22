using GoBd.Reader.Data;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Runs one export through both engines and compares what they found.
/// </summary>
/// <remarks>
/// The comparison is by finding, not by count: code, arguments and order all have to match. Two
/// engines that agree on how many defects a file has and disagree about which ones have not
/// agreed about anything, and a bound that kept a different set in each would make one report
/// unciteable against the other.
/// <para>
/// Not all of it is independent, and the test is worth no more than the part that is. The cell
/// checks — types, formats, accuracy, maximum lengths — are written twice, once in C# and once
/// as predicates the store evaluates, and so are the key checks. Those are the real comparison.
/// The structural defects and the header check reach both sides through the same reader, because
/// the store is fed an extract that reader produced; for those, this asserts that a fixture
/// exists and that nothing is lost on the way through the store, not that two implementations
/// agree.
/// </para>
/// </remarks>
public static class EngineComparison
{
    /// <summary>Findings the streaming engine produces, in production order.</summary>
    public static IReadOnlyList<Finding> Streaming(StoreHarness harness, int bound)
    {
        ArgumentNullException.ThrowIfNull(harness);

        // One context, so the three checks share one budget per table exactly as a real run does.
        var options = new ContentOptions(bound);
        return harness.Streaming(
            options,
            new RecordConformanceCheck(),
            new HeaderRowCheck(),
            new KeyIntegrityCheck());
    }

    /// <summary>Findings the store produces, in the same order.</summary>
    public static IReadOnlyList<Finding> Store(StoreHarness harness, int bound)
    {
        ArgumentNullException.ThrowIfNull(harness);

        var store = harness.Store();
        var budget = new FindingBudget(bound);
        var findings = new List<Finding>();
        foreach (var table in harness.DataSet.Tables)
        {
            findings.AddRange(store.Import(table, budget).Findings);
        }

        // The header check is streaming in both engines: it reads one record, so there is nothing
        // for the store to do faster, and the reader needs its findings as much as the CLI does.
        findings.AddRange(harness.Streaming(new ContentOptions(bound), new HeaderRowCheck()));
        findings.AddRange(StoreKeyChecks.Run(store, harness.DataSet, budget));
        return findings;
    }

    /// <summary>Asserts the two engines found the same defects, and returns them.</summary>
    public static IReadOnlyList<Finding> Agree(
        StoreHarness harness,
        int bound = ContentOptions.DefaultMaximumFindingsPerTable)
    {
        var streaming = Streaming(harness, bound);
        var store = Store(harness, bound);

        store.ShouldBe(streaming, Describe(streaming, store));
        return streaming;
    }

    private static string Describe(IReadOnlyList<Finding> streaming, IReadOnlyList<Finding> store) =>
        "streaming: " + Render(streaming) + "; store: " + Render(store);

    private static string Render(IEnumerable<Finding> findings) =>
        string.Join(" | ", findings.Select(finding => finding.Code + "(" + string.Join(",", finding.Arguments) + ")"));
}
