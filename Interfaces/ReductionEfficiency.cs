namespace API.Interfaces;

/// <summary>
/// Ranking rules for "which of these reductions is the most efficient", used to pick a
/// default reduction when a problem has more than one outgoing edge to the same target.
/// <para>
/// Primary key is <see cref="ReductionComplexityBucket"/> (how long the reduction takes
/// to run), which is what "most efficient" means per issue #376. <see cref="ReductionCost"/>
/// breaks ties, because between two reductions that run in the same time class the one
/// producing the smaller instance leaves less work for the downstream solver.
/// </para>
/// </summary>
static class ReductionEfficiency
{
    /// <summary>
    /// True when <paramref name="candidate"/> is strictly more efficient than
    /// <paramref name="incumbent"/>.
    /// <para>
    /// Unclassified is NOT treated as fastest even though its underlying value is 0: an
    /// undeclared reduction is unknown, not instantaneous, so ranking it first would let
    /// a missing declaration silently win. A classified reduction always beats an
    /// unclassified one; two unclassified reductions never displace each other, leaving
    /// declaration order as the stable fallback.
    /// </para>
    /// </summary>
    public static bool IsMoreEfficient(IReduction candidate, IReduction incumbent)
    {
        bool candidateKnown = candidate.complexityBucket != ReductionComplexityBucket.Unclassified;
        bool incumbentKnown = incumbent.complexityBucket != ReductionComplexityBucket.Unclassified;

        if (candidateKnown != incumbentKnown) return candidateKnown;
        if (!candidateKnown) return false;

        if (candidate.complexityBucket != incumbent.complexityBucket)
            return candidate.complexityBucket < incumbent.complexityBucket;

        // Same time class — fall back to output blow-up, applying the same
        // "unclassified is unknown, not best" rule.
        bool candidateCostKnown = candidate.cost != ReductionCost.Unclassified;
        bool incumbentCostKnown = incumbent.cost != ReductionCost.Unclassified;

        if (candidateCostKnown != incumbentCostKnown) return candidateCostKnown;
        if (!candidateCostKnown) return false;

        return candidate.cost < incumbent.cost;
    }

    /// <summary>
    /// The most efficient reduction in <paramref name="reductions"/>, or null when the
    /// sequence is empty. Ties keep the first-seen entry, so the result is stable.
    /// </summary>
    public static IReduction? MostEfficient(IEnumerable<IReduction> reductions)
    {
        IReduction? best = null;
        foreach (var reduction in reductions)
        {
            if (best is null || IsMoreEfficient(reduction, best)) best = reduction;
        }
        return best;
    }
}
