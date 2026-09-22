namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// The collection the tests that assert wall-clock budgets belong to.
/// </summary>
/// <remarks>
/// Run on its own, never beside the rest of this assembly. A page budget of tens of milliseconds
/// says something about the store only when the store is what the machine is busy with; with
/// two dozen other test classes importing exports on every core, the same assertion measures
/// the runner's scheduling and fails for reasons that have nothing to do with the code.
/// <para>
/// It cannot be given the machine, though: the reader's own test assembly runs beside this one,
/// so every budget here is judged by a percentile rather than by the slowest sample, which is
/// always the machine rather than the store.
/// </para>
/// </remarks>
[CollectionDefinition(DisableParallelization = true)]
public sealed class Measured
{
    /// <summary>The duration that the given share of the samples do not exceed.</summary>
    public static TimeSpan Percentile(IReadOnlyCollection<TimeSpan> samples, double share)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var sorted = samples.Order().ToArray();
        var rank = (int)Math.Ceiling(share * sorted.Length) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
    }
}
